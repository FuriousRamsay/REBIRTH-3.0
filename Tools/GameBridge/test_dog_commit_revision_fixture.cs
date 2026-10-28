using System;
struct RebirthNpcStableId {}
class ItemClass {public string Name="item";}
class ItemValue {public ItemClass ItemClass=new ItemClass();}
class ItemStack {public int count;public ItemValue itemValue=new ItemValue();public bool IsEmpty(){return count==0;}}
class PackedBoolArray {public int Length=1;public bool this[int i]{get{return false;}}}
class RebirthNpcInventorySlotRecord {public bool Locked,Reserved;public string ItemValueSerialization,MetadataOrCustomData;public int ItemCount;}
class Inventory {public uint InventoryRevision;public RebirthNpcInventorySlotRecord[] SlotRecords=new[]{new RebirthNpcInventorySlotRecord()};public string ContentChecksum;}
class RebirthNpcPersistentRecord {public Inventory Inventory;public uint AggregateRevision=1;}
class RebirthNpcPersistentRecordView {public uint AggregateRevision;}
class RebirthDogPersistentRecordView {}
// Detached candidate publication mirrors the native aggregate boundary: a
// callback refusal must never expose partially projected slots or a revision.
static class RebirthNpcAggregatePersistenceStore {public static RebirthNpcPersistentRecord Record;public static int Published;public static bool Reject;public static bool TryMutateExisting(RebirthNpcStableId id,uint revision,Func<RebirthNpcPersistentRecord,bool> f){if(Reject||Record.AggregateRevision!=revision)return false;var candidate=new RebirthNpcPersistentRecord {AggregateRevision=Record.AggregateRevision};if(Record.Inventory!=null){var old=Record.Inventory;var slot=old.SlotRecords[0];candidate.Inventory=new Inventory {InventoryRevision=old.InventoryRevision,ContentChecksum=old.ContentChecksum,SlotRecords=new[]{new RebirthNpcInventorySlotRecord {Locked=slot.Locked,Reserved=slot.Reserved,ItemCount=slot.ItemCount,ItemValueSerialization=slot.ItemValueSerialization,MetadataOrCustomData=slot.MetadataOrCustomData}}};}if(!f(candidate))return false;Record.Inventory=candidate.Inventory;Record.AggregateRevision=candidate.AggregateRevision+1;Published++;return true;}}
// This fixture exercises ordinary dog commits outside either private original
// mutation scope. Original-specific scenarios exercise the actual scope helpers.
static class RemoteResourceTransactions {public static bool ActionBlocksNpc(RebirthNpcStableId id){return false;}public static bool ActionAllowsDog(RebirthNpcStableId id,uint revision,ItemStack[] slots,PackedBoolArray locks){return false;}public static bool StampActionDogCandidate(RebirthNpcPersistentRecord row){return true;}}
static class RebirthCookingNpcReturn {public static bool RefuseStamp;public static bool ValidateDogCandidateBefore(RebirthNpcPersistentRecord row){return true;}public static bool PreserveUnchangedDogSlot(int slot){return false;}public static bool StampDogCandidate(RebirthNpcPersistentRecord row){return !RefuseStamp;}}
static class RebirthDogStateService {public static int Normalized;public static bool TryGetView(RebirthNpcStableId id,out RebirthNpcPersistentRecordView r,out RebirthDogPersistentRecordView d){r=new RebirthNpcPersistentRecordView {AggregateRevision=RebirthNpcAggregatePersistenceStore.Record.AggregateRevision};d=new RebirthDogPersistentRecordView();return true;}public static bool IsDog(RebirthNpcPersistentRecord r){return true;}public static void EnsureInventoryRecord(RebirthNpcPersistentRecord r){Normalized++;if(r.Inventory==null)r.Inventory=new Inventory {InventoryRevision=1};}}
class Subject {public const int Capacity=1;public static int Changes;static string SerializeItemValue(ItemValue v){return "item";}static string ComputeChecksum(RebirthNpcInventorySlotRecord[] s){return "sum";}static void Invalidate(RebirthNpcStableId id,string why){Changes++;}static void SynchronizeActiveInventoryEffects(RebirthNpcStableId id){}
// COMMIT
}
class Check
{
    static void Main()
    {
        var r=new RebirthNpcPersistentRecord();RebirthNpcAggregatePersistenceStore.Record=r;
        string e;var items=new[]{new ItemStack {count=3}};
        if(!Subject.CommitWorkingSlots(new RebirthNpcStableId(),0,items,null,out e)||r.Inventory.InventoryRevision!=2)throw new Exception("first initialization");
        int n=RebirthDogStateService.Normalized,published=RebirthNpcAggregatePersistenceStore.Published;
        if(Subject.CommitWorkingSlots(new RebirthNpcStableId(),0,new[]{new ItemStack {count=9}},null,out e)||r.Inventory.SlotRecords[0].ItemCount!=3||RebirthDogStateService.Normalized!=n)throw new Exception("stale zero overwrite");
        if(RebirthNpcAggregatePersistenceStore.Published!=published)throw new Exception("rejected commit published");
        if(!Subject.CommitWorkingSlots(new RebirthNpcStableId(),2,items,null,out e))throw new Exception("matching revision");
        if(Subject.CommitWorkingSlots(new RebirthNpcStableId(),2,items,null,out e))throw new Exception("stale nonzero");
        r.Inventory.InventoryRevision=0;
        if(!Subject.CommitWorkingSlots(new RebirthNpcStableId(),0,items,null,out e)||r.Inventory.InventoryRevision!=1)throw new Exception("real zero revision");

        // A receipt stamp can reject after the candidate projection. The live
        // inventory, revision, publication count and effect callbacks must stay put.
        RebirthCookingNpcReturn.RefuseStamp=true;
        published=RebirthNpcAggregatePersistenceStore.Published;int effects=Subject.Changes;
        Subject.WorkingCommit observation;
        if(Subject.CommitWorkingSlots(new RebirthNpcStableId(),1,new[]{new ItemStack {count=9}},null,out e,out observation)||
            !observation.Returned||observation.Published||observation.PublishedRevision!=1||
            r.Inventory.InventoryRevision!=1||r.Inventory.SlotRecords[0].ItemCount!=3||
            RebirthNpcAggregatePersistenceStore.Published!=published||Subject.Changes!=effects||string.IsNullOrEmpty(e))
            throw new Exception("refused candidate leaked or missing native refusal observation");
        RebirthCookingNpcReturn.RefuseStamp=false;
        if(!Subject.CommitWorkingSlots(new RebirthNpcStableId(),1,items,null,out e,out observation)||
            !observation.Returned||!observation.Published||observation.PublishedRevision!=2)
            throw new Exception("missing positive native publication observation");
        RebirthNpcAggregatePersistenceStore.Reject=true;published=Subject.Changes;
        if(Subject.CommitWorkingSlots(new RebirthNpcStableId(),2,items,null,out e,out observation)||Subject.Changes!=published||
            string.IsNullOrEmpty(e)||!observation.Returned||observation.Published)throw new Exception("aggregate conflict reported success");
        Console.WriteLine("PASS initialization, exact revisions, detached refusal, native observations, aggregate conflict without effects");
    }
}
