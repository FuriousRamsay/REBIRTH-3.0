using System;
using System.Collections.Generic;

// B10/B11 awards for the NPC-led loot path. Both are awarded once per durable receipt:
//  - Lockpick success: shared skill.lockpicking through the B13 journal (QueueAward), raw unit 1,
//    receipt = the lockpick attempt id (the same id the G03 custody ledger settles).
//  - Committed loot leg: there is no shared player Looting skill (progression.xml has none; see
//    RebirthG02WorkSkills), so the committed smartLootMove leg feeds the NPC's own Looting profession
//    track, keyed by the operation id (Commit dedupes on OutcomeId), exactly once on the 3 -> 4 publish.
// A restart between the phase-4 publish and the award can miss a Looting-track point (never duplicate it).
// Lockpick awards that the journal cannot accept yet are retried from Tick; nothing waits on them.
internal static class RebirthP11LootAward
{
    private struct Pending { internal RebirthNpcStableId Npc; internal string Receipt; }
    private static readonly Queue<Pending> pending = new Queue<Pending>();
    private static readonly HashSet<string> queued = new HashSet<string>();

    internal static void Lockpick(EntityRebirthHumanoidNPC npc, Guid attempt)
    {
        try
        {
            if (npc?.RebirthRuntimeState == null || attempt == Guid.Empty) return;
            Offer(npc.RebirthRuntimeState.StableId, "npc:lockpick:" + attempt.ToString("N"));
        }
        catch { }
    }

    internal static void LegCommitted(EntityRebirthHumanoidNPC npc, RebirthSmartLootingHumanOriginal row)
    {
        try
        {
            if (npc?.RebirthRuntimeState == null || row == null || row.Operation == Guid.Empty || row.Quantity <= 0) return;
            string container = row.SourceBefore[row.SourceSlot]?.itemValue?.ItemClass?.GetItemName() ?? "container";
            RebirthNpcProfessionOutcomePublisher.Looting(row.Operation, npc.RebirthRuntimeState.StableId, container, row.Quantity, 1);
        }
        catch { }
    }

    private static void Offer(RebirthNpcStableId npc, string receipt)
    {
        var result = Queue(npc, receipt);
        if (result == RebirthP1AwardQueueResult.Unavailable && queued.Count < 32 && queued.Add(receipt))
            pending.Enqueue(new Pending { Npc = npc, Receipt = receipt });
    }

    private static RebirthP1AwardQueueResult Queue(RebirthNpcStableId npc, string receipt)
    {
        try
        {
            return RebirthP1SharedWorkAward.QueueAward(npc, RebirthSurvivorIds.SkillLockpicking, receipt, 1f,
                "p11:lockpick:npc", "saved lockpick attempt that unlocked the container");
        }
        catch { return RebirthP1AwardQueueResult.Unavailable; }
    }

    // Paced by RebirthSmartLootingHumanTransfer.Tick (5 s). Acknowledged, Journaled and Invalid are final.
    internal static void Tick()
    {
        int count = pending.Count;
        for (int i = 0; i < count; i++)
        {
            var item = pending.Dequeue();
            if (Queue(item.Npc, item.Receipt) == RebirthP1AwardQueueResult.Unavailable) pending.Enqueue(item);
            else queued.Remove(item.Receipt);
        }
    }
}
