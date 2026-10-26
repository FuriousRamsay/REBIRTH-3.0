using System;
using System.Collections.Generic;
using UnityEngine;

// Follow-activity step for corpse / entity loot containers, called by RebirthP11FollowLootDispatcher
// (server, main thread, already paced, Follow / combat / keep-up priority already applied). Like the tile
// activity it never moves the NPC: only a corpse already within ReachMeters is considered, one exact
// stack leg per step, and a stack the NPC bag cannot hold stays in the corpse (no storage fallback).
// Both entry points return false immediately while CorpseSourceQualified is false.
internal static class RebirthP11CorpseActivity
{
    private const float ReachMeters = 3f;
    private const float EmptyCooldown = 30f;
    private const float FullCooldown = 60f;
    private const float StepDelay = 0.25f;

    private static readonly Dictionary<string, float> cooldown = new Dictionary<string, float>();
    private static readonly List<Entity> scratch = new List<Entity>();

    // Settles this NPC's pending corpse leg first (the leg owns the NPC until it is terminal).
    internal static bool Settle(EntityPlayer owner, EntityRebirthHumanoidNPC npc, out float delay)
    {
        delay = StepDelay;
        try
        {
            if (!RebirthP11SmartLootGate.CorpseSourceQualified || npc?.RebirthRuntimeState == null ||
                !RebirthP11CorpseOriginal.HasPending(npc.RebirthRuntimeState.StableId)) return false;
            if (RebirthP11CorpseOriginal.TryReadPending(npc.world, out var rows))
            {
                var row = Array.Find(rows, x => x.Npc == npc.RebirthRuntimeState.StableId);
                if (row != null) RebirthP11CorpseTransfer.TryAdvance(owner, npc, row.Operation);
            }
            return true;
        }
        catch { return true; }
    }

    internal static bool TryDispatch(World world, EntityPlayer owner, EntityRebirthHumanoidNPC npc, out float delay)
    {
        delay = StepDelay;
        try
        {
            if (!RebirthP11SmartLootGate.CorpseSourceQualified || npc?.RebirthRuntimeState == null ||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId)) return false;
            string npcKey = npc.RebirthRuntimeState.StableId.ToString();
            float now = Time.unscaledTime;
            var box = new Bounds(npc.position, new Vector3(ReachMeters * 2f, ReachMeters * 2f, ReachMeters * 2f));
            scratch.Clear();
            world.GetEntitiesInBounds(typeof(EntityLootContainer), box, scratch);
            EntityLootContainer best = null;
            float bestDistance = ReachMeters * ReachMeters;
            for (int i = 0; i < scratch.Count; i++)
            {
                if (scratch[i] is not EntityLootContainer corpse) continue;
                string key = RebirthP11CorpseSource.Key(corpse);
                if (cooldown.TryGetValue(npcKey + "|" + key, out float until))
                {
                    if (now < until) continue;
                    cooldown.Remove(npcKey + "|" + key);
                }
                if (!RebirthP11CorpseSource.TryResolve(world, key, out var live) || !ReferenceEquals(live, corpse)) continue;
                float d = (npc.position - corpse.position).sqrMagnitude;
                if (d > bestDistance) continue;
                bestDistance = d; best = corpse;
            }
            scratch.Clear();
            if (best == null) return false;

            string corpseKey = npcKey + "|" + RebirthP11CorpseSource.Key(best);
            if (!RebirthP11CorpseSource.NoNativeLock(best)) { Park(corpseKey, now + EmptyCooldown); return true; }
            bool bagFull = false, dogDeferred = false;
            var items = best.bag.ItemGrid.items;
            for (int cell = 0; cell < items.Length; cell++)
            {
                ItemStack stack = items[cell];
                if (stack == null || stack.IsEmpty()) continue;
                // Matching stacks first, then empty cells (same order as the tile activity / Take All).
                int slot = RebirthP11CorpseTransfer.FindSlot(npc, stack, 0);
                if (slot < 0) slot = RebirthP11CorpseTransfer.FindSlot(npc, stack, 1);
                if (slot < 0) { bagFull = true; continue; }
                // Unified ordering (gated): a dog that outranks this NPC's cell keeps the stack in the corpse.
                if (RebirthP11UnifiedSelector.ChooseForNpcLeg(owner, items, cell, npc, slot, out _) ==
                    RebirthP11UnifiedSelector.Winner.Dog) { dogDeferred = true; continue; }
                if (!RebirthP11CorpseTransfer.TryPrepare(owner, best, cell, npc, slot, out Guid operation, true))
                { Park(corpseKey, now + EmptyCooldown); return true; }
                RebirthP11CorpseTransfer.TryAdvance(owner, npc, operation);
                return true;
            }
            Park(corpseKey, now + (bagFull && !dogDeferred ? FullCooldown : EmptyCooldown));
            return true;
        }
        catch { scratch.Clear(); return false; }
    }

    private static void Park(string key, float until)
    {
        if (cooldown.Count >= 64) cooldown.Clear();
        cooldown[key] = until;
    }
}
