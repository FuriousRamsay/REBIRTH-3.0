using System;
using HarmonyLib;

#nullable disable

public enum RebirthAlwaysStaggerDecisionReason
{
    Allowed,
    Disabled,
    NotAuthoritative,
    InvalidTarget,
    NotEnemy,
    AnimalExcluded,
    NotHeadshot,
    ZeroDamage,
    Fatal,
    NotDirectHit
}

public static class RebirthAlwaysStaggerRuntimePolicy
{
    private static RebirthAlwaysStaggerMode s_mode = RebirthAlwaysStaggerMode.AllQualifyingHits;
    public static RebirthAlwaysStaggerMode Mode { get { return s_mode; } }
    public static bool PotentiallyEnabled { get { return s_mode != RebirthAlwaysStaggerMode.Disabled; } }

    public static void SetMode(RebirthAlwaysStaggerMode mode)
    {
        if (mode < RebirthAlwaysStaggerMode.Disabled || mode > RebirthAlwaysStaggerMode.AllQualifyingHits)
            mode = RebirthAlwaysStaggerMode.AllQualifyingHits;
        s_mode = mode;
    }
}

public static class RebirthAlwaysStaggerPolicy
{
    public static bool DebugEnabled;

    public static bool ShouldApply(EntityAlive target, DamageResponse response, out RebirthAlwaysStaggerDecisionReason reason)
    {
        if (!RebirthAlwaysStaggerRuntimePolicy.PotentiallyEnabled)
        { reason = RebirthAlwaysStaggerDecisionReason.Disabled; return false; }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && !connection.IsServer)
        { reason = RebirthAlwaysStaggerDecisionReason.NotAuthoritative; return false; }

        if (target == null)
        { reason = RebirthAlwaysStaggerDecisionReason.InvalidTarget; return false; }
        if (!(target is EntityEnemy))
        { reason = RebirthAlwaysStaggerDecisionReason.NotEnemy; return false; }
        if (IsAnimal(target))
        { reason = RebirthAlwaysStaggerDecisionReason.AnimalExcluded; return false; }
        // Bleeding, burning and other buff damage are not weapon impacts.
        // Repeated forced pain animations can interrupt the end of a get-up.
        if (response.Source == null || response.Source.damageSource == EnumDamageSource.Internal ||
            response.Source.BuffClass != null)
        { reason = RebirthAlwaysStaggerDecisionReason.NotDirectHit; return false; }
        if (response.Strength <= 0)
        { reason = RebirthAlwaysStaggerDecisionReason.ZeroDamage; return false; }
        if (response.Fatal)
        { reason = RebirthAlwaysStaggerDecisionReason.Fatal; return false; }
        if (RebirthAlwaysStaggerRuntimePolicy.Mode == RebirthAlwaysStaggerMode.HeadshotsOnly &&
            (response.HitBodyPart & EnumBodyPartHit.Head) == EnumBodyPartHit.None)
        { reason = RebirthAlwaysStaggerDecisionReason.NotHeadshot; return false; }

        reason = RebirthAlwaysStaggerDecisionReason.Allowed;
        return true;
    }

    private static bool IsAnimal(EntityAlive target)
    {
        if (target is EntityAnimal) return true;
        string className = target.EntityClass == null ? null : target.EntityClass.entityClassName;
        return !string.IsNullOrEmpty(className) && className.IndexOf("animal", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void CancelPendingMeleeImpact(EntityAlive target)
    {
        if (target == null || target.emodel == null || target.emodel.avatarController == null) return;
        AvatarController controller = target.emodel.avatarController;
        AvatarZombieController zombie = controller as AvatarZombieController;
        if (zombie != null)
        {
            zombie.isAttackImpact = false;
            zombie.attackPlayingTime = 0f;
            return;
        }
        AvatarSDCSController sdcs = controller as AvatarSDCSController;
        if (sdcs != null)
        {
            sdcs.isAttackImpact = false;
            sdcs.timeAttackAnimationPlaying = 0f;
            return;
        }
        LegacyAvatarController legacy = controller as LegacyAvatarController;
        if (legacy != null)
        {
            legacy.isAttackImpact = false;
            legacy.timeAttackAnimationPlaying = 0f;
        }
    }

    public static void Trace(EntityAlive target, DamageResponse response, RebirthAlwaysStaggerDecisionReason reason, string phase)
    {
        if (!DebugEnabled) return;
        Log.Out("[Rebirth][AlwaysStagger] phase=" + phase + " mode=" + RebirthAlwaysStaggerRuntimePolicy.Mode +
                " reason=" + reason + " entity=" + (target == null ? "null" : target.EntityName) +
                " strength=" + response.Strength + " fatal=" + response.Fatal +
                " painHit=" + response.PainHit + " bodyPart=" + response.HitBodyPart);
    }
}


public struct RebirthAlwaysStaggerPainOverrideState
{
    public bool Applied;
    public float PainResistPercent;
    public float PainHitsFelt;
}

public static class RebirthAlwaysStaggerPatches
{
    public static void NormalizeZombieBleedResponse(EntityAlive target, ref DamageResponse response)
    {
        var source = response.Source;
        if (!(target is EntityZombie) || response.Fatal || source == null ||
            source.damageSource != EnumDamageSource.Internal ||
            source.damageType != EnumDamageTypes.BloodLoss || source.BuffClass == null) return;
        // Native threshold checks can reuse StunProne/StunKnee accumulated by an earlier
        // strike, even for a source whose CanStun is false. Blood loss must not renew
        // that knockdown. Leave HP, ownership, credit and an existing stun untouched.
        response.Stun = EnumEntityStunType.None;
        response.StunDuration = 0f;
        response.PainHit = false;
    }
    public static void DamageEntityLocalPostfix(EntityAlive __instance, ref DamageResponse __result)
    {
        NormalizeZombieBleedResponse(__instance, ref __result);
        RebirthAlwaysStaggerDecisionReason reason;
        if (!RebirthAlwaysStaggerPolicy.ShouldApply(__instance, __result, out reason))
        { RebirthAlwaysStaggerPolicy.Trace(__instance, __result, reason, "damageEntityLocal.postfix"); return; }
        __result.PainHit = true;
        RebirthAlwaysStaggerPolicy.CancelPendingMeleeImpact(__instance);
        RebirthAlwaysStaggerPolicy.Trace(__instance, __result, reason, "damageEntityLocal.postfix");
    }

    public static void ProcessDamageResponsePrefix(
        EntityAlive __instance,
        ref DamageResponse _dmResponse,
        ref RebirthAlwaysStaggerPainOverrideState state)
    {
        state = default(RebirthAlwaysStaggerPainOverrideState);
        NormalizeZombieBleedResponse(__instance, ref _dmResponse);
        RebirthAlwaysStaggerDecisionReason reason;
        if (!RebirthAlwaysStaggerPolicy.ShouldApply(__instance, _dmResponse, out reason)) return;

        state.Applied = true;
        state.PainResistPercent = __instance.painResistPercent;
        state.PainHitsFelt = __instance.painHitsFelt;
        _dmResponse.PainHit = true;
        __instance.painResistPercent = 0f;
        __instance.painHitsFelt = 0f;
        RebirthAlwaysStaggerPolicy.CancelPendingMeleeImpact(__instance);
    }

    public static void RestorePainOverride(
        EntityAlive __instance,
        ref RebirthAlwaysStaggerPainOverrideState state)
    {
        if (!state.Applied || __instance == null) return;
        __instance.painResistPercent = state.PainResistPercent;
        __instance.painHitsFelt = state.PainHitsFelt;
        state.Applied = false;
    }
}

public static class RebirthAlwaysStaggerPatchInstaller
{
    public const string HarmonyId = "rebirth.3.1.alwaysstagger";
    private static readonly Harmony s_harmony = new Harmony(HarmonyId);
    private static bool s_installed;
    public static bool Installed { get { return s_installed; } }

    public static void Install()
    {
        if (s_installed) return;
        RebirthHarmonyBootstrap.PatchClassOnce(s_harmony, typeof(RebirthAlwaysStaggerDamagePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(s_harmony, typeof(RebirthAlwaysStaggerResponsePatch));
        s_installed = true;
    }
}

[HarmonyPatch(typeof(EntityAlive), "damageEntityLocal")]
internal static class RebirthAlwaysStaggerDamagePatch
{
    private static void Postfix(EntityAlive __instance, ref DamageResponse __result)
    {
        RebirthAlwaysStaggerPatches.DamageEntityLocalPostfix(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthAlwaysStaggerResponsePatch
{
    private static void Prefix(
        EntityAlive __instance,
        ref DamageResponse _dmResponse,
        ref RebirthAlwaysStaggerPainOverrideState __state)
    {
        RebirthAlwaysStaggerPatches.ProcessDamageResponsePrefix(
            __instance, ref _dmResponse, ref __state);
    }

    private static void Postfix(
        EntityAlive __instance,
        ref RebirthAlwaysStaggerPainOverrideState __state)
    {
        RebirthAlwaysStaggerPatches.RestorePainOverride(__instance, ref __state);
    }

    private static Exception Finalizer(
        EntityAlive __instance,
        Exception __exception,
        ref RebirthAlwaysStaggerPainOverrideState __state)
    {
        RebirthAlwaysStaggerPatches.RestorePainOverride(__instance, ref __state);
        return __exception;
    }
}
