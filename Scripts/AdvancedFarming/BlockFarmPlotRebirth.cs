using Platform;

#nullable disable

public class BlockFarmPlotRebirth : Block
{
    public int waterMax = 6;
    public int rainCollectionSeconds = 600;
    public ulong TickRate = 3000UL;

    protected virtual void addTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        TileEntityFarmPlotRebirth te = new TileEntityFarmPlotRebirth(chunk);
        te.localChunkPos = World.toBlock(blockPos);
        te.waterMax = ResolveWaterMax(te);
        chunk.AddTileEntity(te);
    }

    protected virtual void removeTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        chunk.RemoveTileEntityAt<TileEntityFarmPlotRebirth>((World)world, World.toBlock(blockPos));
    }

    public BlockFarmPlotRebirth()
    {
        HasTileEntity = true;
    }

    public override bool HasBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.HasBlockActivationCommands(world, blockValue, blockPos, entityFocusing);
        return IsActivated(world, blockPos);
    }

    public override void Init()
    {
        base.Init();
        if (Properties.Values.ContainsKey("WaterMax"))
            int.TryParse(Properties.Values["WaterMax"], out waterMax);
        if (Properties.Values.ContainsKey("RainCollectionSeconds"))
            int.TryParse(Properties.Values["RainCollectionSeconds"], out rainCollectionSeconds);
        if (rainCollectionSeconds < 1)
            rainCollectionSeconds = 600;
        if (Properties.Values.ContainsKey("TickRate"))
            ulong.TryParse(Properties.Values["TickRate"], out TickRate);
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (addedByPlayer == null) return;
        if (world.GetTileEntity(blockPos) == null)
            addTileEntity(world, chunk, blockPos, blockValue);
        var placedTe = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (placedTe != null) placedTe.FarmingActivated = true;

        TileEntityFarmPlotRebirth activationTe = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (activationTe == null || !activationTe.FarmingActivated) return;
        AdvancedFarmingWaterProviderRegistry.RegisterProvider(world, blockPos, blockValue);
        AdvancedFarmingActiveAreaRegistry.RegisterFarmPlot(blockPos);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || GameManager.Instance.IsEditMode())
            return;

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
    }

    public override void OnBlockLoaded(WorldBase world, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockLoaded(world, blockPos, blockValue);
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        TileEntityFarmPlotRebirth activationTe = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (activationTe == null || !activationTe.FarmingActivated) return;
        AdvancedFarmingWaterProviderRegistry.RegisterProvider(world, blockPos, blockValue);
        AdvancedFarmingActiveAreaRegistry.RegisterFarmPlot(blockPos);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        TileEntityFarmPlotRebirth te = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (te != null)
        {
            World authoritativeWorld = world as World;
            te.LastRainSampleWorldTime = authoritativeWorld != null ? authoritativeWorld.GetWorldTime() : 0UL;
            te.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        }

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
    }

    public override void OnBlockRemoved(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockRemoved(world, chunk, blockPos, blockValue);
        TileEntityFarmPlotRebirth te = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (te != null)
            te.OnDestroy();
        removeTileEntity(world, chunk, blockPos, blockValue);
        AdvancedFarmingWaterProviderRegistry.UnregisterProvider(blockPos);
        AdvancedFarmingActiveAreaRegistry.UnregisterFarmPlot(blockPos);
    }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.GetActivationText(world, blockValue, blockPos, entityFocusing);
        return IsActivated(world, blockPos) ? AdvancedFarmingHoverTextService.GetFarmPlotText(world, blockValue, blockPos) : string.Empty;
    }

    public override bool UpdateTick(WorldBase world, Vector3i blockPos, BlockValue blockValue, bool bRandomTick, ulong ticksIfLoaded, GameRandom rnd)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.UpdateTick(world, blockPos, blockValue, bRandomTick, ticksIfLoaded, rnd);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;

        TileEntityFarmPlotRebirth te = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (te == null || !te.FarmingActivated) return false;
        if (te != null)
            AdvancedFarmingCatchupService.ProcessFarmPlot(world, blockPos, blockValue, te, RebirthUtilities.TotalGameSecondsPassed());

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
        return false;
    }


    public override int OnBlockDamaged(WorldBase world, BlockValueRef blockValueRef, BlockValue blockValue, int damagePoints, int entityIdThatDamaged, ItemActionAttack.AttackHitInfo attackHitInfo, bool useHarvestTool, bool bypassMaxDamage, int recDepth = 0)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockDamaged(world, blockValueRef, blockValue, damagePoints, entityIdThatDamaged, attackHitInfo, useHarvestTool, bypassMaxDamage, recDepth);

        EntityAlive entity = world != null ? world.GetEntity(entityIdThatDamaged) as EntityAlive : null;
        if (!RebirthUtilities.IsFarmPlotDamageTool(entity))
            return blockValue.damage;

        // Shoveling a farm plot should only return the crop/seed when the plot is actually destroyed.
        // Do not harvest, replant, clear, or award the crop from ordinary damage ticks.
        return base.OnBlockDamaged(world, blockValueRef, blockValue, damagePoints, entityIdThatDamaged, attackHitInfo, useHarvestTool, bypassMaxDamage, recDepth);
    }

    public override Block.DestroyedResult OnBlockDestroyedBy(WorldBase world, BlockValueRef blockValueRef, BlockValue blockValue, int entityId, bool useHarvestTool)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockDestroyedBy(world, blockValueRef, blockValue, entityId, useHarvestTool);

        EntityPlayer player = world != null ? world.GetEntity(entityId) as EntityPlayer : null;
        Vector3i plotPos = blockValueRef.BlockPosition;
        RebirthUtilities.HarvestOrReturnCropAbovePlot(world, plotPos + Vector3i.up, player);
        return base.OnBlockDestroyedBy(world, blockValueRef, blockValue, entityId, useHarvestTool);
    }

    public static bool IsActivated(WorldBase world, Vector3i pos)
    {
        return (world.GetTileEntity(pos) as TileEntityFarmPlotRebirth)?.FarmingActivated == true;
    }

    public static void ActivateForPlanting(WorldBase world, Vector3i pos)
    {
        var value = world.GetBlock(pos);
        var block = value.Block as BlockFarmPlotRebirth;
        if (block == null || !AdvancedFarmingRuntimePolicy.Enabled) return;
        var te = world.GetTileEntity(pos) as TileEntityFarmPlotRebirth;
        if (te == null)
        {
            var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
            if (chunk == null) return;
            block.addTileEntity(world, chunk, pos, value);
            te = world.GetTileEntity(pos) as TileEntityFarmPlotRebirth;
        }
        if (te == null || te.FarmingActivated) return;
        te.FarmingActivated = true;
        te.SetModified();
        AdvancedFarmingActiveAreaRegistry.RegisterFarmPlot(pos);
        AdvancedFarmingWaterProviderRegistry.RegisterProvider(world, pos, value);
        if (!world.IsRemote())
            world.GetWBT().AddScheduledBlockUpdate(pos, block.blockID,
                AdvancedFarmingRuntimePolicy.GetJitteredDelay(pos, block.TickRate));
    }
    private int ResolveWaterMax(TileEntityFarmPlotRebirth te)
    {
        if (Properties.Values.ContainsKey("WaterMax") && int.TryParse(Properties.Values["WaterMax"], out int parsed) && parsed > 0)
            return parsed;
        return te != null && te.waterMax > 0 ? te.waterMax : waterMax;
    }
}
