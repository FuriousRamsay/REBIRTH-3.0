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
            harmony.CreateClassProcessor(typeof(RebirthPurgeTraderListingHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeCachedTraderListingHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeNewTraderQuestHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeSharedAcceptanceHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeShareEntryHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeServerShareHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPurgeDialogOfferHook)).Patch();
            ModEvents.GameStarting.RegisterHandler(Starting);
            installed = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
    }
    private static void Starting(ref ModEvents.SGameStartingData data) { RebirthPurgeBiomeHazardPolicy.Reset(); }
}
[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.PopulateActiveQuests))]
internal static class RebirthPurgeTraderGenerationHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref List<Quest> __result)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        __result = new List<Quest>(); return false;
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref List<Quest> __result)
    {
        if (RebirthSandboxOptionManager.Current.IsPurge) __result = new List<Quest>();
    }
}
[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.SetActiveQuests))]
internal static class RebirthPurgeTraderListingHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityTrader __instance)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        __instance.activeQuests = new List<Quest>(); return false;
    }
}
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.AddQuest))]
internal static class RebirthPurgeNewTraderQuestHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Quest q, Quest.QuestSource questSource)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        // Loading existing owned quests is preserved. No inventory/custody is deleted.
        return questSource != Quest.QuestSource.Trader &&
            !(questSource == Quest.QuestSource.PartyShare && RebirthPurgeTraderJobPolicy.IsTraderJob(q));
    }
}
internal static class RebirthPurgeTraderJobPolicy
{
    internal static bool IsTraderJob(Quest quest) => quest != null &&
        (quest.PositionData.ContainsKey(Quest.PositionDataTypes.TraderPosition)
        || RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(quest));
}
[HarmonyPatch(typeof(PartyQuests), nameof(PartyQuests.AcceptSharedQuest))]
internal static class RebirthPurgeSharedAcceptanceHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(SharedQuestEntry _sharedQuest)
    {
        return !RebirthSandboxOptionManager.Current.IsPurge || !RebirthPurgeTraderJobPolicy.IsTraderJob(_sharedQuest?.Quest);
    }
}
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.AddSharedQuestEntry))]
internal static class RebirthPurgeShareEntryHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NetPackageSharedQuest.SharedQuestData sqd, ref bool __result)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || sqd == null) return true;
        var quest = string.IsNullOrEmpty(sqd.questID) ? null : QuestClass.CreateQuest(sqd.questID);
        if (!RebirthPurgeTraderJobPolicy.IsTraderJob(quest) && !RebirthUtilities.IsVanillaTrader(sqd.questGiverID)) return true;
        // No new trader/job sharing in Purge. Existing shared custody remains owned.
        __result = false; return false;
    }
}


[HarmonyPatch(typeof(QuestEventManager), nameof(QuestEventManager.GetQuestList),
    new Type[] { typeof(World), typeof(int), typeof(int) })]
internal static class RebirthPurgeCachedTraderListingHook
{
    // This is the offer cache, not the player's owned quest journal.
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref List<Quest> __result)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        __result = new List<Quest>(); return false;
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref List<Quest> __result)
    {
        if (RebirthSandboxOptionManager.Current.IsPurge) __result = new List<Quest>();
    }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.QuestShareServer),
    new Type[] { typeof(NetPackageSharedQuest.SharedQuestData) })]
internal static class RebirthPurgeServerShareHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NetPackageSharedQuest.SharedQuestData sqd)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || sqd == null
            || sqd.questEvent != NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.ShareQuest) return true;
        // Removing members/owned shares must remain functional in existing saves.
        if (RebirthUtilities.IsVanillaTrader(sqd.questGiverID)) return false;
        var quest = string.IsNullOrEmpty(sqd.questID) ? null : QuestClass.CreateQuest(sqd.questID);
        return !RebirthPurgeTraderJobPolicy.IsTraderJob(quest);
    }
}
[HarmonyPatch(typeof(DialogActionAddQuest), nameof(DialogActionAddQuest.PerformAction),
    new Type[] { typeof(EntityPlayer) })]
internal static class RebirthPurgeDialogOfferHook
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(DialogActionAddQuest __instance, EntityPlayer player)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        if (RebirthPurgeTraderJobPolicy.IsTraderJob(__instance?.Quest)) return false;
        return !(player is EntityPlayerLocal local && local.PlayerUI?.xui?.Dialog?.Respondent is EntityTrader);
    }
}
