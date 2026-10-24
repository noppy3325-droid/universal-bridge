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
    public bool Pending => _enteredAt.HasValue;
    public void Reset(bool mustLeave = true) { _enteredAt = null; _mustLeave = mustLeave; }
    public bool Observe(PixelPoint point, DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings, long now)
        => Update(point, source, all, settings, now, physicalMotion: true);
    public bool Poll(PixelPoint point, DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings, long now)
        => Update(point, source, all, settings, now, physicalMotion: false);
    private bool Update(PixelPoint point, DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings, long now, bool physicalMotion)
    {
        if (!IsAtPortal(point, source, all, settings))
        { _enteredAt = null; _mustLeave = false; return false; }
        if (_mustLeave || (_enteredAt is null && !physicalMotion)) return false;
        _enteredAt ??= now;
        if (now - _enteredAt.Value < settings.EdgeDwellMs) return false;
        Reset(); return true;
    }
    public static bool IsAtPortal(PixelPoint point, DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings)
    {
        if (source.Width <= 0 || source.Height <= 0) return false;
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
        return atEdge && !all.Any(m => m.Id != source.Id && m.Contains(beyond.X, beyond.Y));
    }
    public static IReadOnlyList<(double Start, double End)> AvailableSegments(DisplayInfo source, IReadOnlyList<DisplayInfo> all, AppSettings settings)
    {
        if (source.Width <= 0 || source.Height <= 0) return [];
        var segments = new List<(double Start, double End)> { (settings.PortalStart, settings.PortalEnd) };
        var vertical = settings.Edge is ScreenEdge.Left or ScreenEdge.Right;
        var cross = settings.Edge switch { ScreenEdge.Right => source.Right, ScreenEdge.Left => source.Left - 1, ScreenEdge.Top => source.Top - 1, _ => source.Bottom };
        foreach (var other in all.Where(display => display.Id != source.Id))
        {
            if (vertical ? cross < other.Left || cross >= other.Right : cross < other.Top || cross >= other.Bottom) continue;
            var start = Math.Clamp((vertical ? other.Top - (double)source.Top : other.Left - (double)source.Left) / (vertical ? source.Height : source.Width), 0, 1);
            var end = Math.Clamp((vertical ? other.Bottom - (double)source.Top : other.Right - (double)source.Left) / (vertical ? source.Height : source.Width), 0, 1);
            if (start >= end) continue;
            var next = new List<(double Start, double End)>();
            foreach (var segment in segments)
            {
                if (end <= segment.Start || start >= segment.End) next.Add(segment);
                else
                {
                    if (start > segment.Start) next.Add((segment.Start, start));
                    if (end < segment.End) next.Add((end, segment.End));
                }
            }
            segments = next;
        }
        return segments;
    }
}

public readonly record struct PortalPlacement(ScreenEdge Edge, double Start, double End)
{
    public static PortalPlacement FromDrop(double normalizedX, double normalizedY, double span)
    {
        if (!double.IsFinite(normalizedX) || !double.IsFinite(normalizedY) || !double.IsFinite(span)) throw new ArgumentException("Invalid layout position.");
        var x = normalizedX - .5; var y = normalizedY - .5;
        var edge = Math.Abs(x) >= Math.Abs(y) ? x >= 0 ? ScreenEdge.Right : ScreenEdge.Left : y >= 0 ? ScreenEdge.Bottom : ScreenEdge.Top;
        span = Math.Clamp(span, .2, 1);
        var along = edge is ScreenEdge.Left or ScreenEdge.Right ? normalizedY : normalizedX;
        var start = Math.Clamp(along - span / 2, 0, 1 - span);
        return new(edge, start, start + span);
    }
}

public sealed class MotionAccumulator
{
    private double _x, _y, _wheel;
    public void Add(double x, double y, double wheel) { _x += x; _y += y; _wheel += wheel; }
    public (int X, int Y, int Wheel) Take(int pointerLimit = 32767)
    {
        if (pointerLimit is < 1 or > 32767) throw new ArgumentOutOfRangeException(nameof(pointerLimit));
        // Center the quantization error instead of making each axis wait for a full count.
        // A shared scale also keeps the direction of reports that exceed the HID range.
        var scale = Math.Min(1, pointerLimit / Math.Max(Math.Abs(_x), Math.Abs(_y)));
        var x = (int)Math.Clamp(Math.Round(_x * scale), -pointerLimit, pointerLimit);
        var y = (int)Math.Clamp(Math.Round(_y * scale), -pointerLimit, pointerLimit);
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
    public void ClearPointer() { _x = _y = 0; }
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
