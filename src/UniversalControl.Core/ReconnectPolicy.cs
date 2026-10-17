namespace UniversalControl.Core;

/// <summary>Initial user attempt plus at most five retries; no automatic input activation.</summary>
public sealed class ReconnectPolicy
{
    public int Failures { get; private set; }
    public long NextAttemptAt { get; private set; }
    public void Reset() { Failures = 0; NextAttemptAt = 0; }
    public void RecordFailure(long now)
    {
        Failures++;
        NextAttemptAt = now + Math.Min(30_000, (1L << Math.Min(Failures, 5)) * 1000);
    }
    public bool CanRetry(long now) => Failures <= 5 && now >= NextAttemptAt;
}
