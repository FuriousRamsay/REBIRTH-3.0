// AUTHORED_NOT_RUN. Complete actual terminal partial, claim lookup and binary
// journal. Native identity and the already authenticated host proof are adapters.
// No inventory setter or native actor image reader is supplied to this fixture.
using System;using System.IO;using System.Linq;using System.Text;using System.Globalization;using System.Security.Cryptography;using System.Collections.Generic;using System.Xml.Linq;
class World{internal WorldState worldState=new WorldState();internal EntityPlayerLocal Player;internal bool IsRemote()=>true;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player;}
class WorldState{internal string Guid="11111111-1111-1111-1111-111111111111";}
class Buffs{internal float GetCustomVar(string key)=>0;}
class EntityPlayerLocal{internal World world;internal Buffs Buffs=new Buffs();internal int entityId=1,Died=2;internal string Owner="owner",Creation=new string('c',32);internal bool IsSpawned()=>true;internal bool IsDead()=>false;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
interface INetConnection{bool IsDisconnected();}
class Connection:INetConnection{public bool IsDisconnected()=>false;}
class ConnectionManager{internal bool IsServer=false,IsConnected=true;internal INetConnection[] connectionToServer=new INetConnection[]{new Connection()};}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal p)=>p.Creation;}
class RebirthStablePlayerIdentity{
 internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayerLocal p,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{CanonicalId=p.Owner};return true;}
 // ACTUAL_OWNER_KEY
}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner?.Length==64&&creation?.Length==32;}
enum RemoteResourceClientOperation:byte{Craft=1,CookingPull=5}
static class RebirthRemoteResourceDurableJournal{
 // ACTUAL_DIGEST
 // ACTUAL_OPERATION
 private static bool Hex(string value,int size)=>value?.Length==size&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int limit){if(text.Length>limit)throw new InvalidDataException();return XElement.Parse(text);}}
static class RebirthCookingRemoteAdmissionBackend{internal static bool NativePositive;internal static bool Phase(RebirthStablePlayerIdentity identity,string receipt,out float phase){phase=2;return NativePositive;}internal static bool NativePhase(RebirthStablePlayerIdentity identity,string receipt,out float phase,out bool found){phase=0;found=false;return false;}}
class ItemStack{internal int count;internal string Tag="rich";internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack{count=count,Tag=Tag};}
class ActorImage{}
static class RebirthCookingOverflowOriginal{internal static bool Terminal(RebirthCamperTransferJournal.Record record,EntityPlayerLocal player,string owner,string creation,ItemStack[] items,int deaths,int entity)=>throw new Exception("overflow qualification belongs to its independent cohort");}
class XUiC_RebirthCookingWorkspace{internal object CustodyStation=new object();internal ItemStack[] Grid=Enumerable.Range(0,12).Select(i=>new ItemStack()).ToArray();internal int Writes;internal ItemStack[] ReadCustodyGrid()=>Grid;internal void WriteCustodyGrid(int slot,ItemStack item){Writes++;Grid[slot]=item;}internal void CustodyStatus(string message){}}
static class RebirthCookingStationLocator{internal static string Encode(object station)=>"station";}
static class RebirthCookingPull{internal static int Requeries,Completes;internal static long LastRequest;internal static bool MatchesLocalTerminal(EntityPlayerLocal player,long request,string world,string operation,string binding,string child,string gridHash,string ownerProof,string disposition)=>false;internal static bool RequeryGridTerminal(EntityPlayerLocal player,long request){Requeries++;LastRequest=request;return true;}internal static void CompleteGridOriginal(EntityPlayerLocal player,long request){Completes++;LastRequest=request;}}
// ACTUAL_BINARY_JOURNAL
static class RebirthCamperTransferHistoryScope{
 internal class Index{internal RebirthCamperTransferJournal.Record FindPending(Guid key)=>null;internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid key,string owner)=>new RebirthCamperTransferJournal.Record[0];internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid key,string owner)=>new RebirthCamperTransferJournal.Record[0];}
 internal static bool TryGet(RebirthCamperTransferJournal journal,out Index index){index=null;return false;}
}
internal static partial class RebirthCookingGridCustody{
 private class Intent{internal Guid Operation;internal World WorldObject;internal string Owner,Creation,NativeReceipt,Action;internal EntityPlayerLocal Player;internal XUiC_RebirthCookingWorkspace Workspace;internal RebirthCamperTransferJournal.Record Record;internal ItemStack[] GridBefore,GridAfter;internal ActorImage Before=new ActorImage(),After=new ActorImage();internal long FetchId;internal int ServerPhase,Deaths;}
 private static readonly List<Intent> Active=new List<Intent>();
 private static readonly Dictionary<XUiC_RebirthCookingWorkspace,World> RecoveryHolds=new Dictionary<XUiC_RebirthCookingWorkspace,World>();
 private static Guid selected;
 internal static void Select(Guid child)=>selected=child;
 private static Guid Key(EntityPlayerLocal player,string owner,string location)=>Guid.Empty;
 private static RebirthCamperTransferJournal.Record Latest(Guid key)=>Journal().Read(selected);
 // Native actor/grid hydration is explicitly adapted. The actual terminal
 // matcher still checks the complete immutable binary record and host hashes.
 private static Intent Parse(RebirthCamperTransferJournal.Record record,XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player){var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));var before=Enumerable.Range(0,12).Select(i=>new ItemStack()).ToArray();var after=before.Select(i=>i.Clone()).ToArray();after[0].count=2;return new Intent{Operation=record.Operation,Record=record,WorldObject=player.world,Player=player,Workspace=workspace,Owner=(string)root.Attribute("owner"),Creation=record.OwnerCreation,Action=(string)root.Attribute("action"),FetchId=(long)root.Attribute("fetchId"),GridBefore=before,GridAfter=after};}
 private static bool Same(ItemStack[] a,ItemStack[] b)=>a.Length==b.Length&&a.Select((item,index)=>item.count==b[index].count&&item.Tag==b[index].Tag).All(value=>value);
 private static bool Same(ItemStack a,ItemStack b)=>a.count==b.count&&a.Tag==b.Tag;
 private static bool HostSaved(Intent intent,int phase,ActorImage actor)=>throw new Exception("native witness must not replace fresh committed terminal");
 private static bool Saved(Intent intent,int phase,ActorImage actor)=>throw new Exception("native actor read must not run during terminal restoration");
 private static bool CompleteExternal(Guid operation)=>throw new Exception("unrelated external path");
 private static bool RejectUnappliedExternal(Guid operation,EntityPlayerLocal player)=>throw new Exception("unrelated external path");
 // This cohort contains fetched terminal records only. Never fabricate a
 // successful owner/source-return or overflow witness for another record kind.
 private static bool TryRecoverSavedOwnerReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,RebirthCamperTransferJournal.Record record,out bool recovered){recovered=false;if(XElement.Parse(Encoding.UTF8.GetString(record.Before)).Element("ownerReturn")!=null)throw new Exception("owner return excluded from fetched terminal cohort");return false;}
 private static bool TryRecoverSavedSourceReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,RebirthCamperTransferJournal.Record record,out bool recovered){recovered=false;throw new Exception("source return excluded from fetched terminal cohort");}
 private static ItemStack[] OwnerOverflow(RebirthCamperTransferJournal.Record record)=>throw new Exception("overflow excluded from fetched terminal cohort");
 // ACTUAL_GRID_JOURNAL
 // ACTUAL_GRID_RECORDS
 // ACTUAL_HAS_CLAIM
 // ACTUAL_GRID_RECOVER
 private static string Receipt(Guid child)=>"rbCookingGrid_"+child.ToString("N");
 internal static void Seed(EntityPlayerLocal p,Guid child){Active.Clear();Active.Add(new Intent{Operation=child,WorldObject=p.world,Owner=p.Owner,Creation=p.Creation});}
 internal static bool Released=>Active.Count==0;
}
// ACTUAL_TERMINAL_PARTIAL
static class Program{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  foreach(bool prepared in new[]{false,true})for(int variant=0;variant<12;variant++){
   ThreadManager.Main=true;GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-fetched-terminal-"+Guid.NewGuid().ToString("N"));
   var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
   var child=Guid.NewGuid();var key=Guid.NewGuid();
   var grid=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("fetchId",17),new XAttribute("world",world.worldState.Guid),new XAttribute("owner",player.Owner),new XAttribute("creation",player.Creation),new XAttribute("deaths",1),new XAttribute("action","fetched"),
    new XElement("beforeActor","opaque-before"),new XElement("afterActor","opaque-after"),new XElement("beforeGrid","opaque-before-grid"),new XElement("afterGrid","opaque-after-grid"));
   if(variant==4)grid.SetAttributeValue("deaths",3);
   if(variant==5)grid.SetAttributeValue("owner","foreign");
   if(variant==6)grid.SetAttributeValue("world",Guid.NewGuid().ToString());
   string raw=grid.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(raw);
   var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.Root,"REBIRTH","CookingGridCustody"));
   journal.Prepare(child,key,player.Creation,1,bytes,variant==7?new byte[]{1}:bytes);
   if(variant==8)journal.Advance(child,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
   else if(!prepared)journal.Advance(child,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
   string sourceWorld=new string('a',64),binding=new string('b',64);
   string source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,RebirthStablePlayerIdentity.ComputeStorageKey(player.Owner),player.Creation,1,17,RemoteResourceClientOperation.CookingPull).ToString("N");
   string action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child.ToString("N")),new XElement("original",raw)).ToString(SaveOptions.DisableFormatting);
   string hash=RebirthRemoteResourceDurableJournal.Digest(action),proof=RebirthRemoteResourceDurableJournal.Digest(child.ToString("N")+"|2|opaque-after|"+action);
   if(variant==1)hash=new string('f',64);if(variant==2)proof=new string('f',64);if(variant==3)source=Guid.NewGuid().ToString("N");
   if(variant==9)ThreadManager.Main=false;if(variant==10)world.Player=new EntityPlayerLocal{world=world};if(variant==11)player.Creation=new string('d',32);
   RebirthCookingGridCustody.Seed(player,child);
   var before=journal.Read(child);
   bool accepted=RebirthCookingGridCustody.RetireFetchedTerminal(player,17,sourceWorld,source,binding,child.ToString("N"),hash,proof,"grid-owned");
   if(variant==0){
    Need(accepted&&journal.Read(child).State==RebirthCamperTransferJournal.Phase.Applied&&RebirthCookingGridCustody.Released,"exact host terminal must finish only local metadata after old-life native file is gone");
    Need(RebirthCookingGridCustody.HasFetchClaim(player,17,out bool complete)&&complete,"fresh archived terminal must qualify completed child without current native phase");
    SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
    Need(RebirthCookingGridCustody.HasFetchClaim(player,17,out complete)&&!complete,"cold/reconnected terminal file cannot qualify paid grid restoration without a fresh host reply");
    var workspace=new XUiC_RebirthCookingWorkspace();RebirthCookingGridCustody.Select(child);RebirthCookingPull.Requeries=RebirthCookingPull.Completes=0;
    Need(!RebirthCookingGridCustody.Recover(workspace,player)&&workspace.Writes==0&&RebirthCookingPull.Requeries==1&&RebirthCookingPull.LastRequest==17,"cold archived child must requery exact original before any paid grid restoration");
    Need(RebirthCookingGridCustody.RetireFetchedTerminal(player,17,sourceWorld,source,binding,child.ToString("N"),hash,proof,"grid-owned"),"exact terminal retry must be idempotent");
    Need(RebirthCookingGridCustody.HasFetchClaim(player,17,out complete)&&complete,"fresh matching host terminal must requalify only the same child");
    Need(RebirthCookingGridCustody.Recover(workspace,player)&&workspace.Writes==1&&workspace.Grid[0].count==2&&RebirthCookingPull.Completes==1,"fresh exact terminal must restore only retained custom grid custody");
    string final=Path.Combine(GameIO.Root,"REBIRTH","CookingGridFetchedTerminals",child.ToString("N")+".2.rcj");byte[] finalBytes=File.ReadAllBytes(final),damaged=(byte[])finalBytes.Clone();damaged[0]^=1;File.WriteAllBytes(final,damaged);RebirthCookingRemoteAdmissionBackend.NativePositive=true;
    try{Need(RebirthCookingGridCustody.HasFetchClaim(player,17,out complete)&&!complete,"corrupt newest terminal cannot fall back to native phase and authorize paid grid restoration");}
    finally{File.WriteAllBytes(final,finalBytes);RebirthCookingRemoteAdmissionBackend.NativePositive=false;}
   }else Need(!accepted&&journal.Read(child).State==before.State&&!RebirthCookingGridCustody.Released,"mismatched terminal must retain original custody "+variant);
  }
  PublicationFailureCases();
  Console.WriteLine("PASS actual fetched terminal/local journal retirement and cold proof lookup; native source/owner effects and topology NOT_RUN");
 }
 static void PublicationFailureCases(){
  foreach(bool failLocal in new[]{false,true}){
   ThreadManager.Main=true;GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-fetched-terminal-retry-"+Guid.NewGuid().ToString("N"));
   var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
   var child=Guid.NewGuid();var grid=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("fetchId",17),new XAttribute("world",world.worldState.Guid),new XAttribute("owner",player.Owner),new XAttribute("creation",player.Creation),new XAttribute("deaths",1),new XAttribute("action","fetched-return"),new XElement("afterActor","saved-after"));
   string raw=grid.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(raw);
   var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.Root,"REBIRTH","CookingGridCustody"));journal.Prepare(child,Guid.NewGuid(),player.Creation,1,bytes,bytes);journal.Advance(child,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
   string sourceWorld=new string('a',64),binding=new string('b',64),source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,RebirthStablePlayerIdentity.ComputeStorageKey(player.Owner),player.Creation,1,17,RemoteResourceClientOperation.CookingPull).ToString("N");
   string action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child.ToString("N")),new XElement("original",raw)).ToString(SaveOptions.DisableFormatting),hash=RebirthRemoteResourceDurableJournal.Digest(action),proof=RebirthRemoteResourceDurableJournal.Digest(child.ToString("N")+"|2|saved-after|"+action);
   bool fired=false;Action<string,RebirthCamperTransferJournal.Record,bool> failure=(root,record,published)=>{if(!fired&&published&&record.State==RebirthCamperTransferJournal.Phase.Applied&&root.EndsWith(failLocal?"CookingGridCustody":"CookingGridFetchedTerminals",StringComparison.Ordinal)){fired=true;throw new IOException("post-publication interruption");}};
   RebirthCookingGridCustody.Seed(player,child);RebirthCamperTransferJournal.PublicationFinished+=failure;
   try{Need(!RebirthCookingGridCustody.RetireFetchedTerminal(player,17,sourceWorld,source,binding,child.ToString("N"),hash,proof,"refunded")&&fired,"injected metadata interruption must stay retryable");}
   finally{RebirthCamperTransferJournal.PublicationFinished-=failure;}
   Need(RebirthCookingGridCustody.RetireFetchedTerminal(player,17,sourceWorld,source,binding,child.ToString("N"),hash,proof,"refunded")&&RebirthCookingGridCustody.Released,"positive archived terminal must complete metadata-only retry");
  }
 }
}
