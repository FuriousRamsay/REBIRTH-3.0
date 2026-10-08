using HarmonyLib;

#nullable disable

/// <summary>
/// 3.1 compatibility for lootable/explodable world vehicles.
///
/// In 2.6, the composite vehicle block destruction path fell back to Downgrade,
/// allowing the configured DowngradeBlock chain to reach carRespawner_FR.
/// In 3.1, TEFeatureExplodable can return Remove from the same normal destruction
/// path. Remove bypasses DowngradeBlock entirely, so no respawn marker is created.
///
/// This patch bypasses the new 3.1 feature-result path only when the configured
/// downgrade chain is proven to terminate at carRespawner_FR. This prevents the
/// delayed TEFeatureExplodable explosion from removing the newly created marker.
/// Other composite loot containers are untouched.
/// </summary>
[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.OnBlockDestroyedBy))]
internal static class RebirthVehicleLootContainerDowngradePatch
{
    private static bool Prefix(
        BlockCompositeTileEntity __instance,
        ref Block.DestroyedResult __result)
    {
        if (!RebirthVehicleBlockRespawnRuntimePolicy.Enabled
            || !RebirthVehicleRespawnDowngradeChain.EndsAtRespawnMarker(__instance))
            return true;

        // Reproduce the 2.6 composite vehicle destruction outcome. Returning false
        // prevents TEFeatureExplodable from scheduling a delayed explosion that can
        // destroy carRespawner_FR after Block.OnBlockDamaged installs it.
        __result = Block.DestroyedResult.Downgrade;

#if DEBUG && REBIRTH_DEBUG
        RebirthVehicleRespawnDebugPolicy.RecordForcedCompositeDowngrade(
            __instance != null ? __instance.blockName : string.Empty);
#endif

        return false;
    }
}

internal static class RebirthVehicleRespawnDowngradeChain
{
    private const int MaximumChainDepth = 16;

    public static bool EndsAtRespawnMarker(Block source)
    {
        if (source == null)
            return false;

        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        if (marker.isair)
            return false;

        BlockValue current = source.DowngradeBlock;
        int previousType = -1;

        for (int depth = 0; depth < MaximumChainDepth; depth++)
        {
            if (current.isair)
                return false;

            if (current.type == marker.type)
                return true;

            if (current.type == previousType || current.Block == null)
                return false;

            previousType = current.type;
            current = current.Block.DowngradeBlock;
        }

        return false;
    }
}
