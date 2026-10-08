using Platform;
using UnityEngine;

#nullable disable

public class BlockWaterTankRebirth : Block
{
    public int waterMax = 400;
    public ulong TickRate = 3000UL;
    public float TakeDelay = 0.5f;

    protected virtual void addTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        TileEntityWaterTankRebirth te = new TileEntityWaterTankRebirth(chunk);
        te.localChunkPos = World.toBlock(blockPos);
        te.waterMax = ResolveWaterMax(te);
        chunk.AddTileEntity(te);
    }

    protected virtual void removeTileEntity(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        chunk.RemoveTileEntityAt<TileEntityWaterTankRebirth>((World)world, World.toBlock(blockPos));
    }

    public BlockWaterTankRebirth()
    {
        HasTileEntity = true;
    }

    public override bool HasBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.HasBlockActivationCommands(world, blockValue, blockPos, entityFocusing);
        return true;
    }

    public override void Init()
    {
        base.Init();
        if (Properties.Values.ContainsKey("MaxWater"))
            int.TryParse(Properties.Values["MaxWater"], out waterMax);
        if (Properties.Values.ContainsKey("TickRate"))
            ulong.TryParse(Properties.Values["TickRate"], out TickRate);
        if (Properties.Values.ContainsKey("TakeDelay"))
            float.TryParse(Properties.Values["TakeDelay"], out TakeDelay);
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (world.GetTileEntity(blockPos) == null)
            addTileEntity(world, chunk, blockPos, blockValue);

        AdvancedFarmingWaterProviderRegistry.RegisterProvider(world, blockPos, blockValue);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || GameManager.Instance.IsEditMode())
            return;

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
    }

    public override void OnBlockLoaded(WorldBase world, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockLoaded(world, blockPos, blockValue);
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        AdvancedFarmingWaterProviderRegistry.RegisterProvider(world, blockPos, blockValue);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
    }

    public override void OnBlockRemoved(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockRemoved(world, chunk, blockPos, blockValue);
        TileEntityWaterTankRebirth te = world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        if (te != null)
            te.OnDestroy();
        removeTileEntity(world, chunk, blockPos, blockValue);
        AdvancedFarmingWaterProviderRegistry.UnregisterProvider(blockPos);
    }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.GetActivationText(world, blockValue, blockPos, entityFocusing);
        return AdvancedFarmingHoverTextService.GetWaterProviderText(world, blockValue, blockPos);
    }

    public override bool OnBlockActivated(string commandName, WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.OnBlockActivated(commandName, world, blockPos, blockValue, player);

        if (blockValue.ischild)
        {
            Vector3i parentPos = blockValue.Block.multiBlockPos.GetParentPos(blockPos, blockValue);
            BlockValue parent = world.GetBlock(parentPos);
            return OnBlockActivated(commandName, world, parentPos, parent, player);
        }

        if (commandName == cmds[1].text)
        {
            if (((World)world).IsWithinTraderArea(blockPos))
            {
                Audio.Manager.PlayInsidePlayerHead("ui_denied");
                GameManager.ShowTooltip(player, Localization.Get("ttBelongsToTrader"), string.Empty, "ui_denied");
                return false;
            }

            return RebirthUtilities.TryTakeBlockItem(world, blockPos, blockValue, player, 1);
        }

        return false;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.GetBlockActivationCommands(world, blockValue, blockPos, entityFocusing);
        return cmds;
    }

    public override bool UpdateTick(WorldBase world, Vector3i blockPos, BlockValue blockValue, bool bRandomTick, ulong ticksIfLoaded, GameRandom rnd)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return base.UpdateTick(world, blockPos, blockValue, bRandomTick, ticksIfLoaded, rnd);

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;

        TileEntityWaterTankRebirth te = world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        if (te != null)
            AdvancedFarmingCatchupService.ProcessWaterProvider(world, blockPos, blockValue, te, RebirthUtilities.TotalGameSecondsPassed());

        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetJitteredDelay(blockPos, TickRate));
        return false;
    }

    private int ResolveWaterMax(TileEntityWaterTankRebirth te)
    {
        if (Properties.Values.ContainsKey("MaxWater") && int.TryParse(Properties.Values["MaxWater"], out int parsed) && parsed > 0)
            return parsed;
        return te != null && te.waterMax > 0 ? te.waterMax : waterMax;
    }
}
