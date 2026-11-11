using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

#nullable disable

/// <summary>
/// Trader prefabs get working forge, workbench and chemistry helpers. Mixer and Combine Station
/// blocks are excluded from their native prefab copy. Wild station helpers keep their normal rules.
/// </summary>
internal static class RebirthTraderStationPlaceholders
{
    [ThreadStatic] internal static int TraderPrefabDepth;
    private const string Mixer = "cementMixer";
    private const string MixerHelper = "cntCementMixerBustedRandomLootHelper";
    private const string CollapsedMixer = "cntCollapsedCementMixer";
    private static readonly object Gate = new object();
    private static volatile Dictionary<int, int> fixedTypes;

    private static Dictionary<int, int> FixedTypes()
    {
        if (fixedTypes != null) return fixedTypes;
        lock (Gate)
        {
            if (fixedTypes != null) return fixedTypes;
            var map = new Dictionary<int, int>();
            Add(map, "cntForgeBustedRandomLootHelper", "forge");
            Add(map, "cntWorkbenchBustedRandomLootHelper", "workbench");
            Add(map, "cntChemistryStationBustedRandomLootHelper", "chemistryStation");
            foreach (string name in new[] { "combineStation", Mixer, MixerHelper, CollapsedMixer })
                if (Block.GetBlockByName(name) is Block block) map[block.blockID] = BlockValue.Air.type;
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
        if (TraderPrefabDepth <= 0 || !FixedTypes().TryGetValue(helper.type, out int type)) return;
        if (type == BlockValue.Air.type) result = BlockValue.Air;
        else result.type = type;
    }

    internal static bool Excluded(BlockValue block)
        => FixedTypes().TryGetValue(block.type, out int type) && type == BlockValue.Air.type;

    internal static bool MatchesSaved(BlockValue authored, BlockValue current)
    {
        if (authored.ischild || current.ischild || !Excluded(authored) || !Excluded(current)) return false;
        if (authored.type == current.type) return authored.rotation == current.rotation;
        return authored.Block?.GetBlockName() == MixerHelper &&
            (current.Block?.GetBlockName() is Mixer or CollapsedMixer);
    }

    internal static BlockValue FilterAuthored(BlockValue block)
        => TraderPrefabDepth > 0 && Excluded(block) ? BlockValue.Air : block;

    internal static IEnumerable<CodeInstruction> FilterPrefabCopy(IEnumerable<CodeInstruction> instructions)
    {
        var getBlock = AccessTools.Method(typeof(Prefab), nameof(Prefab.GetBlock), new[] { typeof(int), typeof(int), typeof(int) });
        var filter = AccessTools.Method(typeof(RebirthTraderStationPlaceholders), nameof(FilterAuthored));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(getBlock)) yield return new CodeInstruction(OpCodes.Call, filter);
        }
    }
}

[HarmonyPatch(typeof(Prefab), nameof(Prefab.CopyBlocksIntoChunkNoEntities))]
internal static class RebirthTraderStationChunkCopyPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => RebirthTraderStationPlaceholders.FilterPrefabCopy(instructions);

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
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => RebirthTraderStationPlaceholders.FilterPrefabCopy(instructions);

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
