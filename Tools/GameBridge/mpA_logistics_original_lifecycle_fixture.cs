using System;
using System.Collections.Generic;
using System.Linq;

// PREPARED, NOT RUN. A future disposable multiplayer harness creates the
// original through the real UI and samples this observer between real native
// callbacks/reconnects. No receipt fields, source images or phases are forged.
// Sampling establishes durable monotonicity; it is not physical conservation
// proof without the separate real endpoint/owner/body scenario observations.
internal sealed class mpA_logistics_original_lifecycle_fixture
{
    private readonly string owner,binding,world,creation;private readonly Guid operation;
    private int phase=-1,outcome;private bool rejecting,escrow;
    private readonly Dictionary<string,int> nativeAttempted=new Dictionary<string,int>(),nativePublished=new Dictionary<string,int>();
    private readonly Dictionary<string,int> npcAttempted=new Dictionary<string,int>(),npcPublished=new Dictionary<string,int>();
    internal mpA_logistics_original_lifecycle_fixture(RebirthLogisticsTransferJournal.Record actual)
    {
        if(actual==null||!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var saved)||
            RebirthLogisticsTransferJournal.Encode(saved)!=RebirthLogisticsTransferJournal.Encode(actual))throw new Exception("Requires actual saved original, not a supplied replacement plan.");
        owner=actual.Owner;operation=actual.Operation;world=actual.World;creation=actual.Creation;binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(actual);Sample();
    }
    internal RebirthLogisticsTransferJournal.Record Sample()
    {
        if(!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var actual)||actual.World!=world||actual.Creation!=creation||
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(actual)!=binding)throw new Exception("Original disappeared, rebound or changed immutable plan.");
        if(actual.Phase<phase||outcome!=0&&actual.Outcome!=outcome||rejecting&&!actual.Rejecting||escrow&&!actual.OwnerEscrowRecovery)
            throw new Exception("Durable outcome regressed/replanned across lifecycle.");
        if((actual.Legs.Any(value=>value.Attempted>0)||actual.Npcs.Any(value=>value.Attempted>0))&&string.IsNullOrEmpty(actual.OwnerDebitProof))
            throw new Exception("Source effect was attempted before original saved owner debit.");
        bool credit=actual.Legs.Any(value=>value.Attempted>=2)||actual.Npcs.Any(value=>value.Attempted>=2);
        if(credit&&(actual.Legs.Any(value=>value.Published<1)||actual.Npcs.Any(value=>value.Published<1)))
            throw new Exception("Original destination credit preceded complete original source debit publication.");
        foreach(var leg in actual.Legs){Monotonic(nativeAttempted,leg.Id,leg.Attempted);Monotonic(nativePublished,leg.Id,leg.Published);}
        foreach(var leg in actual.Npcs){Monotonic(npcAttempted,leg.StableId,leg.Attempted);Monotonic(npcPublished,leg.StableId,leg.Published);}
        phase=actual.Phase;outcome=actual.Outcome;rejecting=actual.Rejecting;escrow=actual.OwnerEscrowRecovery;
        return actual;
    }
    private static void Monotonic(Dictionary<string,int> seen,string id,int value)
    {if(seen.TryGetValue(id,out int before)&&value<before)throw new Exception("Original source effect/publication regressed.");seen[id]=value;}
    internal void RequireUnpaidCancellationPrepared()
    {
        var actual=Sample();if(actual.Phase!=1||!actual.Rejecting||!string.IsNullOrEmpty(actual.OwnerDebitProof)||actual.Legs.Count!=0||actual.Npcs.Count!=0||
            actual.BeforeOwner.Any(value=>!value.IsEmpty())||actual.DebitedOwner.Any(value=>!value.IsEmpty())||actual.AfterOwner.Any(value=>!value.IsEmpty()))
            throw new Exception("Real discovery cancellation did not freeze an original zero-effect plan.");
        // Native full Before+belt and saved phase5 are still required for
        // terminal no-payment proof; this prepared record supplies neither.
    }
    internal void RequireRetainedUnknown()
    {if(Sample().Phase==4)throw new Exception("Unproved missing/ambiguous native custody was terminalized.");}
    internal void RequireSettledOutcome(int expectedOutcome)
    {
        var actual=Sample();if(actual.Phase!=4||actual.Outcome!=expectedOutcome)throw new Exception("Actual saved original not terminal with expected outcome.");
        if(expectedOutcome==1&&(actual.Legs.Any(value=>value.Published!=2)||actual.Npcs.Any(value=>value.Published!=2)))throw new Exception("Terminal paid original lacks final source publication.");
        if(expectedOutcome==3&&(!actual.Rejecting||string.IsNullOrEmpty(actual.OwnerNoEffectProof)||!string.IsNullOrEmpty(actual.OwnerDebitProof)||
            actual.Legs.Any(value=>value.Attempted!=0||value.Published!=0)||actual.Npcs.Any(value=>value.Attempted!=0||value.Published!=0)))throw new Exception("No-effect terminal invented effects/payment.");
        // Lookup is deliberately repeated after normal archive/cold reload.
        // This observer cannot make the archive or manufacture terminal proof.
    }
    internal void RequirePhysicalSettlement()
    {
        var actual=Sample();if(actual.Phase!=4||!RebirthLogisticsDeathJournal.TryRead(actual,out var physical)||physical.State!=4||
            physical.Operation!=operation||physical.Binding!=binding||physical.Outcome!=actual.Outcome||physical.BeforeProof?.Length!=64||physical.FinalProof?.Length!=64||
            physical.OwnerFileProof?.Length!=64||physical.Parcels.Any(value=>value.State<3||value.BeforeProof?.Length!=64||value.FinalProof?.Length!=64)||
            !RebirthLogisticsDeathPartition.ValidateFrozenPlan(actual,physical))throw new Exception("Real physical original lacks complete saved conserved mapping/publication.");
        // Bodies may be legitimately looted after terminal. This method reads
        // positive retained publication, never infers success from absence.
    }
    internal static void AfterActualPureAdmissionExpiry(RebirthLogisticsOriginalAdmission actual)
    {
        if(actual==null||!actual.Expired)throw new Exception("Harness must wait for actual pre-effect watch expiry.");
        // Future harness now ticks original transport and retries the SAME UI
        // owner operation; Sample then verifies same digest/operation. No clock
        // replacement, manual phase change or replacement mutating GUID here.
    }
}
