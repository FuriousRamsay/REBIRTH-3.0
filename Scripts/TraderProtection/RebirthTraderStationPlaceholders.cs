using System;
using System.Collections.Generic;
using HarmonyLib;

#nullable disable

/// <summary>
/// Random station helpers stay random in the wild, but a trader POI always gets the
/// matching broken station. Only newly generated chunks are affected: saved chunks keep their blocks.
/// </summary>
internal static class RebirthTraderStationPlaceholders
{
    [ThreadStatic] internal static int TraderPrefabDepth;
    private static readonly object Gate = new object();
    private static Dictionary<int, int> fixedTypes;

    private static Dictionary<int, int> FixedTypes()
    {
        lock (Gate)
        {
            if (fixedTypes != null) return fixedTypes;
            var map = new Dictionary<int, int>();
            Add(map, "cntForgeBustedRandomLootHelper", "cntForgeWorkstationBroken");
            Add(map, "cntWorkbenchBustedRandomLootHelper", "cntCollapsedWorkbench");
            Add(map, "cntCementMixerBustedRandomLootHelper", "cntCollapsedCementMixer");
            Add(map, "cntChemistryStationBustedRandomLootHelper", "cntCollapsedChemistryStation");
            return fixedTypes = map;
        }
    }

    private static void Add(Dictionary<int, int> map, string helper, string station)
    {
        Block source = Block.GetBlockByName(helper), target = Block.GetBlockByName(station);
        if (source != null && target != null) map[source.blockID] = target.blockID;
    }

    internal static void Apply(BlockValue helper, ref BlockValue result)
    {
        int type;
        if (TraderPrefabDepth > 0 && FixedTypes().TryGetValue(helper.type, out type)) result.type = type;
    }
}

[HarmonyPatch(typeof(Prefab), nameof(Prefab.CopyBlocksIntoChunkNoEntities))]
internal static class RebirthTraderStationChunkCopyPatch
{
    private static void Prefix(Prefab __instance, out bool __state)
    {
        __state = __instance.bTraderArea;
        if (__state) RebirthTraderStationPlaceholders.TraderPrefabDepth++;
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) RebirthTraderStationPlaceholders.TraderPrefabDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(Prefab), nameof(Prefab.CopyIntoLocal))]
internal static class RebirthTraderStationLocalCopyPatch
{
    private static void Prefix(Prefab __instance, out bool __state)
    {
        __state = __instance.bTraderArea;
        if (__state) RebirthTraderStationPlaceholders.TraderPrefabDepth++;
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) RebirthTraderStationPlaceholders.TraderPrefabDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(BlockPlaceholderMap), nameof(BlockPlaceholderMap.Replace), new[] {
    typeof(BlockValue), typeof(GameRandom), typeof(Chunk), typeof(int), typeof(int), typeof(int),
    typeof(FastTags<TagGroup.Global>), typeof(bool), typeof(bool) })]
internal static class RebirthTraderStationReplacePatch
{
    private static void Postfix(BlockValue __0, ref BlockValue __result)
    {
        RebirthTraderStationPlaceholders.Apply(__0, ref __result);
    }
}

// A separate bootstrap step: a failure here must never stop the trader job patches from installing.
internal static class RebirthTraderStationPlaceholdersInstaller
{
    internal static void Install()
    {
        Harmony harmony = new Harmony("rebirth.trader-stations");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderStationChunkCopyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderStationLocalCopyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderStationReplacePatch));
    }
}
