namespace UniversalControl.Core;

/// <summary>HID Suspend belongs to a GATT session, never to a permanent device ID.</summary>
public sealed class HidSuspensionState<TSession> where TSession : class
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TSession> _suspended = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string id, TSession session, bool suspended)
    {
        lock (_gate)
        {
            if (suspended) _suspended[id] = session;
            else _suspended.Remove(id);
        }
    }

    public bool Observe(string id, Func<TSession, bool> isActive, bool reportsSubscribed)
    {
        lock (_gate)
        {
            if (!_suspended.TryGetValue(id, out var session)) return false;
            if (isActive(session) && reportsSubscribed) return true;
            _suspended.Remove(id);
            return false;
        }
    }


    public void Reset(string id) { lock (_gate) _suspended.Remove(id); }
}
