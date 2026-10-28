using System;
using System.Collections.Generic;

// PREPARED, NOT RUN. Compile with the actual service in an isolated offline
// process after the shared coding gate. Never invoke against a user's world.
internal static class RebirthNpcInventoryGenerationFixture
{
    internal static void Verify()
    {
        var original=RebirthNpcInventoryTransactionService.CapturePersistentRecords();
        try
        {
            var first=Record(1,10,20,2,Guid.NewGuid());
            Require(RebirthNpcInventoryTransactionService.TryReplacePersistentRecords(new[]{first},out _));
            var next=Record(2,1,7,1,Guid.NewGuid());
            Require(RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new[]{next},out var candidate,out _));
            Require(candidate.IsCurrent);Single(first);
            next.Quantities["test"]=99;next.Reservations["test"]=99;next.ReplayJournal[0]=Guid.NewGuid();
            Require(candidate.Commit());Require(!candidate.Commit());Require(!candidate.IsCurrent);
            var actual=RebirthNpcInventoryTransactionService.CapturePersistentRecords();
            Require(actual.Length==1&&actual[0].NpcId==next.NpcId&&actual[0].Revision==1&&actual[0].Quantities["test"]==7&&actual[0].Reservations["test"]==1&&actual[0].ReplayJournal[0]!=next.ReplayJournal[0]);
            var baseline=actual[0];
            Require(RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new[]{first},out var stale,out _));
            var intervening=Record(2,2,8,1,Guid.NewGuid());
            Require(RebirthNpcInventoryTransactionService.TryRestorePersistentRecord(intervening,out _));
            Require(!stale.IsCurrent);Require(!stale.Commit());Single(intervening);
            Require(RebirthNpcInventoryTransactionService.TryReplacePersistentRecords(new[]{baseline},out _));
            Require(!RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new[]{first,first},out _,out _));Single(baseline);
            Require(!RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new[]{first,(RebirthNpcInventoryPersistentRecord)null},out _,out _));Single(baseline);
            var invalid=Record(3,1,1,2,Guid.NewGuid());
            Require(!RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new[]{first,invalid},out _,out _));Single(baseline);
            bool interrupted=false;try{RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(Interrupted(first),out _,out _);}catch(InvalidOperationException){interrupted=true;}
            Require(interrupted);Single(baseline);
            Require(RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(new RebirthNpcInventoryPersistentRecord[0],out var empty,out _));Single(baseline);Require(empty.Commit());Require(RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length==0);
        }
        finally{Require(RebirthNpcInventoryTransactionService.TryReplacePersistentRecords(original,out _));}
    }
    private static RebirthNpcInventoryPersistentRecord Record(ulong id,uint revision,int quantity,int reserved,Guid replay)=>new RebirthNpcInventoryPersistentRecord
    {NpcId=new RebirthNpcStableId(1,id),Revision=revision,Quantities=new Dictionary<string,int>{{"test",quantity}},Reservations=new Dictionary<string,int>{{"test",reserved}},ReplayJournal=new[]{replay},LogisticsEffects=new Dictionary<Guid,string>()};
    private static IEnumerable<RebirthNpcInventoryPersistentRecord> Interrupted(RebirthNpcInventoryPersistentRecord first){yield return first;throw new InvalidOperationException("Injected candidate interruption");}
    private static void Single(RebirthNpcInventoryPersistentRecord expected)
    {var rows=RebirthNpcInventoryTransactionService.CapturePersistentRecords();Require(rows.Length==1&&rows[0].NpcId==expected.NpcId&&rows[0].Revision==expected.Revision&&rows[0].Quantities.Count==expected.Quantities.Count&&rows[0].Quantities["test"]==expected.Quantities["test"]&&rows[0].Reservations["test"]==expected.Reservations["test"]&&rows[0].ReplayJournal[0]==expected.ReplayJournal[0]);}
    private static void Require(bool value){if(!value)throw new Exception("Inventory recovery generation invariant failed");}
}
