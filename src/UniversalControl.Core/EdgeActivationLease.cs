namespace UniversalControl.Core;

public enum EdgeCancellation { None, PointerLeft, SettingsChanged, NotReady, InputHeld, Expired, CursorUnavailable }

/// <summary>A bounded consent to finish a handoff. Small hand jitter does not revoke it.</summary>
public sealed class EdgeActivationLease(DisplayInfo source, IReadOnlyList<DisplayInfo> displays, AppSettings settings, int revision, long requestedAt)
{
    private int _cancelled;
    public EdgeCancellation Cancellation => (EdgeCancellation)Volatile.Read(ref _cancelled);
    public void Cancel(EdgeCancellation reason)
    {
        if (reason != EdgeCancellation.None) Interlocked.CompareExchange(ref _cancelled, (int)reason, 0);
    }
    public bool Validate(PixelPoint? point, int currentRevision, bool ready, bool idle, long now)
    {
        if (Cancellation != EdgeCancellation.None) return false;
        var reason = currentRevision != revision ? EdgeCancellation.SettingsChanged
            : !ready ? EdgeCancellation.NotReady
            : !idle ? EdgeCancellation.InputHeld
            : now - requestedAt > 5000 ? EdgeCancellation.Expired
            : point is not { } cursor ? EdgeCancellation.CursorUnavailable
            : !EdgeDetector.IsAtPortal(cursor, source, displays, settings, EdgeDetector.RetentionTolerance) ? EdgeCancellation.PointerLeft : EdgeCancellation.None;
        Cancel(reason); return Cancellation == EdgeCancellation.None;
    }
}
