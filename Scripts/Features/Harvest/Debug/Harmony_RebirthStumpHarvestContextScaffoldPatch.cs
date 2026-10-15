#if DEBUG
using HarmonyLib;

#nullable disable

/// <summary>
/// 7DTD 3.0 manual-test stump/vehicle harvest context patch.
/// Target: Block.OnBlockDestroyedBy(WorldBase, BlockValueRef, BlockValue, int, bool)
/// </summary>
[HarmonyPatch(typeof(Block), "OnBlockDestroyedBy")]
public static class Harmony_RebirthStumpHarvestContextScaffoldPatch
{
    public static bool Prefix(
        Block __instance,
        WorldBase _world,
        BlockValueRef _bvRef,
        BlockValue _blockValue,
        int _entityId,
        bool _bUseHarvestTool)
    {
        if (!RebirthStumpHarvestContextPolicy.CanStoreContext(_world))
            return true;

        RebirthStumpHarvestContextStore.TryStoreFromBlockDestroyed(__instance, _world, _bvRef.BlockPosition, _blockValue, _entityId);
        return true;
    }
}
#endif
