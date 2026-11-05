using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine.Scripting;

internal static class RebirthPurgeBiomeHazardPolicy
{
    // Exact installed native biome progression family. Pipe/fire-trap "Hazard"
    // buffs and ordinary weather/injury effects are deliberately outside this set.
    internal static readonly HashSet<string> Buffs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "buffBiomeProgressionCheck", "buffForest_Hazard",
        "buffBurnt_Hazard", "buffBurnt_Hazard_Over", "buffBurnt_Hazard_Recover", "buffBurnt_Hazard01", "buffBurnt_Hazard02",
        "buffDesert_Hazard", "buffDesert_Hazard_Over", "buffDesert_Hazard_Recover", "buffDesert_Hazard01", "buffDesert_Hazard02",
        "buffSnow_Hazard", "buffSnow_Hazard_Over", "buffSnow_Hazard_Recover", "buffSnow_Hazard01", "buffSnow_Hazard02",
        "buffWasteland_Hazard", "buffWasteland_Hazard_Over", "buffWasteland_Hazard_Recover", "buffWasteland_Hazard01", "buffWasteland_Hazard02"
    };
    private static readonly string[] Variables =
    {
        "$BiomeProgressionOn",
        "$BurntHazardTimer", "$BurntHazardTimerMax", ".BurntHazardTimerDisplay",
        "$DesertHazardTimer", "$DesertHazardTimerMax", ".DesertHazardTimerDisplay",
        "$SnowHazardTimer", "$SnowHazardTimerMax", ".SnowHazardTimerDisplay",
        "$WastelandHazardTimer", "$WastelandHazardTimerMax", ".WastelandHazardTimerDisplay"
    };
    private sealed class Cleaned { internal int Revision = -1; }
    private static ConditionalWeakTable<EntityBuffs, Cleaned> cleaned = new ConditionalWeakTable<EntityBuffs, Cleaned>();
    internal static void Reset() { cleaned = new ConditionalWeakTable<EntityBuffs, Cleaned>(); }
    internal static bool Suppress(EntityBuffs owner, string name) => RebirthSandboxOptionManager.Current.IsPurge
        && owner?.parent is EntityPlayer && name != null && Buffs.Contains(name);
    internal static void Clean(EntityBuffs buffs)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || !(buffs?.parent is EntityPlayer)) return;
        var receipt = cleaned.GetValue(buffs, _ => new Cleaned());
        int revision = RebirthSandboxOptionManager.Current.Revision;
        if (receipt.Revision == revision) return;
        // Remove normal native buff state through its synchronization path.
        foreach (string name in Buffs) if (buffs.HasBuff(name)) buffs.RemoveBuff(name);
        foreach (string variable in Variables) buffs.RemoveCustomVar(variable);
        receipt.Revision = revision;
    }
}
[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.AddBuff),
    new Type[] { typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
internal static class RebirthPurgeBiomeBuffAddHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityBuffs __instance, string _name, ref EntityBuffs.BuffStatus __result)
    {
        if (!RebirthPurgeBiomeHazardPolicy.Suppress(__instance, _name)) return true;
        __result = EntityBuffs.BuffStatus.FailedGameStat; return false;
    }
}
[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.Tick))]
internal static class RebirthPurgeBiomeBuffTickHook
{
    private static void Prefix(EntityBuffs __instance) { RebirthPurgeBiomeHazardPolicy.Clean(__instance); }
}
[Preserve]
public sealed class RebirthPurgeThemeModApi : IModApi
{
    private static bool installed;
    public void InitMod(Mod mod)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || installed) return;
        var harmony = new Harmony("rebirth.purge.theme");
        try
        {
            harmony.CreateClassProcessor(typeof(RebirthPurgeLootRefreshHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeBiomeBuffAddHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeBiomeBuffTickHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeTraderGenerationHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeCachedTraderListingHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeQuestCandidateHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeNearestPoiHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeQuestRemovalScopeHook)).Patch();
            ModEvents.GameStarting.RegisterHandler(Starting);
            installed = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
    }
    private static void Starting(ref ModEvents.SGameStartingData data) { RebirthPurgeBiomeHazardPolicy.Reset(); }
}
