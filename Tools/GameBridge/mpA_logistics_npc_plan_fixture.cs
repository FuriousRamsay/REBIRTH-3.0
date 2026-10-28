// Actual production mutation method, with disclosed native world/identity,
// journal admission, immutable metadata codec and persistence-status doubles.
// Does not prove actual journal/file/network publication or Harmony behavior.
public enum RebirthNpcInventoryTransactionResult{Applied,Replayed,InvalidRequest,CapabilityDenied,RevisionConflict,InsufficientQuantity,ReservationConflict,NotFound,AuthorityDenied}
public enum RebirthNpcInventoryAuthorityOperations{Transfer,Mutate}
public enum QuickStackRadialAction{Deposit,CompanionInventoryPull}
public struct RebirthNpcStableId{
 public string Text;public bool IsEmpty=>string.IsNullOrEmpty(Text);public override string ToString()=>Text;
 public static bool operator==(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Text==b.Text;public static bool operator!=(RebirthNpcStableId a,RebirthNpcStableId b)=>!(a==b);
 public override bool Equals(object value)=>value is RebirthNpcStableId id&&id==this;public override int GetHashCode()=>Text?.GetHashCode()??0;
}
public struct V3{public float sqrMagnitude;public static V3 operator-(V3 a,V3 b)=>new V3();}
public class Entity{public World world;public int entityId;public V3 position;public bool Dead;public bool IsDead()=>Dead;}
public class EntityPlayer:Entity{public int Died;}
// The death-custody protocol is a separate native integration fixture scope.
// This mutation fixture keeps its original unchanged-life authorization double.
public static class RebirthLogisticsDeathSettlement{internal static bool ActorCurrent(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)=>!actor.IsDead()&&actor.Died==original.Deaths&&RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation);internal static bool MatchesPaidSourceContinuation(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)=>false;}
// Independent custody inputs; no native trade/metabolism/action permit is issued.
internal static class RebirthNpcTradeService{internal static bool Held;internal static bool BlocksOwnerPreparedNpc(RebirthNpcStableId id)=>Held;}
internal static class RebirthNpcMetabolismCustody{internal static bool Held;internal static bool Blocks(RebirthNpcStableId id)=>Held;}
internal static class RemoteResourceTransactions{internal static bool Held;internal static bool ActionBlocksNpc(RebirthNpcStableId id)=>Held;}
public static class RebirthOriginalActionScope{public static bool Matches(EntityPlayer actor,string owner,string creation)=>creation=="creation";} // Native/survivor identity codec is disclosed separately.
public class EntityRebirthNPC:Entity{}
public sealed class World{public bool Remote;public Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();public bool IsRemote()=>Remote;public Entity GetEntity(int id)=>Entities.TryGetValue(id,out var value)?value:null;}
public sealed class GameManager{public static GameManager Instance=new GameManager();public World World;public Players GetPersistentPlayerList()=>new Players();}
public sealed class Players{public Persistent GetPlayerDataFromEntityID(int id)=>new Persistent();}
public sealed class Persistent{public object PrimaryId=new object();}
public static class ThreadManager{public static bool IsMainThread()=>true;}
public sealed class Identity{public string StorageKey=new string('a',64);}
public static class RebirthStablePlayerIdentity{public static bool TryResolveServerEntity(EntityPlayer actor,out Identity identity){identity=new Identity();return true;}}
public static class RebirthCamperCharacterAccess{public static string CurrentCreation(EntityPlayer actor)=>"creation";}
public static class RebirthNpcRuntimeRegistry{public static bool TryGetEntityId(RebirthNpcStableId id,out int entity){entity=2;return !id.IsEmpty;}}
public static class RemoteNpcResourceSource{public static bool IsAuthorized(EntityRebirthNPC npc,EntityPlayer actor,bool exact,out string reason){reason="";return true;}}
public static class RebirthNpcInteractionSessionService{public static bool IsInventoryAccessing(RebirthNpcStableId id)=>false;}
public static class RebirthCompanionInventoryLockService{public static bool IsNpcItemLocked(RebirthNpcStableId id,string key)=>false;}
public static class RebirthNpcInventoryAuthorityService{public static bool Validate(string key,RebirthNpcInventoryAuthorityOperations purpose,RebirthNpcStableId id)=>key=="transfer"&&purpose==RebirthNpcInventoryAuthorityOperations.Transfer;}
public static class LogisticsPreviewService{public const float CompanionRadiusForPlanning=30;}
public sealed class RebirthCompanionListEntry{public string StableId;public bool IsDog;}
public static class RebirthCompanionService{public static bool TryResolveInventoryTransferTarget(World world,int id,object user,string target,out RebirthCompanionListEntry entry){entry=null;return false;}}
public static class RebirthNpcInventoryPersistenceStore{public static int Dirty;public static bool IsTransferLoaded=>true;public static void EnsureLoaded(){}public static void MarkDirty(){Dirty++;}}
public sealed class ItemStack{public string Key="ammo";public int count,Quality;public ItemStack Clone()=>new ItemStack{Key=Key,count=count,Quality=Quality};}
internal sealed class RebirthNpcNativeStackRecord{
 internal Guid StackId;internal string Owner,ItemKey;internal int Count,Quality;
 internal XElement Write()=>new XElement("nativeStack",new XAttribute("id",StackId.ToString("N")),new XAttribute("owner",Owner),new XAttribute("item",ItemKey),new XAttribute("count",Count),new XAttribute("quality",Quality));
 internal static bool TryRead(XElement node,out RebirthNpcNativeStackRecord record){record=new RebirthNpcNativeStackRecord{StackId=Guid.ParseExact((string)node.Attribute("id"),"N"),Owner=(string)node.Attribute("owner"),ItemKey=(string)node.Attribute("item"),Count=(int)node.Attribute("count"),Quality=(int)node.Attribute("quality")};return true;}
 internal static bool TryCapture(Guid id,RebirthNpcStableId owner,ItemStack stack,out RebirthNpcNativeStackRecord record){record=new RebirthNpcNativeStackRecord{StackId=id,Owner=owner.ToString(),ItemKey=stack.Key,Count=stack.count,Quality=stack.Quality};return id!=Guid.Empty&&stack.count>0;}
}
internal sealed class RebirthNpcNativeStackSet{
 internal string Owner;internal uint Revision;internal List<RebirthNpcNativeStackRecord> Records=new List<RebirthNpcNativeStackRecord>();
 internal XElement Write()=>new XElement("nativeStacks",new XAttribute("owner",Owner),new XAttribute("revision",Revision),Records.OrderBy(record=>record.StackId.ToString("N")).Select(record=>record.Write()));
 internal bool FitsQuantities(Dictionary<string,int> quantities)=>Records.GroupBy(record=>record.ItemKey).All(group=>quantities.TryGetValue(group.Key,out int count)&&count>=group.Sum(record=>record.Count));
 internal int GetQuantity(string key)=>Records.Where(record=>record.ItemKey==key).Sum(record=>record.Count);
 internal RebirthNpcNativeStackSet WithRevision(uint revision)=>new RebirthNpcNativeStackSet{Owner=Owner,Revision=revision,Records=new List<RebirthNpcNativeStackRecord>(Records)};
 internal static bool TryCreate(RebirthNpcStableId id,uint revision,IEnumerable<RebirthNpcNativeStackRecord> records,out RebirthNpcNativeStackSet set){set=new RebirthNpcNativeStackSet{Owner=id.ToString(),Revision=revision,Records=records.ToList()};return set.Records.Select(record=>record.StackId).Distinct().Count()==set.Records.Count;}
 internal static bool TryRead(XElement node,out RebirthNpcNativeStackSet set){set=new RebirthNpcNativeStackSet{Owner=(string)node.Attribute("owner"),Revision=(uint)node.Attribute("revision")};foreach(var child in node.Elements()){if(!RebirthNpcNativeStackRecord.TryRead(child,out var record))return false;set.Records.Add(record);}return true;}
}
internal static class RebirthLogisticsTransferJournal{
 internal sealed class NpcEntry{internal string Key;internal int Before,Reserved,Custodied,PendingNative,After;internal bool Locked;}
 internal sealed class NpcCredit{internal Guid StackId;internal string Key;internal ItemStack Stack;}
 internal sealed class NpcLeg{internal string StableId,NativeBefore;internal uint Revision;internal int Published,Attempted=1;internal Guid DebitId,CreditId;internal List<NpcEntry> Entries=new List<NpcEntry>();internal List<NpcCredit> NativeCredits=new List<NpcCredit>();}
 internal sealed class Leg{internal int Published;internal bool Restored;}
 internal sealed class Record{internal Guid Operation=Guid.NewGuid();internal string Owner=new string('a',64),Creation="creation",OwnerDebitProof=new string('b',64),Target="N:target";internal int Phase=1,Deaths,Outcome;internal QuickStackRadialAction Action;internal List<Leg> Legs=new List<Leg>();internal List<NpcLeg> Npcs=new List<NpcLeg>();}
 internal static Record Retained;
 internal static bool TryRead(string owner,Guid operation,out Record record){record=Retained;return record!=null&&record.Owner==owner&&record.Operation==operation;}
 internal static string Encode(Record record)=>record.Operation.ToString("N"); // Only admission identity is doubled here.
}
// Typed receipt codec itself has a separate actual-source fixture. This double
// isolates mutation/replay control flow, not production receipt wire validation.
internal static class RebirthLogisticsNpcEffectReceipt{
 internal const int MaximumReceipts=128;
 internal static string ExpectedLogistics(RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.NpcLeg leg,bool credit,uint? revision=null)=>record.Operation.ToString("N")+":"+leg.StableId+":"+(credit?leg.CreditId:leg.DebitId).ToString("N")+":"+(revision??leg.Revision+(credit?2U:1U));
 internal static bool MatchesLogistics(RebirthLogisticsTransferJournal.Record record,RebirthLogisticsTransferJournal.NpcLeg leg,bool credit,string receipt){var parts=receipt.Split(':');return uint.TryParse(parts.Last(),out uint revision)&&(credit?revision>=leg.Revision+2:revision==leg.Revision+1)&&ExpectedLogistics(record,leg,credit,revision)==receipt;}
 internal static bool Validate(RebirthNpcStableId id,uint revision,IDictionary<Guid,string> effects)=>effects.Count<=128;
}
internal static class RebirthNpcOriginalEffectAdmission{internal static bool IsReservedLogistics(RebirthLogisticsTransferJournal.Record record,RebirthNpcStableId id)=>true;}
public static class RebirthNpcInventoryTransactionService{
 private sealed class InventoryState{internal uint Revision;internal RebirthNpcNativeStackSet NativeStacks;internal Dictionary<string,int> Quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase),Reservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);internal HashSet<Guid> AppliedTransactions=new HashSet<Guid>();internal Dictionary<Guid,string> LogisticsEffects=new Dictionary<Guid,string>();}
 private static object Sync=new object(),generationEpoch=new object();private static int applied;private static InventoryState current;
 private static InventoryState GetOrCreate(RebirthNpcStableId id)=>current;
 private static void RememberTransaction(InventoryState state,Guid id){state.AppliedTransactions.Add(id);}
 private static bool savedDebit=true;
 private static bool HasSavedLogisticsDebit(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsTransferJournal.NpcLeg leg,RebirthNpcStableId id)=>savedDebit; // Positive primary publication is explicitly doubled here.
 /* ACTUAL_NPC_PLAN_METHOD */
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  var world=new World();GameManager.Instance.World=world;var actor=new EntityPlayer{entityId=1,world=world};var npc=new EntityRebirthNPC{entityId=2,world=world};world.Entities.Add(1,actor);world.Entities.Add(2,npc);var id=new RebirthNpcStableId{Text=new string('1',32)};
  var record=new RebirthLogisticsTransferJournal.Record();var leg=new RebirthLogisticsTransferJournal.NpcLeg{StableId=id.ToString(),Revision=4,DebitId=Guid.NewGuid(),CreditId=Guid.NewGuid()};leg.Entries.Add(new RebirthLogisticsTransferJournal.NpcEntry{Key="ammo",Before=0,After=2,PendingNative=2});var originalStack=new ItemStack{count=2,Quality=7};leg.NativeCredits.Add(new RebirthLogisticsTransferJournal.NpcCredit{Key="ammo",StackId=Guid.NewGuid(),Stack=originalStack});record.Npcs.Add(leg);record.Legs.Add(new RebirthLogisticsTransferJournal.Leg());RebirthLogisticsTransferJournal.Retained=record;current=new InventoryState{Revision=4};
  Check(ApplyLogisticsOriginalPlan(actor,record,id,false,"mutate",out _)==RebirthNpcInventoryTransactionResult.AuthorityDenied&&current.Revision==4,"Transfer does not accept a generic Mutate key");
  for(int gate=0;gate<3;gate++){
   RebirthNpcTradeService.Held=gate==0;RebirthNpcMetabolismCustody.Held=gate==1;RemoteResourceTransactions.Held=gate==2;
   Check(ApplyLogisticsOriginalPlan(actor,record,id,false,"transfer",out _)==RebirthNpcInventoryTransactionResult.ReservationConflict&&current.Revision==4&&current.Quantities.Count==0,"independent custody hold bypassed "+gate);
  }
  RebirthNpcTradeService.Held=RebirthNpcMetabolismCustody.Held=RemoteResourceTransactions.Held=false;
  string proof=record.OwnerDebitProof;record.OwnerDebitProof="";Check(ApplyLogisticsOriginalPlan(actor,record,id,false,"transfer",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest,"no source mutation without retained owner-debit witness");record.OwnerDebitProof=proof;
  Check(ApplyLogisticsOriginalPlan(actor,record,id,false,"transfer",out _)==RebirthNpcInventoryTransactionResult.Applied&&current.Revision==5&&current.Quantities.Count==0&&current.NativeStacks==null,"original debit stage remembered before native destination credits");
  Check(ApplyLogisticsOriginalPlan(actor,record,id,false,"transfer",out _)==RebirthNpcInventoryTransactionResult.Replayed&&current.Revision==5,"exact debit postimage replays once");leg.Published=1;leg.Attempted=2;
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest&&current.Revision==5,"unpublished source endpoint cannot credit native NPC custody");record.Legs[0].Published=1;
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.Applied&&current.Revision==6&&current.Quantities["ammo"]==2&&current.NativeStacks.Records.Single().Quality==7,"paid original native quality retained with semantic credit");
  Check(current.NativeStacks.Records.Single().StackId==leg.NativeCredits[0].StackId&&RebirthNpcInventoryPersistenceStore.Dirty==2,"original stable stack ID and two runtime stages retained");
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.Replayed&&current.Quantities["ammo"]==2&&RebirthNpcInventoryPersistenceStore.Dirty==2,"exact native credit retry never doubles quantity");
  current.Quantities["ammo"]=3;string actualEffect=current.LogisticsEffects[leg.CreditId];current.LogisticsEffects.Remove(leg.CreditId);
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest&&current.Quantities["ammo"]==3,"generic GUID alone cannot overwrite unknown changed postimage");
  current.LogisticsEffects.Add(leg.CreditId,actualEffect);current.Revision=7;current.NativeStacks=current.NativeStacks.WithRevision(7);
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.Replayed&&current.Quantities["ammo"]==3&&RebirthNpcInventoryPersistenceStore.Dirty==2,"typed original effect survives later legitimate mutation without replay");actor.Died=1;
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.InvalidRequest,"new-life owner never receives old bag image through ordinary actor path");
  actor.Died=0;current=new InventoryState{Revision=7};current.Quantities.Add("ammo",3);current.Quantities.Add("other",1);current.Reservations.Add("ammo",2);
  RebirthNpcNativeStackRecord.TryCapture(Guid.NewGuid(),id,new ItemStack{Key="other",count=1,Quality=9},out var unrelated);RebirthNpcNativeStackSet.TryCreate(id,7,new[]{unrelated},out current.NativeStacks);
  current.LogisticsEffects.Add(leg.DebitId,RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(record,leg,false));current.AppliedTransactions.Add(leg.DebitId);
  savedDebit=false;Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.RevisionConflict&&current.Quantities["ammo"]==3,"later work cannot replace positive saved original debit proof");savedDebit=true;
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _,true)==RebirthNpcInventoryTransactionResult.Applied&&current.Revision==7&&current.Quantities["ammo"]==3,"continuation preflight has no quantity effect");
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out uint continuedRevision)==RebirthNpcInventoryTransactionResult.Applied&&continuedRevision==8&&current.Quantities["ammo"]==5&&current.Quantities["other"]==1&&current.Reservations["ammo"]==2,"only original positive credit added after later work");
  Check(current.NativeStacks.Records.Any(value=>value.StackId==unrelated.StackId&&value.Quality==9)&&current.NativeStacks.Records.Any(value=>value.StackId==leg.NativeCredits[0].StackId&&value.Quality==7),"current unrelated and original paid native metadata both preserved");
  Check(ApplyLogisticsOriginalPlan(actor,record,id,true,"transfer",out _)==RebirthNpcInventoryTransactionResult.Replayed&&current.Quantities["ammo"]==5,"continued original credit typed receipt prevents duplicate delivery");
  Console.WriteLine("PASS actual NPC original-plan runtime mutation with disclosed native/journal/codec doubles; publication not certified");
 }
}
