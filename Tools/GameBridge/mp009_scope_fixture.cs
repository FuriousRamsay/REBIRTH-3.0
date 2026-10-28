// AUTHORED_NOT_RUN. Actual request/result handlers, journal, dequeue observer,
// client lease/currentness/admission/query pump. Native consumption/UI/wire are doubles.
using System;using System.Globalization;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Threading;using System.Text;using System.Security.Cryptography;using System.Xml;using System.Xml.Linq;using UnityEngine;using HarmonyLib;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]class HarmonyPatch:Attribute{public HarmonyPatch(Type type,string name){}}}
namespace UnityEngine{static class Time{internal static float unscaledTime,realtimeSinceStartup;}}
static class RebirthLogisticsOriginalTransport{internal static bool IsPurposePackage(NetPackage packet)=>false;}
static class RebirthCamperRemovalParcelTransport{internal static bool IsPacket(NetPackage packet)=>false;}
static class RebirthRemoteResourceNetworkScope{internal static bool IsPacket(NetPackage packet)=>false;}
enum RemoteResourceClientOperation:byte{Craft=1,ItemRepair=2,BlockRepair=3,BlockUpgrade=4,CookingPull=5}
enum RemoteResourceTransactionRequestMode:byte{ExecuteOrReplay=1,QueryOnly=2}
enum RemoteResourceTransactionOutcomeState:byte{Unknown=0,Success=1,Failed=2}
class ItemStack{internal int count;internal ItemStack Clone()=>new ItemStack{count=count};}
static class RebirthCamperInventorySnapshot
{
 internal static bool Oversize;
 internal static byte[] Capture(ItemStack[] values){if(Oversize)return new byte[6*1024*1024+1];using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){foreach(var v in values)w.Write(v.count);w.Flush();return s.ToArray();}}
 internal static ItemStack[] Restore(byte[] bytes){using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){var values=new List<ItemStack>();while(s.Position<s.Length)values.Add(new ItemStack{count=r.ReadInt32()});return values.ToArray();}}
}
// Native identity adapter: only the explicitly provisioned fixture save is known.
// The actual store still reads and validates its own worldIdentity.xml file.
static class RemoteResourceWorldIdentity
{internal static bool TryGet(World world,out string key){key=new string('a',64);return ReferenceEquals(GameManager.Instance.World,world)&&File.Exists(Path.Combine(GameIO.Directory,"RebirthData","RemoteResources","worldIdentity.xml"));}}
// ACTUAL_STORE
class PlatformUserIdentifierAbs{internal string CombinedString="owner";public override bool Equals(object value)=>value is PlatformUserIdentifierAbs p&&p.CombinedString==CombinedString;public override int GetHashCode()=>CombinedString.GetHashCode();}
interface INetConnection{bool IsDisconnected();}
class NetConnectionAbs:INetConnection{internal ClientInfo cInfo;internal bool Dead;internal List<NetPackage> receivedPackages=new List<NetPackage>();public bool IsDisconnected()=>Dead;public void GetPackages(){}}
class ClientInfo{internal int entityId=7;internal bool loginDone=true,disconnecting;internal INetConnection[] netConnection;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer?.entityId==id?Peer:null;}
class ConnectionManager{internal bool IsServer=true,IsConnected=true,ThrowSend;internal int Sends;internal Clients Clients=new Clients();internal INetConnection[] connectionToServer;internal INetConnection[] GetConnectionToServer()=>connectionToServer;internal void SendToServer(NetPackage p){Sends++;if(ThrowSend)throw new IOException("ambiguous native send");}internal List<NetPackageRemoteResourceConsumeResult> Replies=new List<NetPackageRemoteResourceConsumeResult>();internal void SendPackage(NetPackageRemoteResourceConsumeResult p,int _attachedToEntityId){Replies.Add(p);}}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
class EntityPlayer{internal int entityId=7,Died;internal string Creation="life";internal World world;internal bool Dead;internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer{}
class WorldState{internal string Guid="world";}
class World{internal WorldState worldState=new WorldState();internal bool Remote;internal EntityPlayer Player;internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null;internal EntityPlayerLocal GetPrimaryPlayer()=>Player as EntityPlayerLocal;}
class PersistentPlayerData{internal PlatformUserIdentifierAbs PrimaryId=new PlatformUserIdentifierAbs();}
class PersistentPlayers{internal PersistentPlayerData Data=new PersistentPlayerData();internal PersistentPlayerData GetPlayerDataFromEntityID(int id)=>Data;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal PersistentPlayers Players=new PersistentPlayers();internal PersistentPlayers GetPersistentPlayerList()=>Players;internal PersistentPlayerData GetPersistentLocalPlayer()=>Players.Data;}
static class GameIO{internal static string Directory="fixture-save";internal static string GetSaveGameDir()=>Directory;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
class RebirthStablePlayerIdentity{internal string StorageKey="owner";internal static string Owner="owner";internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity id){id=new RebirthStablePlayerIdentity{StorageKey=Owner};return true;}}
static class RebirthDogLifecycleService{internal static bool HasHireInventoryHold(EntityPlayerLocal p)=>false;}
static class RebirthBackpackLibraryReservation{internal static bool BlocksResourceUse(EntityPlayer p)=>false;}
static class RemoteResourcesRuntimePolicy{internal static bool Enabled=true;}
static class RebirthSurvivorMode{internal static bool IsEnabledForCurrentWorld()=>true;}
static class RemoteResourceDiagnostics{internal static void Write(string text){}}
static class RemoteResourceLiveSync{internal static void PushSnapshot(EntityPlayer p,string reason){}}
static class Log{internal static void Warning(string text){}}
class NetPackage{internal int Channel;internal ClientInfo Sender;internal bool ValidEntityIdForSender(int id)=>Sender?.entityId==id;internal bool ValidUserIdForSender(PlatformUserIdentifierAbs id)=>id?.CombinedString=="owner";public virtual void ProcessPackage(World w,GameManager gm){}}
static class NetPackageManager{internal static T GetPackage<T>()where T:new()=>new T();}
static class RemoteResourceTransactions
{internal static int Calls;internal static Action During;internal static bool Throw;internal sealed class ConsumptionResult{internal bool Success;internal string FailureReason;}
 internal static ConsumptionResult TryConsume(EntityPlayer actor,IList<ItemStack> requirements,int multiplier,List<ItemStack> removed,bool local)
 {Calls++;foreach(var s in requirements)removed.Add(s.Clone());var callback=During;During=null;callback?.Invoke();if(Throw)throw new IOException("native setter return unknown");return new ConsumptionResult{Success=true};}}
// ACTUAL_OUTCOME
// ACTUAL_JOURNAL
class NetPackageRemoteResourceConsumeRequest:NetPackage
{internal int playerId=7;internal PlatformUserIdentifierAbs userId=new PlatformUserIdentifierAbs();internal ulong sessionEpoch=1,requestId=1;
 internal RemoteResourceClientOperation operation=RemoteResourceClientOperation.Craft;internal RemoteResourceTransactionRequestMode mode=RemoteResourceTransactionRequestMode.ExecuteOrReplay;
 internal List<ItemStack> requirements=new List<ItemStack>{new ItemStack{count=2}};
 internal NetPackageRemoteResourceConsumeRequest Setup(int player,PlatformUserIdentifierAbs user,ulong epoch,ulong request,RemoteResourceClientOperation kind,RemoteResourceTransactionRequestMode requestMode,List<ItemStack> stacks)
 {playerId=player;userId=user;sessionEpoch=epoch;requestId=request;operation=kind;mode=requestMode;requirements=stacks;return this;}
 // ACTUAL_REQUEST
}
class NetPackageRemoteResourceConsumeResult:NetPackage
{internal ulong sessionEpoch=1,requestId=1;internal RemoteResourceClientOperation operation=RemoteResourceClientOperation.Craft;internal RemoteResourceTransactionOutcomeState outcome=RemoteResourceTransactionOutcomeState.Success;
 internal string reason="";internal List<ItemStack> removed=new List<ItemStack>();
 internal NetPackageRemoteResourceConsumeResult Setup(ulong epoch,ulong request,RemoteResourceClientOperation kind,RemoteResourceTransactionOutcomeState state,string why,List<ItemStack> stacks)
 {sessionEpoch=epoch;requestId=request;operation=kind;outcome=state;reason=why;removed=stacks.Select(s=>s.Clone()).ToList();return this;}
 // ACTUAL_RESULT
}
class NetPackageRebirthMetabolismConsumeRequest:NetPackage{}class NetPackageRebirthNpcConsumptionOwner:NetPackage{}class NetPackageRebirthNpcConsumptionOwnerReturn:NetPackage{}class NetPackageRebirthDogHireOwnerGrant:NetPackage{}class NetPackageRebirthActionPaymentOwnerGrant:NetPackage{}
class NetPackageRebirthCookingPullOriginal:NetPackage{}class NetPackageRebirthCookingPulledOriginal:NetPackage{}class NetPackageRebirthCookingPullNoPayment:NetPackage{}
class NetPackageRebirthCookingGridOwnerWitness:NetPackage{}class NetPackageRebirthCookingGridOwnerWitnessResult:NetPackage{}
class NetPackageRebirthUiQueueInsertionRequestV1:NetPackage{}class NetPackageRebirthUiQueueInsertionReplyV1:NetPackage{}
static class RebirthRemoteResourcePlacedStationOriginal{internal static bool IsCancellationJoinPacket(NetPackage p)=>false;}
static class RebirthProductiveEarnedUiCoordinator{internal static bool IsPendingQueueHandoffNative(NetPackage p)=>false;}
static class RebirthRecyclerPlacedSettlement{internal static bool IsRecyclerPurposePackage(NetPackage p)=>false;}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{private static bool IsHydrationEnvelope(NetPackage p)=>false;}
// ACTUAL_DEQUEUE
static class RemoteResourceClientTransactionCoordinator
{
 // ACTUAL_LEASE
 const int MaxPendingTransactions=128;const float OutcomeQueryDelaySeconds=3f;
 static ulong clientSessionEpoch=1;static readonly Dictionary<ulong,PendingLease> pendingLeases=new Dictionary<ulong,PendingLease>();
 static bool IsDedicatedClient=>SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==false;
 internal static int Deliveries,Queries;
 // ACTUAL_SEND
 // ACTUAL_BEGIN
 // ACTUAL_QUERY
 // Native UI continuation/refund is intentionally not simulated by this fixture.
 internal static void ReceiveResult(ulong epoch,ulong request,RemoteResourceClientOperation kind,RemoteResourceTransactionOutcomeState outcome,string reason,List<ItemStack> items,INetConnection received,int channel)
 {if(pendingLeases.TryGetValue(request,out var lease)&&lease.SessionEpoch==epoch&&lease.Operation==kind&&lease.Current()&&ReferenceEquals(lease.Connection,received)&&channel==0)Deliveries++;}
 internal static void LeaseCases()
 {
  GameIO.Directory="fixture-save";
  pendingLeases.Clear();var actor=new EntityPlayerLocal();var world=new World{Remote=true,Player=actor};actor.world=world;GameManager.Instance.World=world;
  var connection=new NetConnectionAbs();var manager=SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=false,connectionToServer=new INetConnection[]{connection}};
  Program.Need(BeginLease(1,RemoteResourceClientOperation.Craft),"actual lease admitted");var lease=pendingLeases[1];Program.Need(lease.Current(),"actual lease current");
  Program.Need(!BeginLease(1,RemoteResourceClientOperation.BlockRepair)&&ReferenceEquals(pendingLeases[1],lease),"duplicate request cannot replace an existing original lease");
  Program.Need(!BeginLease(0,RemoteResourceClientOperation.Craft)&&!BeginLease(2,(RemoteResourceClientOperation)99),"zero request and unsupported purpose refused before admission");
  actor.Died++;Program.Need(!lease.Current(),"respawn cannot borrow old lease");actor.Died--;
  actor.Creation="other";Program.Need(!lease.Current(),"new character cannot borrow old lease");actor.Creation="life";
  RebirthStablePlayerIdentity.Owner="other";Program.Need(!lease.Current(),"new local owner refused");RebirthStablePlayerIdentity.Owner="owner";
  GameIO.Directory="other-save";Program.Need(!lease.Current(),"changed native save refused");GameIO.Directory="fixture-save";
  world.worldState.Guid="other-world";Program.Need(!lease.Current(),"same World object with changed native GUID refused");world.worldState.Guid="world";
  manager.connectionToServer=new INetConnection[]{new NetConnectionAbs()};Program.Need(!lease.Current(),"replacement connection refused");manager.connectionToServer=new INetConnection[]{connection};
  world.Player=new EntityPlayerLocal{world=world};Program.Need(!lease.Current(),"same numeric entity ID cannot borrow old body");world.Player=actor;
  UnityEngine.Time.unscaledTime=4;UpdatePendingTransactions();Program.Need(manager.Sends==1,"actual original lease queried through real SendRequest");
  actor.world=new World();UnityEngine.Time.unscaledTime=8;UpdatePendingTransactions();Program.Need(manager.Sends==1,"query pump cannot use foreign actor World");actor.world=world;
  Program.Need(!SendRequest(actor,99,RemoteResourceClientOperation.Craft,new List<ItemStack>()),"send without original lease refused");
  Program.Need(!SendRequest(actor,1,RemoteResourceClientOperation.BlockRepair,new List<ItemStack>()),"send with different original purpose refused");
  Program.Need(!SendRequest(actor,1,RemoteResourceClientOperation.Craft,new List<ItemStack>(),RemoteResourceTransactionRequestMode.QueryOnly,2),"send with different original epoch refused");
  Program.Need(manager.Sends==1,"refused sends do not enter native queue");
  manager.ThrowSend=true;try{SendRequest(actor,1,RemoteResourceClientOperation.Craft,new List<ItemStack>(),RemoteResourceTransactionRequestMode.QueryOnly,1);throw new Exception("expected native queue exception");}catch(IOException){}
  manager.ThrowSend=false;Program.Need(pendingLeases.ContainsKey(1)&&manager.Sends==2,"ambiguous native send preserves original lease");
  var reply=new NetPackageRemoteResourceConsumeResult();reply.ProcessPackage(world,GameManager.Instance);Program.Need(Deliveries==0,"unobserved packet cannot deliver");
  Program.Receive(connection,reply);reply.ProcessPackage(world,GameManager.Instance);Program.Need(Deliveries==1,"actual received current server reply delivered");
  GameManager.Instance.World=new World{Remote=true,Player=new EntityPlayerLocal()};reply.ProcessPackage(world,GameManager.Instance);Program.Need(Deliveries==1,"stale native World reply refused");
 }
}
class Program
{
 // Fixture-only pre-cutover publisher. New Resolve never reaches this path.
 // It reconstructs an existing warm publication for historical continuation
 // vectors; actual packet/Resolve identity and native dequeue remain composed.
 static void SeedHistorical(NetPackageRemoteResourceConsumeRequest packet,World world,EntityPlayer actor)
 {
  var type=typeof(RemoteResourceTransactionOutcomeJournal);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
  type.GetMethod("BindWorld",flags).Invoke(null,new object[]{world});
  string key=(string)type.GetMethod("BuildKey",flags).Invoke(null,new object[]{packet.userId,(ulong)1,(ulong)1,packet.operation});
  var pendingType=type.GetNestedType("PendingPublication",System.Reflection.BindingFlags.NonPublic);var pending=Activator.CreateInstance(pendingType,true);var claim=new object();
  var values=new Dictionary<string,object>{{"World",world},{"Actor",actor},{"User",packet.userId},{"Epoch",(ulong)1},{"Request",(ulong)1},{"Kind",packet.operation},{"Requirements",packet.requirements.Select(x=>x.Clone()).ToArray()},
   {"RequirementBytes",RebirthCamperInventorySnapshot.Capture(packet.requirements.ToArray()).Length},{"Claim",claim},{"Key",key},{"Deaths",actor.Died},{"Creation",RebirthCamperCharacterAccess.CurrentCreation(actor)??""},
   {"WorldGuid",world.worldState.Guid},{"Directory",GameIO.GetSaveGameDir()},{"Manager",SingletonMonoBehaviour<ConnectionManager>.Instance},{"Peer",packet.Sender},{"Connection",packet.Sender.netConnection[0]}};
  foreach(var value in values)pendingType.GetField(value.Key,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(pending,value.Value);
  ((System.Collections.IDictionary)type.GetField("InFlight",flags).GetValue(null))[key]=claim;
  ((System.Collections.IDictionary)type.GetField("Publishing",flags).GetValue(null))[key]=pending;
  using(var hash=SHA256.Create())((System.Collections.IDictionary)type.GetField("OriginalRequirements",flags).GetValue(null))[key]=Convert.ToBase64String(hash.ComputeHash(RebirthCamperInventorySnapshot.Capture(packet.requirements.ToArray())));
  type.GetField("pendingRequirementBytes",flags).SetValue(null,Convert.ToInt64(values["RequirementBytes"]));
  type.GetMethod("StepPublication",flags).Invoke(null,new object[]{pending,true});
 }
 internal static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Receive(NetConnectionAbs connection,NetPackage packet)
 {
  var type=typeof(RebirthProductiveEarnedUiDeferredTransport).GetNestedType("ReceiveDequeue",System.Reflection.BindingFlags.NonPublic);
  var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
  connection.receivedPackages.Add(packet);object[] prefix={connection,true,null};type.GetMethod("Prefix",flags).Invoke(null,prefix);
  try{var destination=connection.receivedPackages.ToList();connection.receivedPackages.Clear();type.GetMethod("Postfix",flags).Invoke(null,new object[]{destination,true,prefix[2]});}
  finally{type.GetMethod("Finalizer",flags).Invoke(null,new object[]{null,prefix[2]});}
 }
 static NetPackageRemoteResourceConsumeRequest Server(out World world,out ConnectionManager manager,out EntityPlayer actor)
 {
  GameIO.Directory=@"\\?\"+Path.Combine(Path.GetTempPath(),"rebirth-mp009-original-"+Guid.NewGuid().ToString("N"));
  string identity=Path.Combine(GameIO.Directory,"RebirthData","RemoteResources");Directory.CreateDirectory(identity);
  File.WriteAllText(Path.Combine(identity,"worldIdentity.xml"),"<remoteResourceWorld version=\"1\" key=\""+new string('a',64)+"\" />");
  actor=new EntityPlayer();world=new World{Player=actor};actor.world=world;GameManager.Instance=new GameManager{World=world};
  var peer=new ClientInfo();var connection=new NetConnectionAbs{cInfo=peer};peer.netConnection=new INetConnection[]{connection};
  manager=SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();manager.Clients.Peer=peer;
  return new NetPackageRemoteResourceConsumeRequest{Sender=peer};
 }
 static void Main()
 {
  var packet=Server(out var world,out var manager,out var actor);RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);
  packet.ProcessPackage(world,GameManager.Instance);Need(RemoteResourceTransactions.Calls==0,"request without actual dequeue provenance refused");
  Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==0&&manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Unknown,"fresh native legacy request has no destructive admission or unused proof");
  manager.Replies.Clear();SeedHistorical(packet,world,actor);packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==1&&manager.Replies.Count==1&&manager.Replies[0].outcome==RemoteResourceTransactionOutcomeState.Success,"actual current request retained/replied");
  packet.ProcessPackage(world,GameManager.Instance);Need(RemoteResourceTransactions.Calls==1,"same original replays outcome without debit");
  Need(RemoteResourceTransactionOutcomeJournal.TryGetRetained(world,packet.userId,1,1,packet.operation,out _,out var retained),"retained outcome available");
  var files=Directory.GetFiles(GameIO.Directory,"*.xml",SearchOption.AllDirectories);
  Need(files.Count(p=>p.EndsWith(".original.xml"))==1&&files.Count(p=>p.EndsWith(".entered.xml"))==1&&files.Count(p=>p.EndsWith(".observed.xml"))==1,"actual immutable original, entry fence and observation published");
  Need(!RemoteResourceLegacyOutcomeStore.Entry.Capture(world,actor,packet.userId,1,1,packet.operation,packet.requirements.ToArray(),out _),"existing on-disk original cannot reacquire native invocation authority");
  string observedPath=files.Single(p=>p.EndsWith(".observed.xml")),observedBytes=File.ReadAllText(observedPath);
  File.WriteAllText(observedPath,observedBytes.Replace("<removed>","<removed>AA"));
  Need(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(world,packet.userId,1,1,packet.operation,retained),"changed published observation cannot authorize refund continuation");
  File.WriteAllText(observedPath,observedBytes);
  string originalPath=files.Single(p=>p.EndsWith(".original.xml")),originalBytes=File.ReadAllText(originalPath);
  File.WriteAllText(originalPath,originalBytes+" ");
  Need(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(world,packet.userId,1,1,packet.operation,retained),"changed immutable original invalidates warm continuation even with unchanged observation");
  File.WriteAllText(originalPath,originalBytes);
  var invalidReplay=RemoteResourceTransactionOutcomeJournal.Resolve(world,actor,packet.userId,1,1,packet.operation,new List<ItemStack>(),false);
  Need(invalidReplay.State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==1,"malformed replay cannot manufacture a refundable failure for an existing debit");
  var stale=new World{Player=actor};var result=RemoteResourceTransactionOutcomeJournal.Resolve(stale,actor,packet.userId,1,2,packet.operation,packet.requirements,false);
  Need(result.State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==1&&RemoteResourceTransactionOutcomeJournal.MatchesRetained(world,packet.userId,1,1,packet.operation,retained),"foreign World cannot rebind/erase retained journal");
  RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(stale);
  Need(RemoteResourceTransactionOutcomeJournal.MatchesRetained(world,packet.userId,1,1,packet.operation,retained),"stale native unload cannot clear another World's retained original");
  packet=Server(out world,out manager,out actor);RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);
  Receive(new NetConnectionAbs{cInfo=packet.Sender},packet);packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==1,"foreign native receiving connection refused");
  packet=Server(out world,out manager,out actor);RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);
  RemoteResourceTransactions.Throw=true;SeedHistorical(packet,world,actor);packet.ProcessPackage(world,GameManager.Instance);RemoteResourceTransactions.Throw=false;
  Need(manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Unknown,"native exception is not a refundable failure");int calls=RemoteResourceTransactions.Calls;
  packet.ProcessPackage(world,GameManager.Instance);Need(RemoteResourceTransactions.Calls==calls,"uncertain native removal cannot replay");
  packet=Server(out world,out manager,out actor);RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);
  var body=actor;RemoteResourceTransactions.During=()=>body.Died++;SeedHistorical(packet,world,actor);packet.ProcessPackage(world,GameManager.Instance);
  Need(manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Unknown,"ended-life historical original cannot publish a paid success");
  packet=Server(out world,out manager,out actor);Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);
  RemoteResourceTransactions.During=()=>{UnityEngine.Time.realtimeSinceStartup+=2;RemoteResourceTransactionOutcomeJournal.PumpPublication();};
  calls=RemoteResourceTransactions.Calls;SeedHistorical(packet,world,actor);packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==calls+1&&manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Success,"reentrant metadata pump cannot publish an in-progress native result");
  packet=Server(out world,out manager,out actor);Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);
  string blockedObservation=null;
  RemoteResourceTransactions.During=()=>{
   string original=Directory.GetFiles(GameIO.Directory,"*.original.xml",SearchOption.AllDirectories).Single();
   blockedObservation=original.Replace(".original.xml",".observed.xml");Directory.CreateDirectory(blockedObservation);
  };
  calls=RemoteResourceTransactions.Calls;SeedHistorical(packet,world,actor);packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==calls+1&&manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Unknown,"publication failure retains uncertain reply after one native call");
  Directory.Delete(blockedObservation); // Only this scenario's newly created empty blocker.
  UnityEngine.Time.realtimeSinceStartup+=2;RemoteResourceTransactionOutcomeJournal.PumpPublication();
  packet.mode=RemoteResourceTransactionRequestMode.QueryOnly;packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==calls+1&&manager.Replies.Last().outcome==RemoteResourceTransactionOutcomeState.Success,"metadata recovery publishes original return without repeating native removal");
  RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);
  packet.mode=RemoteResourceTransactionRequestMode.ExecuteOrReplay;packet.ProcessPackage(world,GameManager.Instance);
  Need(RemoteResourceTransactions.Calls==calls+1&&manager.Replies.Last().outcome==RemoteResourceTransactionOutcomeState.Unknown,"retired warm journal cannot turn cold persisted original into another native debit");
  packet=Server(out world,out manager,out actor);Receive((NetConnectionAbs)packet.Sender.netConnection[0],packet);
  calls=RemoteResourceTransactions.Calls;RebirthCamperInventorySnapshot.Oversize=true;
  try{packet.ProcessPackage(world,GameManager.Instance);}finally{RebirthCamperInventorySnapshot.Oversize=false;}
  Need(RemoteResourceTransactions.Calls==calls&&manager.Replies.Single().outcome==RemoteResourceTransactionOutcomeState.Unknown,"oversized original refused before retained clone or native removal");
  var cookingRequest=new NetPackageRebirthCookingPullOriginal{Sender=packet.Sender};
  Need(!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(cookingRequest,out _),"unobserved cooking original has no native connection authority");
  Receive((NetConnectionAbs)packet.Sender.netConnection[0],cookingRequest);
  Need(RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(cookingRequest,out var cookingPeer)&&ReferenceEquals(cookingPeer,packet.Sender.netConnection[0]),"actual server dequeue collector classifies cooking original purpose");
  RemoteResourceClientTransactionCoordinator.LeaseCases();
  var cookingResult=new NetPackageRebirthCookingPulledOriginal();var hostConnection=new NetConnectionAbs();
  Need(!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(cookingResult,out _),"unobserved cooking result cannot qualify terminal receipt");
  Receive(hostConnection,cookingResult);
  Need(RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(cookingResult,out var receivedHost)&&ReferenceEquals(receivedHost,hostConnection),"actual client dequeue collector classifies cooking terminal result purpose");
  Console.WriteLine("PASS actual legacy store/journal/packet/dequeue/lease scenarios; native saved-source recovery/UI/topology NOT_RUN");
 }
}
