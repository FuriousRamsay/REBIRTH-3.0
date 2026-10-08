#nullable disable

public enum RebirthHealingOverlapDenialReason
{
    None = 0,
    HealingAlreadySufficient = 1
}

public readonly struct RebirthHealingOverlapDecision
{
    public readonly bool Allowed;
    public readonly RebirthHealingOverlapDenialReason Reason;
    public readonly float PendingMedicalHealing;

    public RebirthHealingOverlapDecision(bool allowed, RebirthHealingOverlapDenialReason reason, float pendingMedicalHealing)
    {
        Allowed = allowed;
        Reason = reason;
        PendingMedicalHealing = pendingMedicalHealing;
    }
}

/// <summary>
/// Shared REBIRTH policy for direct medical use and future quick-treatment callers.
/// The default preserves the 2.6 10-health threshold while allowing the sandbox
/// option to select Off / 10 / 20 / 30 pending health. The bleeding exception
/// remains consistent across all callers.
/// </summary>
public static class RebirthHealingOverlapPolicy
{
    public const float LegacyPendingHealingThreshold = 10f;
    public const string PendingMedicalHealingCVar = "medicalRegHealthAmount";
    public const string BleedingBuff = "buffInjuryBleeding";

    public static RebirthHealingOverlapDecision Evaluate(EntityAlive patient, bool treatmentStopsBleeding)
    {
        if (patient == null || !RebirthHealingOverlapRuntimePolicy.Enabled)
            return new RebirthHealingOverlapDecision(true, RebirthHealingOverlapDenialReason.None, 0f);

        float pending = patient.Buffs.GetCustomVar(PendingMedicalHealingCVar);
        float threshold = RebirthHealingOverlapRuntimePolicy.PendingHealingThreshold;
        if (pending <= threshold)
            return new RebirthHealingOverlapDecision(true, RebirthHealingOverlapDenialReason.None, pending);

        if (treatmentStopsBleeding && patient.Buffs.HasBuff(BleedingBuff))
            return new RebirthHealingOverlapDecision(true, RebirthHealingOverlapDenialReason.None, pending);

        return new RebirthHealingOverlapDecision(false, RebirthHealingOverlapDenialReason.HealingAlreadySufficient, pending);
    }
}

public static class RebirthHealingOverlapRuntimePolicy
{
    private static RebirthHealingOverlapThreshold configuredThreshold = RebirthHealingOverlapThreshold.Health10;
    private static float pendingHealingThreshold = 10f;

    public static bool Enabled { get { return configuredThreshold != RebirthHealingOverlapThreshold.Off; } }
    public static RebirthHealingOverlapThreshold ConfiguredThreshold { get { return configuredThreshold; } }
    public static float PendingHealingThreshold { get { return pendingHealingThreshold; } }

    public static void SetThreshold(RebirthHealingOverlapThreshold value)
    {
        configuredThreshold = RebirthHealingOverlapThresholdPolicy.Normalize(value);
        pendingHealingThreshold = RebirthHealingOverlapThresholdPolicy.ToHealth(configuredThreshold);
    }
}
