// AUTHORED_NOT_RUN. Actual request fields, reader/writer and handler. Native
// identity, fragments, source coordinator and saved terminal service are adapters.
using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using System.Xml.Linq;
class ItemStack{internal int count=2;internal bool IsEmpty()=>false;}
class World{internal WorldState worldState=new WorldState();internal EntityPlayer Actor;internal bool IsRemote()=>false;internal object GetEntity(int id)=>Actor;}
class WorldState{internal string Guid="world";}
class EntityPlayer{internal World world;internal int Died=3;internal bool IsDead()=>false;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
interface INetConnection{bool IsDisconnected();}
class NativeConnection:INetConnection{public bool IsDisconnected()=>false;}
class Peer{internal bool loginDone=true,disconnecting;internal INetConnection[] netConnection=new INetConnection[]{new NativeConnection()};}
class Clients{internal Peer Registered;internal Peer ForEntityId(int id)=>Registered;}
class ConnectionManager{internal bool IsServer=true;internal Clients Clients=new Clients();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
class Identity{internal string CanonicalId="owner",StorageKey="owner-key";}
static class RebirthStablePlayerIdentity{internal static bool TryResolveServerEntity(EntityPlayer p,out Identity i){i=new Identity();return true;}internal static bool TryFromClientInfo(Peer p,out Identity i){i=new Identity();return true;}}
static class RebirthWorldCharacterService{internal static bool TryGetIdentity(EntityPlayer p,out Identity i){i=new Identity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer p)=>"creation";}
static class RebirthSurvivorRequestScope{internal static bool Matches(string a,string b)=>a==b;}
static class RebirthRemoteResourceRepairProtocol{internal static bool SupportsServerActor(EntityPlayer p)=>true;}
static class RebirthCookingRemoteAdmissionBackend{internal static Guid StationKey(string a,string b)=>new Guid("11111111-1111-1111-1111-111111111111");}
static class RebirthRemoteResourceFragments{internal const int ChunkText=1024;internal static bool TryAdd(string scope,Guid frame,string hash,int total,int count,int index,string packed,float now,out byte[] complete){complete=new byte[]{1};return true;}}
static class Time{internal static float realtimeSinceStartup;}
static class RebirthCookingPull{internal static List<ItemStack> DecodeCanonical(string text,int maximum)=>new List<ItemStack>{new ItemStack()};}
static class RebirthCookingCatalogue{internal static bool IsIngredient(ItemStack value)=>true;}
static class RebirthCamperInventorySnapshot{internal static ItemStack[] Restore(byte[] bytes)=>new[]{new ItemStack()};}
enum RemoteResourceClientOperation:byte{CookingPull=5}
static class RebirthRemoteResourceDurableOutcome{internal static bool IsRestoredCookingRefusal(RebirthRemoteResourceDurableJournal.Record record)=>false;}
static class RebirthRemoteResourceDurableJournal{
 internal class Record{internal int Phase=2;internal string World="resource-world",Binding="binding";internal Guid Operation=Guid.NewGuid();internal XElement Copy()=>new XElement("original",new XAttribute("outcome",1),new XElement("reason",""),new XElement("removed",Convert.ToBase64String(new byte[]{1})),new XElement("sources"));}
}
static class RebirthRemoteResourceDurableDispatch{
 internal static int Debits;internal static bool Terminal;
 internal static bool TryReadCookingReplay(EntityPlayer p,long id,List<ItemStack> needs,out RebirthRemoteResourceDurableJournal.Record record){record=Terminal?new RebirthRemoteResourceDurableJournal.Record{Phase=4}:null;return Terminal;}
 internal static bool TryReadCookingMetadata(EntityPlayer p,long id,List<ItemStack> needs,out RebirthRemoteResourceDurableJournal.Record record)=>TryReadCookingReplay(p,id,needs,out record);
 internal static bool Resolve(EntityPlayer p,ulong epoch,ulong id,RemoteResourceClientOperation kind,List<ItemStack> needs,bool query,out RebirthRemoteResourceDurableJournal.Record record){Debits++;record=new RebirthRemoteResourceDurableJournal.Record();return true;}
}
static class NetPackageRebirthCookingPulledOriginal{internal static int Replies;internal static void Send(int player,long request,List<ItemStack> items,string reason,string world,string owner,string creation,string sourceWorld,string operation,string binding,bool terminal,bool noPayment,string ownerAction,string proof,string disposition){Replies++;}}
class PooledBinaryReader:BinaryReader{internal PooledBinaryReader(Stream s):base(s,Encoding.UTF8,true){}}
class PooledBinaryWriter:BinaryWriter{internal PooledBinaryWriter(Stream s):base(s,Encoding.UTF8,true){}}
class PlatformUserIdentifierAbs{internal void ToStream(BinaryWriter writer)=>writer.Write("owner");internal static PlatformUserIdentifierAbs FromStream(BinaryReader reader){if(reader.ReadString()!="owner")throw new InvalidDataException();return new PlatformUserIdentifierAbs();}}
static class RebirthCookingGridCustody{
 // ACTUAL_READ_STRING
 // ACTUAL_WRITE_STRING
}
class NetPackage{internal Peer Sender;internal INetConnection Received;internal int Channel;internal bool ValidEntityIdForSender(int id)=>true;internal bool ValidUserIdForSender(PlatformUserIdentifierAbs user)=>true;public virtual void ProcessPackage(World w,GameManager gm){}public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){w.Write((ushort)123);}}
// Dequeue lineage adapter; the actual collector is composed by mp009_scope.
static class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryGetReceivedConnection(NetPackage packet,out INetConnection connection){connection=packet.Received;return connection!=null;}}
class ActualHandler:NetPackage{
 // ACTUAL_FIELDS
 internal ActualHandler(){playerId=1;user=new PlatformUserIdentifierAbs();id=17;worldId="world";creation="creation";hash=new string('a',64);packed="chunk";total=1;count=1;index=0;}
 internal void SetLife(int life,bool metadata){deaths=life;queryOnly=metadata;}
 internal bool HasLife(int life,bool metadata)=>deaths==life&&queryOnly==metadata;
 // ACTUAL_READ
 // ACTUAL_WRITE
 // ACTUAL_HANDLER
}
class ActualResult:NetPackage{
 // ACTUAL_RESULT_FIELDS
 internal ActualResult(bool refund=false,bool noEffect=false){id=17;error="";worldId="world";owner="owner";creation="creation";packed="chunk";hash=new string('a',64);sourceWorld=new string('b',64);sourceOperation=Guid.NewGuid().ToString("N");sourceBinding=new string('c',64);total=1;count=1;index=0;settled=true;noPayment=noEffect;
  gridChild=noEffect?"":Guid.NewGuid().ToString("N");gridHash=noEffect?"":new string('d',64);ownerProof=new string('e',64);disposition=noEffect?"unused-no-payment":refund?"refunded":"grid-owned";}
 internal string TerminalImage()=>gridChild+"|"+gridHash+"|"+ownerProof+"|"+disposition;
 // ACTUAL_RESULT_READ
 // ACTUAL_RESULT_WRITE
}
static class Program{
 static void Need(bool value,string text){if(!value)throw new Exception(text);}
 static void Main(){
  foreach(int life in new[]{-1,2,3,4})foreach(bool metadata in new[]{false,true})foreach(bool terminal in new[]{false,true}){
   var world=new World();world.Actor=new EntityPlayer{world=world};GameManager.Instance.World=world;
   var peer=new Peer();SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=peer;
   var packet=new ActualHandler{Sender=peer,Received=peer.netConnection[0]};packet.SetLife(life,metadata);
   using(var stream=new MemoryStream()){
    using(var writer=new PooledBinaryWriter(stream)){packet.write(writer);writer.Flush();}
    stream.Position=0;using(var reader=new PooledBinaryReader(stream)){reader.ReadUInt16();packet=new ActualHandler{Sender=peer,Received=peer.netConnection[0]};packet.read(reader);Need(packet.HasLife(life,metadata)&&stream.Position==stream.Length,"actual wire must preserve original life and metadata mode");}
    byte[] old=stream.ToArray();old[5]=0x33;using(var legacy=new MemoryStream(old))using(var reader=new PooledBinaryReader(legacy)){reader.ReadUInt16();bool refused=false;try{new ActualHandler().read(reader);}catch(InvalidDataException){refused=true;}Need(refused,"unscoped v3 request must be refused before admission");}
   }
   RebirthRemoteResourceDurableDispatch.Debits=0;RebirthRemoteResourceDurableDispatch.Terminal=terminal;NetPackageRebirthCookingPulledOriginal.Replies=0;
   packet.ProcessPackage(world,GameManager.Instance);
   bool admit=!terminal&&!metadata&&life==3;
   Need(RebirthRemoteResourceDurableDispatch.Debits==(admit?1:0),"wrong-life or metadata-only request reached fresh withdrawal");
   Need(NetPackageRebirthCookingPulledOriginal.Replies==((terminal||admit)?1:0),"retained terminal/new current-life result routing differs");
   foreach(int stale in new[]{0,1,2,3,4,5,6}){
    RebirthRemoteResourceDurableDispatch.Debits=0;NetPackageRebirthCookingPulledOriginal.Replies=0;
    ThreadManager.Main=stale!=0;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=stale!=1;peer.loginDone=stale!=2;peer.disconnecting=stale==3;
    packet.Received=stale==5?null:stale==6?new NativeConnection():peer.netConnection[0];
    packet.ProcessPackage(world,stale==4?new GameManager():GameManager.Instance);
    Need(RebirthRemoteResourceDurableDispatch.Debits==0&&NetPackageRebirthCookingPulledOriginal.Replies==0,"stale native entry reached terminal/source path");
   }
   ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;peer.loginDone=true;peer.disconnecting=false;
  }
  foreach(int kind in new[]{0,1,2}){
   var result=new ActualResult(kind==1,kind==2);string expected=result.TerminalImage();
   using(var stream=new MemoryStream()){
    using(var writer=new PooledBinaryWriter(stream)){result.write(writer);writer.Flush();}
    stream.Position=0;using(var reader=new PooledBinaryReader(stream)){reader.ReadUInt16();var read=new ActualResult();read.read(reader);Need(read.TerminalImage()==expected&&stream.Position==stream.Length,"actual result wire lost exact terminal child/hash/proof/disposition");}
    byte[] old=stream.ToArray();old[5]=0x33;using(var legacy=new MemoryStream(old))using(var reader=new PooledBinaryReader(legacy)){reader.ReadUInt16();bool refused=false;try{new ActualResult().read(reader);}catch(InvalidDataException){refused=true;}Need(refused,"old result wire cannot omit terminal proof fields");}
   }
  }
  Console.WriteLine("PASS actual cooking request/result wire, life admission and terminal-only requery; native debit/persistence/topology NOT_RUN");
 }
}
