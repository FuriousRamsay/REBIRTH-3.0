using System;
#nullable disable

// REVIEW: host-local native seed action only. Remote authority is not supplied by this class.
public static class AdvancedFarmingLocalPlantingAdmission
{
    public static LocalScope Begin(ItemInventoryData data, BlockPlacement.Result placement, BlockValue actualTarget)
    { return LocalScope.Begin(data, placement, actualTarget); }
    internal static bool ProtectNewerAdded(WorldBase world, Vector3i pos, TileEntityPlantGrowingRebirth te)
    { return LocalScope.ProtectNewerAdded(world, pos, te); }
    internal static bool ObserveAddedReset(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
    { return LocalScope.ObserveAddedReset(world, pos, value, te); }
    internal static bool TryAdmit(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
    { return LocalScope.TryAdmit(world, pos, value, te); }

    public sealed class LocalScope : IDisposable
    {
        [ThreadStatic] private static LocalScope current;
        private readonly World world;
        private readonly EntityPlayerLocal player;
        private readonly ItemInventoryData data;
        private readonly Vector3i position;
        private readonly BlockValue target;
        private readonly int thread;
        private TileEntityPlantGrowingRebirth admitted;
        private ulong incarnation, revision;
        private bool valid = true, completed, disposed, originResetObserved;
        private LocalScope(World world, EntityPlayerLocal player, ItemInventoryData data, BlockPlacement.Result placement, BlockValue actualTarget)
        {
            this.world = world; this.player = player; this.data = data;
            position = placement.blockPos; target = actualTarget;
            thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        internal static LocalScope Begin(ItemInventoryData data, BlockPlacement.Result placement, BlockValue actualTarget)
        {
            GameManager gm = GameManager.Instance;
            EntityPlayerLocal player = data != null ? data.holdingEntity as EntityPlayerLocal : null;
            World world = gm != null ? gm.World : null;
            BlockPlantGrowingRebirth crop = actualTarget.Block as BlockPlantGrowingRebirth;
            if (gm == null || gm.IsEditMode() || world == null || world.IsRemote() || player == null ||
                !object.ReferenceEquals(world.GetPrimaryPlayer(), player) || !object.ReferenceEquals(data.world, world) ||
                !object.ReferenceEquals(player.inventory.holdingItemData, data) || player.inventory.holdingItemStack.count <= 0 ||
                !data.IsBlock || data.itemValue.ToBlockValue().type != actualTarget.type ||
                placement.placement != BlockPlacement.EnumPlacement.Voxel || crop == null || !crop.IsSeedStage(actualTarget) ||
                world.GetTileEntity(placement.blockPos) is TileEntityPlantGrowingRebirth) return null;
            if (current != null) { current.valid = false; return null; }
            current = new LocalScope(world, player, data, placement, actualTarget);
            return current;
        }
        private bool ActorCurrent()
        {
            GameManager gm = GameManager.Instance;
            return valid && !disposed && thread == System.Threading.Thread.CurrentThread.ManagedThreadId &&
                gm != null && !gm.IsEditMode() && object.ReferenceEquals(gm.World, world) && !world.IsRemote() &&
                object.ReferenceEquals(world.GetPrimaryPlayer(), player) && object.ReferenceEquals(data.world, world) &&
                object.ReferenceEquals(data.holdingEntity, player) && object.ReferenceEquals(player.inventory.holdingItemData, data) &&
                player.inventory.holdingItemStack.count > 0 && data.IsBlock && data.itemValue.ToBlockValue().type == target.type;
        }
        internal static bool ProtectNewerAdded(WorldBase world, Vector3i pos, TileEntityPlantGrowingRebirth te)
        {
            LocalScope scope = current;
            if (scope == null || scope.disposed || !object.ReferenceEquals(scope.world, world) ||
                !SamePos(scope.position, pos) || !object.ReferenceEquals(scope.admitted, te) || te == null) return false;
            if (te.PlantIncarnation == scope.incarnation && te.StateRevision == scope.revision) return false;
            scope.valid = false;
            return true;
        }
        internal static bool ObserveAddedReset(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
        {
            LocalScope scope = current;
            if (scope != null && !scope.disposed && object.ReferenceEquals(scope.world, world) && SamePos(scope.position, pos) &&
                object.ReferenceEquals(scope.admitted, te) && te.PlantIncarnation == scope.incarnation && te.StateRevision == scope.revision &&
                te.PlantOrigin == AdvancedFarmingPlantOrigin.Player && SameBlock(scope.target, value))
            { scope.originResetObserved = true; return true; }
            return false;
        }
        internal static bool TryAdmit(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
        {
            LocalScope scope = current;
            if (scope == null) return false;
            if (!scope.ActorCurrent() || scope.admitted != null || !object.ReferenceEquals(scope.world, world) ||
                !SamePos(scope.position, pos) || !SameBlock(scope.target, value) || !SameBlock(world.GetBlock(pos), value) ||
                !object.ReferenceEquals(world.GetTileEntity(pos), te) || te == null || te.PlantIncarnation == 0 || te.StateRevision == 0)
            { scope.valid = false; return false; }
            scope.admitted = te; scope.incarnation = te.PlantIncarnation; scope.revision = te.StateRevision;
            te.PlantOrigin = AdvancedFarmingPlantOrigin.Player;
            return true;
        }
        private bool TargetCurrent()
        { return ActorCurrent() && admitted != null && object.ReferenceEquals(world.GetTileEntity(position), admitted) &&
            SameBlock(world.GetBlock(position), target) && admitted.PlantIncarnation == incarnation &&
            admitted.StateRevision == revision && admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Player; }
        public bool Complete()
        {
            if (completed || !TargetCurrent()) { valid = false; return false; }
            admitted.SetModified();
            if (!TargetCurrent()) { valid = false; return false; }
            AdvancedFarmingHoverTextService.InvalidatePlantState(position);
            if (!TargetCurrent()) { valid = false; return false; }
            completed = true; return true;
        }
        private bool CleanupCurrent(ulong expectedIncarnation, ulong expectedRevision)
        {
            return object.ReferenceEquals(world.GetTileEntity(position), admitted) &&
                admitted.PlantIncarnation == expectedIncarnation && admitted.StateRevision == expectedRevision &&
                admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Unknown && SameBlock(world.GetBlock(position), target);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Planting scope thread changed.");
            try
            {
                if (!completed && admitted != null && object.ReferenceEquals(world.GetTileEntity(position), admitted) &&
                    admitted.PlantIncarnation == incarnation && admitted.StateRevision == revision &&
                    (admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Player || (originResetObserved && admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Unknown)) &&
                    SameBlock(world.GetBlock(position), target))
                {
                    admitted.PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
                    admitted.AdvanceStateRevision();
                    ulong cleanupRevision = admitted.StateRevision;
                    ulong cleanupIncarnation = admitted.PlantIncarnation;
                    if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                    {
                        bool cancelled = AdvancedFarmingPlantOriginService.TryInvalidateExactScheduledTick(world, position, target.type);
                        if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                        {
                            try { admitted.SetModified(); }
                            finally
                            {
                                if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                                {
                                    AdvancedFarmingHoverTextService.InvalidatePlantState(position);
                                    if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                                    {
                                        BlockPlantGrowing plant = target.Block as BlockPlantGrowing;
                                        if (cancelled && plant != null && !plant.nextPlant.isair) plant.addScheduledTick(world, position);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally { disposed = true; valid = false; if (object.ReferenceEquals(current, this)) current = null; }
        }
        private static bool SamePos(Vector3i a, Vector3i b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
        private static bool SameBlock(BlockValue a, BlockValue b)
        { return a.type == b.type && a.rotation == b.rotation && a.meta == b.meta && a.meta2 == b.meta2 && a.damage == b.damage; }
    }
}





