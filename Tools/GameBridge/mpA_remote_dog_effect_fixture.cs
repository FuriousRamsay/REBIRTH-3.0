public class EntityPlayer{public bool Wrong;}
public enum RebirthNpcInventoryAuthorityOperations{Mutate,Transfer}
public static class RebirthNpcInventoryAuthorityService{public static bool Validate(string key,RebirthNpcInventoryAuthorityOperations purpose,RebirthNpcStableId id)=>key=="mutate"&&purpose==RebirthNpcInventoryAuthorityOperations.Mutate;}
internal static class RebirthNpcOriginalEffectAdmission{internal static bool Reserved=true;internal static bool IsReservedRemoteResource(RebirthRemoteResourceNpcEffectPlan plan)=>Reserved;}
internal sealed class RebirthRemoteResourceNpcEffectPlan{
 internal Guid Operation=Guid.NewGuid(),MutationId=Guid.NewGuid();internal string World=new string('b',64),Owner=new string('c',64),Creation="creation",Binding=new string('d',64),SourceId;
 internal int Deaths,Attempted;internal uint Revision=4;internal bool DogStorage=true;internal RebirthNpcStableId NpcId;
 internal ItemStack[] Before,After;internal bool[] Locks;
 internal ItemStack[] CopyBeforeSlots()=>ItemStack.Clone(Before);internal ItemStack[] CopyAfterSlots()=>ItemStack.Clone(After);internal bool[] CopyLocks()=>(bool[])Locks.Clone();
}
internal static class RebirthRemoteResourceDurableOutcome{
 internal static RebirthRemoteResourceNpcEffectPlan Plan,Terminal;
 internal static bool TryReadNpcEffectPlan(EntityPlayer actor,Guid op,string source,out RebirthRemoteResourceNpcEffectPlan plan){plan=Plan;return plan!=null&&op==plan.Operation&&source==plan.SourceId;}
 internal static bool TryReadSavedTerminalNpcEffectPlan(string owner,Guid op,string source,out RebirthRemoteResourceNpcEffectPlan plan){plan=Terminal;return plan!=null&&owner==plan.Owner&&op==plan.Operation&&source==plan.SourceId;}
}
public static class Fixture{
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Main(){
  var id=new RebirthNpcStableId{Text=new string('a',32)};var actor=new EntityPlayer();var plan=new RebirthRemoteResourceNpcEffectPlan{NpcId=id,SourceId="N:"+id,Before=new[]{new ItemStack(new ItemValue{Quality=7},4),ItemStack.Empty},After=new[]{new ItemStack(new ItemValue{Quality=7},2),ItemStack.Empty},Locks=new[]{false,false}};RebirthRemoteResourceDurableOutcome.Plan=plan;
  var overflow=new[]{new RebirthNpcInventorySlotRecord{SlotIndex=99,ItemCount=8,ItemValueSerialization="unknown-original"}};
  RebirthNpcAggregatePersistenceStore.Current=new RebirthNpcPersistentRecord{Identity=new RebirthNpcIdentityRecord{StableNpcId=id},AggregateRevision=1,UnknownOptionalElements=new[]{"<unrelated />"},Inventory=new RebirthNpcInventoryRecord{InventoryRevision=4,OverflowRecords=overflow,SlotRecords=new[]{new RebirthNpcInventorySlotRecord{SlotIndex=0,ItemCount=4,ItemValueSerialization="7"},new RebirthNpcInventorySlotRecord{SlotIndex=1,ItemCount=0,ItemValueSerialization=""}}}};
  Check(!RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"mutate",out _,out _),"prepared intent is not actual attempted effect");plan.Attempted=1;
  Check(!RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"transfer",out _,out _),"fetch keeps existing Mutate policy");
  RebirthNpcAggregatePersistenceStore.Current.Inventory.SlotRecords[0].Reserved=true;Check(!RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"mutate",out _,out _),"reserved source cell refused");RebirthNpcAggregatePersistenceStore.Current.Inventory.SlotRecords[0].Reserved=false;
  Check(RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"mutate",out bool replay,out _)&&!replay,"original exact slot CAS");
  var saved=RebirthNpcAggregatePersistenceStore.Current;Check(saved.Inventory.SlotRecords[0].ItemCount==2&&saved.Inventory.SlotRecords[0].ItemValueSerialization=="7"&&ReferenceEquals(saved.Inventory.OverflowRecords,overflow)&&saved.UnknownOptionalElements[0]=="<unrelated />","metadata overflow unrelated elements preserved");
  Check(RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"mutate",out replay,out _)&&replay&&RebirthNpcAggregatePersistenceStore.Writes==1,"exact typed effect replay");
  saved.Inventory.InventoryRevision=6;saved.Inventory.SlotRecords[0].ItemCount=3;Check(RebirthDogInventoryService.CommitRemoteResourceOriginalSlots(actor,plan.Operation,plan.SourceId,"mutate",out replay,out _)&&replay&&saved.Inventory.SlotRecords[0].ItemCount==3,"later legitimate native state preserved");
  string receipt=RebirthRemoteResourceDogTransfer.Receipt(plan);saved.UnknownOptionalElements=saved.UnknownOptionalElements.Concat(new[]{receipt}).ToArray();Check(!RebirthRemoteResourceDogTransfer.TryReceipt(saved,out _),"duplicate known effect refuses");saved.UnknownOptionalElements=new[]{"<unrelated />",receipt};
  var next=new RebirthRemoteResourceNpcEffectPlan{NpcId=id,SourceId=plan.SourceId,Before=plan.After,After=plan.After,Locks=plan.Locks};Check(!RebirthRemoteResourceDogTransfer.MayReplaceReceipt(saved,next),"unknown previous outcome retains original receipt");RebirthRemoteResourceDurableOutcome.Terminal=plan;Check(RebirthRemoteResourceDogTransfer.MayReplaceReceipt(saved,next),"exact positive terminal original permits bounded receipt retirement");
  Console.WriteLine("PASS actual fetch dog receipt/CAS with disclosed native/DTO/terminal doubles; no saved publication certification");
 }
}
