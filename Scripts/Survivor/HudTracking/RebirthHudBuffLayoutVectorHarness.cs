using System;
using UnityEngine;

#nullable disable

/// <summary>Deterministic source-level vectors for Chunk E layout math.</summary>
public static class RebirthHudBuffLayoutVectorHarness
{
    public static void RunOrThrow()
    {
        AssertEqual(0, RebirthHudTrackingLayoutMetrics.CalculateBuffYOffset(0f), "zero-tracker-offset");
        AssertEqual(98, RebirthHudTrackingLayoutMetrics.CalculateBuffYOffset(90f), "tracker-gap-offset");

        int noBuffs = RebirthHudTrackingLayoutMetrics.CalculateMeasuredCapacity(1080, 1f, 0);
        int fourBuffs = RebirthHudTrackingLayoutMetrics.CalculateMeasuredCapacity(1080, 1f, 4);
        int twelveBuffs = RebirthHudTrackingLayoutMetrics.CalculateMeasuredCapacity(1080, 1f, 12);
        AssertEqual(15, noBuffs, "1080-no-buffs-capacity");
        AssertEqual(15, fourBuffs, "1080-four-buffs-capacity");
        AssertEqual(15, twelveBuffs, "1080-twelve-buffs-capacity");
        if (!(noBuffs >= fourBuffs && fourBuffs >= twelveBuffs))
            throw new InvalidOperationException("buff capacity must shrink monotonically as active buff rows increase");

        int scaled = RebirthHudTrackingLayoutMetrics.CalculateMeasuredCapacity(640, 1.5f, 4);
        if (scaled >= fourBuffs)
            throw new InvalidOperationException("larger UI scale must not increase safe tracker capacity");
    }

    private static void AssertEqual(int expected, int actual, string name)
    {
        if (expected != actual)
            throw new InvalidOperationException(name + " expected " + expected + " actual " + actual);
    }
}
