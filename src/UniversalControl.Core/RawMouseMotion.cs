namespace UniversalControl.Core;

/// <summary>Absolute raw devices need their own baseline; relative device counts pass through unchanged.</summary>
public sealed class RawMouseMotion
{
    private readonly Dictionary<nint, (int X, int Y, int Width, int Height)> _absolute = [];
    public (double X, double Y) Translate(nint device, bool absolute, int x, int y, int width, int height)
    {
        if (!absolute) { _absolute.Remove(device); return (x, y); }
        var previous = _absolute.GetValueOrDefault(device);
        _absolute[device] = (x, y, width, height);
        if (previous.Width != width || previous.Height != height || width < 2 || height < 2) return (0, 0);
        return ((x - (double)previous.X) * (width - 1) / 65535d, (y - (double)previous.Y) * (height - 1) / 65535d);
    }
    public void Clear() => _absolute.Clear();
}
