using System;

#nullable disable

/// <summary>Shared page stops for native pixel viewports and custom full-row/index authorities.</summary>
internal static class RebirthScrollbarPagingPolicy
{
    internal static bool Enabled => RebirthScrollbarPagingInstaller.Available && RebirthSandboxOptionManager.Current != null &&
        RebirthSandboxOptionManager.Current.ScrollbarMode == RebirthScrollbarMode.Paged;

    internal static float PageStep(float viewport, float rowStride = 0f)
    {
        if (!Finite(viewport) || viewport <= 0f) return 1f;
        if (!Finite(rowStride) || rowStride <= 0f) return viewport;
        return Math.Max(rowStride, (float)Math.Floor(viewport / rowStride) * rowStride);
    }

    internal static float SnapAbsolute(float desired, float maximum, float pageStep, bool enabled)
    {
        // Off is an exact pass-through: each existing authority retains its prior clamp/input math.
        if (!enabled) return desired;
        maximum = Limit(maximum);
        pageStep = Stride(pageStep);
        float value = Clamp(Finite(desired) ? desired : 0f, 0f, maximum);
        float lower = Math.Min(maximum, (float)Math.Floor(value / pageStep) * pageStep);
        float upper = Math.Min(maximum, lower + pageStep);
        return value - lower <= upper - value ? lower : upper;
    }

    internal static float Step(float current, float maximum, float pageStep, int direction)
    {
        maximum = Limit(maximum);
        pageStep = Stride(pageStep);
        current = Clamp(Finite(current) ? current : 0f, 0f, maximum);
        if (direction == 0) return SnapAbsolute(current, maximum, pageStep, true);
        // Reverse from a short terminal page reaches the preceding lattice page, never max-step.
        float next = direction > 0 ? ((float)Math.Floor(current / pageStep) + 1f) * pageStep
            : ((float)Math.Ceiling(current / pageStep) - 1f) * pageStep;
        return Clamp(next, 0f, maximum);
    }

    internal static float EnsureVisible(float current, float itemTop, float itemBottom,
        float maximum, float viewport, float pageStep)
    {
        maximum = Limit(maximum);
        pageStep = Stride(pageStep);
        viewport = Finite(viewport) ? Math.Max(0f, viewport) : 0f;
        current = SnapAbsolute(current, maximum, pageStep, true);
        if (!Finite(itemTop) || !Finite(itemBottom)) return current;
        if (itemBottom < itemTop) { float swap = itemTop; itemTop = itemBottom; itemBottom = swap; }
        if (itemTop >= current && itemBottom <= current + viewport) return current;
        if (itemBottom - itemTop > viewport && itemTop < current + viewport && itemBottom > current)
            return current; // A giant row cannot fit; keep an already intersecting page stable.
        float low = Math.Max(0f, itemBottom - viewport);
        float high = Math.Min(maximum, itemTop);
        float first = (float)Math.Ceiling(low / pageStep) * pageStep;
        float last = (float)Math.Floor(high / pageStep) * pageStep;
        bool regular = first <= last && first <= maximum;
        bool terminal = maximum >= low && maximum <= high;
        if (regular || terminal)
        {
            float candidate = regular ? Clamp(current, first, last) : maximum;
            if (terminal && Math.Abs(maximum - current) < Math.Abs(candidate - current)) candidate = maximum;
            return candidate;
        }
        // Giant/boundary-spanning rows use a bounded start page without inventing an intermediate stop.
        return Clamp((float)Math.Floor(Math.Max(0f, itemTop) / pageStep) * pageStep, 0f, maximum);
    }

    private static float Limit(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    private static float Stride(float value) => Finite(value) && value > 0f ? value : 1f;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}
