using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

#nullable disable

/// <summary>
/// Narrows the native 3.0 Headshots Only sandbox option to the REBIRTH 2.6 behavioral scope.
/// No additional option is introduced: when PassiveEffects.HeadShotOnly is inactive this module is inert.
/// </summary>
public static class RebirthHeadshotOnlyBehavior
{
    private const float RangedBodyDamageFactor = 0.05f;
    private const float MeleeBodyDamageFactor = 0.025f;
    private const float HelmetHeadDamageFactor = 0.85f;

    private static readonly FastTags<TagGroup.Global> TagNoHeadshot = FastTags<TagGroup.Global>.Parse("noheadshot");
    private static readonly FastTags<TagGroup.Global> TagHelmet = FastTags<TagGroup.Global>.Parse("helmet");
    private static readonly FastTags<TagGroup.Global> TagRanged = FastTags<TagGroup.Global>.Parse("ranged");

    [ThreadStatic]
    private static int s_nativeFilterDepth;

    public static void ProcessDamageResponsePrefix(EntityAlive __instance, ref DamageResponse _dmResponse)
    {
        s_nativeFilterDepth++;

        if (__instance == null || _dmResponse.Source == null)
            return;

        World world = GameManager.Instance == null ? null : GameManager.Instance.World;
        if (world == null)
            return;

        EntityPlayer attacker = world.GetEntity(_dmResponse.Source.getEntityId()) as EntityPlayer;
        if (attacker == null)
            return;

        // The existing native sandbox option owns enablement. This module adds no REBIRTH option.
        if (EffectManager.GetValue(PassiveEffects.HeadShotOnly, _entity: attacker) <= 0f)
            return;

        // Base 3.0 applies HeadShotOnly to every EntityAlive. REBIRTH 2.6 limited it to
        // non-animal zombies and allowed explicit entity-tag opt-outs.
        if (!IsRestrictedTarget(__instance))
            return;

        if (IsJunkTurretAttack(attacker, _dmResponse.Source))
            return;

        // Heat damage remains fully effective even when it does not resolve to the head.
        if (_dmResponse.Source.GetDamageType() == EnumDamageTypes.Heat)
            return;

        bool isHeadshot = (_dmResponse.HitBodyPart & EnumBodyPartHit.Head) != EnumBodyPartHit.None;
        if (isHeadshot)
        {
            if (__instance.HasAnyTags(TagHelmet))
                ScaleDamage(ref _dmResponse, HelmetHeadDamageFactor, __instance.Health);
            return;
        }

        bool ranged = IsRangedPlayerAttack(_dmResponse.Source);
        ScaleDamage(ref _dmResponse, ranged ? RangedBodyDamageFactor : MeleeBodyDamageFactor, __instance.Health);
    }

    public static Exception ProcessDamageResponseFinalizer(Exception __exception)
    {
        if (s_nativeFilterDepth > 0)
            s_nativeFilterDepth--;
        return __exception;
    }

    /// <summary>
    /// Injected immediately after the native HeadShotOnly passive lookup inside
    /// EntityAlive.ProcessDamageResponseLocal. The prefix above has already applied the
    /// REBIRTH result, so the original all-entity zero-damage branch must be suppressed.
    /// </summary>
    public static float FilterNativeHeadshotOnlyValue(float nativeValue)
    {
        return s_nativeFilterDepth > 0 ? 0f : nativeValue;
    }

    private static bool IsRestrictedTarget(EntityAlive target)
    {
        if (!(target is EntityZombie))
            return false;
        if (target is EntityAnimal)
            return false;
        if (target.HasAnyTags(TagNoHeadshot))
            return false;

        string className = target.EntityClass == null ? null : target.EntityClass.entityClassName;
        if (string.IsNullOrEmpty(className))
            return true;

        // Preserve the final 2.6 animal-zombie exclusion without requiring a new XML tag on
        // every inherited custom class. The explicit noheadshot tag remains the preferred opt-out.
        return className.IndexOf("animal", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool IsJunkTurretAttack(EntityPlayer attacker, DamageSource source)
    {
        if (source == null || source.AttackingItem == null || source.AttackingItem.ItemClass == null)
            return false;

        string attackingItemName = source.AttackingItem.ItemClass.GetItemName();
        if (string.IsNullOrEmpty(attackingItemName) ||
            attackingItemName.IndexOf("JunkTurret", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        // Preserve the 2.6 special-case using immutable attack provenance. A player may
        // switch the held slot before a delayed hit resolves, so current inventory state
        // must not reclassify the already-created damage event.
        return !string.Equals(attackingItemName, "gunBotT2JunkTurret", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRangedPlayerAttack(DamageSource source)
    {
        return source != null
            && source.AttackingItem != null
            && source.AttackingItem.ItemClass != null
            && source.AttackingItem.ItemClass.HasAnyTags(TagRanged);
    }

    private static void ScaleDamage(ref DamageResponse response, float factor, int targetHealth)
    {
        int original = response.Strength;
        if (original <= 0)
            return;

        int adjusted = (int)(original * factor);
        if (adjusted < 0)
            adjusted = 0;

        response.Strength = adjusted;
        if (response.Fatal && adjusted < targetHealth)
            response.Fatal = false;
    }
}

public static class RebirthHeadshotOnlyBehaviorPatches
{
    public static IEnumerable<CodeInstruction> ProcessDamageResponseTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        MethodInfo filter = AccessTools.Method(
            typeof(RebirthHeadshotOnlyBehavior),
            nameof(RebirthHeadshotOnlyBehavior.FilterNativeHeadshotOnlyValue),
            new[] { typeof(float) });
        if (filter == null)
            throw new InvalidOperationException("REBIRTH Headshot Only filter signature is unavailable.");
        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            MethodInfo called = codes[i].operand as MethodInfo;
            if (called == null || called.DeclaringType != typeof(EffectManager) || called.Name != "GetValue")
                continue;

            bool headshotConstantNearby = false;
            int start = Math.Max(0, i - 48);
            for (int j = start; j < i; j++)
            {
                int value;
                if (TryGetLoadedInt(codes[j], out value) && value == (int)PassiveEffects.HeadShotOnly)
                {
                    headshotConstantNearby = true;
                    break;
                }
            }

            if (!headshotConstantNearby)
                continue;

            codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, filter));
            patched = true;
            break;
        }

        if (!patched)
            Log.Error("[Rebirth][HeadshotOnly] Could not locate the native HeadShotOnly lookup in EntityAlive.ProcessDamageResponseLocal.");

        return codes;
    }

    private static bool TryGetLoadedInt(CodeInstruction instruction, out int value)
    {
        value = 0;
        if (instruction.opcode == OpCodes.Ldc_I4_M1) { value = -1; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_0) { value = 0; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_1) { value = 1; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_2) { value = 2; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_3) { value = 3; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_4) { value = 4; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_5) { value = 5; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_6) { value = 6; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_7) { value = 7; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_8) { value = 8; return true; }
        if (instruction.opcode == OpCodes.Ldc_I4_S) { value = Convert.ToInt32(instruction.operand); return true; }
        if (instruction.opcode == OpCodes.Ldc_I4) { value = Convert.ToInt32(instruction.operand); return true; }
        return false;
    }
}

public static class RebirthHeadshotOnlyBehaviorInstaller
{
    public const string HarmonyId = "rebirth.3.1.nativeheadshotonly.behavior";
    private static readonly Harmony s_harmony = new Harmony(HarmonyId);
    private static bool s_installed;

    public static bool Installed { get { return s_installed; } }

    public static void Install()
    {
        if (s_installed)
            return;

        RebirthHarmonyBootstrap.PatchClassOnce(s_harmony, typeof(RebirthHeadshotOnlyDamageResponsePatch));

        s_installed = true;
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[Rebirth][HeadshotOnly] Native Headshots Only behavior exceptions installed."); }
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthHeadshotOnlyDamageResponsePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(EntityAlive __instance, ref DamageResponse _dmResponse)
    {
        RebirthHeadshotOnlyBehavior.ProcessDamageResponsePrefix(__instance, ref _dmResponse);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return RebirthHeadshotOnlyBehaviorPatches.ProcessDamageResponseTranspiler(instructions);
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception)
    {
        return RebirthHeadshotOnlyBehavior.ProcessDamageResponseFinalizer(__exception);
    }
}
