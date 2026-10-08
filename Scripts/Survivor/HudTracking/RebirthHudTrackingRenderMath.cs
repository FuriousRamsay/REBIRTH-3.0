using System;

#nullable disable

/// <summary>
/// Pure render-window math shared by the live HUD and deterministic validation harnesses.
/// A compact overflow indicator consumes one visual row when the resolvable selection exceeds
/// the current safe capacity; stored preferences themselves are never truncated.
/// </summary>
public static class RebirthHudTrackingRenderMath
{
    public static void CalculateVisibleWindow(int resolvableCount, int safeCapacity, out int dataRows, out int overflowCount, out int visualRows)
    {
        int total = Math.Max(0, resolvableCount);
        int capacity = Math.Max(0, safeCapacity);
        if (capacity == 0) { dataRows = 0; overflowCount = total; visualRows = 0; return; }
        if (total <= capacity)
        {
            dataRows = total;
            overflowCount = 0;
            visualRows = total;
            return;
        }

        dataRows = Math.Max(0, capacity - 1);
        overflowCount = Math.Max(0, total - dataRows);
        visualRows = dataRows + 1;
    }

    /// <summary>
    /// Calculates a practical-Skill fractional gain across integer rollover, e.g.
    /// 24 + .90 -> 25 + .02 = +.12.
    /// Negative/zero results are intentionally not treated as gain pulses.
    /// </summary>
    public static float CalculateSkillGain(float previousValue, float previousProgress, float currentValue, float currentProgress)
    {
        double before = previousValue + Clamp01(previousProgress);
        double after = currentValue + Clamp01(currentProgress);
        double delta = after - before;
        if (delta <= 0.0005d || double.IsNaN(delta) || double.IsInfinity(delta)) return 0f;
        return (float)Math.Min(delta, 9999d);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
