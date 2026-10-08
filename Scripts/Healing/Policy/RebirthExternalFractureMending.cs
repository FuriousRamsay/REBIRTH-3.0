using System;
using HarmonyLib;
using UnityEngine;

/// <summary>Freezes external healer Medicine for native delayed fracture-buff initialization.</summary>
public static class RebirthExternalFractureMending
{
    private const string Prefix = "$rebirthExternalMending_";

    internal static bool IsTreatmentBuff(string name) => CanonicalName(name) != null;

    private static string CanonicalName(string name)
    {
        // Native BuffClass and BuffValue.Read lowercase their names. XML action names may
        // retain author casing; all marker/event paths must resolve to the same key.
        if (string.Equals(name, "buffLegSplinted", StringComparison.OrdinalIgnoreCase)) return "buffLegSplinted";
        if (string.Equals(name, "buffLegCast", StringComparison.OrdinalIgnoreCase)) return "buffLegCast";
        if (string.Equals(name, "buffArmSplinted", StringComparison.OrdinalIgnoreCase)) return "buffArmSplinted";
        if (string.Equals(name, "buffArmCast", StringComparison.OrdinalIgnoreCase)) return "buffArmCast";
        return null;
    }

    private static string BaseCVar(string name)
    {
        return name == "buffLegSplinted" || name == "buffLegCast"
            ? "$legTreatedCritHealingBase" : "$armTreatedCritHealingBase";
    }

    internal static void Stage(EntityBuffs buffs, string name, int instigatorId, BuffValue before)
    {
        name = CanonicalName(name);
        EntityAlive patient = buffs?.parent;
        if (!IsTreatmentBuff(name) || buffs == null) return;
        BuffValue added = buffs.GetBuff(name);
        if (added == null || ReferenceEquals(added, before)) return;
        // A replacement native buff cannot inherit an earlier pending factor, even if
        // admission of this new treatment fails or the native instigator ID is reused.
        Clear(buffs, Prefix + name);
        if (!(patient is EntityPlayer) || patient.world == null || patient.world.IsRemote() ||
            !ReferenceEquals(patient.world.GetEntity(patient.entityId), patient) ||
            patient.IsDead() || added.Started || added.Remove ||
            added.Invalid || added.InstigatorId != instigatorId) return;
        EntityPlayer healer = patient.world.GetEntity(instigatorId) as EntityPlayer;
        if (!RebirthExternalTreatmentTargetPolicy.Allows(healer != null && !healer.IsDead(),
            true, true, healer != null && ReferenceEquals(healer.world, patient.world),
            ReferenceEquals(healer, patient), healer == null ? float.NaN :
                Vector3.SqrMagnitude(healer.position - patient.position))) return;
        float skill;
        if (!RebirthServiceCraftSkillService.TryGetSkillValue(healer, "skill.medicine", out skill) ||
            float.IsNaN(skill) || float.IsInfinity(skill)) return;
        float delta = skill < 0f
            ? Mathf.Clamp01(-skill / 50f) * RebirthProgressionRuntimeConfig.MedicineFractureHealingNegative
            : Mathf.Clamp01(skill / 100f) * RebirthProgressionRuntimeConfig.MedicineFractureHealingPositive;
        float factor = Mathf.Max(0.10f, 1f + delta);
        if (float.IsNaN(factor) || float.IsInfinity(factor)) return;
        // Full native instigator ID is split into exactly representable 16-bit CVars.
        // The frozen factor, not a future entity-ID lookup, survives a save before buff start.
        string key = Prefix + name;
        buffs.SetCustomVar(key + "_lo", instigatorId & 65535, false);
        buffs.SetCustomVar(key + "_hi", (int)((uint)instigatorId >> 16), false);
        buffs.SetCustomVar(key, factor, false);
    }

    internal static void CompleteNativeEvent(MinEventTypes type, BuffClass definition, MinEventParams context)
    {
        EntityAlive patient = context?.Self;
        BuffValue buff = context?.Buff;
        string name = CanonicalName(definition?.Name);
        if (!IsTreatmentBuff(name) || patient?.Buffs == null || patient.world == null ||
            patient.world.IsRemote() || !ReferenceEquals(patient.world.GetEntity(patient.entityId), patient) ||
            buff == null || !ReferenceEquals(patient.Buffs.GetBuff(name), buff)) return;
        string key = Prefix + name;
        if (type == MinEventTypes.onSelfBuffRemove)
        {
            Clear(patient.Buffs, key);
            return;
        }
        if (type != MinEventTypes.onSelfBuffStart || buff.Started || buff.Remove || buff.Invalid || patient.IsDead()) return;
        float factor = patient.Buffs.GetCustomVar(key);
        if (!(factor > 0f) || float.IsNaN(factor) || float.IsInfinity(factor)) return;
        float low = patient.Buffs.GetCustomVar(key + "_lo"), high = patient.Buffs.GetCustomVar(key + "_hi");
        if (low != (buff.InstigatorId & 65535) || high != (int)((uint)buff.InstigatorId >> 16))
        {
            Clear(patient.Buffs, key);
            return;
        }
        float nativeBase = patient.Buffs.GetCustomVar(BaseCVar(name));
        if (!(nativeBase > 0f) || float.IsNaN(nativeBase) || float.IsInfinity(nativeBase)) return;
        float adjusted = nativeBase * factor;
        if (float.IsNaN(adjusted) || float.IsInfinity(adjusted)) return;
        patient.Buffs.SetCustomVar(BaseCVar(name), Mathf.Max(0.001f, adjusted));
        Clear(patient.Buffs, key);
    }

    private static void Clear(EntityBuffs buffs, string key)
    {
        buffs.SetCustomVar(key, 0f, false);
        buffs.SetCustomVar(key + "_lo", 0f, false);
        buffs.SetCustomVar(key + "_hi", 0f, false);
    }
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.AddBuff),
    new Type[] { typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
internal static class RebirthExternalFractureBuffAddedPatch
{
    static void Prefix(EntityBuffs __instance, string _name, out BuffValue __state)
    {
        __state = RebirthExternalFractureMending.IsTreatmentBuff(_name) ? __instance.GetBuff(_name) : null;
    }
    static void Postfix(EntityBuffs __instance, string _name, int _instigatorId,
        EntityBuffs.BuffStatus __result, BuffValue __state)
    {
        if (__result == EntityBuffs.BuffStatus.Added)
            RebirthExternalFractureMending.Stage(__instance, _name, _instigatorId, __state);
    }
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.FireEvent),
    new Type[] { typeof(MinEventTypes), typeof(BuffClass), typeof(MinEventParams) })]
internal static class RebirthExternalFractureBuffEventPatch
{
    static void Postfix(MinEventTypes _eventType, BuffClass _buffClass, MinEventParams _params)
    {
        RebirthExternalFractureMending.CompleteNativeEvent(_eventType, _buffClass, _params);
    }
}
