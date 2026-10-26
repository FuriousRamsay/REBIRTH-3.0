using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

// P11 wave 4 (dog side): crash-safe "native source slot -> dog inventory" Smart Looting leg.
//
// Custody (see RebirthDogSmartLootOriginal): the write-ahead row is saved on the dog's aggregate row
// BEFORE the source is touched; the source debit is applied and proven saved through the same source
// lease the human leg uses; the dog credit, its revision bump and the row's phase 3 are ONE aggregate
// publication; recovery settles only through TryAdvance of the same saved row (same operation id).
// No request id is ever resumed; a new Take All always plans a new row.
//
// Source lease: RebirthRemoteResourceDogSourceCustody binds the dog row to the shared source lease
// (Domain.DogSmartLooting in RebirthRemoteResourceSourceCustody / NativeOriginal). The only code path
// that debits the source is lease.ApplyStage(1), which needs the row SAVED at phase 1 / attempted 1,
// so no debit can happen without a saved lease. Should SourceCustody ever be null every entry point
// fails closed. JournaledLegQualified is a qualification gate and stays false.
internal static class RebirthDogSmartLootTransfer
{
#if DEBUG
    // DEBUG builds only: read-only view over RebirthP11GateOverride.DogLeg (default OFF, guarded `rbp11 gates dogleg`).
    internal static bool JournaledLegQualified => RebirthP11GateOverride.DogLegOn;
#else
    internal const bool JournaledLegQualified = false;
#endif

    internal enum RouteResult : byte { NotEligible, Pending, Completed }

    // The subset of RebirthRemoteResourceSourceCustody.Lease a dog leg needs (stage 0 = untouched,
    // stage 1 = whole-stack debit applied).
    internal interface ISourceLease
    {
        bool TryObserveSaved(int stage, out string proof);
        bool ApplyStage(int stage);
        void Release();
    }

    internal interface ISourceCustody
    {
        bool TryReserve(EntityPlayer actor, RebirthDogSmartLootOriginal row, Func<bool> current, out ISourceLease lease);
    }

    // Null => fail closed.
    internal static ISourceCustody SourceCustody = new RebirthRemoteResourceDogSourceCustody();

    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;
    private static bool recoveryObservedEmpty;

    internal static bool Active => JournaledLegQualified && SourceCustody != null;

    // -- dispatcher-facing candidate API -------------------------------------------------------
    // The one dog target (and the inventory revision it was observed at) that would take the whole
    // incoming stack. Same decision RebirthP11UnifiedSelector.TryDog uses.
    internal static bool TryGetCandidate(EntityPlayer owner, ItemStack incoming,
        out RebirthNpcStableId dogId, out uint revision) =>
        RebirthDogInventoryPolicy.TrySelectAutoRouteTarget(owner, incoming, out dogId, out revision);

    internal static bool HasPendingAt(Vector3i position)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthDogSmartLootOriginal.TryReadPending(world, out var rows)) return true;
            return rows.Any(row => row.SourcePosition == position);
        }
        catch { return true; }
    }

    // -- prepare ---------------------------------------------------------------------------------
    // Plans and durably publishes phase 0. Never mutates the source or the dog inventory.
    // npcActivity: NPC-led leg; admitted by the absence of any native lock instead of the owner's lock.
    internal static bool TryPrepareDogLeg(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        RebirthNpcStableId dogId, uint expectedRevision, out Guid operation, bool npcActivity = false)
    {
        operation = Guid.Empty;
        try
        {
            World world = GameManager.Instance?.World;
            if (!Active || !ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                owner == null || owner.IsDead() || source?.ItemGrid?.items == null || dogId.IsEmpty ||
                expectedRevision == 0 || sourceSlot < 0 || sourceSlot >= source.ItemGrid.items.Length ||
                !ReferenceEquals(owner.world, world) || !ReferenceEquals(world.GetEntity(owner.entityId), owner) ||
                RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) ||
                !(npcActivity ? RebirthP11NpcLootAccess.NoNativeLock(source) : OwnsLootLock(source, owner.entityId)) ||
                (owner.position - source.ToWorldCenterPos()).sqrMagnitude >
                    (Constants.cCollectItemDistance + 30f) * (Constants.cCollectItemDistance + 30f) ||
                source.ItemGrid.SlotLocks == null || source.ItemGrid.SlotLocks.Length != source.ItemGrid.items.Length ||
                source.ItemGrid.SlotLocks[sourceSlot] ||
                !RebirthRemoteResourceSourceCustody.CanAdmitNewOriginal() ||
                !DogCurrent(owner, dogId) || RebirthDogSmartLootOriginal.HasPending(dogId) ||
                HasPendingAt(source.ToWorldPos()) || HumanPendingAt(world, source.ToWorldPos())) return false;

            ItemStack incoming = source.ItemGrid.items[sourceSlot];
            if (incoming == null || incoming.IsEmpty() || incoming.count <= 0 ||
                !RebirthDogInventoryPolicy.IsDogAutoLootItem(incoming.itemValue)) return false;
            // Re-select: the caller's id/revision must still be THE target for this exact stack.
            if (!RebirthDogInventoryPolicy.TrySelectAutoRouteTarget(owner, incoming, out var selected, out uint revision) ||
                selected != dogId || revision != expectedRevision) return false;
            var snapshot = RebirthDogInventoryService.GetSnapshot(dogId);
            if (snapshot.Revision != expectedRevision) return false;

            bool freshToken = !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string incarnation);
            if (freshToken) incarnation = Guid.NewGuid().ToString("N");

            Guid requested = Guid.NewGuid();
            recoveryObservedEmpty = false; // wake recovery before the durable publication
            var inputs = new RebirthDogSmartLootInputs {
                Operation = requested, Dog = dogId, Owner = identity.StorageKey,
                Creation = RebirthCamperCharacterAccess.CurrentCreation(owner),
                World = RebirthLogisticsTransferJournal.WorldKey(world), Deaths = owner.Died,
                SourcePosition = source.ToWorldPos(), SourceSlot = sourceSlot, SourceIncarnation = incarnation,
                DogRevision = expectedRevision, NpcActivity = npcActivity,
                SourceBefore = ItemStack.Clone((IList<ItemStack>)source.ItemGrid.items),
                SourceLocks = Enumerable.Range(0, source.ItemGrid.items.Length).Select(i => source.ItemGrid.SlotLocks[i]).ToArray(),
                DogBefore = ItemStack.Clone((IList<ItemStack>)snapshot.Slots),
                DogLocks = Enumerable.Range(0, snapshot.Slots.Length).Select(i => snapshot.Locks != null && snapshot.Locks[i]).ToArray()
            };
            if (!RebirthDogSmartLootOriginal.TryPlan(inputs, out var row) || !Current(owner, row)) return false;
            // Reserved destination cells (an Original action holds them) are never touched by a credit.
            if (!TryReadRecord(dogId, out var record) || record.Inventory?.SlotRecords?.Length != RebirthDogInventoryService.Capacity ||
                Enumerable.Range(0, row.DogBefore.Length).Any(i =>
                    !RebirthStationGridIngredients.IsSameStackSnapshot(row.DogBefore[i], row.DogAfter[i]) &&
                    record.Inventory.SlotRecords[i].Reserved)) return false;
            if (freshToken && !RebirthP11StorageIncarnation.TryMint(world, source, incarnation)) return false;
            if (!row.SourceMatches(world, false) || !row.DogMatches(false) || !TryPublish(row)) return false;
            // Evidence only; Blocks() covers the source from now on. A failure here is harmless because the
            // first TryAdvance reserves the lease through Bootstrap/Reserve anyway.
            RebirthRemoteResourceSourceCustody.TryAdmitDogSmartLooting(row);
            operation = requested;
            return true;
        }
        catch { return false; }
    }

    private static bool HumanPendingAt(World world, Vector3i position) =>
        !RebirthSmartLootingHumanOriginal.TryReadPending(world, out var human) ||
        human.Any(row => row.SourcePosition == position);

    // -- Take All entry (player lock) ------------------------------------------------------------
    internal static RouteResult TryRouteOne(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        out string remainderFrame)
    {
        remainderFrame = null; // a dog takes the whole stack or nothing
        try
        {
            World world = GameManager.Instance?.World;
            if (!Active || world?.IsRemote() != false || source?.ItemGrid?.items == null ||
                sourceSlot < 0 || sourceSlot >= source.ItemGrid.items.Length) return RouteResult.NotEligible;
            if (HasPendingAt(source.ToWorldPos()) || HumanPendingAt(world, source.ToWorldPos()))
                return RouteResult.Pending;
            ItemStack incoming = source.ItemGrid.items[sourceSlot];
            if (incoming == null || incoming.IsEmpty() ||
                !TryGetCandidate(owner, incoming, out var dogId, out uint revision)) return RouteResult.NotEligible;
            if (!TryPrepareDogLeg(owner, source, sourceSlot, dogId, revision, out Guid operation))
                return HasPendingAt(source.ToWorldPos()) ? RouteResult.Pending : RouteResult.NotEligible;
            for (int step = 0; step < 5; step++)
            {
                if (!TryAdvance(owner, operation)) return RouteResult.Pending;
                if (!RebirthDogSmartLootOriginal.TryReadSaved(dogId, operation, out var row)) return RouteResult.Pending;
                if (row.Phase == 4) return row.Cancelled ? RouteResult.NotEligible : RouteResult.Completed;
            }
            return RouteResult.Pending;
        }
        catch { return RouteResult.Pending; }
    }

    // -- advance / recovery ----------------------------------------------------------------------
    // Operation-keyed recovery entry for NPC-led and restart paths: resolves the owner from the
    // saved row's own scope. Never takes a player loot lock.
    internal static bool TryAdvance(Guid operation)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthDogSmartLootOriginal.TryReadPending(world, out var rows)) return false;
            var row = rows.SingleOrDefault(x => x.Operation == operation);
            if (row == null) return false;
            var owner = ResolveOwner(world, row);
            return owner != null && TryAdvance(owner, operation);
        }
        catch { return false; }
    }

    private static EntityPlayer ResolveOwner(World world, RebirthDogSmartLootOriginal row) =>
        world?.Players?.list?.FirstOrDefault(player => player != null && !player.IsDead() &&
            RebirthOriginalActionScope.Matches(player, row.Owner, row.Creation));

    internal static void RecoverTick(World world)
    {
        if (!ReferenceEquals(world, recoveryWorld))
        { recoveryWorld = world; nextRecovery = 0f; recoveryCursor = 0; recoveryObservedEmpty = false; }
        if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling || Time.unscaledTime < nextRecovery ||
            recoveryObservedEmpty || SourceCustody == null) return;
        nextRecovery = Time.unscaledTime + 5f;
        if (!RebirthDogSmartLootOriginal.TryReadPending(world, out var pending)) return;
        if (pending.Length == 0) { recoveryObservedEmpty = true; return; }
        int count = Math.Min(4, pending.Length);
        for (int i = 0; i < count; i++)
        {
            var row = pending[(recoveryCursor + i) % pending.Length];
            var owner = ResolveOwner(world, row);
            if (owner != null) TryAdvance(owner, row.Operation);
        }
        recoveryCursor = (recoveryCursor + count) % pending.Length;
    }

    private static bool TryReadRecord(RebirthNpcStableId dog, out RebirthNpcPersistentRecord record) =>
        RebirthNpcAggregatePersistenceStore.TryGet(dog, out record) && RebirthDogStateService.IsDog(record);

    // Dog is an Active dog of this owner. The runtime entity is NOT required: the credit is an
    // aggregate publication, so recovery works while the dog's chunk is unloaded.
    // allowAwaitingRespawn: recovery of an already-attempted row may settle through a dead dog's retained
    // record (AwaitingRespawn): the credit is an aggregate write and needs no live entity.
    private static bool DogCurrent(EntityPlayer owner, RebirthNpcStableId dog, bool allowAwaitingRespawn = false)
    {
        if (!RebirthDogStateService.TryGetView(dog, out var view, out var dogView) || dogView == null ||
            view.Ownership == null ||
            (dogView.Lifecycle != RebirthDogLifecycleKind.Active &&
                !(allowAwaitingRespawn && dogView.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)) ||
            !RebirthDogLifecycleService.TryResolveOwnerId(owner, out string ownerId) ||
            !string.Equals(view.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase) ||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(dog) ||
            RemoteResourceTransactions.ActionBlocksNpc(dog)) return false;
        return !RebirthNpcRuntimeRegistry.TryGetEntityId(dog, out int entityId) ||
            !RebirthDogStorageService.IsOpenServer(entityId);
    }

    private static bool Current(EntityPlayer owner, RebirthDogSmartLootOriginal row)
    {
        World world = GameManager.Instance?.World;
        return ThreadManager.IsMainThread() && world?.IsRemote() == false && owner != null && row != null &&
            row.Valid() && !owner.IsDead() && owner.Died >= row.Deaths && ReferenceEquals(owner.world, world) &&
            ReferenceEquals(world.GetEntity(owner.entityId), owner) &&
            RebirthOriginalActionScope.Matches(owner, row.Owner, row.Creation) &&
            RebirthLogisticsTransferJournal.WorldKey(world) == row.World &&
            RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) &&
            identity.StorageKey == row.Owner && DogCurrent(owner, row.Dog, row.Attempted == 1);
    }

    // Player-led legs need the owner's loot lock or no lock at all (a restart released it); an
    // NPC-led leg needs no lock. A FOREIGN lock holder keeps the leg waiting. After the debit is
    // proven saved no lock is consulted: the item is already out of the source.
    private static bool BeforeAdmission(World world, RebirthDogSmartLootOriginal row, EntityPlayer owner)
    {
        TEFeatureStorage source = world.GetTileEntity(row.SourcePosition)?.GetSelfOrFeature<TEFeatureStorage>();
        return source != null && (OwnsLootLock(source, owner.entityId) || RebirthP11NpcLootAccess.NoNativeLock(source));
    }

    internal static bool TryAdvance(EntityPlayer owner, Guid operation)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (SourceCustody == null || owner == null || !FindDog(operation, out var dogId) ||
                !RebirthDogSmartLootOriginal.TryReadSaved(dogId, operation, out var row) ||
                !Current(owner, row)) return false;
            if (row.Phase == 4) return row.Cancelled || SavedCredit(row);
            bool OriginalCurrent() => Current(owner, row) &&
                RebirthDogSmartLootOriginal.TryReadSaved(row.Dog, row.Operation, out var saved) && saved.Binding == row.Binding;
            if (!SourceCustody.TryReserve(owner, row, OriginalCurrent, out var lease) || lease == null) return false;

            if (row.Phase == 0)
            {
                // Attempted == 0 proves no debit was ever applied (ApplyStage needs the saved phase-1 row), so a
                // source or dog positively diverged from the before image retires the row instead of holding the
                // dog inventory forever. The proof binds the unattempted row, not a ledger observation.
                if (row.SourceDiverged(world) || row.DogDiverged())
                {
                    row.Phase = 4; row.Cancelled = true;
                    row.CancellationProof = RebirthRemoteResourceDurableJournal.Digest(row.Binding + "|cancelUnattempted");
                    if (!TryPublish(row)) return false;
                    lease.Release();
                    return true;
                }
                if (!BeforeAdmission(world, row, owner) || !row.SourceMatches(world, false) || !row.DogMatches(false) ||
                    !lease.TryObserveSaved(0, out _)) return false;
                row.Phase = 1; row.Attempted = 1;
                return TryPublish(row);
            }
            if (row.Phase == 1)
            {
                if (!lease.TryObserveSaved(1, out string proof))
                {
                    bool before = BeforeAdmission(world, row, owner) && row.SourceMatches(world, false) && row.DogMatches(false);
                    if (!before || !lease.TryObserveSaved(0, out _)) return false;
                    if (!lease.ApplyStage(1) || !lease.TryObserveSaved(1, out proof))
                    {
                        // Cold attempt with no retained permit: retire only while source and dog are still
                        // exactly untouched and the lease positively observes stage zero.
                        if (!row.SourceMatches(world, false) || !row.DogMatches(false) ||
                            !lease.TryObserveSaved(0, out string beforeProof)) return false;
                        row.Phase = 4; row.Cancelled = true; row.CancellationProof = beforeProof;
                        return TryPublish(row);
                    }
                }
                row.SourceProof = proof; row.Phase = 2; row.Published = 1;
                return TryPublish(row);
            }
            if (!lease.TryObserveSaved(1, out string savedProof) || savedProof != row.SourceProof) return false;
            if (row.Phase == 2)
                return RebirthDogInventoryService.CommitSmartLootCredit(row, OriginalCurrent, out _) && SavedCredit(row);
            if (row.Phase != 3 || !SavedCredit(row)) return false;
            row.Phase = 4;
            if (!TryPublish(row)) return false;
            lease.Release();
            return true;
        }
        catch (Exception error)
        {
            Log.Warning("[REBIRTH Smart Looting] native dog transfer retained: " + error.Message);
            return false;
        }
    }

    // The credit is durable when the SAVED dog row carries this operation at phase >= 3 (written in the
    // same aggregate publication as the inventory) and the saved inventory is at/after the after image.
    internal static bool SavedCredit(RebirthDogSmartLootOriginal row)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(row.Dog, out var saved) ||
                !RebirthDogSmartLootOriginal.TryFind(saved, out var savedRow) || savedRow.Operation != row.Operation ||
                savedRow.Binding != row.Binding || savedRow.Phase < 3 || saved.Inventory == null ||
                saved.Inventory.InventoryRevision < row.DogRevision + 1) return false;
            return saved.Inventory.InventoryRevision > row.DogRevision + 1 ||
                RebirthRemoteResourceDogTransfer.Matches(saved, row.DogAfter, row.DogLocks, row.DogRevision + 1);
        }
        catch { return false; }
    }

    // Allowed transitions: create (phase 0, no live row or a terminal one), 0->1, 1->2, 1->4 (cancelled),
    // 3->4. 2->3 exists only inside CommitSmartLootCredit (atomic with the inventory).
    internal static bool TryPublish(RebirthDogSmartLootOriginal next)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || GameManager.Instance?.World?.IsRemote() != false || next == null || !next.Valid())
                return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!TryReadRecord(next.Dog, out var observed)) return false;
                bool exists = HasElement(observed);
                if (!Transition(observed, next, exists)) return false;
                bool done = false;
                try
                {
                    done = RebirthNpcAggregatePersistenceStore.TryMutateExisting(next.Dog, observed.AggregateRevision, record =>
                    {
                        if (!RebirthDogStateService.IsDog(record) ||
                            !Transition(record, next, HasElement(record)))
                            return false;
                        RebirthDogSmartLootOriginal.Set(record, next);
                        return true;
                    });
                }
                catch { done = false; }
                if (!done) continue;
                RebirthNpcAggregatePersistenceStore.Save();
                return RebirthDogSmartLootOriginal.TryReadSaved(next.Dog, next.Operation, out var saved) &&
                    System.Xml.Linq.XNode.DeepEquals(saved.Write(), next.Write());
            }
            return false;
        }
        catch { return false; }
    }

    private static bool HasElement(RebirthNpcPersistentRecord record) =>
        (record.UnknownOptionalElements ?? new string[0]).Any(x => x != null &&
            x.TrimStart().StartsWith("<" + RebirthDogSmartLootOriginal.ElementName, StringComparison.Ordinal));

    // In-memory lookup of the dog carrying this operation's row (no disk read).
    private static bool FindDog(Guid operation, out RebirthNpcStableId dog)
    {
        dog = default;
        foreach (var record in RebirthNpcAggregatePersistenceStore.Snapshot())
            if (RebirthDogStateService.IsDog(record) && RebirthDogSmartLootOriginal.TryFind(record, out var row) &&
                row.Operation == operation) { dog = record.Identity.StableNpcId; return true; }
        return false;
    }

    private static bool Transition(RebirthNpcPersistentRecord record, RebirthDogSmartLootOriginal next, bool exists)
    {
        if (!exists)
            return next.Phase == 0 && next.Attempted == 0 && next.Published == 0;
        if (!RebirthDogSmartLootOriginal.TryFind(record, out var previous)) return false;
        if (next.Phase == 0)
            return previous.Phase == 4 && previous.Operation != next.Operation;
        return previous.Operation == next.Operation && previous.Binding == next.Binding &&
            (next.Phase == previous.Phase + 1 && next.Phase != 3 ||
             (previous.Phase == 0 || previous.Phase == 1) && next.Phase == 4 && next.Cancelled &&
                next.Attempted == previous.Attempted) &&
            next.Attempted >= previous.Attempted && next.Attempted <= previous.Attempted + 1 &&
            next.Published >= previous.Published && next.Published <= previous.Published + 1;
    }

    private static bool OwnsLootLock(ILockTarget source, int playerId)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (source == null || connection == null || !connection.IsServer) return false;
        var entry = new LockManager.LockEntry(source, 0);
        if (source.IsSharedLock(0))
        {
            if (!LockManager.Instance.sharedLocks.TryGetByValue(entry, out IReadOnlyCollection<int> owners) || owners == null)
                return false;
            return owners.Contains(playerId);
        }
        return LockManager.Instance.singleLocks.TryGetByValue(entry, out int holder) && holder == playerId;
    }
}

// While a non-terminal row exists the dog inventory is held: every generic inventory commit, lock
// toggle and storage open refuses (they would break the exact before image the credit compares).
internal static class RebirthDogSmartLootGuard
{
    private static readonly HashSet<string> RemovalLogged = new HashSet<string>(StringComparer.Ordinal);

    // Removal/replacement/retirement/ownership-change of a dog record is refused while its smart-loot
    // row is non-terminal (phase 0-3): the row carries the uncredited source debit and the only
    // capability to settle it. A MISSING record is not blocked (nothing left to protect). Fails closed.
    // The caller keeps the dog and the record intact and retries later.
    internal static bool BlocksRemoval(RebirthNpcStableId id, string site)
    {
        try
        {
            if (id.IsEmpty || !RebirthNpcAggregatePersistenceStore.TryGet(id, out _)) return false;
            if (!RebirthDogSmartLootOriginal.HasPending(id)) return false;
            bool first; lock (RemovalLogged) first = RemovalLogged.Add(site + "|" + id);
            if (first) Log.Warning("[REBIRTH Smart Looting] dog record change refused (" + site + ") stableId=" + id +
                ": an open smart-loot row holds an uncredited source debit; retry after it settles.");
            return true;
        }
        catch { return true; }
    }

    internal static bool Blocks(RebirthNpcStableId id)
    {
        try
        {
            if (id.IsEmpty || !RebirthNpcAggregatePersistenceStore.TryGetView(id, out var view) ||
                view.UnknownOptionalElements == null) return false;
            bool any = false;
            for (int i = 0; i < view.UnknownOptionalElements.Length; i++)
            {
                string text = view.UnknownOptionalElements[i];
                if (text != null && text.TrimStart().StartsWith("<" + RebirthDogSmartLootOriginal.ElementName, StringComparison.Ordinal))
                { any = true; break; }
            }
            return any && RebirthDogSmartLootOriginal.HasPending(id);
        }
        catch { return true; }
    }
}

public static partial class RebirthDogInventoryService
{
    // Atomic credit: verifies the saved row is this operation at phase 2, the dog inventory equals the
    // before image (slots, locks, revision), then writes the after image, revision+1, checksum and the
    // phase-3 row in one aggregate publication, and saves. Replays a phase >= 3 row without re-adding.
    internal static bool CommitSmartLootCredit(RebirthDogSmartLootOriginal row, Func<bool> originalCurrent, out bool replayed)
    {
        replayed = false;
        try
        {
            if (row == null || !ThreadManager.IsMainThread() || GameManager.Instance?.World?.IsRemote() != false ||
                originalCurrent?.Invoke() != true || RemoteResourceTransactions.ActionBlocksNpc(row.Dog)) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!RebirthNpcAggregatePersistenceStore.TryGet(row.Dog, out var observed) ||
                    !RebirthDogStateService.IsDog(observed) ||
                    !RebirthDogSmartLootOriginal.TryFind(observed, out var live) || live.Operation != row.Operation ||
                    live.Binding != row.Binding) return false;
                if (live.Phase >= 3)
                {
                    replayed = true;
                    RebirthNpcAggregatePersistenceStore.Save();
                    return originalCurrent();
                }
                if (live.Phase != 2 || live.Published != 1 || live.SourceProof != row.SourceProof) return false;
                bool done = RebirthNpcAggregatePersistenceStore.TryMutateExisting(row.Dog, observed.AggregateRevision, record =>
                {
                    if (!originalCurrent() || !RebirthDogStateService.IsDog(record) ||
                        !RebirthDogSmartLootOriginal.TryFind(record, out var stored) || stored.Phase != 2 ||
                        stored.Operation != row.Operation || stored.Binding != row.Binding ||
                        !RebirthRemoteResourceDogTransfer.Matches(record, row.DogBefore, row.DogLocks, row.DogRevision))
                        return false;
                    for (int i = 0; i < Capacity; i++)
                    {
                        if (RebirthStationGridIngredients.IsSameStackSnapshot(row.DogBefore[i], row.DogAfter[i])) continue;
                        var cell = record.Inventory.SlotRecords[i];
                        if (cell.Reserved) return false;
                        var item = row.DogAfter[i];
                        cell.ItemValueSerialization = item.IsEmpty() ? string.Empty : SerializeItemValue(item.itemValue);
                        cell.ItemCount = item.IsEmpty() ? 0 : item.count;
                        cell.MetadataOrCustomData = item.IsEmpty() ? string.Empty : item.itemValue.ItemClass?.Name ?? string.Empty;
                    }
                    record.Inventory.InventoryRevision = row.DogRevision + 1;
                    record.Inventory.ContentChecksum = ComputeChecksum(record.Inventory.SlotRecords);
                    var credited = stored;
                    credited.Phase = 3;
                    RebirthDogSmartLootOriginal.Set(record, credited);
                    return true;
                });
                if (!done) continue;
                Invalidate(row.Dog, "smart loot dog credit");
                SynchronizeActiveInventoryEffects(row.Dog);
                RebirthNpcAggregatePersistenceStore.Save();
                return originalCurrent();
            }
            return false;
        }
        catch { return false; }
    }
}

[Preserve]
public sealed class RebirthDogSmartLootRecoveryModApi : IModApi
{
    private static bool installed;
    public void InitMod(Mod mod)
    {
        if (installed) return;
        installed = true;
        ModEvents.GameUpdate.RegisterHandler(Update);
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        try { RebirthDogSmartLootTransfer.RecoverTick(GameManager.Instance?.World); }
        catch { }
    }
}
