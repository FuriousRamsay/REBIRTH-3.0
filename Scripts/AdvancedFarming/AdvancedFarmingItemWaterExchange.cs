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
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return false;

        if (world == null || world.IsRemote())
            return false;

        EntityAlive entity = world.GetEntity(playerEntityId) as EntityAlive;
        if (entity == null || entity.inventory == null)
            return false;

        Vector3i playerPos = entity.GetBlockPosition();
        if (System.Math.Abs(playerPos.x - blockPos.x) > 8 || System.Math.Abs(playerPos.y - blockPos.y) > 8 || System.Math.Abs(playerPos.z - blockPos.z) > 8)
            return false;

        Ray ray = entity.GetLookRay();
        ray.origin += ray.direction.normalized * 0.5f;
        if (!Voxel.Raycast(world, ray, Constants.cDigAndBuildDistance, -538480645, 4095, 0.0f))
            return false;
        if (!Voxel.voxelRayHitInfo.bHitValid)
            return false;
        Vector3i focusedPos = Voxel.voxelRayHitInfo.hit.blockPos;
        if (focusedPos.x != blockPos.x || focusedPos.y != blockPos.y || focusedPos.z != blockPos.z)
            return false;

        BlockValue blockValue = world.GetBlock(blockPos);
        if (!IsAdvancedWaterStorage(blockValue))
            return false;

        TileEntityWaterTankRebirth tank = world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        if (tank == null)
            return false;

        string heldItemName = entity.inventory.holdingItem != null ? entity.inventory.holdingItem.GetItemName() : string.Empty;
        if (string.IsNullOrEmpty(heldItemName))
            return false;

        // Do not trust the client-provided item name. It is only a sanity check for stale local UI.
        if (!string.IsNullOrEmpty(requestedItemName) && !string.Equals(requestedItemName, heldItemName, System.StringComparison.OrdinalIgnoreCase))
            return false;

        int maxWater = ResolveMaxWater(blockValue, tank);
        int delta = GetWaterDelta(heldItemName, entity.inventory.holdingCount, tank.waterCount, maxWater);
        if (delta == 0)
            return false;

        string changeItemToItem = ResolveChangedItemName(heldItemName);
        if (string.IsNullOrEmpty(changeItemToItem))
            return false;
        ItemValue outputValue = ItemClass.GetItem(changeItemToItem);
        if (outputValue == null || outputValue.IsEmpty())
            return false;

        int originalWater = tank.waterCount;
        ItemStack originalStack = entity.inventory.GetItem(entity.inventory.holdingItemIdx).Clone();
        try
        {
            // Inventory output is validated before either side mutates. If the item write
            // fails, restore both sides rather than silently consuming water or a container.
            ExchangeHeldItem(entity, entity.inventory.holdingItemIdx, changeItemToItem);
            ApplyWaterDelta(world, blockPos, blockValue, tank, delta);
            return true;
        }
        catch (System.Exception ex)
        {
            try { entity.inventory.SetItem(entity.inventory.holdingItemIdx, originalStack); } catch { }
            try { tank.waterCount = originalWater; tank.setModified(); } catch { }
            Log.Warning("[REBIRTH Farming] water exchange rolled back: " + ex.Message);
            return false;
        }
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
