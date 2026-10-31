namespace UniversalControl.Core;

/// <summary>Estimated boundary crossing is only a candidate; require a deliberate, fresh outward push.</summary>
public sealed class EdgeReturnGuard
{
    public const int EntryGraceMs = 1000;
    public const int MinimumDwellMs = 300;
    public const int MaximumGapMs = 180;
    public const int MinimumPushCounts = 96;
    private long? _started, _lastMotion;
    private double _distance, _reverseJitter;
    public bool Pending => _started.HasValue;

    public void Reset() { _started = _lastMotion = null; _distance = _reverseJitter = 0; }
    public bool Observe(bool atBoundary, double dx, double dy, bool eligible, bool physicalIdle, AppSettings settings, long elapsedMs)
    {
        if (!settings.AutoSwitch || !settings.ReturnAtEdge || !physicalIdle || !atBoundary || elapsedMs < EntryGraceMs)
        { Reset(); return false; }
        if (_lastMotion is { } last && (elapsedMs < last || elapsedMs - last > MaximumGapMs)) Reset();
        // Timer ticks cannot count as pressure, finish a dwell or replay a stale crossing.
        if (dx == 0 && dy == 0) return false;
        if (!eligible) { Reset(); return false; }
        var normal = settings.Edge switch { ScreenEdge.Right => -dx, ScreenEdge.Left => dx, ScreenEdge.Top => dy, _ => -dy };
        var tangent = settings.Edge is ScreenEdge.Left or ScreenEdge.Right ? dy : dx;
        if (normal <= 0)
        {
            // A small sensor reversal during a deliberate push is not a new gesture.
            // Never complete a return from that reversal, and do not tolerate repeated retreat.
            if (Pending && normal < 0 && normal >= -2 && Math.Abs(tangent) <= 4 && (_reverseJitter -= normal) <= 4)
            { _distance = Math.Max(0, _distance + normal); return false; }
            Reset(); return false;
        }
        if (_started is null)
        {
            // The arrival packet may contain a huge flick from the middle of the screen.
            // None of that distance proves continued pressure at the boundary.
            _started = _lastMotion = elapsedMs; _distance = 0; return false;
        }
        _lastMotion = elapsedMs;
        _reverseJitter = Math.Max(0, _reverseJitter - normal);
        _distance += normal;
        return elapsedMs - _started.Value >= Math.Max(MinimumDwellMs, settings.EdgeDwellMs) && _distance >= MinimumPushCounts;
    }
}
