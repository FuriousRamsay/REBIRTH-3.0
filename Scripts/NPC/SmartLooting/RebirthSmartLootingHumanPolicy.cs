using System;

// B11's authoritative human destination ordering. The native Take All bridge
// may call this only after it has admitted the player's real loot lock. It
// deliberately returns one cell, so each durable original owns one exact
// source debit and one exact native bag credit.
internal static class RebirthSmartLootingHumanPolicy
{
    internal static bool TrySelect(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        bool requireComplete,
        out EntityRebirthHumanoidNPC destination, out int destinationSlot)
    {
        destination = null;
        destinationSlot = -1;
        try
        {
            World world = GameManager.Instance?.World;
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || owner == null ||
                source?.ItemGrid?.items == null || sourceSlot < 0 ||
                sourceSlot >= source.ItemGrid.items.Length || owner.IsDead() ||
                !ReferenceEquals(owner.world, world) ||
                !RebirthSandboxOptionManager.Current.SmartLooting ||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity)) return false;
            ItemStack incoming = source.ItemGrid.items[sourceSlot];
            if (incoming == null || incoming.IsEmpty() || incoming.count <= 0 ||
                incoming.itemValue?.ItemClass == null) return false;

            // The runtime registry is already ordered by stable ID. Matching
            // cells precede empty cells across every eligible companion.
            var states = RebirthNpcRuntimeRegistry.GetSnapshot();
            for (int pass = 0; pass < 2; pass++)
            foreach (var state in states)
            {
                if (state.Presence != RebirthNpcPresenceState.Active ||
                    state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                    !string.Equals(state.OwnerId, identity.CanonicalId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out int id) ||
                    world.GetEntity(id) is not EntityRebirthHumanoidNPC npc || npc.IsDead() ||
                    npc.IsPreparedRestorationPending ||
                    (npc.position - owner.position).sqrMagnitude >
                        RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance ||
                    npc.bag?.ItemGrid?.items == null ||
                    npc.bag.ItemGrid.items.Length != npc.bag.SlotCount ||
                    npc.bag.ItemGrid.SlotLocks == null ||
                    npc.bag.ItemGrid.SlotLocks.Length != npc.bag.SlotCount) continue;
                for (int slot = 0; slot < npc.bag.SlotCount; slot++)
                {
                    if (npc.bag.ItemGrid.SlotLocks[slot] ||
                        !RebirthHumanEquipment.Accept(10 + slot, incoming)) continue;
                    ItemStack held = npc.bag.ItemGrid[slot];
                    bool empty = held == null || held.IsEmpty();
                    if (pass == 0 ? empty || !held.CanStackWith(incoming) : !empty) continue;
                    int maximum = Math.Max(1, incoming.itemValue.ItemClass.Stacknumber.Value);
                    if (!empty && held.count >= maximum) continue;
                    if (requireComplete && maximum - (empty ? 0 : held.count) < incoming.count)
                        continue;
                    destination = npc;
                    destinationSlot = slot;
                    return true;
                }
            }
            return false;
        }
        catch { return false; }
    }
}
