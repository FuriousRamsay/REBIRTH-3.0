using System;

// B09/B11 unified ordering: the one deterministic decision between a same-owner
// human companion and a same-owner dog for a single source slot. It only reads;
// the human leg (RebirthSmartLootingHumanTransfer.TryRouteOne) and the dog leg
// (RebirthDogInventoryService.TryAddAtRevision) keep their own custody. Callers
// act on exactly one winner, so a stack can never be offered to both kinds.
// Inert unless RebirthP11SmartLootGate.UnifiedOrderingQualified is enabled.
internal static class RebirthP11UnifiedSelector
{
    internal enum Winner : byte { None, Human, Dog }

    // Player Take All on a tile source. Server main thread only. 'Winner.Dog' means: skip the human route.
    // Named-source filtering lives HERE, not in the caller: an unnamed source (no durable incarnation)
    // never offers a human candidate, so it resolves only to Dog or None and can never bypass the
    // custody rules of the saved human original; a dog-eligible item from such a source is still routed.
    internal static Winner Choose(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        out RebirthP11LootCandidate chosen)
    {
        chosen = default;
        try
        {
            if (!Admissible(owner, source?.ItemGrid?.items, sourceSlot, out ItemStack incoming)) return Winner.None;
            bool named = RebirthP11StorageIncarnation.TryResolve(GameManager.Instance?.World, source, false, out _);
            RebirthP11LootCandidate human = default;
            bool haveHuman = named && TryHuman(owner, source, sourceSlot, incoming, out human);
            bool haveDog = TryDog(owner, incoming, out var dog);
            return Decide(haveHuman, human, haveDog, dog, out chosen);
        }
        catch { return Winner.None; }
    }

    // Player Take All on a corpse (entity) source; the corpse human leg is gated by CorpseSourceQualified.
    internal static Winner Choose(EntityPlayer owner, EntityLootContainer corpse, int sourceSlot,
        out RebirthP11LootCandidate chosen)
    {
        chosen = default;
        try
        {
            if (!RebirthP11SmartLootGate.CorpseSourceQualified ||
                !Admissible(owner, corpse?.bag?.ItemGrid?.items, sourceSlot, out ItemStack incoming)) return Winner.None;
            RebirthP11LootCandidate human = default;
            bool haveHuman = RebirthP11CorpseTransfer.TrySelectDestination(owner, corpse, sourceSlot,
                out var npc, out int slot) && Candidate(npc, slot, incoming, out human);
            bool haveDog = TryDog(owner, incoming, out var dog);
            return Decide(haveHuman, human, haveDog, dog, out chosen);
        }
        catch { return Winner.None; }
    }

    // NPC-led leg (Follow dispatcher / autoloot): the human candidate is the stepping NPC and the
    // cell it would credit, the only human an NPC-led leg can reach. Dog wins only when it outranks
    // that exact cell; the caller then LEAVES the stack in the source (no dog-side custody exists
    // for an NPC-led source debit), so a stack is never moved twice and never lost.
    internal static Winner ChooseForNpcLeg(EntityPlayer owner, ItemStack[] sourceItems, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int npcSlot, out RebirthP11LootCandidate chosen)
    {
        chosen = default;
        try
        {
            if (!Admissible(owner, sourceItems, sourceSlot, out ItemStack incoming)) return Winner.None;
            bool haveHuman = Candidate(npc, npcSlot, incoming, out var human);
            bool haveDog = TryDog(owner, incoming, out var dog);
            return Decide(haveHuman, human, haveDog, dog, out chosen);
        }
        catch { return Winner.None; }
    }

    private static bool Admissible(EntityPlayer owner, ItemStack[] items, int sourceSlot, out ItemStack incoming)
    {
        incoming = null;
        if (!RebirthP11SmartLootGate.UnifiedOrderingQualified || owner == null || items == null ||
            sourceSlot < 0 || sourceSlot >= items.Length) return false;
        incoming = items[sourceSlot];
        return incoming != null && !incoming.IsEmpty() && incoming.count > 0;
    }

    private static Winner Decide(bool haveHuman, RebirthP11LootCandidate human, bool haveDog,
        RebirthP11LootCandidate dog, out RebirthP11LootCandidate chosen)
    {
        chosen = default;
        if (!haveHuman && !haveDog) return Winner.None;
        if (haveHuman && haveDog)
        {
            bool dogFirst = RebirthP11LootCandidateComparer.Instance.Compare(dog, human) < 0;
            chosen = dogFirst ? dog : human;
            return dogFirst ? Winner.Dog : Winner.Human;
        }
        chosen = haveHuman ? human : dog;
        return haveHuman ? Winner.Human : Winner.Dog;
    }

    private static bool Candidate(EntityRebirthHumanoidNPC npc, int slot, ItemStack incoming,
        out RebirthP11LootCandidate candidate)
    {
        candidate = default;
        if (npc?.RebirthRuntimeState == null || npc.bag?.ItemGrid == null || slot < 0 || slot >= npc.bag.SlotCount)
            return false;
        ItemStack held = npc.bag.ItemGrid[slot];
        bool matches = held != null && !held.IsEmpty() && held.CanStackWith(incoming);
        candidate = new RebirthP11LootCandidate(RebirthP11LootTarget.Human,
            npc.RebirthRuntimeState.StableId.ToString(), matches, slot);
        return true;
    }

    // The human policy already returns matching cells before empty ones; the
    // candidate only records which class that cell fell into.
    private static bool TryHuman(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        ItemStack incoming, out RebirthP11LootCandidate candidate)
    {
        candidate = default;
        if (!RebirthSmartLootingHumanPolicy.TrySelect(owner, source, sourceSlot, false,
                out var npc, out int slot)) return false;
        return Candidate(npc, slot, incoming, out candidate);
    }

    // Dogs take a whole stack or nothing (CanAcceptAll inside TrySelectAutoRouteTarget).
    private static bool TryDog(EntityPlayer owner, ItemStack incoming,
        out RebirthP11LootCandidate candidate)
    {
        candidate = default;
        if (!RebirthDogInventoryPolicy.TrySelectAutoRouteTarget(owner, incoming,
                out var id, out _)) return false;
        var slots = RebirthDogInventoryService.GetSnapshot(id).Slots;
        bool matches = false;
        int slot = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack held = slots[i];
            if (held == null || held.IsEmpty() || !held.CanStackWith(incoming)) continue;
            matches = true; slot = i; break;
        }
        candidate = new RebirthP11LootCandidate(RebirthP11LootTarget.Dog, id.ToString(), matches, slot);
        return true;
    }
}
