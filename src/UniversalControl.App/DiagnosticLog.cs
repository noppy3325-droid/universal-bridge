using System.Threading.Channels;

namespace UniversalControl.App;

public sealed class DiagnosticLog : IAsyncDisposable
{
    private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Queue<string> _recent = new();
    private readonly Task _writer;
    public string Path { get; }
    public string? Error { get; private set; }
    public DiagnosticLog(string directory)
    {
        Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "diagnostic.log");
        _writer = Task.Run(async () =>
        {
            await foreach (var line in _queue.Reader.ReadAllAsync())
            {
                lock (_recent) { _recent.Enqueue(line); while (_recent.Count > 160) _recent.Dequeue(); }
                try
                {
                    if (File.Exists(Path) && new FileInfo(Path).Length > 1_048_576) File.Move(Path, Path + ".1", true);
                    await File.AppendAllTextAsync(Path, line + Environment.NewLine);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = ex.Message; }
            }
        });
    }
    public void Write(string message) => _queue.Writer.TryWrite($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}  {message}");
    public string Recent { get { lock (_recent) return string.Join(Environment.NewLine, _recent); } }
    public async ValueTask DisposeAsync() { _queue.Writer.TryComplete(); await _writer; }
}
