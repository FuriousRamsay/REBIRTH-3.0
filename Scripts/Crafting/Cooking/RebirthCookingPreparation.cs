using System;
using System.Collections.Generic;

/// <summary>Window-owned preparation. Closing cancels work; only completed preparation has a timed benefit.</summary>
public sealed class RebirthCookingPreparation
{
    public const float StudySeconds = 10f;
    public const float BenefitSeconds = 300f;
    private readonly Dictionary<string, double> ready = new Dictionary<string, double>(StringComparer.Ordinal);
    private string pending;
    private double started;
    public bool IsPreparing => pending != null;
    public void Begin(string recipe, double now) { pending = recipe; started = now; }
    public void Cancel() { pending = null; started = 0; }
    public float Progress(double now) => pending == null ? 0 : (float)Math.Max(0, Math.Min(1, (now - started) / StudySeconds));
    public float Remaining(string recipe, double now) => recipe != null && ready.TryGetValue(recipe, out double until) ? (float)Math.Max(0, until - now) : 0;
    public bool Tick(bool windowOpen, string recipe, double now)
    {
        if (!windowOpen || pending != null && !string.Equals(pending, recipe, StringComparison.Ordinal)) { Cancel(); return false; }
        if (pending == null || Progress(now) < 1) return false;
        ready[pending] = now + BenefitSeconds;
        Cancel();
        return true;
    }
    public void Reset() { Cancel(); ready.Clear(); }
}
