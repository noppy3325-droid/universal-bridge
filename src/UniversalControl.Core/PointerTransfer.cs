namespace UniversalControl.Core;

/// <summary>Radial soft knee for fast input. Fine motion is linear; compressed distance is not replayed later.</summary>
public static class PointerTransfer
{
    public static (int X, int Y) Apply(int x, int y, double elapsedMs)
    {
        var maximum = 8 * Math.Clamp(elapsedMs, 4, 32); // 8000 HID counts/s, with no catch-up burst after a stall.
        var knee = maximum / 2;
        var length = Math.Sqrt((double)x * x + (double)y * y);
        if (length <= knee) return (x, y);
        var compressed = knee + (length - knee) / (1 + (length - knee) / (maximum - knee));
        return ((int)Math.Round(x * compressed / length), (int)Math.Round(y * compressed / length));
    }
}
