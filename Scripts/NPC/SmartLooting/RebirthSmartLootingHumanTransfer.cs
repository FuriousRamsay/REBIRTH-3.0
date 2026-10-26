using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

// B11's staged human backend. No UI/Take All caller is registered until native
// destination custody and restart reconciliation have been qualified together.
internal static class RebirthSmartLootingHumanTransfer
{
    internal enum RouteResult : byte { NotEligible, Pending, Completed }
    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;
    private static bool recoveryObservedEmpty;

    internal static void Tick()
    {
        World world = GameManager.Instance?.World;
        if (!ReferenceEquals(world, recoveryWorld))
        {
            recoveryWorld = world; nextRecovery = 0f; recoveryCursor = 0;
            recoveryObservedEmpty = false;
        }
        if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
            Time.unscaledTime < nextRecovery)
            return;
        nextRecovery = Time.unscaledTime + 5f;
        // Open-receipt recovery keeps retrying (a source may load late); it is a no-op while gated.
        RebirthP11NpcLootOpen.RecoverTick(world);
        RebirthP11LootAward.Tick();
        // Corpse legs settle even if the gate is later closed: a saved row is never stranded.
        RebirthP11CorpseTransfer.RecoverTick(world);
        if (recoveryObservedEmpty) return;
        if (!RebirthSmartLootingHumanOriginal.TryReadPending(world, out var pending)) return;
        // A successful empty observation after persistence recovery is the
        // world-load audit. New preparations wake this worker explicitly.
        if (pending.Length == 0) { recoveryObservedEmpty = true; return; }
        int count = Math.Min(4, pending.Length);
        for (int i = 0; i < count; i++)
        {
            var row = pending[(recoveryCursor + i) % pending.Length];
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(row.Npc, out int entity) ||
                world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc || npc.IsDead()) continue;
            var owner = world.Players?.list?.FirstOrDefault(player => player != null &&
                !player.IsDead() && RebirthOriginalActionScope.Matches(player, row.Owner, row.Creation));
            if (owner != null) TryAdvance(owner, npc, row.Operation);
        }
        recoveryCursor = (recoveryCursor + count) % pending.Length;
    }
    private static bool Current(EntityPlayer owner, EntityRebirthHumanoidNPC npc,
        RebirthSmartLootingHumanOriginal row)
    {
        World world = GameManager.Instance?.World;
        return ThreadManager.IsMainThread() && world?.IsRemote() == false &&
            owner != null && npc?.RebirthRuntimeState != null && row != null && row.Valid() &&
            !owner.IsDead() && !npc.IsDead() && !npc.IsPreparedRestorationPending &&
            owner.Died == row.Deaths && ReferenceEquals(owner.world, world) &&
            ReferenceEquals(npc.world, world) &&
            ReferenceEquals(world.GetEntity(owner.entityId), owner) &&
            ReferenceEquals(world.GetEntity(npc.entityId), npc) &&
            npc.RebirthRuntimeState.StableId == row.Npc &&
            npc.RebirthRuntimeState.OwnershipKind == RebirthNpcOwnershipKind.Player &&
            RebirthOriginalActionScope.Matches(owner, row.Owner, row.Creation) &&
            RebirthLogisticsTransferJournal.WorldKey(world) == row.World &&
            RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) &&
            string.Equals(identity.CanonicalId, npc.RebirthRuntimeState.OwnerId,
                StringComparison.OrdinalIgnoreCase) &&
            (npc.position - owner.position).sqrMagnitude <=
                RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance;
    }

    internal static bool TryPrepareNpcActivity(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, out Guid operation) =>
        TryPrepareNpcActivityOrdered(owner, source, sourceSlot, npc, destinationSlot, out operation);

    // Defense in depth: the activity pre-skips a dog-won cell; a direct caller is refused here too
    // (stack stays in the source, no row written). Inert while UnifiedOrderingQualified is false.
    private static bool TryPrepareNpcActivityOrdered(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, out Guid operation)
    {
        operation = Guid.Empty;
        if (RebirthP11UnifiedSelector.ChooseForNpcLeg(owner, source?.ItemGrid?.items, sourceSlot, npc,
                destinationSlot, out _) == RebirthP11UnifiedSelector.Winner.Dog) return false;
        return TryPrepare(owner, source, sourceSlot, npc, destinationSlot, out operation, true);
    }

    internal static bool TryPrepare(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, out Guid operation, bool npcActivity = false)
    {
        operation = Guid.Empty;
        try
        {
            if (owner == null || source == null || npc?.RebirthRuntimeState == null ||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthP11CorpseOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthNpcWorkNativeTransferOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId) ||
                !RebirthRemoteResourceSourceCustody.CanAdmitNewOriginal()) return false;
            XElement equipment = RebirthHumanEquipment.Read(npc);
            // An unsettled first-touch roll owns its container until recovery classifies the
            // grid; an NPC-led leg also stays out of a retained conflict (foreign content).
            if (RebirthP11NpcLootOpenReceipt.HasPendingAt(equipment, source.ToWorldPos(), npcActivity)) return false;
            if (RebirthDogSmartLootTransfer.HasPendingAt(source.ToWorldPos())) return false;
            if (equipment.Element("bookMove") != null || equipment.Element("swap") != null ||
                equipment.Elements("workMove").Any(work => (int?)work.Attribute("phase") != 4))
                return false;
            Guid requested = Guid.NewGuid();
            // The durable save may succeed even if its immediate witness read
            // fails. Wake recovery before attempting that publication.
            recoveryObservedEmpty = false;
            if (!RebirthSmartLootingHumanOriginal.TryPlan(owner, source, sourceSlot, npc,
                    destinationSlot, requested, out var row, npcActivity) || !Current(owner, npc, row) ||
                !row.SourceMatches(owner.world, owner, false) ||
                !row.DestinationMatches(npc, false) ||
                !RebirthSmartLootingHumanOriginal.TryPublish(npc, row)) return false;
            operation = requested;
            return true;
        }
        catch { return false; }
    }

    // One native source slot and one native human cell form a durable leg.
    // The client may mirror a partial remainder only after terminal source
    // and destination witnesses. Pending never means vanilla fallback.
    internal static RouteResult TryRouteOne(EntityPlayer owner, TEFeatureStorage source,
        int sourceSlot, out string remainderFrame)
    {
        remainderFrame = null;
        try
        {
            World world = GameManager.Instance?.World;
            if (world?.IsRemote() != false || source == null ||
                !RebirthSmartLootingHumanOriginal.TryReadPending(world, out var pending))
                return RouteResult.Pending;
            if (pending.Any(row => row.SourcePosition == source.ToWorldPos()) ||
                RebirthDogSmartLootTransfer.HasPendingAt(source.ToWorldPos()))
                return RouteResult.Pending;
            if (!RebirthSmartLootingHumanPolicy.TrySelect(owner, source, sourceSlot, false,
                    out var npc, out int slot)) return RouteResult.NotEligible;
            if (!TryPrepare(owner, source, sourceSlot, npc, slot, out Guid operation))
            {
                // Publication can succeed while its immediate saved witness
                // fails. A dog or vanilla fallback must not claim that source.
                if (!RebirthSmartLootingHumanOriginal.TryReadPending(world, out var retained))
                    return RouteResult.Pending;
                return retained.Any(row => row.SourcePosition == source.ToWorldPos())
                    ? RouteResult.Pending : RouteResult.NotEligible;
            }
            for (int step = 0; step < 4; step++)
            {
                if (!TryAdvance(owner, npc, operation)) return RouteResult.Pending;
                if (!RebirthSmartLootingHumanOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId,
                        operation, out var row)) return RouteResult.Pending;
                if (row.Phase == 4)
                {
                    if (row.Cancelled) return RouteResult.NotEligible;
                    remainderFrame = RebirthHumanEquipment.Frame(row.SourceAfter[sourceSlot]);
                    return RouteResult.Completed;
                }
            }
            return RouteResult.Pending;
        }
        catch { return RouteResult.Pending; }
    }


    private static bool SavedCredit(RebirthSmartLootingHumanOriginal row)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(row.Npc, out var person) ||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(row.Npc, out var inventory) ||
                inventory.NativeStacks == null) return false;
            XElement equipment = (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).SingleOrDefault(x => x.Name == "humanEquipment");
            XElement binding = equipment?.Element("bindings")?.Elements("binding")
                .SingleOrDefault(x => (string)x.Attribute("area") == "bag" &&
                    (int)x.Attribute("index") == row.DestinationSlot);
            if (!Guid.TryParse((string)binding?.Attribute("id"), out var stackId) ||
                stackId == Guid.Empty) return false;
            var expected = row.DestinationAfter[row.DestinationSlot];
            var records = inventory.NativeStacks.Write().Elements()
                .Select(x => RebirthNpcNativeStackRecord.TryRead(x, out var item) ? item : null)
                .Where(x => x != null && x.StackId == stackId).ToArray();
            return records.Length == 1 && records[0].Count == expected.count &&
                records[0].Payload == RebirthNativeItemCodec.Encode(expected.itemValue);
        }
        catch { return false; }
    }

    private static bool CreditBag(EntityRebirthHumanoidNPC npc,
        RebirthSmartLootingHumanOriginal row)
    {
        try
        {
            if (!RebirthSmartLootingHumanOriginal.TryReadSaved(row.Npc, row.Operation, out var saved) ||
                saved.Binding != row.Binding || saved.Phase != 2) return false;
            if (!row.DestinationMatches(npc, false) && !row.DestinationMatches(npc, true)) return false;
            if (row.DestinationMatches(npc, false))
                npc.bag.SetSlot(row.DestinationSlot, row.DestinationAfter[row.DestinationSlot].Clone());
            XElement equipment = RebirthHumanEquipment.Read(npc);
            RebirthHumanEquipment.Save(npc, equipment);
            return row.DestinationMatches(npc, true) && SavedCredit(row);
        }
        catch { return false; }
    }

    internal static bool TryAdvance(EntityPlayer owner, EntityRebirthHumanoidNPC npc, Guid operation)
    {
        try
        {
            if (npc?.RebirthRuntimeState == null ||
                !RebirthSmartLootingHumanOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId,
                    operation, out var row) || !Current(owner, npc, row)) return false;
            if (row.Phase == 4) return row.Cancelled || SavedCredit(row);
            bool OriginalCurrent() => Current(owner, npc, row) &&
                RebirthSmartLootingHumanOriginal.TryReadSaved(row.Npc, row.Operation, out var saved) &&
                saved.Binding == row.Binding;
            if (!RebirthRemoteResourceSourceCustody.TryReserveSmartLooting(owner, row,
                    OriginalCurrent, out var lease)) return false;
            if (row.Phase == 0)
            {
                if (!row.SourceMatches(owner.world, owner, false) ||
                    !row.DestinationMatches(npc, false) || !lease.TryObserveSaved(0)) return false;
                row.Phase = 1; row.Attempted = 1;
                return RebirthSmartLootingHumanOriginal.TryPublish(npc, row);
            }
            if (row.Phase == 1)
            {
                if (!lease.TryObserveSaved(1, out var proof))
                {
                    bool before = row.SourceMatches(owner.world, owner, false) &&
                        row.DestinationMatches(npc, false);
                    if (!before || !lease.TryObserveSaved(0)) return false;
                    if (!lease.ApplyStage(1) || !lease.TryObserveSaved(1, out proof))
                    {
                        // A cold saved attempt has no retained one-use source
                        // permit. Retire it only while the exact saved source and
                        // destination are still untouched and the source lease
                        // positively observes stage zero with no debit effect.
                        if (!row.SourceMatches(owner.world, owner, false) ||
                            !row.DestinationMatches(npc, false) ||
                            !lease.TryObserveSaved(0, out var beforeProof)) return false;
                        row.Phase = 4; row.Cancelled = true;
                        row.CancellationProof = beforeProof;
                        return RebirthSmartLootingHumanOriginal.TryPublish(npc, row);
                    }
                }
                row.SourceProof = proof; row.Phase = 2; row.Published = 1;
                return RebirthSmartLootingHumanOriginal.TryPublish(npc, row);
            }
            if (!lease.TryObserveSaved(1, out var savedProof) ||
                savedProof != row.SourceProof) return false;
            if (row.Phase == 2)
            {
                if (!CreditBag(npc, row)) return false;
                row.Phase = 3;
                return RebirthSmartLootingHumanOriginal.TryPublish(npc, row);
            }
            if (row.Phase != 3 || !SavedCredit(row)) return false;
            row.Phase = 4;
            if (!RebirthSmartLootingHumanOriginal.TryPublish(npc, row)) return false;
            lease.Dispose();
            if (row.NpcActivity && !row.Cancelled) RebirthP11LootAward.LegCommitted(npc, row);
            return true;
        }
        catch (Exception error)
        {
            Log.Warning("[REBIRTH Smart Looting] native human transfer retained: " + error.Message);
            return false;
        }
    }
}

[Preserve]
public sealed class RebirthSmartLootingRecoveryModApi : IModApi
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
        RebirthSmartLootingHumanTransfer.Tick();
        RebirthP11FollowLootDispatcher.Tick();
    }
}
