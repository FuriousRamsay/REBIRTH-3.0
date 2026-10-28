// The production caller authority/native file readers are not extracted here.
public struct RebirthNpcStableId
{
 public string Text;public bool IsEmpty=>string.IsNullOrEmpty(Text);public override string ToString()=>Text;
 public static bool TryParse(string value,out RebirthNpcStableId id){id=new RebirthNpcStableId{Text=value};return value?.Length==32;}
 public static bool operator==(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Text==b.Text;public static bool operator!=(RebirthNpcStableId a,RebirthNpcStableId b)=>!(a==b);
 public override bool Equals(object o)=>o is RebirthNpcStableId id&&id==this;public override int GetHashCode()=>Text?.GetHashCode()??0;
}
public enum RemoteResourceSourceKind{NpcStorage}
internal static class RemoteResourceTransactions{internal static bool Held;internal static bool ActionBlocksNpc(RebirthNpcStableId id)=>Held;}
public sealed class ItemClass{public string Name="ammo";public Number Stacknumber=new Number();public sealed class Number{public int Value=10;}}
public sealed class ItemValue{public int Quality;public ItemClass ItemClass=new ItemClass();}
public sealed class ItemStack
{
 public int count;public ItemValue itemValue=new ItemValue();public static ItemStack Empty=>new ItemStack();public bool IsEmpty()=>count==0;
 public ItemStack(){}public ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}
 public ItemStack Clone()=>new ItemStack(new ItemValue{Quality=itemValue.Quality},count);public static ItemStack[] Clone(ItemStack[] values)=>values.Select(value=>value.Clone()).ToArray();
 public bool CanStackPartlyWith(ItemStack incoming,out int capacity){capacity=Math.Max(0,10-count);return itemValue.Quality==incoming.itemValue.Quality&&capacity>0;}
}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&(a.count==0||a.itemValue.Quality==b.itemValue.Quality);}
public static class RebirthCamperInventorySnapshot{public static byte[] Capture(ItemStack[] values){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){foreach(var value in values){w.Write(value.count);w.Write(value.itemValue.Quality);}return s.ToArray();}}}
public static class RebirthLogisticsTransferJournal
{
 public sealed class Leg{public string Id,NativeIdentity;public RemoteResourceSourceKind Kind;public ItemStack[] Before,After;public bool[] Locks;}
 public sealed class Record{public Guid Operation;public string World="world",Owner="owner",Creation="creation";public int Deaths,Phase;public List<Leg> Legs=new List<Leg>();}
 public static Record Old;public static bool TryRead(string owner,Guid operation,out Record record){record=Old;return record!=null&&record.Owner==owner&&record.Operation==operation;}
}
public sealed class RebirthNpcInventorySlotRecord{public int SlotIndex,ItemCount;public string ItemValueSerialization,MetadataOrCustomData;public bool Locked,Reserved;}
public sealed class RebirthNpcInventoryRecord{public uint InventoryRevision;public RebirthNpcInventorySlotRecord[] SlotRecords,OverflowRecords;public string ContentChecksum;}
public sealed class RebirthNpcIdentityRecord{public RebirthNpcStableId StableNpcId;}
public sealed class RebirthNpcPersistentRecord{public RebirthNpcIdentityRecord Identity;public RebirthNpcInventoryRecord Inventory;public uint AggregateRevision;public string[] UnknownOptionalElements;}
public static class RebirthNpcAggregatePersistenceStore
{
 public static RebirthNpcPersistentRecord Current;public static int Writes;
 public static bool SavedAvailable=true;
 public static bool TryReadSavedLogisticsDog(RebirthNpcStableId id,out RebirthNpcPersistentRecord row){row=Current;return SavedAvailable&&row.Identity.StableNpcId==id;} // Final native publication is explicitly doubled.
 public static bool TryGet(RebirthNpcStableId id,out RebirthNpcPersistentRecord row){row=Current;return row.Identity.StableNpcId==id;}
 public static bool TryMutateExisting(RebirthNpcStableId id,uint revision,Func<RebirthNpcPersistentRecord,bool> callback)
 {
  if(Current.AggregateRevision!=revision)return false;
  var row=new RebirthNpcPersistentRecord{Identity=Current.Identity,AggregateRevision=revision,UnknownOptionalElements=(string[])Current.UnknownOptionalElements.Clone(),Inventory=new RebirthNpcInventoryRecord{InventoryRevision=Current.Inventory.InventoryRevision,OverflowRecords=Current.Inventory.OverflowRecords,SlotRecords=Current.Inventory.SlotRecords.Select(s=>new RebirthNpcInventorySlotRecord{SlotIndex=s.SlotIndex,ItemCount=s.ItemCount,ItemValueSerialization=s.ItemValueSerialization,MetadataOrCustomData=s.MetadataOrCustomData,Locked=s.Locked,Reserved=s.Reserved}).ToArray()}};
  if(!callback(row))return false;row.AggregateRevision++;Current=row;Writes++;return true;
 }
}
public static class RebirthDogStateService{public static bool IsDog(RebirthNpcPersistentRecord value)=>true;public static void EnsureInventoryRecord(RebirthNpcPersistentRecord row){}}
public static class RebirthNativeItemCodec{public static bool TryDecode(string text,out ItemValue value){value=null;if(!int.TryParse(text,out int quality))return false;value=new ItemValue{Quality=quality};return true;}}
public static class RebirthLogisticsPlanning{public static bool IsActive=>false;}
public static class ThreadManager{public static bool IsMainThread()=>true;}
public sealed class World{public bool IsRemote()=>false;}
public sealed class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
public static class RebirthDogInventoryService
{
 public const int Capacity=2;
 private static string SerializeItemValue(ItemValue value)=>value.Quality.ToString(CultureInfo.InvariantCulture);
 private static string ComputeChecksum(RebirthNpcInventorySlotRecord[] values)=>"fixture";
 private static void Invalidate(RebirthNpcStableId id,string reason){}private static void SynchronizeActiveInventoryEffects(RebirthNpcStableId id){}
 /* ACTUAL_DOG_CAS */
}
public static class Fixture
{
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Main()
 {
  var id=new RebirthNpcStableId{Text=new string('a',32)};var leg=new RebirthLogisticsTransferJournal.Leg{Id="N:"+id,NativeIdentity=RebirthLogisticsDogTransfer.Identity(id,4),Kind=RemoteResourceSourceKind.NpcStorage,Before=new[]{new ItemStack(new ItemValue{Quality=7},4),ItemStack.Empty},After=new[]{new ItemStack(new ItemValue{Quality=7},2),new ItemStack(new ItemValue{Quality=9},1)},Locks=new[]{false,false}};
  var record=new RebirthLogisticsTransferJournal.Record{Operation=Guid.NewGuid()};record.Legs.Add(leg);
  var overflow=new[]{new RebirthNpcInventorySlotRecord{SlotIndex=99,ItemCount=8,ItemValueSerialization="unknown-old-frame"}};
  RebirthNpcAggregatePersistenceStore.Current=new RebirthNpcPersistentRecord{Identity=new RebirthNpcIdentityRecord{StableNpcId=id},AggregateRevision=1,UnknownOptionalElements=new[]{"<unrelated value=\"preserved\" />"},Inventory=new RebirthNpcInventoryRecord{InventoryRevision=4,OverflowRecords=overflow,SlotRecords=new[]{new RebirthNpcInventorySlotRecord{SlotIndex=0,ItemCount=4,ItemValueSerialization="7"},new RebirthNpcInventorySlotRecord{SlotIndex=1,ItemCount=0,ItemValueSerialization=""}}}};
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,false,()=>true,out bool replay,out _)&&!replay,"original debit CAS");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,false,()=>true,out replay,out _)&&replay&&RebirthNpcAggregatePersistenceStore.Writes==1,"debit exact replay");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out replay,out _)&&!replay,"original credit CAS");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out replay,out _)&&replay&&RebirthNpcAggregatePersistenceStore.Writes==2,"credit exact replay");
  var saved=RebirthNpcAggregatePersistenceStore.Current;
  Check(saved.Inventory.InventoryRevision==6&&saved.Inventory.SlotRecords[0].ItemCount==2&&saved.Inventory.SlotRecords[1].ItemValueSerialization=="9","exact metadata/count");
  Check(ReferenceEquals(saved.Inventory.OverflowRecords,overflow)&&saved.UnknownOptionalElements[0]=="<unrelated value=\"preserved\" />","unknown and overflow preserved");
  saved.Inventory.SlotRecords[0].ItemCount++;Check(!RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out _,out _),"changed postimage not replay");saved.Inventory.SlotRecords[0].ItemCount--;
  saved.UnknownOptionalElements=saved.UnknownOptionalElements.Concat(new[]{RebirthLogisticsDogTransfer.Receipt(record,leg,true)}).ToArray();Check(!RebirthLogisticsDogTransfer.TryReceipt(saved,out _),"duplicate known receipt refused");
  saved.UnknownOptionalElements=new[]{"<unrelated value=\"preserved\" />",RebirthLogisticsDogTransfer.Receipt(record,leg,false)};saved.Inventory.InventoryRevision=7;
  saved.Inventory.SlotRecords[0].ItemCount=3;saved.Inventory.SlotRecords[0].ItemValueSerialization="opaque-unrelated-original";saved.Inventory.SlotRecords[0].Locked=true;saved.Inventory.SlotRecords[0].Reserved=true;
  saved.Inventory.SlotRecords[1].ItemCount=0;saved.Inventory.SlotRecords[1].ItemValueSerialization="";
  RebirthNpcAggregatePersistenceStore.SavedAvailable=false;Check(!RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out _,out _),"continuation requires positive native saved original debit");RebirthNpcAggregatePersistenceStore.SavedAvailable=true;
  saved.Inventory.SlotRecords[1].ItemCount=1;saved.Inventory.SlotRecords[1].ItemValueSerialization="5";Check(!RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out _,out _)&&RebirthNpcAggregatePersistenceStore.Writes==2,"conflicting original destination retains credit without reslotting or partial write");saved.Inventory.SlotRecords[1].ItemCount=0;saved.Inventory.SlotRecords[1].ItemValueSerialization="";
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out _,out _,true)&&RebirthNpcAggregatePersistenceStore.Writes==2,"continuation preflight publishes no candidate slots");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out replay,out _)&&!replay,"only original positive native slot delta continued");
  saved=RebirthNpcAggregatePersistenceStore.Current;Check(saved.Inventory.InventoryRevision==8&&saved.Inventory.SlotRecords[0].ItemValueSerialization=="opaque-unrelated-original"&&saved.Inventory.SlotRecords[0].ItemCount==3&&saved.Inventory.SlotRecords[0].Locked&&saved.Inventory.SlotRecords[0].Reserved&&saved.Inventory.SlotRecords[1].ItemCount==1&&saved.Inventory.SlotRecords[1].ItemValueSerialization=="9"&&ReferenceEquals(saved.Inventory.OverflowRecords,overflow),"unrelated opaque native cell flags and overflow preserved");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(record,leg,true,()=>true,out replay,out _)&&replay&&RebirthNpcAggregatePersistenceStore.Writes==3,"original aggregate candidate receipt prevents partial cell replay");
  var nextRecord=new RebirthLogisticsTransferJournal.Record{Operation=Guid.NewGuid()};var nextLeg=new RebirthLogisticsTransferJournal.Leg{Id=leg.Id,NativeIdentity=leg.NativeIdentity,Kind=leg.Kind,Before=new[]{new ItemStack(new ItemValue{Quality=7},4),ItemStack.Empty},After=new[]{new ItemStack(new ItemValue{Quality=7},5),new ItemStack(new ItemValue{Quality=9},1)},Locks=new[]{false,false}};nextRecord.Legs.Add(nextLeg);
  saved.Inventory.InventoryRevision=5;saved.UnknownOptionalElements=new[]{"<unrelated />",RebirthLogisticsDogTransfer.Receipt(nextRecord,nextLeg,false)};saved.Inventory.SlotRecords[0].ItemCount=4;saved.Inventory.SlotRecords[0].ItemValueSerialization="7";saved.Inventory.SlotRecords[0].Locked=false;saved.Inventory.SlotRecords[0].Reserved=false;saved.Inventory.SlotRecords[1].ItemCount=0;saved.Inventory.SlotRecords[1].ItemValueSerialization="";saved.Inventory.SlotRecords[1].Reserved=true;
  Check(!RebirthDogInventoryService.CommitLogisticsOriginalSlots(nextRecord,nextLeg,true,()=>true,out _,out _)&&saved.Inventory.SlotRecords[0].ItemCount==4&&RebirthNpcAggregatePersistenceStore.Writes==3,"late candidate cell refusal publishes none of an earlier planned cell");saved.Inventory.SlotRecords[1].Reserved=false;
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(nextRecord,nextLeg,true,()=>true,out replay,out _)&&!replay&&RebirthNpcAggregatePersistenceStore.Current.Inventory.SlotRecords[0].ItemCount==5&&RebirthNpcAggregatePersistenceStore.Writes==4,"original retry publishes both candidate cells atomically");
  Check(RebirthDogInventoryService.CommitLogisticsOriginalSlots(nextRecord,nextLeg,true,()=>true,out replay,out _)&&replay&&RebirthNpcAggregatePersistenceStore.Writes==4,"published candidate retry cannot add either cell twice");
  Console.WriteLine("PASS actual dog receipt/stage/CAS fixture (native aggregate and codec doubles)");
 }
}
