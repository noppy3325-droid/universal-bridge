namespace UniversalControl.Core;

public enum GestureAction { None, NextApp, PreviousApp, Home, Search, ZoomIn, ZoomOut }
public sealed class TouchpadGestures
{
    private double _scaleRemainder;
    public void Reset() => _scaleRemainder = 0;
    public int ZoomSteps(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0) return 0;
        _scaleRemainder += Math.Log(scale) / Math.Log(1.15);
        var steps = (int)Math.Clamp(Math.Truncate(_scaleRemainder), -3, 3);
        _scaleRemainder -= steps; return steps;
    }
    public static GestureAction Swipe(double x, double y, AppSettings settings)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Max(Math.Abs(x), Math.Abs(y)) < 300) return GestureAction.None;
        return Math.Abs(x) > Math.Abs(y) ? x < 0 ? settings.SwipeLeft : settings.SwipeRight : y < 0 ? settings.SwipeUp : settings.SwipeDown;
    }
    public static (byte Modifiers, byte Usage) Shortcut(GestureAction action) => action switch
    {
        GestureAction.NextApp => (8, 0x2B), GestureAction.PreviousApp => (10, 0x2B),
        GestureAction.Home => (8, 0x0B), GestureAction.Search => (8, 0x2C),
        GestureAction.ZoomIn => (10, 0x2E), GestureAction.ZoomOut => (8, 0x2D), _ => (0, 0)
    };
}
