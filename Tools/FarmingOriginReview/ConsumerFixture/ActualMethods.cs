using System;public class BlockPlantGrowingRebirth:BlockPlantGrowing{public int blockID=7;public ulong TickRate=100;public bool FullyGrown=true;public bool IsSeedStage(BlockValue v)=>true;public bool IsFullyGrownCrop(BlockValue v)=>FullyGrown;private bool IsFullyGrown(BlockValue v)=>FullyGrown;public override int OnBlockDamaged(WorldBase world, BlockValueRef blockValueRef, BlockValue blockValue, int damagePoints, int entityIdThatDamaged, ItemActionAttack.AttackHitInfo attackHitInfo, bool useHarvestTool, bool bypassMaxDamage, int recDepth = 0)
    {
        if (world != null && world.IsRemote() || !AdvancedFarmingRuntimePolicy.Enabled || !AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, blockValueRef.BlockPosition))
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
    }private void ScheduleNext(WorldBase world, Vector3i blockPos)
    {
        // Recheck after catchup/native callbacks: a replacement or hash alias must not
        // inherit this crop's custom schedule or lose another position's entry.
        if (world == null || world.IsRemote() || !AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, blockPos) ||
            world.GetBlock(blockPos).type != blockID ||
            !AdvancedFarmingPlantOriginService.TryInvalidateExactScheduledTick(world, blockPos, blockID))
            return;
        world.GetWBT().AddScheduledBlockUpdate(blockPos, blockID, AdvancedFarmingRuntimePolicy.GetPlantWakeDelay(blockPos, TickRate));
    }public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
        // Universal origin incarnation exists even before farming enable or plot creation.
        if (world.GetTileEntity(blockPos) == null)
            addTileEntity(world, chunk, blockPos, blockValue);

        if (!world.IsRemote())
        {
            TileEntityPlantGrowingRebirth addedTe = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
            if (addedTe != null)
            {
                if (addedTe.PlantIncarnation == 0UL)
                    addedTe.BeginNewPlantIncarnation();
                else
                    addedTe.EnsureAuthoritativeIncarnation();
            }
            // Root-owned proposed handoff consumes ONLY trusted human placement/stage admission.
            // It must not infer Player from addedByPlayer, actor id, bonus or crop family.
            AdvancedFarmingPlantOriginService.OnNativePlantAdded(world, blockPos, blockValue, addedTe);
            RebirthCropProvenanceAdapter.ApplyPending(world, blockPos);
            RebirthCropProvenanceAdapter.CapturePlanting(world, blockPos, addedByPlayer);
        }


        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, blockPos) ||
            !AdvancedFarmingActiveAreaRegistry.IsPlantInActiveFarm(world, blockPos))
        {
            return;
        }

        // BlockPlantGrowing scheduled the vanilla tick. Advanced Farming owns the schedule
        // while enabled, so remove that entry before adding the custom deterministic tick.
        if (!world.IsRemote() && !AdvancedFarmingPlantOriginService.TryInvalidateExactScheduledTick(world, blockPos, blockID))
            return;

        if (IsFullyGrown(blockValue))
            return;

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || GameManager.Instance.IsEditMode())
            return;

        ScheduleNext(world, blockPos);
    }protected virtual void addTileEntity(WorldBase w,Chunk c,Vector3i p,BlockValue v){w.Tile=new TileEntityPlantGrowingRebirth();}public void InvokeSchedule(WorldBase w,Vector3i p)=>ScheduleNext(w,p);}