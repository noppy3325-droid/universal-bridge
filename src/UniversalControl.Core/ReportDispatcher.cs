using System.Threading.Channels;

namespace UniversalControl.Core;

public interface IHidTransport
{
    bool IsReady(string targetId);
    Task SendAsync(string targetId, HidFrame frame, CancellationToken cancellationToken);
    async Task<int> ReleaseAsync(string targetId, CancellationToken cancellationToken)
    {
        if (!IsReady(targetId)) return 0;
        await SendAsync(targetId, HidReports.Keyboard(0, []), cancellationToken);
        await SendAsync(targetId, HidReports.Mouse(0), cancellationToken);
        return 2;
    }
}

public sealed class ReportDispatcher : IAsyncDisposable
{
    private sealed record Pending(string Target, long Epoch, HidFrame Frame);
    private readonly IHidTransport _transport;
    private readonly Channel<Pending> _queue;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _sendGate = new(1);
    private readonly Task _pump;
    private long _epoch;
    private int _faulted, _resets;
    private long _sent;
    public bool Healthy => Volatile.Read(ref _faulted) == 0 && !_stop.IsCancellationRequested;
    public bool Settling => Volatile.Read(ref _resets) > 0;
    public long Sent => Interlocked.Read(ref _sent);
    public event Action<string>? Failed;

    public ReportDispatcher(IHidTransport transport, int capacity = 256)
    {
        _transport = transport;
        _queue = Channel.CreateBounded<Pending>(new BoundedChannelOptions(capacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        _pump = Task.Run(PumpAsync);
    }

    public void SetEpoch(long epoch) => Interlocked.Exchange(ref _epoch, epoch);
    public bool Enqueue(string target, long epoch, HidFrame frame)
    {
        HidReports.Validate(frame);
        if (!Healthy || !_queue.Writer.TryWrite(new(target, epoch, frame)))
        {
            Fail(L.T("入力送信キューが停止または上限に達したためWindowsへ戻しました。サービスを再起動してください。"));
            return false;
        }
        return true;
    }

    private void Fail(string reason)
    {
        if (Interlocked.Exchange(ref _faulted, 1) == 0) Failed?.Invoke(reason);
    }

    private async Task SendBoundedAsync(string target, HidFrame frame, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        await _transport.SendAsync(target, frame, timeout.Token).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        Interlocked.Increment(ref _sent);
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var next in _queue.Reader.ReadAllAsync(_stop.Token))
            {
                if (!Healthy || next.Epoch != Interlocked.Read(ref _epoch)) continue;
                await _sendGate.WaitAsync(_stop.Token);
                try
                {
                    if (next.Epoch != Interlocked.Read(ref _epoch)) continue;
                    if (!_transport.IsReady(next.Target)) throw new IOException(L.T("選択した端末とのHID接続が切れました。"));
                    await SendBoundedAsync(next.Target, next.Frame, _stop.Token);
                }
                catch (Exception ex) when (!_stop.IsCancellationRequested) { Fail(L.T("Bluetooth送信失敗: ") + ex.Message); }
                finally { _sendGate.Release(); }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async Task NeutralizeAsync(string target)
    {
        Interlocked.Increment(ref _resets);
        try
        {
            await _sendGate.WaitAsync(_stop.Token);
            try
            {
                // Release may still be possible when a readiness snapshot has been revoked.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(1));
                var released = await _transport.ReleaseAsync(target, timeout.Token).WaitAsync(TimeSpan.FromSeconds(1), _stop.Token);
                Interlocked.Add(ref _sent, released);
            }
            finally { _sendGate.Release(); }
        }
        catch (Exception ex) when (!_stop.IsCancellationRequested) { Fail(L.T("キー解放送信失敗: ") + ex.Message); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { Interlocked.Decrement(ref _resets); }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete(); _stop.Cancel();
        try { await _pump.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
        _stop.Dispose();
    }
}
