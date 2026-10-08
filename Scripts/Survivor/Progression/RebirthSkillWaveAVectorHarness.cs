using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

/// <summary>
/// Pure-data Chunk 5 regression vectors. These tests exercise signed endpoint math and the
/// bounded design invariants without requiring a live world, entity, trader or lock container.
/// </summary>
public static class RebirthSkillWaveAVectorHarness
{
    private sealed class Result
    {
        public int Passed;
        public readonly List<string> Failures = new List<string>();
    }

    public static string RunAll()
    {
        Result r = new Result();

        // Universal signed-curve invariants.
        Check(r, "zero is neutral", Near(RebirthSkillWaveAService.SignedEndpoint(0f, 0.5f, -0.4f), 0f));
        Check(r, "minus50 reaches negative endpoint", Near(RebirthSkillWaveAService.SignedEndpoint(-50f, 0.5f, -0.4f), 0.5f));
        Check(r, "plus100 reaches positive endpoint", Near(RebirthSkillWaveAService.SignedEndpoint(100f, 0.5f, -0.4f), -0.4f));
        Check(r, "negative interpolation", Near(RebirthSkillWaveAService.SignedEndpoint(-25f, 0.5f, -0.4f), 0.25f));
        Check(r, "positive interpolation", Near(RebirthSkillWaveAService.SignedEndpoint(50f, 0.5f, -0.4f), -0.2f));
        Check(r, "lower clamp", Near(RebirthSkillWaveAService.SignedEndpoint(-500f, 0.5f, -0.4f), 0.5f));
        Check(r, "upper clamp", Near(RebirthSkillWaveAService.SignedEndpoint(500f, 0.5f, -0.4f), -0.4f));

        // Lockpicking: weak is slower/more break-prone; strong is better, never instant/free.
        float lockTimeWeak = 1f + RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.LockpickNegativeTime, RebirthProgressionRuntimeConfig.LockpickPositiveTime);
        float lockTimeStrong = 1f + RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.LockpickNegativeTime, RebirthProgressionRuntimeConfig.LockpickPositiveTime);
        float lockBreakWeak = 1f + RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.LockpickNegativeBreak, RebirthProgressionRuntimeConfig.LockpickPositiveBreak);
        float lockBreakStrong = 1f + RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.LockpickNegativeBreak, RebirthProgressionRuntimeConfig.LockpickPositiveBreak);
        Check(r, "lock weak slower", lockTimeWeak > 1f);
        Check(r, "lock strong faster but nonzero", lockTimeStrong > 0f && lockTimeStrong < 1f);
        Check(r, "lock weak breaks more", lockBreakWeak > 1f);
        Check(r, "lock strong break multiplier nonzero", lockBreakStrong > 0f && lockBreakStrong < 1f);

        // Phase 10 splits practical commerce evidence: purchases train Bartering and sales train
        // Trading. Their passive-effect envelope remains bounded to the historical combined cap.
        float barterWeak = RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.BarterNegative, RebirthProgressionRuntimeConfig.BarterLegacyPositiveCap);
        float barterLegacyStrong = RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.BarterNegative, RebirthProgressionRuntimeConfig.BarterLegacyPositiveCap);
        float tradingStrong = RebirthProgressionRuntimeConfig.TradingPositive;
        Check(r, "barter weak compatibility remains negative", barterWeak < 0f);
        Check(r, "legacy barter positive is capped", barterLegacyStrong > 0f && barterLegacyStrong <= 0.04f + 0.0001f);
        Check(r, "trading positive is primary contribution", tradingStrong > barterLegacyStrong && tradingStrong <= 0.12f + 0.0001f);
        Check(r, "combined commerce cap stays historical", barterLegacyStrong + tradingStrong <= RebirthProgressionRuntimeConfig.BarterPositive + 0.0001f);

        // Athletics endpoints use three independent native effects.
        Check(r, "athletics weak jump penalty", RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.AthleticsNegativeJump, RebirthProgressionRuntimeConfig.AthleticsPositiveJump) < 0f);
        Check(r, "athletics strong jump benefit", RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.AthleticsNegativeJump, RebirthProgressionRuntimeConfig.AthleticsPositiveJump) > 0f);
        Check(r, "athletics weak stamina penalty", RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.AthleticsNegativeStamina, RebirthProgressionRuntimeConfig.AthleticsPositiveStamina) > 0f);
        Check(r, "athletics strong stamina benefit", RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.AthleticsNegativeStamina, RebirthProgressionRuntimeConfig.AthleticsPositiveStamina) < 0f);
        Check(r, "athletics weak fall tolerance worse", RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.AthleticsNegativeFall, RebirthProgressionRuntimeConfig.AthleticsPositiveFall) > 0f);
        Check(r, "athletics strong fall tolerance better", RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.AthleticsNegativeFall, RebirthProgressionRuntimeConfig.AthleticsPositiveFall) < 0f);
        Check(r, "walking is qualified exertion", RebirthSkillWaveAService.IsQualifiedLocomotionTagText("walking"));
        Check(r, "running is qualified exertion", RebirthSkillWaveAService.IsQualifiedLocomotionTagText("running"));
        Check(r, "crouch walking remains qualified exertion", RebirthSkillWaveAService.IsQualifiedLocomotionTagText("crouching,walking"));
        Check(r, "jump only is not exertion witness", !RebirthSkillWaveAService.IsQualifiedLocomotionTagText("jumping"));
        Check(r, "idle/empty is not exertion witness", !RebirthSkillWaveAService.IsQualifiedLocomotionTagText(string.Empty));
        Check(r, "vehicle token is not exertion witness", !RebirthSkillWaveAService.IsQualifiedLocomotionTagText("vehicle"));
        Check(r, "neutral locomotion distance is unchanged", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(10f, 1.53f, 1.53f, 1f, 12f), 10f));
        Check(r, "double movement speed does not double exertion", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(10f, 3.06f, 1.53f, 1f, 12f), 5f));
        Check(r, "half movement speed preserves active-time exertion", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(5f, 0.765f, 1.53f, 1f, 12f), 10f));
        Check(r, "mobility multiplier is normalized out", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(10f, 1.53f, 1.53f, 2f, 12f), 5f));
        Check(r, "invalid locomotion speed awards zero", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(10f, 0f, 1.53f, 1f, 12f), 0f));
        Check(r, "neutral equivalent uses existing sample ceiling", Near(RebirthSkillWaveAService.NormalizeLocomotionDistance(10f, 0.01f, 1.53f, 1f, 12f), 12f));
        Check(r, "neutral walk source constant exact", Near(RebirthSkillWaveAService.NeutralWalkSpeed, 1.53f));
        Check(r, "neutral run source constant exact", Near(RebirthSkillWaveAService.NeutralRunSpeed, 1.10f));
        Check(r, "neutral crouch source constant exact", Near(RebirthSkillWaveAService.NeutralCrouchSpeed, 1.04f));

        // Stealth changes real native noise/light multipliers without approaching invisibility.
        float stealthNoiseWeak = 1f + RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.StealthNegativeNoise, RebirthProgressionRuntimeConfig.StealthPositiveNoise);
        float stealthNoiseStrong = 1f + RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.StealthNegativeNoise, RebirthProgressionRuntimeConfig.StealthPositiveNoise);
        float stealthLightStrong = 1f + RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.StealthNegativeLight, RebirthProgressionRuntimeConfig.StealthPositiveLight);
        Check(r, "stealth weak noisier", stealthNoiseWeak > 1f);
        Check(r, "stealth strong quieter but not silent", stealthNoiseStrong > 0.5f && stealthNoiseStrong < 1f);
        Check(r, "stealth strong light still meaningful", stealthLightStrong > 0.5f && stealthLightStrong < 1f);

        // Armor Proficiency only offsets a fraction of burden. Positive +100 must never erase it.
        float armorWeakFactor = RebirthSkillWaveAService.SignedEndpoint(-50f, RebirthProgressionRuntimeConfig.ArmorNegativeBurden, RebirthProgressionRuntimeConfig.ArmorPositiveRecovery);
        float armorStrongFactor = RebirthSkillWaveAService.SignedEndpoint(100f, RebirthProgressionRuntimeConfig.ArmorNegativeBurden, RebirthProgressionRuntimeConfig.ArmorPositiveRecovery);
        const float heavyMobilityBurden = 0.075f;
        float weakNetBurden = heavyMobilityBurden - heavyMobilityBurden * armorWeakFactor;
        float strongNetBurden = heavyMobilityBurden - heavyMobilityBurden * armorStrongFactor;
        Check(r, "armor weak increases burden", armorWeakFactor < 0f && weakNetBurden > heavyMobilityBurden);
        Check(r, "armor strong restores only part", armorStrongFactor > 0f && armorStrongFactor < 1f && strongNetBurden > 0f && strongNetBurden < heavyMobilityBurden);
        Check(r, "armor configured recovery capped at half", armorStrongFactor <= 0.50f + 0.0001f);

        // Runtime cadence + LBD tuning invariants: passive synchronization must be throttled;
        // every award is positive/bounded and macro guards are nonzero.
        Check(r, "passive sync cadence bounded", RebirthProgressionRuntimeConfig.WaveAPassiveSyncSeconds >= 0.25f && RebirthProgressionRuntimeConfig.WaveAPassiveSyncSeconds <= 2f);
        Check(r, "movement sample cadence bounded", RebirthProgressionRuntimeConfig.WaveASampleSeconds >= 0.25f && RebirthProgressionRuntimeConfig.WaveASampleSeconds <= 2f);
        Check(r, "barter awards bounded", RebirthProgressionRuntimeConfig.BarterBaseAward > 0f && RebirthProgressionRuntimeConfig.BarterBaseAward + RebirthProgressionRuntimeConfig.BarterValueAwardCap <= 1f);
        Check(r, "barter global guard active", RebirthProgressionRuntimeConfig.BarterGlobalSeconds >= 1f);
        Check(r, "trading repeat guard active", RebirthProgressionRuntimeConfig.BarterRepeatSeconds >= 30f);
        Check(r, "trading minimum value active", RebirthProgressionRuntimeConfig.TradingMinimumValue > 0f && RebirthProgressionRuntimeConfig.TradingMinimumUnitValue > 0f);
        Check(r, "trading spoof ceiling active", RebirthProgressionRuntimeConfig.TradingValueSpoofMultiplier >= 5f);
        Check(r, "trading variety window active", RebirthProgressionRuntimeConfig.TradingVarietyWindowSeconds >= RebirthProgressionRuntimeConfig.BarterRepeatSeconds);
        Check(r, "barter loop guard active", RebirthProgressionRuntimeConfig.BarterLoopSeconds >= 60f);
        Check(r, "athletics distance guard active", RebirthProgressionRuntimeConfig.AthleticsDistance >= 10f);
        Check(r, "stealth movement award disabled", !RebirthSkillWaveAService.StealthMovementTrainingEnabled);
        Check(r, "combat equivalent cap active", RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit > 0f && RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit <= 6.0f + 0.0001f);
        Check(r, "armor distance guard active", RebirthProgressionRuntimeConfig.ArmorDistance >= 10f);

        int total = r.Passed + r.Failures.Count;
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH Survivor Skill Wave A] vectors: ").Append(r.Failures.Count == 0 ? "PASS" : "FAIL")
            .Append(" ").Append(r.Passed).Append('/').Append(total);
        for (int i = 0; i < r.Failures.Count; i++) b.Append("\n  FAIL: ").Append(r.Failures[i]);
        return b.ToString();
    }

    private static bool Near(float a, float b) { return Math.Abs(a - b) <= 0.0001f; }
    private static void Check(Result r, string name, bool ok) { if (ok) r.Passed++; else r.Failures.Add(name); }
}
