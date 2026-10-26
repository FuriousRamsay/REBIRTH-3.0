using System;
using System.Linq;

// B10 Follow-conditional loot activity. NOT a base work assignment: the global Follow
// suspension of base work is untouched. P6's bounded Follow-activity dispatcher calls
// TryStep once per budgeted slot, after combat / keep-up priority, for a following NPC
// and a candidate container position it has already routed to (within StorageDistance).
// Each step performs at most one native leg: first-touch open, else one exact-stack
// transfer into the NPC bag through RebirthSmartLootingHumanOriginal (npcActivity=true),
// which carries the saved debit, native credit witness and restart recovery.
internal static class RebirthP11FollowLootActivity
{
    internal enum StepResult : byte
    {
        Gated,              // qualification gate closed
        NotFollowing,       // Follow predicate false (P6 RebirthP6WorkFollowState)
        Denied,             // access rule (locked / jammed / quest / native lock / lockpick)
        NothingToLoot,      // container empty or no eligible cell
        Opened,             // first-touch generation done; caller re-steps next slot
        LegPending,         // an original was prepared/advanced; caller re-steps to settle
        LegCompleted,       // one exact stack reached terminal in the NPC bag
        Lockpicking,        // lockpick attempt pending or a pick broke; re-step
        Unlocked,           // lock opened by the NPC; re-resolve the (replaced) container next step
        Searching,          // NPC is searching a first-touch container for its effective search time
        BagFull             // items remain but no NPC bag cell accepts them; the remainder stays in the source
    }

    // In-memory only: a restart simply restarts the search; nothing is mutated while searching.
    private static readonly System.Collections.Generic.Dictionary<string, float> searchStart =
        new System.Collections.Generic.Dictionary<string, float>();

    internal static StepResult TryStep(EntityPlayer owner, EntityRebirthHumanoidNPC npc,
        TEFeatureStorage source, out Guid operation)
    {
        operation = Guid.Empty;
        try
        {
            if (!RebirthP11SmartLootGate.FollowActivityQualified) return StepResult.Gated;
            if (npc == null || !RebirthP6WorkFollowState.IsFollowing(npc)) return StepResult.NotFollowing;
            // A pending leg is settled first: its own lease re-checks the native lock state, so a
            // container that became locked between legs retains the row instead of being skipped.
            if (RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId))
            {
                if (!RebirthSmartLootingHumanOriginal.TryReadPending(npc.world, out var rows)) return StepResult.LegPending;
                var row = rows.FirstOrDefault(x => x.Npc == npc.RebirthRuntimeState.StableId);
                if (row == null) return StepResult.LegPending;
                operation = row.Operation;
                return RebirthSmartLootingHumanTransfer.TryAdvance(owner, npc, row.Operation)
                    ? StepResult.LegCompleted : StepResult.LegPending;
            }

            var access = RebirthP11NpcLootAccess.Classify(owner, source);
            if (access == RebirthP11NpcLootAccessResult.NeedsLockpick)
            {
                switch (RebirthP11NpcLootAccess.TryLockpick(npc, source))
                {
                    case RebirthP11LockpickStep.Waiting:
                    case RebirthP11LockpickStep.Broke: return StepResult.Lockpicking;
                    case RebirthP11LockpickStep.Unlocked: return StepResult.Unlocked;
                    default: return StepResult.Denied;
                }
            }
            if (access != RebirthP11NpcLootAccessResult.Open) return StepResult.Denied;
            if (!source.ItemGrid.Touched && RebirthP11NpcLootOpen.TrySearchSeconds(npc, source, out float seconds))
            {
                string key = npc.RebirthRuntimeState.StableId + "|" + source.ToWorldPos();
                float now = UnityEngine.Time.time;
                if (!searchStart.TryGetValue(key, out float started))
                {
                    if (searchStart.Count >= 64) searchStart.Clear();
                    searchStart[key] = now;
                    started = now;
                }
                if (now - started < seconds) return StepResult.Searching;
                searchStart.Remove(key);
            }
            var open = RebirthP11NpcLootOpen.TryOpen(owner, npc, source, out operation);
            if (open == RebirthP11NpcLootOpen.OpenResult.Generated) return StepResult.Opened;
            if (open == RebirthP11NpcLootOpen.OpenResult.Blocked) return StepResult.Denied;

            // Overflow rule: a stack with no room stays in the source container (a partial leg already
            // journals the exact remainder there). There is deliberately no storage fallback.
            bool bagFull = false, dogDeferred = false;
            // Deterministic: first non-empty source cell with room in the NPC bag; matching
            // stacks before empty cells (same ordering as the player Take All path).
            for (int cell = 0; cell < source.ItemGrid.Length; cell++)
            {
                ItemStack stack = source.ItemGrid.items[cell];
                if (stack == null || stack.IsEmpty()) continue;
                int slot = FindSlot(npc, stack);
                if (slot < 0) { bagFull = true; continue; }
                // Unified ordering (gated): a dog that outranks this NPC's cell keeps the stack in the source.
                if (RebirthP11UnifiedSelector.ChooseForNpcLeg(owner, source.ItemGrid.items, cell, npc, slot, out _) ==
                    RebirthP11UnifiedSelector.Winner.Dog) { dogDeferred = true; continue; }
                if (!RebirthSmartLootingHumanTransfer.TryPrepareNpcActivity(owner, source, cell, npc, slot, out operation))
                    return StepResult.Denied;
                return StepResult.LegPending;
            }
            return bagFull && !dogDeferred ? StepResult.BagFull : StepResult.NothingToLoot;
        }
        catch { return StepResult.Denied; }
    }

    private static int FindSlot(EntityRebirthHumanoidNPC npc, ItemStack incoming)
    {
        if (npc.bag?.ItemGrid?.items == null || incoming?.itemValue?.ItemClass == null) return -1;
        int max = Math.Max(1, incoming.itemValue.ItemClass.Stacknumber.Value);
        for (int pass = 0; pass < 2; pass++)
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
}
