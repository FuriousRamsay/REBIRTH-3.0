using System;
using System.Collections.Generic;
#nullable disable

public enum AdvancedFarmingPlantOrigin : byte { Unknown = 0, Player = 1, System = 2 }

// Isolated review candidate. All callers must run on the authoritative native game thread.
// Human planting admission is deliberately absent until its authenticated native path is qualified.
public static class AdvancedFarmingPlantOriginService
{
    internal interface IRemoteAdmissionHooks
    {
        bool ProtectNewerAdded(WorldBase world, Vector3i pos, TileEntityPlantGrowingRebirth te);
        bool ObserveAddedReset(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te);
        bool TryAdmit(WorldBase world, Vector3i pos, BlockValue value, TileEntityPlantGrowingRebirth te);
    }
    private static IRemoteAdmissionHooks remoteHooks;
    internal static void RegisterRemoteAdmissionHooks(IRemoteAdmissionHooks hooks)
    {
        if (hooks == null) throw new ArgumentNullException(nameof(hooks));
        if (remoteHooks != null && !object.ReferenceEquals(remoteHooks, hooks))
            throw new InvalidOperationException("Remote origin hook owner already registered.");
        remoteHooks = hooks;
    }

    public static bool IsValid(AdvancedFarmingPlantOrigin value)
    { return value == AdvancedFarmingPlantOrigin.Unknown || value == AdvancedFarmingPlantOrigin.Player || value == AdvancedFarmingPlantOrigin.System; }

    public static bool IsPlayerPlanted(WorldBase world, Vector3i pos)
    {
        if (world == null) return false;
        var te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        return te != null && te.PlantIncarnation != 0 && te.StateRevision != 0 && te.PlantOrigin == AdvancedFarmingPlantOrigin.Player;
    }

    public static NativeTransition BeginNativeTransition(WorldBase world, Vector3i pos,
        BlockValue sourceValue, BlockValue targetValue, TileEntityPlantGrowingRebirth source)
    { return NativeTransition.Begin(world, pos, sourceValue, targetValue, source); }
    public static void OnNativePlantRemoving(WorldBase world, Vector3i pos, BlockValue value,
        TileEntityPlantGrowingRebirth source)
    { NativeTransition.Removing(world, pos, value, source); }
    public static void OnNativePlantAdded(WorldBase world, Vector3i pos, BlockValue value,
        TileEntityPlantGrowingRebirth te)
    { NativeTransition.Added(world, pos, value, te); }
    internal static bool TryInvalidateExactScheduledTick(WorldBase world, Vector3i pos, int blockId)
    {
        var ticker = world.GetWBT();
        if (ticker == null || blockId == 0) return false;
        lock (ticker.lockObject)
        {
            WorldBlockTickerEntry entry;
            if (ticker.scheduledTicksDict.TryGetValue(WorldBlockTickerEntry.ToHashCode(pos, blockId), out entry) &&
                (entry == null || entry.blockID != blockId || !SamePos(entry.worldPos, pos))) return false;
            ticker.InvalidateScheduledBlockUpdate(pos, blockId);
            return true;
        }
    }
    private static bool SamePos(Vector3i a, Vector3i b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
    private static bool SameBlock(BlockValue a, BlockValue b)
    { return a.type == b.type && a.rotation == b.rotation && a.meta == b.meta && a.meta2 == b.meta2 && a.damage == b.damage; }

    public sealed class NativeTransition : IDisposable
    {
        [ThreadStatic] private static List<NativeTransition> active;
    // Must be called immediately around the existing synchronous native mutation, never an RPC wait.
    internal static NativeTransition Begin(WorldBase world, Vector3i pos,
        BlockValue sourceValue, BlockValue targetValue, TileEntityPlantGrowingRebirth source)
    {
        if (world == null || world.IsRemote() || source == null || targetValue.isair ||
            !object.ReferenceEquals(world.GetTileEntity(pos), source) || !SameBlock(world.GetBlock(pos), sourceValue) ||
            source.PlantIncarnation == 0 || source.StateRevision == 0 || !IsValid(source.PlantOrigin)) return null;
        if (active == null) active = new List<NativeTransition>();
        foreach (var existing in active)
            if (object.ReferenceEquals(existing.world, world) && SamePos(existing.pos, pos))
            { existing.valid = false; return null; }
        var scope = new NativeTransition(world, pos, sourceValue, targetValue, source);
        active.Add(scope);
        return scope;
    }

    // Exact hook BEFORE native removal callbacks; no identity inference after the TE is gone.
    internal static void Removing(WorldBase world, Vector3i pos, BlockValue value,
        TileEntityPlantGrowingRebirth source)
    {
        var scope = Find(world, pos);
        if (scope == null) return;
        if (scope.removed || scope.admitted != null || !scope.mutationStarted || !scope.SourceFieldsUnchanged() ||
            !object.ReferenceEquals(source, scope.source) || !object.ReferenceEquals(world.GetTileEntity(pos), source) ||
            !SameBlock(world.GetBlock(pos), scope.targetValue) || !SameBlock(value, scope.sourceValue))
        { scope.valid = false; return; }
        scope.removed = true;
    }

    // Exact hook after new TE publication, before deciding its farming schedule.
    internal static void Added(WorldBase world, Vector3i pos, BlockValue value,
        TileEntityPlantGrowingRebirth te)
    {
        if (world == null || world.IsRemote() || te == null || !object.ReferenceEquals(world.GetTileEntity(pos), te)) return;
        if (AdvancedFarmingLocalPlantingAdmission.ProtectNewerAdded(world, pos, te) ||
            (remoteHooks != null && remoteHooks.ProtectNewerAdded(world, pos, te))) return;
        var resetScope = Find(world, pos);
        if (resetScope != null && object.ReferenceEquals(resetScope.admitted, te) &&
            (te.PlantIncarnation != resetScope.targetIncarnation || te.StateRevision != resetScope.targetRevision))
        { resetScope.valid = false; return; }
        if (resetScope != null && object.ReferenceEquals(resetScope.admitted, te) &&
            te.PlantIncarnation == resetScope.targetIncarnation && te.StateRevision == resetScope.targetRevision &&
            te.PlantOrigin == resetScope.origin && SameBlock(value, resetScope.targetValue))
            resetScope.originResetObserved = true;
        bool localResetWitness = AdvancedFarmingLocalPlantingAdmission.ObserveAddedReset(world, pos, value, te);
        bool remoteResetWitness = remoteHooks != null && remoteHooks.ObserveAddedReset(world, pos, value, te);
        // Fresh native TEs start Unknown; existing identity resets require an active owner witness.
        if (resetScope != null || localResetWitness || remoteResetWitness || te.PlantIncarnation == 0 || te.StateRevision == 0)
            te.PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
        if (AdvancedFarmingLocalPlantingAdmission.TryAdmit(world, pos, value, te)) return;
        if (remoteHooks != null && remoteHooks.TryAdmit(world, pos, value, te)) return;
        var scope = Find(world, pos);
        if (scope == null) return;
        if (!scope.valid || !scope.removed || scope.admitted != null ||
            !scope.SourceFieldsUnchanged() || !SameBlock(value, scope.targetValue) ||
            !SameBlock(world.GetBlock(pos), scope.targetValue) || object.ReferenceEquals(te, scope.source) ||
            te.PlantIncarnation == 0 || te.PlantIncarnation == scope.incarnation || te.StateRevision == 0)
        { scope.valid = false; return; }
        scope.admitted = te;
        scope.targetIncarnation = te.PlantIncarnation;
        scope.targetRevision = te.StateRevision;
        te.PlantOrigin = scope.origin;
    }

    private static NativeTransition Find(WorldBase world, Vector3i pos)
    {
        if (active != null)
            for (int i = active.Count - 1; i >= 0; --i)
                if (object.ReferenceEquals(active[i].world, world) && SamePos(active[i].pos, pos)) return active[i];
        return null;
    }

        private readonly WorldBase world;
        private readonly Vector3i pos;
        private readonly BlockValue sourceValue, targetValue;
        private readonly TileEntityPlantGrowingRebirth source;
        private readonly ulong incarnation, revision;
        private readonly AdvancedFarmingPlantOrigin origin;
        private readonly int thread;
        private bool valid = true, mutationStarted, removed, finished, disposed, originResetObserved;
        private TileEntityPlantGrowingRebirth admitted;
        private ulong targetIncarnation, targetRevision;
        private NativeTransition(WorldBase world, Vector3i pos, BlockValue from, BlockValue to, TileEntityPlantGrowingRebirth source)
        {
            this.world = world; this.pos = pos; sourceValue = from; targetValue = to; this.source = source;
            incarnation = source.PlantIncarnation; revision = source.StateRevision; origin = source.PlantOrigin;
            thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        private bool SourceFieldsUnchanged()
        { return source.PlantIncarnation == incarnation && source.StateRevision == revision && source.PlantOrigin == origin; }
        private bool SourceCurrent()
        { return valid && !disposed && thread == System.Threading.Thread.CurrentThread.ManagedThreadId &&
            object.ReferenceEquals(world.GetTileEntity(pos), source) && SameBlock(world.GetBlock(pos), sourceValue) && SourceFieldsUnchanged(); }
        public bool BeforeMutation()
        {
            if (mutationStarted || removed || admitted != null || !SourceCurrent()) { valid = false; return false; }
            mutationStarted = true;
            return true;
        }
        private bool TargetCurrent()
        { return valid && !disposed && thread == System.Threading.Thread.CurrentThread.ManagedThreadId && removed && admitted != null &&
            SourceFieldsUnchanged() && object.ReferenceEquals(world.GetTileEntity(pos), admitted) && SameBlock(world.GetBlock(pos), targetValue) &&
            admitted.PlantIncarnation == targetIncarnation && admitted.StateRevision == targetRevision && admitted.PlantOrigin == origin; }
        public bool Complete()
        {
            if (finished || !TargetCurrent()) { valid = false; return false; }
            admitted.SetModified();
            if (!TargetCurrent()) { valid = false; return false; }
            AdvancedFarmingHoverTextService.InvalidatePlantState(pos);
            if (!TargetCurrent()) { valid = false; return false; }
            finished = true;
            return true;
        }
        private bool CleanupCurrent(ulong expectedIncarnation, ulong expectedRevision)
        {
            return object.ReferenceEquals(world.GetTileEntity(pos), admitted) &&
                admitted.PlantIncarnation == expectedIncarnation && admitted.StateRevision == expectedRevision &&
                admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Unknown && SameBlock(world.GetBlock(pos), targetValue);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Crop transition disposed on a different thread.");
            try
            {
                if (!finished && admitted != null && object.ReferenceEquals(world.GetTileEntity(pos), admitted) &&
                    admitted.PlantIncarnation == targetIncarnation && admitted.StateRevision == targetRevision &&
                    (admitted.PlantOrigin == origin || (originResetObserved && admitted.PlantOrigin == AdvancedFarmingPlantOrigin.Unknown)) &&
                    SameBlock(world.GetBlock(pos), targetValue))
                {
                    admitted.PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
                    admitted.AdvanceStateRevision();
                    ulong cleanupIncarnation = admitted.PlantIncarnation;
                    ulong cleanupRevision = admitted.StateRevision;
                    if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                    {
                        bool cancelled = TryInvalidateExactScheduledTick(world, pos, targetValue.type);
                        if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                        {
                            try { admitted.SetModified(); }
                            finally
                            {
                                if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                                {
                                    AdvancedFarmingHoverTextService.InvalidatePlantState(pos);
                                    if (CleanupCurrent(cleanupIncarnation, cleanupRevision))
                                    {
                                        BlockPlantGrowing nativePlant = targetValue.Block as BlockPlantGrowing;
                                        if (cancelled && nativePlant != null && !nativePlant.nextPlant.isair)
                                            nativePlant.addScheduledTick(world, pos);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally { disposed = true; valid = false; active.Remove(this); }
        }
    }
}








