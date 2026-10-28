// AUTHORED_NOT_RUN. Actual return coordinator/journal; native NPC/save/codec adapters explicit.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Security.Cryptography;
// ACTUAL_NPC_RETURN
struct RebirthNpcStableId {
 internal string Value;internal bool IsEmpty=>Value==null;public override string ToString()=>Value;
 public static bool operator ==(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Value==b.Value;public static bool operator !=(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Value!=b.Value;
 public override bool Equals(object o)=>o is RebirthNpcStableId id&&id==this;public override int GetHashCode()=>Value?.GetHashCode()??0;
}
class RuntimeState {internal RebirthNpcStableId StableId;}
class WorldState {internal string Guid=System.Guid.NewGuid().ToString("N");}
class World {internal EntityPlayer Actor;internal Npc Npc;internal WorldState worldState=new WorldState();internal bool IsRemote()=>false;internal EntityPlayer GetPrimaryPlayer()=>Actor;internal object GetEntity(int id)=>id==1?(object)Actor:id==2?Npc:null;}
class EntityPlayer {internal World world;internal int entityId=1,Died;internal bool Dead;internal string Owner="owner",Creation="creation";internal bool IsDead()=>Dead;}
class Npc {internal World world;internal int entityId=2;internal bool Dog,Dead;internal RuntimeState RebirthRuntimeState=new RuntimeState{StableId=new RebirthNpcStableId{Value="npc"}};internal bool IsDead()=>Dead;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class GameIO {internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class RebirthCookingRemoteAdmission {internal static XElement ReadProtocolXml(string text,int maximum){if(System.Text.Encoding.UTF8.GetByteCount(text)>maximum)throw new IOException();return XElement.Parse(text);}}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
static class RebirthNpcRecoveryGeneration {internal static ulong Value=1;internal static ulong RequireCurrent()=>Value;}
class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{CanonicalId=actor.Owner};return actor.Owner!=null;}}
class StackLimit {internal int Value=10;}
class ItemClass {internal StackLimit Stacknumber=new StackLimit();internal string GetItemName()=>"ingredient";internal static ItemValue GetItem(string key,bool ignore)=>new ItemValue();}
class ItemTag {internal string Value="plain";}
class ItemValue {internal int type=1;internal ItemTag Mutable=new ItemTag();internal string Tag{get=>Mutable.Value;set=>Mutable.Value=value;}internal ItemClass ItemClass=new ItemClass();internal ItemValue Clone()=>new ItemValue{type=type,Mutable=Mutable};}
class ItemStack {internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int number){itemValue=value;count=number;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack[] Clone(ItemStack[] slots)=>slots.Select(slot=>slot.Clone()).ToArray();}
class PackedBoolArray {internal bool[] Values;internal PackedBoolArray(int size){Values=new bool[size];}internal int Length=>Values.Length;internal bool this[int index]{get=>Values[index];set=>Values[index]=value;}}
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.type==b.itemValue.type&&a.itemValue.Tag==b.itemValue.Tag);}
static class RebirthNativeItemCodec {internal static string Encode(ItemValue value)=>value.type+":"+value.Tag;}
static class RebirthCamperInventorySnapshot {
 internal static byte[] Capture(ItemStack[] slots)=>System.Text.Encoding.UTF8.GetBytes(string.Join(";",slots.Select(slot=>slot.count+":"+slot.itemValue.Tag)));
 internal static ItemStack[] Restore(byte[] bytes)=>System.Text.Encoding.UTF8.GetString(bytes).Split(';').Select(text=>{var row=text.Split(':');return new ItemStack(new ItemValue{Tag=row[1]},int.Parse(row[0]));}).ToArray();
}
class RebirthNpcNativeStackSet {internal uint Revision;internal RebirthNpcNativeStackSet WithRevision(uint revision)=>new RebirthNpcNativeStackSet{Revision=revision};internal XElement Write()=>new XElement("native",new XAttribute("revision",Revision));}
class RebirthNpcInventorySnapshot {
 internal RebirthNpcStableId NpcId;internal uint Revision;internal Dictionary<string,int> Quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase),Reservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
 internal Dictionary<Guid,string> LogisticsEffects=new Dictionary<Guid,string>();internal RebirthNpcNativeStackSet NativeStacks;
 internal RebirthNpcInventorySnapshot Clone()=>new RebirthNpcInventorySnapshot{NpcId=NpcId,Revision=Revision,Quantities=new Dictionary<string,int>(Quantities,StringComparer.OrdinalIgnoreCase),Reservations=new Dictionary<string,int>(Reservations,StringComparer.OrdinalIgnoreCase),LogisticsEffects=new Dictionary<Guid,string>(LogisticsEffects),NativeStacks=NativeStacks?.WithRevision(Revision)};
}
class RebirthNpcInventoryPersistentRecord:RebirthNpcInventorySnapshot {internal Guid[] ReplayJournal;}
class RebirthDogInventorySnapshot {
 internal ItemStack[] Slots;internal PackedBoolArray Locks;internal uint Revision;
 internal RebirthDogInventorySnapshot Clone(){var result=new RebirthDogInventorySnapshot{Slots=ItemStack.Clone(Slots),Locks=new PackedBoolArray(Slots.Length),Revision=Revision};for(int i=0;i<Slots.Length;i++)result.Locks[i]=Locks[i];return result;}
}
class DogSlotRecord {internal bool Reserved,Locked;}
class DogInventoryRecord {internal uint InventoryRevision;internal DogSlotRecord[] SlotRecords;}
class NpcIdentity {internal RebirthNpcStableId StableNpcId;}
class RebirthNpcPersistentRecord {
 internal RebirthDogInventorySnapshot Dog;internal bool[] Reserved=new bool[2];internal RebirthNpcStableId StableId=new RebirthNpcStableId{Value="npc"};
 internal NpcIdentity Identity=>new NpcIdentity{StableNpcId=StableId};internal string[] UnknownOptionalElements=Array.Empty<string>();
 internal DogInventoryRecord Inventory=>Dog==null?null:new DogInventoryRecord{InventoryRevision=Dog.Revision,SlotRecords=Enumerable.Range(0,Dog.Slots.Length).Select(i=>new DogSlotRecord{Locked=Dog.Locks[i],Reserved=Reserved[i]}).ToArray()};
}
static class Native {
 internal static RebirthNpcInventorySnapshot Logical;internal static RebirthDogInventorySnapshot Dog;
 internal static RebirthNpcInventoryPersistentRecord SavedLogical;internal static RebirthNpcPersistentRecord SavedDog;
 internal static string[] DogFragments=Array.Empty<string>();
 internal static bool[] DogReserved=new bool[2];
 internal static bool Save=true,Authorized=true,Busy;internal static int Calls;internal static uint Expected;internal static Guid Mutation;internal static string Mode="normal";
 internal static void Enter(Guid operation,uint expected){
  Calls++;Expected=expected;Mutation=operation;
  string path=Path.Combine(GameIO.Root,"RebirthData","CookingIngredientReturns",operation.ToString("N")+".intent.xml");
  if(!File.Exists(path)||XElement.Load(path).Attribute("operation").Value!=operation.ToString("N"))throw new Exception("native mutation preceded durable original");
 }
}
class RemoteNpcResourceSource {
 internal enum CommitDisposition {NotInvoked,Refused,Published,Unknown}
 internal sealed class CommitEvent {internal RemoteNpcResourceSource Source;internal Guid Mutation;internal uint BeforeRevision,AfterRevision;internal CommitDisposition Disposition;}
 internal Npc NativeActor;internal RebirthNpcStableId NativeIdentity=>NativeActor.RebirthRuntimeState.StableId;
 internal bool IsDogStorage=>NativeActor.Dog;internal uint OriginalRevision;internal ItemStack[] Slots;internal PackedBoolArray SlotLocks;internal CommitEvent LastCommitEvent;
 internal RemoteNpcResourceSource(Npc actor){NativeActor=actor;OriginalRevision=actor.Dog?Native.Dog.Revision:Native.Logical.Revision;Slots=ItemStack.Clone(Native.Dog.Slots);SlotLocks=new PackedBoolArray(Slots.Length);for(int i=0;i<Slots.Length;i++)SlotLocks[i]=Native.Dog.Locks[i];}
 internal bool IsLoaded=>!NativeActor.Dead;internal bool IsBusy=>Native.Busy;internal bool IsAuthorized(EntityPlayer actor,bool exact,out string reason){reason="";return Native.Authorized;}
 internal void SetSlot(int slot,ItemStack item)=>Slots[slot]=item.Clone();
 internal void MarkModifiedOriginal(Guid operation){
  Native.Enter(operation,OriginalRevision);LastCommitEvent=new CommitEvent{Source=this,Mutation=operation,BeforeRevision=OriginalRevision,AfterRevision=OriginalRevision,Disposition=CommitDisposition.Unknown};
  if(Native.Mode=="refused"){LastCommitEvent.Disposition=CommitDisposition.Refused;return;}
  if(Native.Mode=="mutate-argument")Slots[0].itemValue.Tag="changed-native-argument";
  if(Native.Mode!="suppressed"){
   var beforeCandidate=new RebirthNpcPersistentRecord{Dog=Native.Dog.Clone(),Reserved=(bool[])Native.DogReserved.Clone()};
   if(Native.Mode=="wrong-target-candidate")beforeCandidate.StableId=new RebirthNpcStableId{Value="foreign"};
   if(!RebirthCookingNpcReturn.ValidateDogCandidateBefore(beforeCandidate)){LastCommitEvent.Disposition=CommitDisposition.Refused;return;}
   var candidate=new RebirthNpcPersistentRecord{Dog=new RebirthDogInventorySnapshot{Slots=ItemStack.Clone(Slots),Locks=Native.Dog.Clone().Locks,Revision=OriginalRevision+1},Reserved=(bool[])Native.DogReserved.Clone(),UnknownOptionalElements=(string[])Native.DogFragments.Clone()};
   for(int i=0;i<candidate.Reserved.Length;i++)if(!RebirthCookingNpcReturn.PreserveUnchangedDogSlot(i))candidate.Reserved[i]=false;
   if(!RebirthCookingNpcReturn.StampDogCandidate(candidate)){LastCommitEvent.Disposition=CommitDisposition.Refused;return;}
   Native.Dog=candidate.Dog.Clone();Native.DogReserved=(bool[])candidate.Reserved.Clone();Native.DogFragments=(string[])candidate.UnknownOptionalElements.Clone();
   if(Native.Mode=="mutate-other")Native.Dog.Slots[1]=new ItemStack(new ItemValue{Tag="other"},1);
  }
  if(Native.Mode=="throw-after")throw new IOException("injected missing native return");
  LastCommitEvent.AfterRevision=OriginalRevision+1;LastCommitEvent.Disposition=CommitDisposition.Published;
 }
}
enum RebirthNpcInventoryTransactionResult {Applied,Replayed,InvalidRequest,RevisionConflict}
enum RebirthNpcInventoryAuthorityOperations {Mutate}
class RebirthNpcInventoryAuthorityLease:IDisposable {internal string AuthorityKey="lease";public void Dispose(){}}
static class RebirthNpcInventoryAuthorityService {internal static RebirthNpcInventoryAuthorityLease Issue(string owner,RebirthNpcInventoryAuthorityOperations operations,TimeSpan duration,RebirthNpcStableId id)=>new RebirthNpcInventoryAuthorityLease();}
class RebirthNpcInventoryMutation {internal string Key;internal int Delta;internal RebirthNpcInventoryMutation(string key,int count){Key=key;Delta=count;}}
class RebirthNpcInventoryTransaction {internal Guid Operation;internal uint Revision;internal RebirthNpcInventoryMutation[] Mutations;internal RebirthNpcInventoryTransaction(Guid operation,RebirthNpcStableId id,uint revision,string authority,RebirthNpcInventoryMutation[] mutations){Operation=operation;Revision=revision;Mutations=mutations;}}
static class RebirthNpcInventoryTransactionService {
 internal static RebirthNpcInventorySnapshot GetSnapshot(RebirthNpcStableId id)=>Native.Logical.Clone();
 internal static RebirthNpcInventoryTransactionResult Apply(RebirthNpcInventoryTransaction transaction,out uint revision){
  Native.Enter(transaction.Operation,transaction.Revision);revision=Native.Logical.Revision;
  if(Native.Mode=="refused")return RebirthNpcInventoryTransactionResult.RevisionConflict;
  if(Native.Mode!="suppressed"){
   foreach(var mutation in transaction.Mutations){Native.Logical.Quantities.TryGetValue(mutation.Key,out int count);Native.Logical.Quantities[mutation.Key]=count+mutation.Delta;}
   Native.Logical.Revision++;if(Native.Logical.NativeStacks!=null)Native.Logical.NativeStacks=Native.Logical.NativeStacks.WithRevision(Native.Logical.Revision);
  }
  if(Native.Mode=="throw-after")throw new IOException("injected missing native return");
  revision=transaction.Revision+1;return RebirthNpcInventoryTransactionResult.Applied;
 }
}
static class RebirthDogInventoryService {internal static RebirthDogInventorySnapshot GetSnapshot(RebirthNpcStableId id)=>Native.Dog.Clone();}
static class RebirthNpcInventoryPersistenceStore {
 internal static void Save(){if(!Native.Save)return;var current=Native.Logical;Native.SavedLogical=new RebirthNpcInventoryPersistentRecord{NpcId=current.NpcId,Revision=current.Revision,Quantities=new Dictionary<string,int>(current.Quantities),Reservations=new Dictionary<string,int>(current.Reservations),LogisticsEffects=new Dictionary<Guid,string>(current.LogisticsEffects),NativeStacks=current.NativeStacks?.WithRevision(current.Revision),ReplayJournal=new[]{Native.Mutation}};}
 internal static bool TryReadSavedTransferSnapshot(RebirthNpcStableId id,out RebirthNpcInventoryPersistentRecord saved){saved=Native.SavedLogical;return saved!=null;}
 internal static bool ObserveSavedTransferSnapshot(RebirthNpcStableId id,Func<RebirthNpcInventoryPersistentRecord,bool> predicate)=>TryReadSavedTransferSnapshot(id,out var saved)&&predicate(saved);
}
static class RebirthNpcAggregatePersistenceStore {
 internal static void Save(){if(Native.Save){var frozen=Native.Dog.Clone();frozen.Slots=RebirthCamperInventorySnapshot.Restore(RebirthCamperInventorySnapshot.Capture(Native.Dog.Slots));Native.SavedDog=new RebirthNpcPersistentRecord{Dog=frozen,Reserved=(bool[])Native.DogReserved.Clone(),UnknownOptionalElements=(string[])Native.DogFragments.Clone()};}}
 internal static bool TryReadSavedLogisticsDog(RebirthNpcStableId id,out RebirthNpcPersistentRecord saved){saved=Native.SavedDog;return saved!=null;}
 internal static bool ObserveSavedLogisticsDog(RebirthNpcStableId id,Func<RebirthNpcPersistentRecord,bool> predicate)=>TryReadSavedLogisticsDog(id,out var saved)&&predicate(saved);
}
static class RebirthRemoteResourceDogTransfer {internal static bool Matches(RebirthNpcPersistentRecord record,ItemStack[] slots,bool[] locks,uint revision)=>record?.Dog!=null&&record.Dog.Revision==revision&&slots.Select((slot,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(slot,record.Dog.Slots[index])&&record.Dog.Locks[index]==locks[index]).All(value=>value);}
static class RebirthCompanionInventoryLockService {internal static bool IsNpcItemLocked(RebirthNpcStableId id,string key)=>false;}
static class RebirthRemoteResourceSourceCustody {internal static bool BlocksSource(RemoteNpcResourceSource source)=>false;}
static class RebirthNpcMetabolismCustody {internal static bool Blocks(RebirthNpcStableId id)=>false;}
static class RemoteResourceTransactions {
 internal static bool ActionBlocksNpc(RebirthNpcStableId id)=>false;
 internal static byte[] CaptureOriginalNativeFrame(ItemStack[] slots,ref long remaining){var image=RebirthCamperInventorySnapshot.Capture(slots);if(image.Length>remaining)throw new IOException();remaining-=image.Length;return image;}
 internal static long MeasureOriginalNativeFrame(byte[] image)=>1024+image.Length;
 internal static bool ActionSameNpc(RebirthNpcInventorySnapshot a,RebirthNpcInventorySnapshot b)=>a!=null&&b!=null&&a.NpcId==b.NpcId&&a.Revision==b.Revision&&a.Quantities.Count==b.Quantities.Count&&a.Quantities.All(pair=>b.Quantities.TryGetValue(pair.Key,out int count)&&count==pair.Value)&&a.Reservations.Count==b.Reservations.Count&&a.Reservations.All(pair=>b.Reservations.TryGetValue(pair.Key,out int count)&&count==pair.Value)&&XNode.DeepEquals(a.NativeStacks?.Write(),b.NativeStacks?.Write())&&a.LogisticsEffects.Count==b.LogisticsEffects.Count&&a.LogisticsEffects.All(pair=>b.LogisticsEffects.TryGetValue(pair.Key,out string value)&&value==pair.Value);
}
static class RebirthProductiveEarnedObservationBudget {sealed class Lease:IDisposable {public void Dispose(){}}internal static bool Available=true;internal static bool TryRetainDetached(long count,out IDisposable lease){lease=Available?new Lease():null;return lease!=null;}internal static bool TryResizeDetached(IDisposable lease,long count)=>Available;}
static class RebirthRemoteResourceDurableJournal {internal static string Digest(string text){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
static class RemoteResourceDiagnostics {internal static void Write(string message){}}
static class Program {
 static EntityPlayer actor;static RemoteNpcResourceSource source;
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static ItemStack Offered(int count=3)=>new ItemStack(new ItemValue(),count);
 static void Setup(bool dog=false){
  Native.Mode="normal";Native.Save=Native.Authorized=true;Native.Busy=false;Native.Calls=0;Native.SavedDog=null;Native.SavedLogical=null;Native.DogFragments=Array.Empty<string>();Native.DogReserved=new bool[2];RebirthProductiveEarnedObservationBudget.Available=true;
  var world=new World();actor=new EntityPlayer{world=world};var npc=new Npc{world=world,Dog=dog};world.Actor=actor;world.Npc=npc;GameManager.Instance.World=world;
  Native.Logical=new RebirthNpcInventorySnapshot{NpcId=npc.RebirthRuntimeState.StableId,Revision=1,NativeStacks=new RebirthNpcNativeStackSet{Revision=1}};
  Native.Dog=new RebirthDogInventorySnapshot{Slots=new[]{Offered(0),Offered(0)},Locks=new PackedBoolArray(2),Revision=1};source=new RemoteNpcResourceSource(npc);
  // The original withdrawal advanced the revision and removed the entire row.
  Native.Logical.Revision=2;Native.Logical.NativeStacks=Native.Logical.NativeStacks.WithRevision(2);Native.Dog.Revision=2;
 }
 static RebirthCookingNpcReturn.Result Return(ref RebirthCookingNpcReturn.Attempt attempt,int count=3)=>RebirthCookingNpcReturn.Restore(actor,source,Offered(count),0,count,ref attempt);
 public static void Main(){
  GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-npc-return-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Root);
  try{Run();}finally{string temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!Path.GetFullPath(GameIO.Root).StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(GameIO.Root).StartsWith("rebirth-npc-return-"))throw new Exception("unsafe fixture cleanup");Directory.Delete(GameIO.Root,true);}
 }
 static void Run(){
  Setup();RebirthCookingNpcReturn.Attempt attempt=null;var result=Return(ref attempt);
  Need(result.Count==3&&!result.Pending&&Native.Expected==2&&source.OriginalRevision==1&&Native.Logical.Quantities["ingredient"]==3,"fully withdrawn logical source did not receive fresh additive deposit");
  Need(File.Exists(Path.Combine(attempt.Directory,attempt.Operation.ToString("N")+".published.xml")),"positive saved source lacked outcome publication");
  Setup(true);attempt=null;result=Return(ref attempt);Need(result.Count==3&&!result.Pending&&Native.Dog.Revision==3&&Native.Dog.Slots[0].count==3,"fresh dog native revision was not used");
  Setup(true);Native.DogFragments=new[]{"<unrelated note=\"keep\" />"};Native.DogReserved[1]=true;attempt=null;result=Return(ref attempt);
  Need(result.Count==3&&!result.Pending&&Native.DogFragments.Contains("<unrelated note=\"keep\" />")&&Native.DogFragments.Length==2&&Native.DogReserved[1],"native return erased unrelated metadata or reservation");
  RebirthCookingNpcReturn.Attempt next=null;result=Return(ref next,2);
  Need(result.Count==2&&!result.Pending&&Native.Dog.Slots[0].count==5&&Native.DogFragments.Length==2&&Native.Calls==2,"positive old marker could not retire for an independent return");
  Setup(true);Native.Save=false;attempt=null;Return(ref attempt);next=null;result=Return(ref next,2);
  Need(!result.Pending&&result.Count==0&&Native.Calls==1&&Native.Dog.Slots[0].count==3,"pending source admitted another return");
  Native.Save=true;result=Return(ref attempt);Need(result.Count==3&&!result.Pending&&Native.Calls==1,"original pending source failed to settle without another native call");
  foreach(string mode in new[]{"mutate-argument","wrong-target-candidate"}){
   Setup(true);Native.Mode=mode;attempt=null;result=Return(ref attempt);
   Need(!result.Pending&&result.Count==0&&Native.Dog.Slots[0].count==0&&Native.DogFragments.Length==0,"wrong native argument or candidate acquired return authority");
  }
  Setup(true);Native.DogReserved[0]=true;attempt=null;result=Return(ref attempt);Need(!result.Pending&&result.Count==0&&Native.DogReserved[0]&&Native.Dog.Slots[0].count==0,"protected native target changed");
  Setup(true);Native.Dog.Slots[0]=Offered(8);attempt=null;result=Return(ref attempt,5);Need(result.Count==2&&!result.Pending&&Native.Dog.Slots[0].count==10,"partial dog capacity lost quantity");
  foreach(bool dog in new[]{false,true}){
   Setup(dog);Native.Mode="refused";attempt=null;result=Return(ref attempt);Need(!result.Pending&&result.Count==0&&Native.Calls==1,"explicit unchanged refusal acknowledged items");
   foreach(string mode in new[]{"suppressed","throw-after"}){
    Setup(dog);Native.Mode=mode;attempt=null;result=Return(ref attempt);Need(result.Pending&&result.Count==0&&Native.Calls==1,"unknown native effect acknowledged items");
    Native.Mode="normal";result=Return(ref attempt);
    Need(Native.Calls==1&&(mode=="throw-after"?!result.Pending&&result.Count==3:result.Pending&&result.Count==0),"lost return did not use its saved original or an unproved effect replayed");
   }
   Setup(dog);Native.Save=false;attempt=null;result=Return(ref attempt);Need(result.Pending&&result.Count==0&&Native.Calls==1,"unsaved native return acknowledged");
   Native.Save=true;result=Return(ref attempt);Need(!result.Pending&&result.Count==3&&Native.Calls==1,"saved metadata retry repeated native deposit or lost quantity");
   Setup(dog);Native.Save=false;attempt=null;Return(ref attempt);
   File.WriteAllText(Path.Combine(attempt.Directory,attempt.Operation.ToString("N")+".published.xml"),"foreign");Native.Save=true;
   result=Return(ref attempt);Need(result.Pending&&Native.Calls==1,"conflicting journal was overwritten or native deposit replayed");
  }
  Setup(true);Native.Mode="mutate-other";attempt=null;result=Return(ref attempt);Need(result.Pending&&result.Count==0,"unrelated dog slot mutation accepted");
  Setup();attempt=null;Native.Authorized=false;result=Return(ref attempt);Need(!result.Pending&&result.Count==0&&Native.Calls==0,"unauthorized return mutated source");
  Setup();attempt=null;RebirthProductiveEarnedObservationBudget.Available=false;result=Return(ref attempt);Need(!result.Pending&&Native.Calls==0,"budget pressure entered native mutation");
  Setup();attempt=null;var rich=Offered();rich.itemValue.Tag="rich";result=RebirthCookingNpcReturn.Restore(actor,source,rich,0,3,ref attempt);Need(!result.Pending&&Native.Calls==0,"logical count inventory erased rich metadata");
  Setup();Native.Save=false;attempt=null;Return(ref attempt);actor.Died++;Native.Save=true;result=Return(ref attempt);Need(result.Pending&&Native.Calls==1,"new life inherited old return authority");
  Setup();Native.Save=false;attempt=null;Return(ref attempt);RebirthNpcRecoveryGeneration.Value++;Native.Save=true;result=Return(ref attempt);Need(result.Pending&&Native.Calls==1,"new recovery generation inherited old return authority");
  foreach(bool dog in new[]{false,true}){
   Setup(dog);var child=Guid.NewGuid();attempt=null;
   int planned=RebirthCookingNpcReturn.PlanBound(source,actor,Offered(),0,3,out uint revision);Need(planned==3&&revision==2,"bound source did not plan fresh revision");
   result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,revision,child,ref attempt);Need(!result.Pending&&result.Count==3&&Native.Calls==1,"bound fresh deposit missing");
   attempt=null;result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,revision,child,ref attempt);Need(!result.Pending&&result.Count==3&&Native.Calls==1,"cold bound outcome repeated deposit");
   actor.Died++;Need(RebirthCookingNpcReturn.TryReadBoundOutcome(actor,child,"npc",Offered(),0,3,revision,actor.Died-1,out result)&&!result.Pending&&result.Count==3&&Native.Calls==1,"later-life saved publication was not metadata-only");
   Need(RebirthCookingNpcReturn.TryReadBoundOutcome(actor,child,"foreign",Offered(),0,3,revision,actor.Died-1,out result)&&result.Pending&&Native.Calls==1,"foreign bound target inherited publication");
   Setup(dog);child=Guid.NewGuid();attempt=null;Native.Save=false;result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,2,child,ref attempt);Need(result.Pending&&Native.Calls==1,"unsaved bound effect acknowledged");
   RebirthCookingNpcReturn.Attempt lost=null;result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,2,child,ref lost);Need(result.Pending&&Native.Calls==1,"cold entered unknown repeated deposit");
   Native.Save=true;result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,2,child,ref attempt);Need(!result.Pending&&result.Count==3&&Native.Calls==1,"warm bound saved witness retry repeated deposit");
   Setup(dog);child=Guid.NewGuid();attempt=null;result=RebirthCookingNpcReturn.RestoreBound(actor,source,Offered(),0,3,1,child,ref attempt);Need(!result.Pending&&result.Count==0&&Native.Calls==0,"changed bound revision called native deposit");
  }
  Console.WriteLine("PASS actual fresh companion return/journal/readback coordinator; native saves/codec/transactions are explicit adapters, gameplay NOT RUN");
 }
}
