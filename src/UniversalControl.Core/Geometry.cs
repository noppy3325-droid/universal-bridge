namespace UniversalControl.Core;

public readonly record struct PixelPoint(int X, int Y);
public sealed record DisplayInfo(string Id, int Left, int Top, int Width, int Height, bool Primary = false)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    public override string ToString() => $"{Id}  ·  {Width} × {Height}{(Primary ? L.T("  メイン") : "")}";
}

public sealed class EdgeDetector
{
    private long? _enteredAt;
    private bool _mustLeave;
    public void Reset(bool mustLeave = true) { _enteredAt = null; _mustLeave = mustLeave; }
    public bool Observe(PixelPoint point, DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings, long now)
    {
        var along = settings.Edge is ScreenEdge.Left or ScreenEdge.Right
            ? (point.Y - source.Top) / (double)source.Height : (point.X - source.Left) / (double)source.Width;
        var atEdge = source.Contains(point.X, point.Y) && along >= settings.PortalStart && along <= settings.PortalEnd && settings.Edge switch
        {
            ScreenEdge.Right => point.X >= source.Right - 1,
            ScreenEdge.Left => point.X <= source.Left,
            ScreenEdge.Top => point.Y <= source.Top,
            ScreenEdge.Bottom => point.Y >= source.Bottom - 1, _ => false
        };
        var beyond = settings.Edge switch
        {
            ScreenEdge.Right => new PixelPoint(source.Right, point.Y),
            ScreenEdge.Left => new PixelPoint(source.Left - 1, point.Y),
            ScreenEdge.Top => new PixelPoint(point.X, source.Top - 1),
            _ => new PixelPoint(point.X, source.Bottom)
        };
        if (!atEdge || all.Any(m => m.Id != source.Id && m.Contains(beyond.X, beyond.Y)))
        {
            _enteredAt = null; _mustLeave = false; return false;
        }
        if (_mustLeave) return false;
        _enteredAt ??= now;
        if (now - _enteredAt.Value < settings.EdgeDwellMs) return false;
        Reset(); return true;
    }
}

public sealed class MotionAccumulator
{
    private double _x, _y, _wheel;
    public void Add(double x, double y, double wheel) { _x += x; _y += y; _wheel += wheel; }
    public (int X, int Y, int Wheel) Take()
    {
        var x = TakeAxis(_x, 32767);
        var y = TakeAxis(_y, 32767);
        var w = TakeAxis(_wheel, 127);
        _x -= x; _y -= y; _wheel -= w;
        if (Math.Abs(_x) < 1e-9) _x = 0;
        if (Math.Abs(_y) < 1e-9) _y = 0;
        if (Math.Abs(_wheel) < 1e-9) _wheel = 0;
        return (x, y, w);
    }
    private static int TakeAxis(double value, int limit)
    {
        // E.g. ten 0.1-count samples must emit one count, even if binary arithmetic gives 0.9999999999999999.
        var nearest = Math.Round(value);
        if (Math.Abs(value - nearest) < 1e-9) value = nearest;
        return (int)Math.Clamp(Math.Truncate(value), -limit, limit);
    }
    public void Clear() { _x = _y = _wheel = 0; }
}

/// <summary>Relative-report estimate only. It never measures the iPad cursor.</summary>
public sealed class VirtualCursor
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public void Enter(AppSettings s)
    {
        X = s.Edge == ScreenEdge.Left ? s.VirtualWidth - 24 : s.Edge == ScreenEdge.Right ? 24 : s.VirtualWidth / 2d;
        Y = s.Edge == ScreenEdge.Top ? s.VirtualHeight - 24 : s.Edge == ScreenEdge.Bottom ? 24 : s.VirtualHeight / 2d;
    }
    public bool Move(double dx, double dy, AppSettings s)
    {
        var nextX = X + dx; var nextY = Y + dy;
        var returned = s.Edge switch
        {
            ScreenEdge.Right => nextX < -16, ScreenEdge.Left => nextX > s.VirtualWidth + 16,
            ScreenEdge.Top => nextY > s.VirtualHeight + 16, _ => nextY < -16
        };
        X = Math.Clamp(nextX, -32, s.VirtualWidth + 32); Y = Math.Clamp(nextY, -32, s.VirtualHeight + 32);
        return returned;
    }
}
