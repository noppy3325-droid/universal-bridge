namespace UniversalControl.Core;

/// <summary>Radial soft knee for fast input. Fine motion is linear; compressed distance is not replayed later.</summary>
public static class PointerTransfer
{
    public static (int X, int Y) Apply(int x, int y, double elapsedMs)
    {
        var result = Scale(x, y, elapsedMs);
        return ((int)Math.Round(result.X), (int)Math.Round(result.Y));
    }
    public static (double X, double Y) Scale(double x, double y, double elapsedMs)
    {
        var maximum = 8 * Math.Clamp(elapsedMs, 4, 32); // 8000 HID counts/s, with no catch-up burst after a stall.
        var knee = maximum / 2;
        var length = Math.Sqrt((double)x * x + (double)y * y);
        if (length <= knee) return (x, y);
        var compressed = knee + (length - knee) / (1 + (length - knee) / (maximum - knee));
        return (x * compressed / length, y * compressed / length);
    }
}

/// <summary>Quantizes once, after the radial transfer, and carries both axes' fractional errors.</summary>
public sealed class PointerMotion
{
    private readonly MotionAccumulator _linear = new();
    private double _x, _y, _errorX, _errorY;
    public void Add(double x, double y) { _x += x; _y += y; }
    public (int X, int Y) Take(bool stabilize, double elapsedMs)
    {
        if (!stabilize)
        {
            _linear.Add(_x + _errorX, _y + _errorY, 0); _x = _y = _errorX = _errorY = 0;
            var direct = _linear.Take(); return (direct.X, direct.Y);
        }
        var carried = _linear.Take(); _linear.Clear();
        var filtered = PointerTransfer.Scale(_x + carried.X, _y + carried.Y, elapsedMs);
        _x = _y = 0; // Compressed distance is not replayed as an overshoot tail.
        var desiredX = filtered.X + _errorX; var desiredY = filtered.Y + _errorY;
        var x = (int)Math.Round(desiredX); var y = (int)Math.Round(desiredY);
        _errorX = desiredX - x; _errorY = desiredY - y;
        return (x, y);
    }
    public void Clear() { _linear.Clear(); _x = _y = _errorX = _errorY = 0; }
}
