namespace UniversalControl.Core;

/// <summary>Converts cumulative device-relative translations to wheel units, preserving gesture onset and coalesced motion.</summary>
public sealed class TouchpadScroll
{
    private double _x, _y;
    public (double Vertical, double Horizontal) Update(double cumulativeX, double cumulativeY, double sensitivity)
    {
        if (!double.IsFinite(cumulativeX) || !double.IsFinite(cumulativeY) || !double.IsFinite(sensitivity) || sensitivity <= 0) return (0, 0);
        var dx = cumulativeX - _x; var dy = cumulativeY - _y;
        _x = cumulativeX; _y = cumulativeY;
        return (dy * .4 * sensitivity, dx * .4 * sensitivity);
    }
    public void Reset() => _x = _y = 0;
}
