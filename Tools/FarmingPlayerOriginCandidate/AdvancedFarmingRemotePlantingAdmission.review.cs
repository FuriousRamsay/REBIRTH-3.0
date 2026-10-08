using System;
#nullable disable

// TOOLS REVIEW: host remote admission only; execution/debit/outcome transport belong to root.
internal static class AdvancedFarmingRemotePlantingAdmissionReview
{
    private sealed class HookAdapter : AdvancedFarmingPlantOriginService.IRemoteAdmissionHooks
    {
        internal static readonly HookAdapter Instance=new HookAdapter();
        private HookAdapter(){}
        public bool ProtectNewerAdded(WorldBase world,Vector3i pos,TileEntityPlantGrowingRebirth te)
            => AdvancedFarmingRemotePlantingAdmissionReview.ProtectNewerAdded(world,pos,te);
        public bool ObserveAddedReset(WorldBase world,Vector3i pos,BlockValue value,TileEntityPlantGrowingRebirth te)
            => AdvancedFarmingRemotePlantingAdmissionReview.ObserveAddedReset(world,pos,value,te);
        public bool TryAdmit(WorldBase world,Vector3i pos,BlockValue value,TileEntityPlantGrowingRebirth te)
            => AdvancedFarmingRemotePlantingAdmissionReview.TryAdmit(world,pos,value,te);
    }
    internal static void InstallHooksForReview()
        => AdvancedFarmingPlantOriginService.RegisterRemoteAdmissionHooks(HookAdapter.Instance);
    public static RemoteScope Begin(SeedPlacementSessionAuthority.Reservation reservation,
        SeedPlacementOriginalCommandReview command,World world,EntityPlayer actor)
    { return RemoteScope.Begin(reservation,command,world,actor); }
    internal static bool ProtectNewerAdded(WorldBase world, Vector3i pos, TileEntityPlantGrowingRebirth te)
    { return RemoteScope.ProtectNewerAdded(world, pos, te); }
    internal static bool ObserveAddedReset(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
    { return RemoteScope.ObserveAddedReset(world, pos, value, te); }
    internal static bool TryAdmit(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
    { return RemoteScope.TryAdmit(world, pos, value, te); }

    public sealed class RemoteScope : IDisposable
    {
        [ThreadStatic] private static RemoteScope current;
        private readonly World world;
        private readonly EntityPlayer player;
        private readonly SeedPlacementSessionAuthority.Reservation reservation;
        private readonly SeedPlacementOriginalCommandReview command;
        private readonly BlockValue expectedOld;
        private bool executionBegun;
        private readonly Vector3i position;
        private readonly BlockValue target;
        private readonly int thread;
        private TileEntityPlantGrowingRebirth admitted;
        private ulong incarnation, revision;
        private bool valid = true, completed, disposed, originResetObserved;
        private RemoteScope(SeedPlacementSessionAuthority.Reservation reservation,
            SeedPlacementOriginalCommandReview command,World world,EntityPlayer actor)
        {
            this.reservation=reservation;this.command=command;this.world=world;player=actor;
            position=command.Position;target=command.Target;expectedOld=command.ExpectedOld;
            thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        internal static RemoteScope Begin(SeedPlacementSessionAuthority.Reservation reservation,
            SeedPlacementOriginalCommandReview command,World world,EntityPlayer actor)
        {
            var gm=GameManager.Instance;
            var crop=command?.Target.Block as BlockPlantGrowingRebirth;
            if(command==null||reservation==null||world==null||actor==null||gm==null||gm.IsEditMode()||
                !SeedPlacementSessionAuthority.IsBoundTo(reservation,world,actor)||
                command.Epoch!=reservation.Epoch||command.Nonce!=reservation.Nonce||command.Actor!=actor.entityId||
                crop==null||!crop.IsSeedStage(command.Target)||
                !SameBlock(world.GetBlock(command.Position),command.ExpectedOld)||
                world.GetTileEntity(command.Position) is TileEntityPlantGrowingRebirth)return null;
            if(current!=null){current.valid=false;return null;}
            current=new RemoteScope(reservation,command,world,actor);return current;
        }
        private bool ActorCurrent()
        {
            var gm=GameManager.Instance;
            return valid&&!disposed&&thread==System.Threading.Thread.CurrentThread.ManagedThreadId&&
                gm!=null&&!gm.IsEditMode()&&ReferenceEquals(gm.World,world)&&!world.IsRemote()&&
                SeedPlacementSessionAuthority.IsBoundTo(reservation,world,player);
        }
        public bool BeforeNativeExecution()
        {
            if(executionBegun||!ActorCurrent()||!SameBlock(world.GetBlock(position),expectedOld)||
                world.GetTileEntity(position) is TileEntityPlantGrowingRebirth)
            {valid=false;return false;}
            executionBegun=true;return true;
        }
        internal static bool ProtectNewerAdded(WorldBase world, Vector3i pos, TileEntityPlantGrowingRebirth te)
        {
            RemoteScope scope = current;
            if (scope == null || scope.disposed || !object.ReferenceEquals(scope.world, world) ||
                !SamePos(scope.position, pos) || !object.ReferenceEquals(scope.admitted, te) || te == null) return false;
            if (te.PlantIncarnation == scope.incarnation && te.StateRevision == scope.revision) return false;
            scope.valid = false;
            return true;
        }
        internal static bool ObserveAddedReset(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
        {
            RemoteScope scope = current;
            if (scope != null && !scope.disposed && object.ReferenceEquals(scope.world, world) && SamePos(scope.position, pos) &&
                object.ReferenceEquals(scope.admitted, te) && te.PlantIncarnation == scope.incarnation && te.StateRevision == scope.revision &&
                te.PlantOrigin == AdvancedFarmingPlantOrigin.Player && SameBlock(scope.target, value))
            { scope.originResetObserved = true; return true; }
            return false;
        }
        internal static bool TryAdmit(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te)
        {
            RemoteScope scope = current;
            if (scope == null) return false;
            if (!scope.executionBegun || !scope.ActorCurrent() || scope.admitted != null || !object.ReferenceEquals(scope.world, world) ||
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
                if (!completed && ReferenceEquals(GameManager.Instance?.World,world) && !world.IsRemote() && admitted != null && object.ReferenceEquals(world.GetTileEntity(position), admitted) &&
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







