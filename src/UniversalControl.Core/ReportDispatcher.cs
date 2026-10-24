using System.Threading.Channels;
using System.Buffers.Binary;

namespace UniversalControl.Core;

public interface IHidTransport
{
    bool IsReady(string targetId);
    bool IsTouchpadReady(string targetId) => false;
    int NeutralReportCount => 2;
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
    private sealed class Pending(string target, long epoch, HidFrame frame)
    {
        public string Target { get; } = target;
        public long Epoch { get; } = epoch;
        public HidFrame Frame { get; set; } = frame;
    }
    private readonly IHidTransport _transport;
    private readonly Channel<Pending> _queue;
    private readonly object _queueGate = new();
    private Pending? _motionTail;
    private Pending? _touchpadTail;
    private long _coalescedMotion;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _sendGate = new(1);
    private readonly Task _pump;
    private long _epoch;
    private int _faulted, _resets;
    private long _sent;
    private long _faultVersion;
    private readonly List<Task> _inFlight = [];
    public bool Healthy => Volatile.Read(ref _faulted) == 0 && !_stop.IsCancellationRequested;
    public bool Settling => Volatile.Read(ref _resets) > 0;
    public long Sent => Interlocked.Read(ref _sent);
    public long CoalescedMotion => Interlocked.Read(ref _coalescedMotion);
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
        bool written;
        lock (_queueGate) { _motionTail = _touchpadTail = null; written = Healthy && _queue.Writer.TryWrite(new(target, epoch, frame)); }
        if (!written)
        {
            Fail(L.T("入力送信キューが停止または上限に達したためWindowsへ戻しました。iPadの接続を確認して切り替え直してください。"));
            return false;
        }
        return true;
    }

    // Only adjacent unsent XY motion may merge. Every key, button, wheel and target/epoch
    // transition is an ordering barrier. Saturating a sum would lose travel, so split instead.
    public bool EnqueueMotion(string target, long epoch, HidFrame frame)
    {
        HidReports.Validate(frame);
        if (frame.Kind != ReportKind.Mouse || frame.Payload[5] != 0 || frame.Payload[6] != 0) return Enqueue(target, epoch, frame);
        lock (_queueGate)
        {
            _touchpadTail = null;
            if (Healthy)
            {
                if (_motionTail is { } tail && tail.Epoch == epoch && tail.Target == target && tail.Frame.Payload[0] == frame.Payload[0])
                {
                    var a = tail.Frame.Payload; var b = frame.Payload;
                    var ax = BinaryPrimitives.ReadInt16LittleEndian(a.AsSpan(1)); var ay = BinaryPrimitives.ReadInt16LittleEndian(a.AsSpan(3));
                    var bx = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(1)); var by = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(3));
                    var x = ax + bx; var y = ay + by;
                    if (ax * bx >= 0 && ay * by >= 0 && Math.Abs(x) <= 127 && Math.Abs(y) <= 127)
                    {
                        tail.Frame = HidReports.Mouse((MouseButtons)a[0], x, y);
                        Interlocked.Increment(ref _coalescedMotion); return true;
                    }
                }
                var pending = new Pending(target, epoch, frame);
                if (_queue.Writer.TryWrite(pending)) { _motionTail = pending; return true; }
            }
        }
        Fail(L.T("入力送信キューが停止または上限に達したためWindowsへ戻しました。iPadの接続を確認して切り替え直してください。"));
        return false;
    }
    public bool EnqueueTouchpadUpdate(string target, long epoch, HidFrame frame)
    {
        HidReports.Validate(frame);
        if (frame.Kind != ReportKind.Touchpad) throw new ArgumentException("Expected touchpad report.");
        lock (_queueGate)
        {
            _motionTail = null;
            if (Healthy)
            {
                if (_touchpadTail is { } tail && tail.Target == target && tail.Epoch == epoch)
                { tail.Frame = frame; return true; } // Absolute positions: keep the latest update, never add coordinates.
                var pending = new Pending(target, epoch, frame);
                if (_queue.Writer.TryWrite(pending)) { _touchpadTail = pending; return true; }
            }
        }
        Fail(L.T("入力送信キューが停止または上限に達したためWindowsへ戻しました。iPadの接続を確認して切り替え直してください。"));
        return false;
    }

    private void Fail(string reason)
    {
        Interlocked.Increment(ref _faultVersion);
        if (Interlocked.Exchange(ref _faulted, 1) == 0) Failed?.Invoke(reason);
    }

    private async Task SendBoundedAsync(string target, HidFrame frame, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        var operation = _transport.SendAsync(target, frame, timeout.Token);
        Track(operation);
        await operation.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        Interlocked.Increment(ref _sent);
    }

    // Explicit activation may recover a failed connection, but only after both neutral reports
    // succeed. Old queued epochs remain invalid and an unfinished timed-out send blocks recovery.
    public async Task<bool> TryRecoverAsync(string target)
    {
        await _sendGate.WaitAsync(_stop.Token);
        try
        {
            var epoch = Interlocked.Read(ref _epoch);
            var version = Interlocked.Read(ref _faultVersion);
            if (_inFlight.Any(task => !task.IsCompleted) || !_transport.IsReady(target)) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var operation = _transport.ReleaseAsync(target, timeout.Token);
            Track(operation);
            var released = await operation.WaitAsync(TimeSpan.FromSeconds(1), _stop.Token);
            Interlocked.Add(ref _sent, released);
            if (released != _transport.NeutralReportCount || !_transport.IsReady(target) || epoch != Interlocked.Read(ref _epoch) || version != Interlocked.Read(ref _faultVersion)) return false;
            Interlocked.Exchange(ref _faulted, 0);
            return Healthy;
        }
        catch (Exception) when (!_stop.IsCancellationRequested) { return false; }
        finally { _sendGate.Release(); }
    }

    private void Track(Task operation)
    {
        _inFlight.RemoveAll(task => task.IsCompleted);
        _inFlight.Add(operation);
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var next in _queue.Reader.ReadAllAsync(_stop.Token))
            {
                HidFrame frame;
                lock (_queueGate) { frame = next.Frame; if (ReferenceEquals(_motionTail, next)) _motionTail = null; if (ReferenceEquals(_touchpadTail, next)) _touchpadTail = null; }
                if (!Healthy || next.Epoch != Interlocked.Read(ref _epoch)) continue;
                await _sendGate.WaitAsync(_stop.Token);
                try
                {
                    if (next.Epoch != Interlocked.Read(ref _epoch)) continue;
                    if (!_transport.IsReady(next.Target)) throw new IOException(L.T("選択した端末とのHID接続が切れました。"));
                    await SendBoundedAsync(next.Target, frame, _stop.Token);
                }
                catch (Exception ex) when (!_stop.IsCancellationRequested) { Fail(L.T("Bluetooth送信失敗: ") + ex.Message); }
                finally { _sendGate.Release(); }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async Task<bool> NeutralizeAsync(string target)
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
                var operation = _transport.ReleaseAsync(target, timeout.Token);
                Track(operation);
                var released = await operation.WaitAsync(TimeSpan.FromSeconds(1), _stop.Token);
                Interlocked.Add(ref _sent, released);
                return released == _transport.NeutralReportCount;
            }
            finally { _sendGate.Release(); }
        }
        catch (Exception ex) when (!_stop.IsCancellationRequested) { Fail(L.T("キー解放送信失敗: ") + ex.Message); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { Interlocked.Decrement(ref _resets); }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete(); _stop.Cancel();
        try { await _pump.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
        _stop.Dispose();
    }
}
