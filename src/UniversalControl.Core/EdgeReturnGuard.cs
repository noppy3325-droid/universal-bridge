namespace UniversalControl.Core;

/// <summary>Estimated boundary crossing is only a candidate; require a deliberate, fresh outward push.</summary>
public sealed class EdgeReturnGuard
{
    public const int EntryGraceMs = 1000;
    public const int MinimumDwellMs = 300;
    public const int MaximumGapMs = 120;
    public const int MinimumPushCounts = 96;
    private long? _started, _lastMotion;
    private double _distance;

    public void Reset() { _started = _lastMotion = null; _distance = 0; }
    public bool Observe(bool atBoundary, int dx, int dy, bool eligible, bool physicalIdle, AppSettings settings, long elapsedMs)
    {
        if (!settings.AutoSwitch || !settings.ReturnAtEdge || !physicalIdle || !atBoundary || elapsedMs < EntryGraceMs)
        { Reset(); return false; }
        if (_lastMotion is { } last && (elapsedMs < last || elapsedMs - last > MaximumGapMs)) Reset();
        // Timer ticks cannot count as pressure, finish a dwell or replay a stale crossing.
        if (dx == 0 && dy == 0) return false;
        if (!eligible || !VirtualCursor.TowardWindows(dx, dy, settings.Edge)) { Reset(); return false; }
        if (_started is null)
        {
            // The arrival packet may contain a huge flick from the middle of the screen.
            // None of that distance proves continued pressure at the boundary.
            _started = _lastMotion = elapsedMs; _distance = 0; return false;
        }
        _lastMotion = elapsedMs;
        _distance += settings.Edge is ScreenEdge.Left or ScreenEdge.Right ? Math.Abs((double)dx) : Math.Abs((double)dy);
        return elapsedMs - _started.Value >= Math.Max(MinimumDwellMs, settings.EdgeDwellMs) && _distance >= MinimumPushCounts;
    }
}
