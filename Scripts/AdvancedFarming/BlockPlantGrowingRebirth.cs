using Platform;
using System.Globalization;

#nullable disable

public class BlockPlantGrowingRebirth : BlockPlantGrowing
{
    private static readonly BlockActivationCommand[] SeedPickupCommands =
    {
        new BlockActivationCommand("take", "hand", true)
    };

    protected float nextStageRate;
    public string nextStage;
    public ulong TickRate = 100UL;
    protected static string PropGrowingFertileLevel = "PlantGrowing.FertileLevel";
    protected static string PropGrowingLightLevelStay = "PlantGrowing.LightLevelStay";
    protected static string PropGrowingLightLevelGrow = "PlantGrowing.LightLevelGrow";
    public byte sunLight = 0;
    public byte blockLight = 0;
    public int minTemp = 45;

    private static bool IsMushroomBlock(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        return block != null && block.GetBlockName().IndexOf("mushroom", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool IsSeedStage(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return !string.IsNullOrEmpty(name) && name.IndexOf("3Harvest", System.StringComparison.OrdinalIgnoreCase) < 0 && name.EndsWith("1", System.StringComparison.OrdinalIgnoreCase);
    }

    public int DiagnosticLightLevelStay { get { return lightLevelStay; } }
    public int DiagnosticLightLevelGrow { get { return lightLevelGrow; } }
    public int DiagnosticMinTemperature { get { return minTemp; } }

    public bool IsFullyGrownCrop(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return !string.IsNullOrEmpty(name) && name.IndexOf("3Harvest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public BlockPlantGrowingRebirth()
    {
        // Preserve the REBIRTH default while using the inherited BlockPlantGrowing field.
        // Keeping a second field here would split base-game and REBIRTH light checks.
        lightLevelGrow = 3;
        HasTileEntity = true;
    }

    public override bool HasBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.HasBlockActivationCommands(world, blockValue, blockPos, entityFocusing);

        // Keep activation enabled so GetActivationText can display crop status, but only seed-stage crops
        // expose an actual E pickup command through GetBlockActivationCommands.
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.GetBlockActivationCommands(world, blockValue, blockPos, entityFocusing);

        // Native 3.0 crop blocks do not set CanPickup, so the base implementation returns
        // an empty command array. Supply the normal take/hand command explicitly for stage 1.
        return IsSeedStage(blockValue) ? SeedPickupCommands : BlockActivationCommand.Empty;
    }

    public override bool OnBlockActivated(string commandName, WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockActivated(commandName, world, blockPos, blockValue, player);

        if (!IsSeedStage(blockValue) || !string.Equals(commandName, SeedPickupCommands[0].text, System.StringComparison.Ordinal))
            return false;

        OnBlockActivated(world, blockPos, blockValue, player);
        return true;
    }

    public override bool OnBlockActivated(WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockActivated(world, blockPos, blockValue, player);

        if (!IsSeedStage(blockValue))
            return false;

        ItemStack seedStack = RebirthUtilities.CreateSeedStackFromPlantBlock(blockValue);
        if (seedStack == null || seedStack.IsEmpty())
            return false;

        return RebirthUtilities.TryRemoveBlockAndGrant(world, blockPos, blockValue, player, seedStack);
    }

    public override int OnBlockDamaged(WorldBase world, BlockValueRef blockValueRef, BlockValue blockValue, int damagePoints, int entityIdThatDamaged, ItemActionAttack.AttackHitInfo attackHitInfo, bool useHarvestTool, bool bypassMaxDamage, int recDepth = 0)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockDamaged(world, blockValueRef, blockValue, damagePoints, entityIdThatDamaged, attackHitInfo, useHarvestTool, bypassMaxDamage, recDepth);

        EntityPlayer player = world != null ? world.GetEntity(entityIdThatDamaged) as EntityPlayer : null;
        EntityAlive entity = world != null ? world.GetEntity(entityIdThatDamaged) as EntityAlive : null;
        Vector3i cropPos = blockValueRef.BlockPosition;

        if (!IsFullyGrownCrop(blockValue))
        {
            if (RebirthUtilities.IsHoldingShovel(entity) && RebirthUtilities.ReturnImmatureCropSeedAndClear(world, cropPos, player))
                return 0;

            return blockValue.damage;
        }

        if (RebirthUtilities.IsPlayerGrownHarvestCrop(blockValue) &&
            RebirthUtilities.HarvestPlayerCropAndReplant(world, cropPos, blockValue, player))
        {
            return 0;
        }

        return base.OnBlockDamaged(world, blockValueRef, blockValue, damagePoints, entityIdThatDamaged, attackHitInfo, useHarvestTool, bypassMaxDamage, recDepth);
    }

    public override void Init()
    {
        base.Init();

        DynamicProperties growProps = null;
        if (Properties.Classes != null && Properties.Classes.ContainsKey("PlantGrowing"))
            growProps = Properties.Classes["PlantGrowing"];

        if (Properties.Values.ContainsKey("NextStageRate"))
            nextStageRate = StringParsers.ParseFloat(Properties.Values["NextStageRate"], 0, -1, NumberStyles.Any);
        else if (growProps != null && growProps.Values.ContainsKey("GrowthRate"))
            nextStageRate = StringParsers.ParseFloat(growProps.Values["GrowthRate"], 0, -1, NumberStyles.Any);

        if (Properties.Values.ContainsKey("NextStage"))
            nextStage = Properties.Values["NextStage"];
        else if (growProps != null && growProps.Values.ContainsKey("Next"))
            nextStage = growProps.Values["Next"];

        if (Properties.Values.ContainsKey("TickRate"))
            TickRate = (ulong)(StringParsers.ParseFloat(Properties.Values["TickRate"], 0, -1, NumberStyles.Any) * 20f);

        HasTileEntity = true;
    }

    public override ulong GetTickRate()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.GetTickRate();

#if DEBUG
        if (RebirthVariables.testCropGrowth)
            return 1UL;
#endif
        int seconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds();
        return (ulong)seconds * 20UL;
    }

    public override void LateInit()
    {
        base.LateInit();

        DynamicProperties growProps = null;
        if (Properties.Classes != null && Properties.Classes.ContainsKey("PlantGrowing"))
            growProps = Properties.Classes["PlantGrowing"];

        if (Properties.Values.ContainsKey(PropGrowingFertileLevel))
            fertileLevel = int.Parse(Properties.Values[PropGrowingFertileLevel]);
        else if (growProps != null && growProps.Values.ContainsKey("FertileLevel"))
            fertileLevel = int.Parse(growProps.Values["FertileLevel"]);

        if (Properties.Values.ContainsKey(PropGrowingLightLevelStay))
            lightLevelStay = int.Parse(Properties.Values[PropGrowingLightLevelStay]);
        else if (growProps != null && growProps.Values.ContainsKey("LightLevelStay"))
            lightLevelStay = int.Parse(growProps.Values["LightLevelStay"]);

        if (Properties.Values.ContainsKey(PropGrowingLightLevelGrow))
            lightLevelGrow = int.Parse(Properties.Values[PropGrowingLightLevelGrow]);
        else if (growProps != null && growProps.Values.ContainsKey("LightLevelGrow"))
            lightLevelGrow = int.Parse(growProps.Values["LightLevelGrow"]);

        if (Properties.Values.ContainsKey("TickRate"))
            ulong.TryParse(Properties.Values["TickRate"], out TickRate);
    }

    public override bool CanGrowOn(WorldBase world, Vector3i blockPos, BlockValue blockValueOfPlant)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.CanGrowOn(world, blockPos, blockValueOfPlant);

        if (IsMushroomBlock(blockValueOfPlant))
            return true;

        bool noFertileRequirement = fertileLevel == 0;
        bool hasFertility = world.GetBlock(blockPos).Block.blockMaterial.FertileLevel >= fertileLevel;
        return noFertileRequirement || hasFertility;
    }

    public override bool CanPlaceBlockAt(WorldBase world, Vector3i blockPos, BlockValue blockValue, bool bOmitCollideCheck = false)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.CanPlaceBlockAt(world, blockPos, blockValue, bOmitCollideCheck);

        if (GameManager.Instance.IsEditMode())
            return true;

        if (!(base.CanPlaceBlockAt(world, blockPos, blockValue, bOmitCollideCheck) && CanGrowOn(world, blockPos - Vector3i.up, blockValue)))
            return false;

        // Planting should only validate placement/fertility. Light, temperature, water, and biome
        // are growth conditions and are evaluated by the active crop update path after the seed
        // exists. Blocking placement in the dark prevented players from planting ahead of a later
        // light/heat fix and made diagnostics harder because no plant tile entity existed to audit.
        return true;
    }

    protected virtual void addTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        TileEntityPlantGrowingRebirth te = new TileEntityPlantGrowingRebirth(chunk);
        te.localChunkPos = World.toBlock(blockPos);
        te.GameTimerTicks = GameTimer.Instance.ticks;
        te.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        chunk.AddTileEntity(te);
    }

    protected virtual void removeTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        chunk.RemoveTileEntityAt<TileEntityPlantGrowingRebirth>((World)world, World.toBlock(blockPos));
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
        if (addedByPlayer != null) BlockFarmPlotRebirth.ActivateForPlanting(world, blockPos - Vector3i.up);
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.IsPlantInActiveFarm(world, blockPos))
        {
            return;
        }

        // BlockPlantGrowing scheduled the vanilla tick. Advanced Farming owns the schedule
        // while enabled, so remove that entry before adding the custom deterministic tick.
        if (!world.IsRemote())
            world.GetWBT().InvalidateScheduledBlockUpdate(blockPos, blockID);

        if (world.GetTileEntity(blockPos) == null)
            addTileEntity(world, chunk, blockPos, blockValue);

        if (!world.IsRemote())
        {
            TileEntityPlantGrowingRebirth addedTe = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
            if (addedTe != null)
                addedTe.BeginNewPlantIncarnation();
            RebirthCropProvenanceAdapter.ApplyPending(world, blockPos);
            RebirthCropProvenanceAdapter.CapturePlanting(world, blockPos, addedByPlayer);
        }

        if (IsFullyGrown(blockValue))
            return;

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || GameManager.Instance.IsEditMode())
            return;

        ScheduleNext(world, blockPos);
    }

    public override void OnBlockLoaded(WorldBase world, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockLoaded(world, blockPos, blockValue);
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.IsPlantInActiveFarm(world, blockPos))
        {
            return;
        }

        if (IsFullyGrown(blockValue))
        {
            if (!world.IsRemote())
                world.GetWBT().InvalidateScheduledBlockUpdate(blockPos, blockID);
            return;
        }

        if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
            AdvancedFarmingCatchupService.LogCatchupDebug("BlockLoaded ENTER pos=" + blockPos
                + " block=" + (blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>")
                + " isServer=" + SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer
                + " fullyGrown=" + IsFullyGrown(blockValue));

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
        if (te != null)
            te.SanitizeTimingBaselines();

        if (!IsFullyGrown(blockValue) && te != null)
        {
            if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
                AdvancedFarmingCatchupService.LogCatchupDebug("BlockLoaded PROCESS_SHARED pos=" + blockPos
                    + " block=" + (blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>")
                    + " teLastWorldSeconds=" + te.LastProcessedWorldSeconds
                    + " teGameTicks=" + te.GameTimerTicks
                    + " teAccum=" + te.AccumulatedTicks
                    + " teWatered=" + te.bWatered);

            // Chunk/wake catch-up must use the shared/offline timing path, not the active-direct
            // GameTimerTicks path. The active path is intentionally capped for loaded chunks,
            // while this load path must honor LastProcessedWorldSeconds after the chunk was away.
            AdvancedFarmingCatchupService.ProcessSharedAreaCatchup(world, blockPos, "blockLoaded");

            BlockValue currentBlockValue = world.GetBlock(blockPos);
            TileEntityPlantGrowingRebirth currentTe = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
            if (!IsFullyGrown(currentBlockValue) && currentTe != null)
            {
                if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
                    AdvancedFarmingCatchupService.LogCatchupDebug("BlockLoaded SCHEDULE_NEXT pos=" + blockPos
                        + " block=" + (currentBlockValue.Block != null ? currentBlockValue.Block.GetBlockName() : "<null>")
                        + " teLastWorldSeconds=" + currentTe.LastProcessedWorldSeconds
                        + " teGameTicks=" + currentTe.GameTimerTicks
                        + " teAccum=" + currentTe.AccumulatedTicks
                        + " teWatered=" + currentTe.bWatered);
                ScheduleNext(world, blockPos);
            }
        }
        else if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
        {
            AdvancedFarmingCatchupService.LogCatchupDebug("BlockLoaded SKIP pos=" + blockPos
                + " reason=" + (te == null ? "noTileEntity" : "fullyGrown")
                + " block=" + (blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>"));
        }
    }

    public override void OnBlockRemoved(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockRemoved(world, chunk, blockPos, blockValue);
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
        if (te != null)
            te.OnDestroy();
        removeTileEntity(world, chunk, blockPos, blockValue);
    }

    private static bool IsFullyGrown(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return !string.IsNullOrEmpty(name) && name.IndexOf("3Harvest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public override bool UpdateTick(WorldBase world, Vector3i blockPos, BlockValue blockValue, bool bRandomTick, ulong ticksIfLoaded, GameRandom rnd)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.IsPlantInActiveFarm(world, blockPos))
        {
            return base.UpdateTick(world, blockPos, blockValue, bRandomTick, ticksIfLoaded, rnd);
        }

        if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
            AdvancedFarmingCatchupService.LogCatchupDebug("UpdateTick ENTER pos=" + blockPos
                + " block=" + (blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>")
                + " isServer=" + SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer
                + " fullyGrown=" + IsFullyGrown(blockValue)
                + " bRandomTick=" + bRandomTick
                + " ticksIfLoaded=" + ticksIfLoaded);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;

        if (IsFullyGrown(blockValue))
        {
            if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
                AdvancedFarmingCatchupService.LogCatchupDebug("UpdateTick SKIP pos=" + blockPos + " reason=fullyGrown");
            return false;
        }

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
        if (te == null)
        {
            if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
                AdvancedFarmingCatchupService.LogCatchupDebug("UpdateTick SKIP pos=" + blockPos + " reason=noTileEntity");
            AdvancedFarmingPerfSnapshotService.RecordMissingPlantTileEntityWakeSkip(blockValue);
            return false;
        }

        if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
            AdvancedFarmingCatchupService.LogCatchupDebug("UpdateTick PROCESS_AREA pos=" + blockPos
                + " block=" + (blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>")
                + " teLastWorldSeconds=" + te.LastProcessedWorldSeconds
                + " teGameTicks=" + te.GameTimerTicks
                + " teAccum=" + te.AccumulatedTicks
                + " teWatered=" + te.bWatered);

        AdvancedFarmingCatchupService.ProcessArea(world, blockPos);

        BlockValue currentBlockValue = world.GetBlock(blockPos);
        TileEntityPlantGrowingRebirth currentTe = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
        if (!IsFullyGrown(currentBlockValue) && currentTe != null)
        {
            if (AdvancedFarmingCatchupService.CatchupDebugEnabled)
                AdvancedFarmingCatchupService.LogCatchupDebug("UpdateTick SCHEDULE_NEXT pos=" + blockPos
                    + " block=" + (currentBlockValue.Block != null ? currentBlockValue.Block.GetBlockName() : "<null>")
                    + " teLastWorldSeconds=" + currentTe.LastProcessedWorldSeconds
                    + " teGameTicks=" + currentTe.GameTimerTicks
                    + " teAccum=" + currentTe.AccumulatedTicks
                    + " teWatered=" + currentTe.bWatered);
            ScheduleNext(world, blockPos);
        }

        return false;
    }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || !AdvancedFarmingActiveAreaRegistry.IsPlantInActiveFarm(world, blockPos) || GetBlockName().IndexOf("Dead", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return base.GetActivationText(world, blockValue, blockPos, entityFocusing);

        return AdvancedFarmingHoverTextService.GetPlantText(this, world, blockValue, blockPos, lightLevelStay, lightLevelGrow, minTemp);
    }

    private void ScheduleNext(WorldBase world, Vector3i blockPos)
    {
        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetPlantWakeDelay(blockPos, TickRate));
    }
}
