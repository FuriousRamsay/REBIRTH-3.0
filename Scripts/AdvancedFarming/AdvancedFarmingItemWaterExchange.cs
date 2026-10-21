using UnityEngine;

#nullable disable

/// <summary>
/// Shared item-action helpers for interacting with Advanced Farming water storage.
/// The helpers only target FuriousRamsayWaterTank/FuriousRamsayDewCollector tile entities;
/// natural-water behavior is intentionally left to vanilla CollectWater/DumpWater item actions.
/// </summary>
internal static class AdvancedFarmingItemWaterExchange
{
    public static bool TryGetAdvancedWaterStorage(ItemInventoryData invData, out Vector3i blockPos, out BlockValue blockValue, out TileEntityWaterTankRebirth tank)
    {
        blockPos = Vector3i.zero;
        blockValue = BlockValue.Air;
        tank = null;

        if (invData == null || invData.world == null || invData.holdingEntity == null)
            return false;

        Ray ray = invData.holdingEntity.GetLookRay();
        ray.origin += ray.direction.normalized * 0.5f;

        if (!Voxel.Raycast(invData.world, ray, Constants.cDigAndBuildDistance, -538480645, 4095, 0.0f))
            return false;

        if (!Voxel.voxelRayHitInfo.bHitValid)
            return false;

        blockValue = Voxel.voxelRayHitInfo.hit.blockValue;
        if (!IsAdvancedWaterStorage(blockValue))
            return false;

        blockPos = Voxel.voxelRayHitInfo.hit.blockPos;
        tank = invData.world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        return tank != null;
    }

    public static bool IsAdvancedWaterStorage(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return name == "FuriousRamsayWaterTank" || name == "FuriousRamsayDewCollector";
    }

    public static int ResolveMaxWater(BlockValue blockValue, TileEntityWaterTankRebirth tank)
    {
        if (blockValue.Block != null && blockValue.Block.Properties.Values.ContainsKey("MaxWater") && int.TryParse(blockValue.Block.Properties.Values["MaxWater"], out int max) && max > 0)
        {
            tank.waterMax = max;
            return max;
        }

        return tank.waterMax > 0 ? tank.waterMax : 400;
    }

    public static int GetWaterDelta(string itemName, int holdingCount, int currentWater, int maxWater)
    {
        if (itemName == "bucketRiverWater")
            return holdingCount == 1 && maxWater - currentWater >= 200 ? 200 : 0;

        if (itemName == "drinkJarRiverWater" || itemName == "drinkJarBoiledWater" || itemName == "drinkJarPureMineralWater")
        {
            int count = System.Math.Max(1, holdingCount);
            return maxWater - currentWater >= count ? count : 0;
        }

        if (itemName == "drinkJarEmpty")
        {
            int count = System.Math.Max(1, holdingCount);
            return currentWater >= count ? -count : 0;
        }

        if (itemName == "bucketEmpty")
            return holdingCount == 1 && currentWater >= 200 ? -200 : 0;

        return 0;
    }

    public static bool IsServerAuthority()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer;
    }

    public static void RequestServerWaterExchange(ItemInventoryData invData, Vector3i blockPos, string itemName)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (invData == null || invData.holdingEntity == null)
            return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || connection.IsServer)
            return;

        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRequestWaterTankItemExchangeRebirth>().Setup(invData.holdingEntity.entityId, blockPos, itemName));
    }

    public static bool TryApplyServerAuthoritativeExchange(World world, int playerEntityId, Vector3i blockPos, string requestedItemName)
    {
        // Legacy primitive requests contain no original owner publication.
        // New actions use the receipt protocol and never write a remote replica.
        return false;
    }

    public static void ApplyWaterDelta(World world, Vector3i blockPos, BlockValue blockValue, TileEntityWaterTankRebirth tank, int delta)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        int maxWater = ResolveMaxWater(blockValue, tank);
        int newWater = tank.waterCount + delta;
        if (newWater < 0) newWater = 0;
        if (newWater > maxWater) newWater = maxWater;

        tank.waterCount = newWater;
        int now = RebirthUtilities.TotalGameSecondsPassed();
        tank.timeLapsed = now;
        tank.LastProcessedWorldSeconds = now;
#if DEBUG
        if (AdvancedFarmingDebug.Water)
            AdvancedFarmingDebug.Log("water", "item exchange at " + blockPos + " delta=" + delta + " water=" + tank.waterCount + "/" + tank.waterMax);
#endif
        tank.setModified();

        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            AdvancedFarmingSyncService.SendWaterTank(blockPos, tank.waterCount);
    }

    public static void ExchangeHeldItem(ItemInventoryData invData, string changeItemToItem)
    {
        if (invData == null || invData.holdingEntity == null || invData.holdingEntity.inventory == null || string.IsNullOrEmpty(changeItemToItem))
            return;

        ExchangeHeldItem(invData.holdingEntity, invData.slotIdx, changeItemToItem);
    }

    public static void ExchangeHeldItem(EntityAlive entity, int slotIdx, string changeItemToItem)
    {
        if (entity == null || entity.inventory == null || string.IsNullOrEmpty(changeItemToItem) || slotIdx < 0)
            return;

        ItemValue itemValue = ItemClass.GetItem(changeItemToItem);
        entity.inventory.SetItem(slotIdx, new ItemStack(itemValue, entity.inventory.holdingCount));
    }

    public static string ResolveChangedItemName(string itemName)
    {
        if (itemName == "bucketRiverWater")
            return "bucketEmpty";
        if (itemName == "bucketEmpty")
            return "bucketRiverWater";
        if (itemName == "drinkJarEmpty")
            return "drinkJarRiverWater";
        if (itemName == "drinkJarRiverWater" || itemName == "drinkJarBoiledWater" || itemName == "drinkJarPureMineralWater")
            return "drinkJarEmpty";
        return string.Empty;
    }
}
