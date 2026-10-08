using HarmonyLib;
using System;

#nullable disable

// Fixed 3.1 block-pickup bindings. Only the separate subclass-override installer
// remains dynamic because Harmony does not apply a base virtual patch to methods
// overridden by game/mod block subclasses.

[HarmonyPatch(typeof(BlocksFromXml), nameof(BlocksFromXml.CreateBlocks), new Type[]
{
    typeof(XmlFile), typeof(bool), typeof(bool)
})]
internal static class RebirthBlockPickupBlocksCreateBinding
{
    [HarmonyPrefix]
    private static void Prefix(XmlFile _xmlFile)
    {
        Harmony_RebirthBlockPickupPatches.Prefix_BlocksFromXml_CreateBlocks(_xmlFile);
    }
}

[HarmonyPatch(typeof(Block), nameof(Block.GetActivationText), new Type[]
{
    typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive)
})]
internal static class RebirthBlockPickupBaseActivationTextBinding
{
    [HarmonyPostfix]
    private static void Postfix(
        Block __instance,
        WorldBase _world,
        BlockValue _blockValue,
        Vector3i _blockPos,
        EntityAlive _entityFocusing,
        ref string __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_Block_GetActivationText(
            __instance, _world, _blockValue, _blockPos, _entityFocusing, ref __result);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.Init), new Type[] { })]
internal static class RebirthBlockPickupCompositeInitBinding
{
    [HarmonyPrefix]
    private static void Prefix(BlockCompositeTileEntity __instance)
    {
        Harmony_RebirthBlockPickupPatches.Prefix_BlockCompositeTileEntity_Init(__instance);
    }
}

[HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.PlaceBlock), new Type[]
{
    typeof(WorldBase), typeof(BlockPlacement.Result), typeof(EntityAlive)
})]
internal static class RebirthBlockPickupWorkstationPlaceBinding
{
    [HarmonyPostfix]
    private static void Postfix(WorldBase _world, BlockPlacement.Result _result, EntityAlive _ea)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_BlockWorkstation_PlaceBlock(_world, _result, _ea);
    }
}

[HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.OnBlockRemoved), new Type[]
{
    typeof(WorldBase), typeof(Chunk), typeof(Vector3i), typeof(BlockValue)
})]
internal static class RebirthBlockPickupWorkstationRemovedBinding
{
    [HarmonyPostfix]
    private static void Postfix(Vector3i _blockPos)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_BlockWorkstation_OnBlockRemoved(_blockPos);
    }
}

[HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.GetActivationText), new Type[]
{
    typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive)
})]
internal static class RebirthBlockPickupWorkstationActivationTextBinding
{
    [HarmonyPostfix]
    private static void Postfix(
        WorldBase _world,
        BlockValue _blockValue,
        Vector3i _blockPos,
        EntityAlive _entityFocusing,
        ref string __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_BlockWorkstation_GetActivationText(
            _world, _blockValue, _blockPos, _entityFocusing, ref __result);
    }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.OnLockRequestServer), new Type[]
{
    typeof(int), typeof(PooledBinaryReader), typeof(ushort)
})]
internal static class RebirthBlockPickupTileEntityCanLockBinding
{
    [HarmonyPostfix]
    private static void Postfix(TileEntity __instance, int _lockingPlayerID, ref bool __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_TileEntity_CanLockOnServer(
            __instance, _lockingPlayerID, ref __result);
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveWorld), new Type[] { })]
internal static class RebirthBlockPickupSaveWorldBinding
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        Harmony_RebirthBlockPickupPatches.Postfix_GameManager_SaveWorld();
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.PickupBlockClient), new Type[]
{
    typeof(Vector3i), typeof(BlockValue), typeof(int)
})]
internal static class RebirthBlockPickupClientBinding
{
    [HarmonyPrefix]
    private static bool Prefix(
        GameManager __instance,
        Vector3i _blockPos,
        BlockValue _blockValue,
        int _playerId)
    {
        return Harmony_RebirthBlockPickupPatches.Prefix_GameManager_PickupBlockClient(
            __instance, _blockPos, _blockValue, _playerId);
    }
}

[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.ShouldDestroyOnClose), new Type[] { })]
internal static class RebirthBlockPickupStorageDestroyBinding
{
    [HarmonyPrefix]
    private static bool Prefix(TEFeatureStorage __instance, ref bool __result)
    {
        return Harmony_RebirthBlockPickupPatches.Prefix_TEFeatureStorage_ShouldDestroyOnClose(
            __instance, ref __result);
    }
}

[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.OnAdded), new Type[]
{
    typeof(Vector3i), typeof(BlockValue)
})]
internal static class RebirthBlockPickupStorageAddedBinding
{
    [HarmonyPostfix]
    private static void Postfix(TEFeatureStorage __instance, Vector3i _blockPos, BlockValue _blockValue)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_TEFeatureStorage_OnAdded(
            __instance, _blockPos, _blockValue);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.GetActivationText), new Type[]
{
    typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive)
})]
internal static class RebirthBlockPickupCompositeActivationTextBinding
{
    [HarmonyPostfix]
    private static void Postfix(
        WorldBase _world,
        BlockValue _blockValue,
        Vector3i _blockPos,
        EntityAlive _entityFocusing,
        ref string __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_BlockCompositeTileEntity_GetActivationText(
            _world, _blockValue, _blockPos, _entityFocusing, ref __result);
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.GetBindingValueInternal))]
internal static class RebirthBlockPickupItemBindingValueBinding
{
    [HarmonyPostfix]
    private static void Postfix(
        XUiC_ItemStack __instance,
        ref string _value,
        string _bindingName,
        ref bool __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_XUiC_ItemStack_GetBindingValueInternal(
            __instance, ref _value, _bindingName, ref __result);
    }
}

[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackWith), new Type[]
{
    typeof(ItemStack), typeof(bool)
})]
internal static class RebirthBlockPickupItemCanStackBinding
{
    [HarmonyPostfix]
    private static void Postfix(ItemStack __instance, ItemStack _other, ref bool __result)
    {
        Harmony_RebirthBlockPickupPatches.Postfix_ItemStack_CanStackWith(
            __instance, _other, ref __result);
    }
}
