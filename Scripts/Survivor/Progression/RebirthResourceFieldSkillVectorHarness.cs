using System;
using System.Text;

#nullable disable

/// <summary>Debug-only/pure-math smoke vectors for Chunk 6 signed resource and animal-tracking curves.</summary>
public static class RebirthResourceFieldSkillVectorHarness
{
    public static string RunAll()
    {
        int pass = 0, fail = 0;
        StringBuilder b = new StringBuilder();
        Check(b, ref pass, ref fail, "resource weak yield penalty", RebirthResourceFieldSkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive) < 0f);
        Check(b, ref pass, ref fail, "resource zero neutral", Near(RebirthResourceFieldSkillService.SignedEndpoint(0f, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive), 0f));
        Check(b, ref pass, ref fail, "resource strong yield benefit", RebirthResourceFieldSkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive) > 0f);
        Check(b, ref pass, ref fail, "resource positive bounded", RebirthProgressionRuntimeConfig.ResourceHarvestPositive <= 0.35f);
        Check(b, ref pass, ref fail, "resource negative bounded", RebirthProgressionRuntimeConfig.ResourceHarvestNegative >= -0.25f);

        Check(b, ref pass, ref fail, "farming weak yield penalty", RebirthResourceFieldSkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive) < 0f);
        Check(b, ref pass, ref fail, "farming zero neutral", Near(RebirthResourceFieldSkillService.SignedEndpoint(0f, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive), 0f));
        Check(b, ref pass, ref fail, "farming strong yield benefit", RebirthResourceFieldSkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive) > 0f);
        Check(b, ref pass, ref fail, "farming no growth tuning", RebirthProgressionRuntimeConfig.FarmingHarvestPositive <= 0.30f);

        Check(b, ref pass, ref fail, "animal weak yield penalty", RebirthResourceFieldSkillService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.AnimalHarvestNegative, RebirthProgressionRuntimeConfig.AnimalHarvestPositive) < 0f);
        Check(b, ref pass, ref fail, "animal zero neutral", Near(RebirthResourceFieldSkillService.SignedEndpoint(0f, RebirthProgressionRuntimeConfig.AnimalHarvestNegative, RebirthProgressionRuntimeConfig.AnimalHarvestPositive), 0f));
        Check(b, ref pass, ref fail, "animal strong yield benefit", RebirthResourceFieldSkillService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.AnimalHarvestNegative, RebirthProgressionRuntimeConfig.AnimalHarvestPositive) > 0f);
        string phase8=RebirthPhase8WorldOutputTrainingService.RunVectors();
        Check(b, ref pass, ref fail, "animal processing Phase 8 exact-output vectors", phase8.IndexOf("PASS", StringComparison.OrdinalIgnoreCase)>=0);

        float dWeak = RebirthResourceFieldSkillService.GetTrackingDistance(-50f);
        float dZero = RebirthResourceFieldSkillService.GetTrackingDistance(0f);
        float dStrong = RebirthResourceFieldSkillService.GetTrackingDistance(100f);
        Check(b, ref pass, ref fail, "tracking distance ordered", dWeak < dZero && dZero < dStrong);
        Check(b, ref pass, ref fail, "tracking weak distance", Near(dWeak, RebirthProgressionRuntimeConfig.TrackingMinDistance));
        Check(b, ref pass, ref fail, "tracking neutral distance", Near(dZero, RebirthProgressionRuntimeConfig.TrackingNeutralDistance));
        Check(b, ref pass, ref fail, "tracking strong native ceiling", Near(dStrong, RebirthProgressionRuntimeConfig.TrackingMaxDistance) && dStrong <= 150f);

        float aWeak = RebirthResourceFieldSkillService.GetTrackingAcquireSeconds(-50f);
        float aZero = RebirthResourceFieldSkillService.GetTrackingAcquireSeconds(0f);
        float aStrong = RebirthResourceFieldSkillService.GetTrackingAcquireSeconds(100f);
        Check(b, ref pass, ref fail, "tracking acquisition ordered", aWeak > aZero && aZero > aStrong);
        Check(b, ref pass, ref fail, "tracking weak acquisition", Near(aWeak, RebirthProgressionRuntimeConfig.TrackingNegativeAcquireSeconds));
        Check(b, ref pass, ref fail, "tracking neutral acquisition", Near(aZero, RebirthProgressionRuntimeConfig.TrackingNeutralAcquireSeconds));
        Check(b, ref pass, ref fail, "tracking strong acquisition nonzero", Near(aStrong, RebirthProgressionRuntimeConfig.TrackingPositiveAcquireSeconds) && aStrong >= 0.5f);

        Check(b, ref pass, ref fail, "tracking negative tier easy only", RebirthResourceFieldSkillService.GetTrackingTier(-50f) == 1);
        Check(b, ref pass, ref fail, "tracking neutral tier easy only", RebirthResourceFieldSkillService.GetTrackingTier(0f) == 1);
        Check(b, ref pass, ref fail, "tracking tier2 threshold", RebirthResourceFieldSkillService.GetTrackingTier(1f) == 2 && RebirthResourceFieldSkillService.GetTrackingTier(25f) == 2);
        Check(b, ref pass, ref fail, "tracking tier3 threshold", RebirthResourceFieldSkillService.GetTrackingTier(26f) == 3 && RebirthResourceFieldSkillService.GetTrackingTier(50f) == 3);
        Check(b, ref pass, ref fail, "tracking tier4 threshold", RebirthResourceFieldSkillService.GetTrackingTier(51f) == 4 && RebirthResourceFieldSkillService.GetTrackingTier(75f) == 4);
        Check(b, ref pass, ref fail, "tracking tier5 threshold", RebirthResourceFieldSkillService.GetTrackingTier(76f) == 5 && RebirthResourceFieldSkillService.GetTrackingTier(100f) == 5);
        Check(b, ref pass, ref fail, "tracking repeat guard", RebirthProgressionRuntimeConfig.TrackingRepeatSeconds >= 60f);
        Check(b, ref pass, ref fail, "tracking follow threshold", RebirthProgressionRuntimeConfig.TrackingFollowDistance >= 10f);
        Check(b, ref pass, ref fail, "tracking awards bounded", RebirthProgressionRuntimeConfig.TrackingAcquireAward > 0f && RebirthProgressionRuntimeConfig.TrackingAcquireAward <= 0.50f && RebirthProgressionRuntimeConfig.TrackingFollowAward > 0f && RebirthProgressionRuntimeConfig.TrackingFollowAward <= 0.25f);
        Check(b, ref pass, ref fail, "sample throttle", RebirthProgressionRuntimeConfig.ResourceFieldSampleSeconds >= 0.5f && RebirthProgressionRuntimeConfig.ResourceFieldPassiveSyncSeconds >= 0.25f);
        Check(b, ref pass, ref fail, "teleport guard", RebirthProgressionRuntimeConfig.ResourceFieldMaxSampleDistance >= 3f && RebirthProgressionRuntimeConfig.ResourceFieldMaxSampleDistance <= 20f);

        b.Insert(0, "[REBIRTH Survivor Chunk 6 Resource/Field vectors] " + (fail == 0 ? "PASS" : "FAIL") + " passed=" + pass + "/" + (pass + fail) + "\n");
        return b.ToString().TrimEnd();
    }

    private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.0005f; }
    private static void Check(StringBuilder b, ref int pass, ref int fail, string name, bool ok)
    {
        if (ok) { pass++; b.AppendLine("  PASS " + name); }
        else { fail++; b.AppendLine("  FAIL " + name); }
    }
}
