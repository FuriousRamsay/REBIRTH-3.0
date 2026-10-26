using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Staged corpse leg: the entity counterpart of RebirthSmartLootingHumanTransfer.TryAdvance. Same phases,
// same single-row settle (D-25: a pending leg only ever advances under its own saved operation id), but
// the source debit is applied directly to the corpse grid and proven by a saved entity-span witness
// (RebirthP11CorpseWitness) instead of a static-container lease. Inert while CorpseSourceQualified is
// false: TryPlan refuses, so no row is ever written; recovery below still settles any row that exists.
internal static class RebirthP11CorpseTransfer
{
    private static World recoveryWorld;
    private static int recoveryCursor;
    private static bool recoveryObservedEmpty;

    // Called from RebirthSmartLootingHumanTransfer.Tick (already main-thread, server, 5 s paced).
    internal static void RecoverTick(World world)
    {
        if (!ReferenceEquals(world, recoveryWorld)) { recoveryWorld = world; recoveryCursor = 0; recoveryObservedEmpty = false; }
        if (recoveryObservedEmpty || !RebirthP11CorpseOriginal.TryReadPending(world, out var pending)) return;
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

    private static bool Current(EntityPlayer owner, EntityRebirthHumanoidNPC npc, RebirthP11CorpseOriginal row)
    {
        World world = GameManager.Instance?.World;
        return ThreadManager.IsMainThread() && world?.IsRemote() == false &&
            owner != null && npc?.RebirthRuntimeState != null && row != null && row.Valid() &&
            !owner.IsDead() && !npc.IsDead() && !npc.IsPreparedRestorationPending &&
            owner.Died == row.Deaths && ReferenceEquals(owner.world, world) && ReferenceEquals(npc.world, world) &&
            ReferenceEquals(world.GetEntity(owner.entityId), owner) &&
            ReferenceEquals(world.GetEntity(npc.entityId), npc) &&
            npc.RebirthRuntimeState.StableId == row.Npc &&
            npc.RebirthRuntimeState.OwnershipKind == RebirthNpcOwnershipKind.Player &&
            RebirthOriginalActionScope.Matches(owner, row.Owner, row.Creation) &&
            RebirthLogisticsTransferJournal.WorldKey(world) == row.World &&
            RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) &&
            string.Equals(identity.CanonicalId, npc.RebirthRuntimeState.OwnerId, StringComparison.OrdinalIgnoreCase) &&
            (npc.position - owner.position).sqrMagnitude <=
                RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance;
    }

    internal static bool TryPrepare(EntityPlayer owner, EntityLootContainer source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, out Guid operation, bool npcActivity)
    {
        operation = Guid.Empty;
        try
        {
            if (!RebirthP11SmartLootGate.CorpseSourceQualified) return false;
            World world = GameManager.Instance?.World;
            if (owner == null || source == null || npc?.RebirthRuntimeState == null ||
                RebirthP11CorpseOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthNpcWorkNativeTransferOriginal.HasPending(npc.RebirthRuntimeState.StableId) ||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId) ||
                !RebirthRemoteResourceSourceCustody.CanAdmitNewOriginal()) return false;
            // One open leg per corpse across all NPCs.
            string key = RebirthP11CorpseSource.Key(source);
            if (!RebirthP11CorpseOriginal.TryReadPending(world, out var pending) ||
                pending.Any(x => x.SourceKey == key)) return false;
            XElement equipment = RebirthHumanEquipment.Read(npc);
            if (equipment.Element("bookMove") != null || equipment.Element("swap") != null ||
                equipment.Elements("workMove").Any(work => (int?)work.Attribute("phase") != 4)) return false;
            Guid requested = Guid.NewGuid();
            recoveryObservedEmpty = false;
            if (!RebirthP11CorpseOriginal.TryPlan(owner, source, sourceSlot, npc, destinationSlot, requested,
                    out var row, npcActivity) || !Current(owner, npc, row) ||
                !row.SourceMatches(owner.world, owner, false) || !row.DestinationMatches(npc, false) ||
                !RebirthP11CorpseOriginal.TryPublish(npc, row)) return false;
            operation = requested;
            return true;
        }
        catch { return false; }
    }

    internal static RebirthSmartLootingHumanTransfer.RouteResult TryRouteOne(EntityPlayer owner,
        EntityLootContainer source, int sourceSlot, out string remainderFrame)
    {
        remainderFrame = null;
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthP11SmartLootGate.CorpseSourceQualified || world?.IsRemote() != false || source == null ||
                !RebirthP11CorpseOriginal.TryReadPending(world, out var pending))
                return RebirthSmartLootingHumanTransfer.RouteResult.NotEligible;
            string key = RebirthP11CorpseSource.Key(source);
            if (pending.Any(row => row.SourceKey == key)) return RebirthSmartLootingHumanTransfer.RouteResult.Pending;
            // Unified ordering (gated): a same-owner dog that outranks every human cell wins. There is no
            // corpse dog leg, so NotEligible leaves the stack in the corpse for the vanilla player path;
            // never a human row and never a dog debit, so the stack is not double-routed.
            if (RebirthP11UnifiedSelector.Choose(owner, source, sourceSlot, out _) ==
                RebirthP11UnifiedSelector.Winner.Dog)
                return RebirthSmartLootingHumanTransfer.RouteResult.NotEligible;
            if (!TrySelectDestination(owner, source, sourceSlot, out var npc, out int slot))
                return RebirthSmartLootingHumanTransfer.RouteResult.NotEligible;
            if (!TryPrepare(owner, source, sourceSlot, npc, slot, out Guid operation, false))
            {
                if (!RebirthP11CorpseOriginal.TryReadPending(world, out var retained))
                    return RebirthSmartLootingHumanTransfer.RouteResult.Pending;
                return retained.Any(row => row.SourceKey == key)
                    ? RebirthSmartLootingHumanTransfer.RouteResult.Pending
                    : RebirthSmartLootingHumanTransfer.RouteResult.NotEligible;
            }
            // The witness needs the native snapshot to publish, which can span several ticks: Pending is
            // returned (never a vanilla fallback) and RecoverTick settles the row.
            for (int step = 0; step < 4; step++)
            {
                if (!TryAdvance(owner, npc, operation)) return RebirthSmartLootingHumanTransfer.RouteResult.Pending;
                if (!RebirthP11CorpseOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId, operation, out var row))
                    return RebirthSmartLootingHumanTransfer.RouteResult.Pending;
                if (row.Phase == 4)
                {
                    if (row.Cancelled) return RebirthSmartLootingHumanTransfer.RouteResult.NotEligible;
                    remainderFrame = RebirthHumanEquipment.Frame(row.SourceAfter[sourceSlot]);
                    return RebirthSmartLootingHumanTransfer.RouteResult.Completed;
                }
            }
            return RebirthSmartLootingHumanTransfer.RouteResult.Pending;
        }
        catch { return RebirthSmartLootingHumanTransfer.RouteResult.Pending; }
    }

    // Same ordering as RebirthSmartLootingHumanPolicy.TrySelect: matching partial stacks before empty
    // cells, across the owner's loaded same-owner humans in stable-id order.
    internal static bool TrySelectDestination(EntityPlayer owner, EntityLootContainer source, int sourceSlot,
        out EntityRebirthHumanoidNPC destination, out int destinationSlot)
    {
        destination = null; destinationSlot = -1;
        try
        {
            World world = GameManager.Instance?.World;
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || owner == null ||
                source?.bag?.ItemGrid?.items == null || sourceSlot < 0 || sourceSlot >= source.bag.ItemGrid.items.Length ||
                owner.IsDead() || !ReferenceEquals(owner.world, world) ||
                !RebirthSandboxOptionManager.Current.SmartLooting ||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity)) return false;
            ItemStack incoming = source.bag.ItemGrid.items[sourceSlot];
            if (incoming == null || incoming.IsEmpty() || incoming.count <= 0 || incoming.itemValue?.ItemClass == null)
                return false;
            var states = RebirthNpcRuntimeRegistry.GetSnapshot();
            for (int pass = 0; pass < 2; pass++)
            foreach (var state in states)
            {
                if (state.Presence != RebirthNpcPresenceState.Active ||
                    state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                    !string.Equals(state.OwnerId, identity.CanonicalId, StringComparison.OrdinalIgnoreCase) ||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out int id) ||
                    world.GetEntity(id) is not EntityRebirthHumanoidNPC npc || npc.IsDead() ||
                    npc.IsPreparedRestorationPending ||
                    (npc.position - owner.position).sqrMagnitude >
                        RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance ||
                    npc.bag?.ItemGrid?.items == null || npc.bag.ItemGrid.items.Length != npc.bag.SlotCount ||
                    npc.bag.ItemGrid.SlotLocks == null || npc.bag.ItemGrid.SlotLocks.Length != npc.bag.SlotCount) continue;
                int slot = FindSlot(npc, incoming, pass);
                if (slot < 0) continue;
                destination = npc; destinationSlot = slot;
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    // pass 0: a cell that already holds a stackable stack with room; pass 1: an empty cell.
    internal static int FindSlot(EntityRebirthHumanoidNPC npc, ItemStack incoming, int pass)
    {
        if (npc.bag?.ItemGrid?.items == null || incoming?.itemValue?.ItemClass == null) return -1;
        int max = Math.Max(1, incoming.itemValue.ItemClass.Stacknumber.Value);
        for (int slot = 0; slot < npc.bag.SlotCount; slot++)
        {
            if (npc.bag.ItemGrid.SlotLocks[slot] || !RebirthHumanEquipment.Accept(10 + slot, incoming)) continue;
            ItemStack held = npc.bag.ItemGrid[slot];
            bool empty = held == null || held.IsEmpty();
            if (pass == 0 ? empty || !held.CanStackWith(incoming) : !empty) continue;
            if (!empty && held.count >= max) continue;
            return slot;
        }
        return -1;
    }

    private static bool SavedCredit(RebirthP11CorpseOriginal row)
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
            if (!Guid.TryParse((string)binding?.Attribute("id"), out var stackId) || stackId == Guid.Empty) return false;
            var expected = row.DestinationAfter[row.DestinationSlot];
            var records = inventory.NativeStacks.Write().Elements()
                .Select(x => RebirthNpcNativeStackRecord.TryRead(x, out var item) ? item : null)
                .Where(x => x != null && x.StackId == stackId).ToArray();
            return records.Length == 1 && records[0].Count == expected.count &&
                records[0].Payload == RebirthNativeItemCodec.Encode(expected.itemValue);
        }
        catch { return false; }
    }

    private static bool CreditBag(EntityRebirthHumanoidNPC npc, RebirthP11CorpseOriginal row)
    {
        try
        {
            if (!RebirthP11CorpseOriginal.TryReadSaved(row.Npc, row.Operation, out var saved) ||
                saved.Binding != row.Binding || saved.Phase != 2) return false;
            if (!row.DestinationMatches(npc, false) && !row.DestinationMatches(npc, true)) return false;
            if (row.DestinationMatches(npc, false))
                npc.bag.SetSlot(row.DestinationSlot, row.DestinationAfter[row.DestinationSlot].Clone());
            RebirthHumanEquipment.Save(npc, RebirthHumanEquipment.Read(npc));
            return row.DestinationMatches(npc, true) && SavedCredit(row);
        }
        catch { return false; }
    }

    internal static bool TryAdvance(EntityPlayer owner, EntityRebirthHumanoidNPC npc, Guid operation)
    {
        try
        {
            if (npc?.RebirthRuntimeState == null ||
                !RebirthP11CorpseOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId, operation, out var row) ||
                !Current(owner, npc, row)) return false;
            World world = owner.world;
            if (row.Phase == 4) return row.Cancelled || SavedCredit(row);
            if (row.Phase == 0)
            {
                if (!row.SourceMatches(world, owner, false) || !row.DestinationMatches(npc, false)) return false;
                row.Phase = 1; row.Attempted = 1;
                return RebirthP11CorpseOriginal.TryPublish(npc, row);
            }
            if (row.Phase == 1)
            {
                // The corpse must be present to prove anything. An absent corpse leaves the row pending:
                // whether the debit persisted before it went away cannot be told, so neither cancel nor
                // credit is taken.
                if (!RebirthP11CorpseSource.TryResolve(world, row.SourceKey, out var corpse)) return false;
                if (row.SourceMatches(world, owner, false))
                {
                    if (!row.DestinationMatches(npc, false))
                    {
                        // Debit never applied and the NPC cell changed: retire with a cancel proof.
                        RebirthP11CorpseWitness.Forget(row.Operation);
                        row.Phase = 4; row.Cancelled = true;
                        row.CancellationProof = RebirthRemoteResourceDurableJournal.Digest(
                            "corpse-cancel|" + row.Operation.ToString("N") + "|" + row.Binding);
                        return RebirthP11CorpseOriginal.TryPublish(npc, row);
                    }
                    // Same row, same operation id: apply the exact debit, then wait for the saved witness.
                    corpse.bag.SetSlot(row.SourceSlot, row.SourceAfter[row.SourceSlot].Clone());
                    var chunk = RebirthP11CorpseSource.ChunkOf(world, corpse);
                    if (chunk != null) chunk.isModified = true;
                }
                if (!row.SourceMatches(world, owner, true) || !row.DestinationMatches(npc, false)) return false;
                if (!RebirthP11CorpseWitness.Observe(world, corpse, row.Operation, row.SourceAfter, out string proof))
                    return false;
                row.SourceProof = proof; row.Phase = 2; row.Published = 1;
                return RebirthP11CorpseOriginal.TryPublish(npc, row);
            }
            if (row.Phase == 2)
            {
                if (!CreditBag(npc, row)) return false;
                row.Phase = 3;
                return RebirthP11CorpseOriginal.TryPublish(npc, row);
            }
            if (row.Phase != 3 || !SavedCredit(row)) return false;
            row.Phase = 4;
            if (!RebirthP11CorpseOriginal.TryPublish(npc, row)) return false;
            if (row.NpcActivity && !row.Cancelled) Committed(npc, row);
            return true;
        }
        catch (Exception error)
        {
            Log.Warning("[REBIRTH Smart Looting] corpse transfer retained: " + error.Message);
            return false;
        }
    }

    // Same legacy Looting track call as RebirthP11LootAward.LegCommitted; a restart between the phase-4
    // publish and this call can miss one point, never duplicate it.
    private static void Committed(EntityRebirthHumanoidNPC npc, RebirthP11CorpseOriginal row)
    {
        try
        {
            string container = row.SourceBefore[row.SourceSlot]?.itemValue?.ItemClass?.GetItemName() ?? "container";
            RebirthNpcProfessionOutcomePublisher.Looting(row.Operation, npc.RebirthRuntimeState.StableId, container, row.Quantity, 1);
        }
        catch { }
    }
}
