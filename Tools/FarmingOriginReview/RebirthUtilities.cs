using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthUtilities
{
    private static int s_cachedCampfireBlockId = -2;
    private static int s_cachedWoodBurningStoveBlockId = -2;

    public static int GetCachedHeatBlockIdForAdvancedFarming(string blockName)
    {
        if (string.Equals(blockName, "campfire", StringComparison.OrdinalIgnoreCase))
        {
            if (s_cachedCampfireBlockId == -2)
            {
                Block block = Block.GetBlockByName("campfire", false);
                s_cachedCampfireBlockId = block != null ? block.blockID : -1;
            }

            return s_cachedCampfireBlockId;
        }

        if (string.Equals(blockName, "cntWoodBurningStove", StringComparison.OrdinalIgnoreCase))
        {
            if (s_cachedWoodBurningStoveBlockId == -2)
            {
                Block block = Block.GetBlockByName("cntWoodBurningStove", false);
                s_cachedWoodBurningStoveBlockId = block != null ? block.blockID : -1;
            }

            return s_cachedWoodBurningStoveBlockId;
        }

        Block fallback = Block.GetBlockByName(blockName, false);
        return fallback != null ? fallback.blockID : -1;
    }

    public enum TileEntityRebirth : byte
    {
        TileEntityPlantGrowingRebirth = 200,
        TileEntityFarmPlotRebirth = 201,
        TileEntityWaterTankRebirth = 202,
        TileEntityRepairableVehicleRebirth = 203
    }

    public static int TotalGameSecondsPassed()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return 0;
        return (int)(world.GetWorldTime() / 20UL);
    }

    public static float GetWeatherInfo(Vector3i pos, string key)
    {
        WeatherManager weather = WeatherManager.Instance;
        if (weather == null)
            return 0f;
        if (key == "temp") return weather.GetCurrentTemperatureValue();
        if (key == "clouds") return weather.GetCurrentCloudThicknessPercent() * 10f;
        if (key == "wet") return weather.GetCurrentRainfallPercent() * 10f;
        return 0f;
    }

    public static bool IsHiveDayActive()
    {
        return true;
    }

    public static bool IsVanillaTrader(int entityId)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        Entity entity = world != null ? world.GetEntity(entityId) : null;
        return entity is EntityTrader && !(entity is EntityRebirthNPC);
    }

    public static bool CheckForHeat(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        return AdvancedFarmingHeatQueryService.CheckForHeat(world, plantPos, blockName, blockRange, blockHeightMin, blockHeightMax);
    }

    public static bool CheckForHeatUncached(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        if (world == null || string.IsNullOrEmpty(blockName))
            return false;

        int targetType = GetCachedHeatBlockIdForAdvancedFarming(blockName);
        if (targetType < 0)
            return false;

        long scannedPositions = 0L;
        long tileEntityLookups = 0L;

        for (int x = -blockRange; x <= blockRange; x++)
        {
            for (int z = -blockRange; z <= blockRange; z++)
            {
                for (int y = -blockHeightMin; y <= blockHeightMax; y++)
                {
                    scannedPositions++;
                    Vector3i pos = new Vector3i(plantPos.x + x, plantPos.y + y, plantPos.z + z);
                    BlockValue block = world.GetBlock(pos);
                    if (block.type != targetType)
                        continue;

                    tileEntityLookups++;
                    TileEntity te = world.GetTileEntity(pos);
                    if (te is TileEntityWorkstation workstation && workstation.isBurning)
                    {
                        AdvancedFarmingHeatQueryService.ObserveWorkstationAt(world, pos, true);
                        AdvancedFarmingPerfSnapshotService.RecordHeatScan(scannedPositions, tileEntityLookups, true);
                        return true;
                    }
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordHeatScan(scannedPositions, tileEntityLookups, false);
        return false;
    }

    public static int CheckForWater(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        return AdvancedFarmingWaterProviderRegistry.CheckForWater(world, plantPos, blockName, blockRange, blockHeightMin, blockHeightMax);
    }

    public static int CheckForWaterUncached(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        if (world == null || string.IsNullOrEmpty(blockName))
            return -1;

        Block target = Block.GetBlockByName(blockName, false);
        if (target == null)
            return -1;

        int targetType = target.blockID;
        int best = -1;
        long scannedPositions = 0L;
        long tileEntityLookups = 0L;

        for (int x = -blockRange; x <= blockRange; x++)
        {
            for (int z = -blockRange; z <= blockRange; z++)
            {
                for (int y = -blockHeightMin; y <= blockHeightMax; y++)
                {
                    scannedPositions++;
                    Vector3i pos = new Vector3i(plantPos.x + x, plantPos.y + y, plantPos.z + z);
                    BlockValue block = world.GetBlock(pos);
                    if (block.type != targetType)
                        continue;

                    tileEntityLookups++;
                    if (world.GetTileEntity(pos) is TileEntityWaterTankRebirth tank)
                    {
                        if (tank.waterCount > best)
                            best = tank.waterCount;
                    }
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordWaterScan(scannedPositions, tileEntityLookups, best >= 0);
        return best;
    }

    public static bool TryDepleteWaterStorageFirst(WorldBase world, Vector3i plantPos, int depletion)
    {
        return TryDepleteBestWaterStorage(world, plantPos, depletion);
    }

    public static bool TryDepleteWater(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax, int depletion)
    {
        if (world == null || string.IsNullOrEmpty(blockName) || depletion <= 0)
            return false;

        if (string.Equals(blockName, "FuriousRamsayWaterTank", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "FuriousRamsayDewCollector", StringComparison.OrdinalIgnoreCase))
        {
            return TryDepleteBestWaterStorage(world, plantPos, depletion);
        }

        return false;
    }

    public static bool TryDepleteBestWaterStorage(WorldBase world, Vector3i plantPos, int depletion)
    {
        return AdvancedFarmingWaterProviderRegistry.TryDepleteBestWaterStorage(world, plantPos, depletion);
    }

    public static bool TryDepleteFarmPlot(WorldBase world, Vector3i plantPos, int depletion)
    {
        return AdvancedFarmingWaterProviderRegistry.TryDepleteDirectFarmPlot(world, plantPos, depletion);
    }

    public static bool TryConsumePlantWater(WorldBase world, Vector3i plantPos, int depletion)
    {
        // External storage is authoritative priority: consume from tanks/dew collectors first,
        // then fall back to the direct farm plot below the plant. Water provider height is
        // evaluated relative to the plant position, not the plot position.
        if (TryDepleteWaterStorageFirst(world, plantPos, depletion))
            return true;

        return TryDepleteFarmPlot(world, plantPos, depletion);
    }


    public static bool TryGiveItemToPlayerOrDrop(WorldBase world, EntityPlayer player, ItemStack stack, Vector3i sourcePos)
    {
        if (stack == null || stack.IsEmpty())
            return false;

        EntityPlayerLocal localPlayer = player as EntityPlayerLocal;
        if (localPlayer != null)
        {
            LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(localPlayer);
            if (ui != null && ui.xui != null && ui.xui.PlayerInventory != null && ui.xui.PlayerInventory.AddItem(stack))
                return true;

            GameManager.Instance.ItemDropServer(stack, localPlayer.GetPosition(), UnityEngine.Vector3.zero, localPlayer.entityId, 60f, false);
            return true;
        }

        if (player != null && player.bag != null && player.bag.AddItem(stack))
            return true;

        UnityEngine.Vector3 dropPos = sourcePos.ToVector3() + new UnityEngine.Vector3(0.5f, 0.5f, 0.5f);
        GameManager.Instance.ItemDropServer(stack, dropPos, UnityEngine.Vector3.zero, player != null ? player.entityId : -1, 60f, false);
        return true;
    }

    public static ItemStack CreateSeedStackFromPlantBlock(BlockValue blockValue)
    {
        string seedName = GetSeedItemNameFromPlantBlock(blockValue);
        if (string.IsNullOrEmpty(seedName))
            return ItemStack.Empty;

        ItemValue itemValue = ItemClass.GetItem(seedName);
        if (itemValue.IsEmpty())
            return ItemStack.Empty;

        return new ItemStack(itemValue, 1);
    }

    public static string GetSeedItemNameFromPlantBlock(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return string.Empty;

        string blockName = block.GetBlockName();
        if (string.IsNullOrEmpty(blockName))
            return string.Empty;

        int plantedIndex = blockName.IndexOf("planted", System.StringComparison.OrdinalIgnoreCase);
        if (plantedIndex < 0)
            return string.Empty;

        int suffixIndex = blockName.IndexOf("3Harvest", System.StringComparison.OrdinalIgnoreCase);
        if (suffixIndex > plantedIndex)
            return blockName.Substring(0, suffixIndex) + "1";

        for (int i = blockName.Length - 1; i >= 0; i--)
        {
            char c = blockName[i];
            if (c == '1' || c == '2')
                return blockName.Substring(0, i) + "1";
        }

        return blockName;
    }

    public static bool IsHoldingShovel(EntityAlive entity)
    {
        return IsFarmPlotDamageTool(entity);
    }


    public static int GetCropWaterDepletion(BlockValue blockValue, float temp)
    {
        int depletion = 1;
        string name = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
        bool heatResistant =
            name.IndexOf("yucca", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("cotton", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("coffee", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("aloe", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (temp >= 80f && !heatResistant)
            depletion *= 2;

        return depletion;
    }

    public static bool IsFarmPlotDamageTool(EntityAlive entity)
    {
        if (entity == null || entity.inventory == null)
            return false;

        ItemValue itemValue = entity.inventory.holdingItemItemValue;
        if (itemValue == null || itemValue.IsEmpty() || itemValue.ItemClass == null)
            return false;

        string name = itemValue.ItemClass.Name;
        if (string.IsNullOrEmpty(name))
            return false;

        if (name.IndexOf("shovel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (name.Equals("gunToolDiggerAdmin", System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.IndexOf("Admin", System.StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("Tool", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }


    public static bool IsPlayerGrownHarvestCrop(BlockValue cropValue)
    {
        Block block = cropValue.Block;
        if (block == null)
            return false;

        string blockName = block.GetBlockName();
        return !string.IsNullOrEmpty(blockName) &&
               blockName.EndsWith("3HarvestPlayer", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool HarvestPlayerCropAndReplant(WorldBase world, Vector3i cropPos, BlockValue cropValue, EntityPlayer player)
    {
        if (!AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, cropPos))
            return false;

        if (world == null || !IsPlayerGrownHarvestCrop(cropValue))
            return false;

        string seedName = GetSeedItemNameFromPlantBlock(cropValue);
        if (string.IsNullOrEmpty(seedName))
            return false;

        // Resolve and validate the complete destination stage before any harvest item is
        // published. This prevents an invalid/missing seed definition from granting a
        // harvest while leaving the mature crop available to harvest again.
        BlockValue seedBlock = Block.GetBlockValue(seedName);
        if (seedBlock.isair || seedBlock.Block == null)
            return false;

        List<ItemStack> harvest;
        if (!TryBuildRebirthCropHarvest(world, cropValue, player, out harvest))
            return false;

        seedBlock.rotation = cropValue.rotation;
        seedBlock.meta = cropValue.meta;
        seedBlock.meta2 = (byte)0;

        RebirthCropProvenanceSnapshot provenance = RebirthCropProvenanceAdapter.CaptureSnapshot(world, cropPos);
        TileEntityPlantGrowingRebirth sourceTe = world.GetTileEntity(cropPos) as TileEntityPlantGrowingRebirth;
        using (var transition = AdvancedFarmingPlantOriginService.BeginNativeTransition(world, cropPos, cropValue, seedBlock, sourceTe))
        {
            if (transition == null || !transition.BeforeMutation()) return false;
            SetExposureNeutralPlantBlockRpc(world, cropPos, cropValue, seedBlock);
            // Complete checks exact observed incarnation/callback before any award or bonus restore.
            if (!transition.Complete()) return false;
        }
        RebirthCropProvenanceAdapter.RestoreSnapshot(world, cropPos, provenance);
        // Inventory insertion consumes the supplied stack's count. Capture the committed
        // output before delivery, but award only after every delivery has succeeded.
        int harvestedUnits = CountItemStacks(harvest);
        for (int i = 0; i < harvest.Count; i++)
            if (!TryGiveItemToPlayerOrDrop(world, player, harvest[i], cropPos))
                return false;

        RebirthSkillEventRouter.OnFarmingHarvestCompleted(player, cropValue.Block.GetBlockName(), harvestedUnits);
        return true;
    }

    public static bool HarvestPlayerCropAndClear(WorldBase world, Vector3i cropPos, BlockValue cropValue, EntityPlayer player)
    {
        if (!AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, cropPos))
            return false;

        if (world == null || cropValue.Block == null)
            return false;

        List<ItemStack> harvest;
        if (!TryBuildRebirthCropHarvest(world, cropValue, player, out harvest))
            return false;

        // Commit the world transition before publishing harvest output. If the block
        // mutation is rejected, no items are minted from an unchanged crop.
        SetExposureNeutralPlantBlockRpc(world, cropPos, cropValue, BlockValue.Air);
        if (!world.GetBlock(cropPos).isair)
            return false;

        int harvestedUnits = CountItemStacks(harvest);
        for (int i = 0; i < harvest.Count; i++)
            if (!TryGiveItemToPlayerOrDrop(world, player, harvest[i], cropPos))
                return false;

        if (IsPlayerGrownHarvestCrop(cropValue))
            RebirthSkillEventRouter.OnFarmingHarvestCompleted(player, cropValue.Block.GetBlockName(), harvestedUnits);
        return true;
    }

    public static bool HarvestOrReturnCropAbovePlot(WorldBase world, Vector3i plantPos, EntityPlayer player)
    {
        if (!AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, plantPos))
            return false;

        if (world == null)
            return false;

        BlockValue cropValue = world.GetBlock(plantPos);
        BlockPlantGrowingRebirth plant = cropValue.Block as BlockPlantGrowingRebirth;
        if (plant == null)
            return false;

        if (plant.IsFullyGrownCrop(cropValue))
            return HarvestPlayerCropAndClear(world, plantPos, cropValue, player);

        return ReturnImmatureCropSeedAndClear(world, plantPos, player);
    }

    private static int CountItemStacks(List<ItemStack> stacks)
    {
        int total = 0;
        if (stacks != null)
            for (int i = 0; i < stacks.Count; i++)
                if (stacks[i] != null && !stacks[i].IsEmpty())
                    total += System.Math.Max(0, stacks[i].count);
        return total;
    }

    private static bool TryBuildRebirthCropHarvest(WorldBase world, BlockValue cropValue, EntityPlayer player, out List<ItemStack> result)
    {
        result = new List<ItemStack>();
        Block block = cropValue.Block;
        if (block == null || block.itemsToDrop == null)
            return false;

        List<Block.SItemDropProb> drops;
        if (!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out drops) || drops == null)
            return false;

        for (int i = 0; i < drops.Count; i++)
        {
            Block.SItemDropProb drop = drops[i];
            if (ShouldSkipRebirthCropHarvestDrop(drop))
                continue;

            ItemValue itemValue = drop.name.Equals("*") ? cropValue.ToItemValue() : ItemClass.GetItem(drop.name);
            if (itemValue.IsEmpty())
                continue;

            // Advanced Farming owns this direct output and therefore bypasses native HarvestCount.
            // Mirror the already-authored Farming Skill/Trait yield deltas server-side. A weakness can
            // resolve a valid harvest to zero items; the crop still completes/reseeds so it cannot be rerolled.
            int count = RebirthFoodFarmingButcherySignatureService.RollAdvancedFarmingHarvestCount(world, player, 1);
            if (count > 0)
                result.Add(new ItemStack(itemValue, count));
            return true;
        }

        return false;
    }

    private static bool ShouldSkipRebirthCropHarvestDrop(Block.SItemDropProb drop)
    {
        if (string.IsNullOrEmpty(drop.name))
            return true;

        if (drop.name.IndexOf("planted", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(drop.tag) && drop.tag.IndexOf("Seed", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }


    public static bool ReturnImmatureCropSeedAndClear(WorldBase world, Vector3i plantPos, EntityPlayer player)
    {
        if (!AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, plantPos))
            return false;

        if (world == null)
            return false;

        BlockValue cropValue = world.GetBlock(plantPos);
        if (!(cropValue.Block is BlockPlantGrowingRebirth plant))
            return false;

        if (plant.IsFullyGrownCrop(cropValue))
            return false;

        ItemStack seedStack = CreateSeedStackFromPlantBlock(cropValue);
        if (seedStack == null || seedStack.IsEmpty())
            return false;

        SetExposureNeutralPlantBlockRpc(world, plantPos, cropValue, BlockValue.Air);
        if (!world.GetBlock(plantPos).isair)
            return false;

        return TryGiveItemToPlayerOrDrop(world, player, seedStack, plantPos);
    }

    private static bool SameBlockIdentity(BlockValue actual, BlockValue expected)
    {
        return actual.type == expected.type && actual.meta == expected.meta &&
            actual.meta2 == expected.meta2 && actual.rotation == expected.rotation;
    }

    private static void SetExposureNeutralPlantBlockRpc(WorldBase world, Vector3i pos, BlockValue oldValue, BlockValue newValue)
    {
        if (world == null)
            return;

        bool skipLightUpdate = Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.IsExposureNeutralPlantLikeSwap(oldValue, newValue);
        world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)pos, newValue, !skipLightUpdate));
    }

    public static bool TryRemoveBlockAndGrant(WorldBase world, Vector3i blockPos, BlockValue expectedBlock,
        EntityPlayer player, ItemStack grant)
    {
        if (world == null || player == null || expectedBlock.isair || grant == null || grant.IsEmpty())
            return false;

        BlockValue live = world.GetBlock(blockPos);
        if (!SameBlockIdentity(live, expectedBlock))
            return false;

        SetExposureNeutralPlantBlockRpc(world, blockPos, live, BlockValue.Air);
        if (!world.GetBlock(blockPos).isair)
            return false;

        ItemStack awarded = grant.Clone();
        if (TryGiveItemToPlayerOrDrop(world, player, awarded, blockPos))
            return true;

        // Inventory/drop publication failed after the world mutation. Restore the exact
        // block identity rather than silently completing a one-sided pickup.
        SetExposureNeutralPlantBlockRpc(world, blockPos, BlockValue.Air, live);
        return false;
    }

    public static bool TryTakeBlockItem(WorldBase world, Vector3i blockPos, BlockValue blockValue,
        EntityPlayerLocal player, int count)
    {
        if (world == null || player == null || blockValue.isair || count <= 0)
            return false;
        ItemValue itemValue = blockValue.ToItemValue();
        if (itemValue == null || itemValue.IsEmpty())
            return false;
        ItemStack stack = new ItemStack(itemValue, count);
        if (!TryRemoveBlockAndGrant(world, blockPos, blockValue, player, stack))
            return false;
        QuestEventManager.Current.BlockPickedUp(blockValue.Block.GetBlockName(), blockPos);
        player.AddUIHarvestingItem(stack.Clone(), false);
        return true;
    }

    // Compatibility wrapper for older call sites. The old implementation advertised a
    // timer but removed the block immediately and awarded nothing. Pickup is now a
    // result-bearing world+item transaction; delay is intentionally not simulated here.
    public static void TakeItemWithTimer(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player, float delay, object unused, string unused2, int count, bool unused3)
    {
        TryTakeBlockItem(world, blockPos, blockValue, player, count);
    }
}
