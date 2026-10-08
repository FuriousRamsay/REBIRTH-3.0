using HarmonyLib;
using System;
using System.Collections.Generic;

#nullable disable

// Explicit 3.1 (b14) Harmony bindings. These classes intentionally contain no
// runtime target discovery. Each target and argument list is fixed to the
// supplied 3.1 base-code signature. Tile serializer stream-mode types were updated
// against installed 3/30 build18 metadata; other bindings retain their own audit requirements.

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.InstantiateFromRead), new Type[]
{
    typeof(PooledBinaryReader),
    typeof(StreamModeRead),
    typeof(TileEntityType),
    typeof(Chunk),
    typeof(int[]),
    typeof(Func<int, int, int, BlockValue>)
})]
internal static class RebirthAdvancedFarmingTileEntityInstantiateBinding
{
    [HarmonyPrefix]
    private static bool Prefix(
        PooledBinaryReader _br,
        StreamModeRead _eStreamMode,
        TileEntityType _type,
        Chunk _chunk,
        int[] _blockIdMapping,
        Func<int, int, int, BlockValue> _getBlock,
        ref TileEntity __result)
    {
        return Harmony_RebirthAdvancedFarmingTileEntityInstantiatePatch.Prefix(
            _br, _eStreamMode, _type, _chunk, _blockIdMapping, _getBlock, ref __result);
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.UpdateTick), new Type[] { typeof(World) })]
internal static class RebirthAdvancedFarmingWorkstationHeatBinding
{
    [HarmonyPostfix]
    private static void Postfix(TileEntityWorkstation __instance)
    {
        Harmony_RebirthAdvancedFarmingWorkstationHeatPatch.Postfix(__instance);
    }
}

[HarmonyPatch(typeof(LightProcessor), nameof(LightProcessor.RefreshSunlightAtLocalPos), new Type[]
{
    typeof(Chunk), typeof(int), typeof(int), typeof(bool)
})]
internal static class RebirthAdvancedFarmingRefreshSunlightBinding
{
    [HarmonyPrefix]
    private static bool Prefix(LightProcessor __instance, Chunk c, int x, int z, bool _isSpread)
    {
        return Harmony_RebirthAdvancedFarmingLightProcessorPatch.Prefix_RefreshSunlightAtLocalPos(
            __instance, c, x, z, _isSpread);
    }
}

[HarmonyPatch(typeof(LightProcessor), nameof(LightProcessor.RefreshLightAtLocalPos), new Type[]
{
    typeof(Chunk), typeof(int), typeof(int), typeof(int), typeof(Chunk.LIGHT_TYPE)
})]
internal static class RebirthAdvancedFarmingRefreshLightBinding
{
    [HarmonyPrefix]
    private static bool Prefix(
        LightProcessor __instance,
        ref byte __result,
        Chunk c,
        int x,
        int y,
        int z,
        Chunk.LIGHT_TYPE type)
    {
        return Harmony_RebirthAdvancedFarmingLightProcessorPatch.Prefix_RefreshLightAtLocalPos(
            __instance, ref __result, c, x, y, z, type);
    }
}

[HarmonyPatch(typeof(LightProcessor), nameof(LightProcessor.SpreadLight), new Type[]
{
    typeof(Chunk), typeof(int), typeof(int), typeof(int), typeof(byte), typeof(int), typeof(Chunk.LIGHT_TYPE), typeof(bool)
})]
internal static class RebirthAdvancedFarmingSpreadLightRecursiveBinding
{
    [HarmonyPrefix]
    private static bool Prefix(
        LightProcessor __instance,
        Chunk _chunk,
        int _blockX,
        int _blockY,
        int _blockZ,
        byte _lightValue,
        int depth,
        Chunk.LIGHT_TYPE type,
        bool bSetAtStarterPos)
    {
        return Harmony_RebirthAdvancedFarmingLightProcessorPatch.Prefix_SpreadLightRecursive(
            __instance, _chunk, _blockX, _blockY, _blockZ, _lightValue, depth, type, bSetAtStarterPos);
    }
}

[HarmonyPatch(typeof(LightProcessor), nameof(LightProcessor.UnspreadLight), new Type[]
{
    typeof(Chunk), typeof(int), typeof(int), typeof(int), typeof(byte), typeof(int), typeof(Chunk.LIGHT_TYPE), typeof(List<Vector3i>)
})]
internal static class RebirthAdvancedFarmingUnspreadLightRecursiveBinding
{
    [HarmonyPrefix]
    private static bool Prefix(
        LightProcessor __instance,
        Chunk _chunk,
        int _blockX,
        int _blockY,
        int _blockZ,
        byte _lightValue,
        int depth,
        Chunk.LIGHT_TYPE type,
        List<Vector3i> brightSpots)
    {
        return Harmony_RebirthAdvancedFarmingLightProcessorPatch.Prefix_UnspreadLightRecursive(
            __instance, _chunk, _blockX, _blockY, _blockZ, _lightValue, depth, type, brightSpots);
    }
}

[HarmonyPatch(typeof(TEFeatureDoor), nameof(TEFeatureDoor.SetOpen), new Type[] { typeof(bool), typeof(bool) })]
internal static class RebirthAdvancedFarmingDoorSetOpenBinding
{
    [HarmonyPrefix]
    private static void Prefix(
        TEFeatureDoor __instance,
        ref Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.DoorFeatureStateSignature __state)
    {
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.Prefix(__instance, ref __state);
    }

    [HarmonyPostfix]
    private static void Postfix(
        TEFeatureDoor __instance,
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.DoorFeatureStateSignature __state)
    {
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.Postfix(__instance, __state);
    }
}

[HarmonyPatch(typeof(TEFeatureDoor), nameof(TEFeatureDoor.Read), new Type[]
{
    typeof(PooledBinaryReader), typeof(StreamModeRead)
})]
internal static class RebirthAdvancedFarmingDoorReadBinding
{
    [HarmonyPrefix]
    private static void Prefix(
        TEFeatureDoor __instance,
        ref Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.DoorFeatureStateSignature __state)
    {
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.Prefix(__instance, ref __state);
    }

    [HarmonyPostfix]
    private static void Postfix(
        TEFeatureDoor __instance,
        StreamModeRead _eStreamMode,
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.DoorFeatureStateSignature __state)
    {
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.Postfix_Read(__instance, _eStreamMode, __state);
    }
}

[HarmonyPatch(typeof(ChunkCluster), nameof(ChunkCluster.SetBlock), new Type[]
{
    typeof(Vector3i), typeof(bool), typeof(BlockValue), typeof(bool), typeof(sbyte),
    typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int)
})]
internal static class RebirthAdvancedFarmingChunkSetBlockBinding
{
    [HarmonyPostfix]
    private static void Postfix(BlockValue __result, Vector3i _pos, BlockValue _bv, bool _isUpdateLight)
    {
        Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.Postfix_ChunkClusterSetBlock(
            __result, _pos, _bv, _isUpdateLight);
    }
}

[HarmonyPatch(typeof(ConsoleCmdWeather), nameof(ConsoleCmdWeather.Execute), new Type[]
{
    typeof(List<string>), typeof(CommandSenderInfo)
})]
internal static class RebirthAdvancedFarmingWeatherCommandBinding
{
    [HarmonyPrefix]
    private static void Prefix(List<string> _params)
    {
        Harmony_RebirthWeatherCommandSyncPatch.Prefix(_params);
    }

    [HarmonyPostfix]
    private static void Postfix(List<string> _params)
    {
        Harmony_RebirthWeatherCommandSyncPatch.Postfix(_params);
    }
}
