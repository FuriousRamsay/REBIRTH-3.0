using System;
using System.Collections.Generic;

// PREPARED, NOT RUN. Actual service in an isolated offline fixture process only.
internal static class RebirthNpcEquipmentGenerationFixture
{
    internal static void Verify()
    {
        var original=RebirthNpcEquipmentService.CapturePersistentRecords();
        try
        {
            var first=Record(1,3,"old",Guid.NewGuid());
            Require(RebirthNpcEquipmentService.TryReplacePersistentRecords(new[]{first},out _));
            var next=Record(2,9,"recovered",Guid.NewGuid());
            Require(RebirthNpcEquipmentService.TryPreparePersistentRecords(new[]{next},out var prepared,out _));
            AssertSingle(first);
            next.Slots[RebirthNpcEquipmentSlot.Head]="mutated after preparation";
            next.ReplayJournal[0]=Guid.NewGuid();
            Require(prepared.Commit());Require(!prepared.Commit());
            var committed=RebirthNpcEquipmentService.CapturePersistentRecords();
            Require(committed.Length==1&&committed[0].NpcId.Equals(next.NpcId)&&committed[0].Revision==9&&
                committed[0].Slots[RebirthNpcEquipmentSlot.Head]=="recovered"&&committed[0].ReplayJournal[0]!=next.ReplayJournal[0]);
            var baseline=committed[0];
            Require(RebirthNpcEquipmentService.TryPreparePersistentRecords(new[]{first},out var stale,out _));
            var intervening=Record(2,10,"newer live state",Guid.NewGuid());
            Require(RebirthNpcEquipmentService.TryRestorePersistentRecord(intervening,out _));
            Require(!stale.Commit());Require(!stale.Commit());AssertSingle(intervening);
            Require(RebirthNpcEquipmentService.TryReplacePersistentRecords(new[]{baseline},out _));
            Require(!RebirthNpcEquipmentService.TryPreparePersistentRecords(new[]{first,first},out _,out _));AssertSingle(baseline);
            Require(!RebirthNpcEquipmentService.TryPreparePersistentRecords(new[]{first,(RebirthNpcEquipmentPersistentRecord)null},out _,out _));AssertSingle(baseline);
            var invalid=Record(3,1,"bad",Guid.Empty);
            Require(!RebirthNpcEquipmentService.TryPreparePersistentRecords(new[]{first,invalid},out _,out _));AssertSingle(baseline);
            bool interrupted=false;try{RebirthNpcEquipmentService.TryPreparePersistentRecords(Interrupted(first),out _,out _);}
            catch(InvalidOperationException){interrupted=true;}
            Require(interrupted);AssertSingle(baseline);
            Require(RebirthNpcEquipmentService.TryPreparePersistentRecords(new RebirthNpcEquipmentPersistentRecord[0],out var empty,out _));
            AssertSingle(baseline);Require(empty.Commit());Require(RebirthNpcEquipmentService.CapturePersistentRecords().Length==0);
        }
        finally{Require(RebirthNpcEquipmentService.TryReplacePersistentRecords(original,out _));}
    }
    private static RebirthNpcEquipmentPersistentRecord Record(ulong id,uint revision,string item,Guid replay)=>
        new RebirthNpcEquipmentPersistentRecord{NpcId=new RebirthNpcStableId(1,id),Revision=revision,
            Slots=new Dictionary<RebirthNpcEquipmentSlot,string>{{RebirthNpcEquipmentSlot.Head,item}},ReplayJournal=new[]{replay}};
    private static IEnumerable<RebirthNpcEquipmentPersistentRecord> Interrupted(RebirthNpcEquipmentPersistentRecord first)
    {yield return first;throw new InvalidOperationException("Injected candidate enumeration failure.");}
    private static void AssertSingle(RebirthNpcEquipmentPersistentRecord expected)
    {
        var actual=RebirthNpcEquipmentService.CapturePersistentRecords();
        Require(actual.Length==1&&actual[0].NpcId.Equals(expected.NpcId)&&actual[0].Revision==expected.Revision&&
            actual[0].Slots.Count==expected.Slots.Count&&actual[0].Slots[RebirthNpcEquipmentSlot.Head]==expected.Slots[RebirthNpcEquipmentSlot.Head]&&
            actual[0].ReplayJournal.Length==expected.ReplayJournal.Length&&actual[0].ReplayJournal[0]==expected.ReplayJournal[0]);
    }
    private static void Require(bool value){if(!value)throw new Exception("Equipment generation invariant failed.");}
}
