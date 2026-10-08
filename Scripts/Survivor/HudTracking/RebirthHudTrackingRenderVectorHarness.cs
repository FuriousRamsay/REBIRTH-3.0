using System;
using System.Collections.Generic;

#nullable disable

public static class RebirthHudTrackingRenderVectorHarness
{
    public static IList<string> Run()
    {
        List<string> failures = new List<string>();
        AssertWindow(failures, 0, 8, 0, 0, 0, "empty");
        AssertWindow(failures, 4, 8, 4, 0, 4, "fits");
        AssertWindow(failures, 9, 8, 7, 2, 8, "overflow-reserves-row");
        AssertWindow(failures, 2, 1, 0, 2, 1, "single-row-overflow");

        float rollover = RebirthHudTrackingRenderMath.CalculateSkillGain(24f, 0.90f, 25f, 0.02f);
        if (Math.Abs(rollover - 0.12f) > 0.001f) failures.Add("skill rollover expected 0.12 got " + rollover);
        float ordinary = RebirthHudTrackingRenderMath.CalculateSkillGain(24f, 0.10f, 24f, 0.22f);
        if (Math.Abs(ordinary - 0.12f) > 0.001f) failures.Add("skill fractional gain expected 0.12 got " + ordinary);
        if (RebirthHudTrackingRenderMath.CalculateSkillGain(25f, 0.50f, 25f, 0.40f) != 0f) failures.Add("negative change must not pulse");
        return failures;
    }

    private static void AssertWindow(List<string> failures, int total, int capacity, int expectedRows, int expectedOverflow, int expectedVisual, string name)
    {
        int rows, overflow, visual;
        RebirthHudTrackingRenderMath.CalculateVisibleWindow(total, capacity, out rows, out overflow, out visual);
        if (rows != expectedRows || overflow != expectedOverflow || visual != expectedVisual)
            failures.Add(name + " expected " + expectedRows + "/" + expectedOverflow + "/" + expectedVisual + " got " + rows + "/" + overflow + "/" + visual);
    }
}
