using System;
using System.Linq;

// PREPARED NOT RUN. Genuine original debit/fatal/native publication and an
// actual subsequent native death are supplied by the harness, never fabricated.
internal static class mpA_logistics_later_death_source_fixture
{
    internal static void RequirePaidSourceScope(EntityPlayer actor,RebirthLogisticsTransferJournal.Record actual,bool expected)
    {
        var bag=ItemStack.Clone(actor.bag.ItemGrid.items);var belt=ItemStack.Clone(actor.inventory.items);
        string frozen=RebirthLogisticsTransferJournal.Encode(actual);
        bool current=RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,actual);
        if(current!=expected)throw new Exception("Actual paid source-only lineage differs from expected.");
        if(!RebirthLogisticsDeathPartition.Same(bag,actor.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,actor.inventory.items)||
            !RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var saved)||RebirthLogisticsTransferJournal.Encode(saved)!=frozen)throw new Exception("Source proof gate mutated native owner or original.");
        if(actor.Died>actual.Deaths+1&&RebirthLogisticsDeathSettlement.ActorCurrent(actor,actual))throw new Exception("Generic actor/death scope was widened.");
    }
    internal static void RequireRetainedClaimsOnly(EntityPlayer actor,RebirthLogisticsTransferJournal.Record actual,bool expected)
    {
        string frozen=RebirthLogisticsTransferJournal.Encode(actual);
        bool rebound=RebirthNpcOriginalEffectAdmission.TryRebindPaidSourceOriginal(actor,actual,out var lease);
        if(rebound!=expected||rebound&&!lease.Current())throw new Exception("Original claim rebind did not match already-retained typed claims.");
        if(!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var saved)||RebirthLogisticsTransferJournal.Encode(saved)!=frozen)throw new Exception("Readonly claim rebind changed original phase/plan.");
        // No Dispose/release is used as fixture cleanup; actual pending original
        // remains the sole claim owner until its own saved terminal protocol.
    }
    internal static void RequireSameOriginalPlan(RebirthLogisticsTransferJournal.Record before,RebirthLogisticsTransferJournal.Record after)
    {
        if(before.Operation!=after.Operation||before.Owner!=after.Owner||before.World!=after.World||before.Creation!=after.Creation||before.Deaths!=after.Deaths||
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(before)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(after)||before.Legs.Count!=after.Legs.Count||before.Npcs.Count!=after.Npcs.Count)
            throw new Exception("Later-death continuation replanned or introduced a fresh source leg.");
    }
}
// Deferred native vectors: die again before any source effect, after first saved
// native debit, between NPC source receipts, after exact credit, and after saved
// Outcome but before physical output. Same original attempts/receipts settle;
// duplicate/cold resume cannot repeat an attempted-unknown native/NPC leg.
// Actual full physical mismatch, changed GUID/body cells/partition/fatal, wrong
// stable native primary/world/creation, source lock/range/access change or lost
// positive proof retains. No fresh owner reserve/debit/setter is enabled.
// Positive native2 with no journal OwnerDebitProof may only publish existing
// ConfirmOwnerDebit metadata first; every source effect still requires durable
// original debit proof. Readonly Phase2/3 followthrough cannot replan or write
// the current new-life bag; logical owed output follows original parcel custody.
