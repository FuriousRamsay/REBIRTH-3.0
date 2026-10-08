using System;using System.IO;using System.Linq;using System.Text;
namespace UnityEngine.Scripting{public class PreserveAttribute:Attribute{}}
public enum NetPackageDirection{ToServer}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s,Encoding.UTF8,true){}}
public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s,Encoding.UTF8,true){}}
public abstract class NetPackage{public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader r);public virtual void write(PooledBinaryWriter w){w.Write((ushort)0);}public abstract void ProcessPackage(World w,GameManager g);public bool ValidEntityIdForSender(int id)=>Sender?.EntityId==id;}
public class ClientInfo{public int EntityId=7;}
public class EntityPlayer{public int entityId=7;}
public class World{public bool Remote;public EntityPlayer Player=new();public bool IsRemote()=>Remote;public object GetEntity(int id)=>Player?.entityId==id?Player:null;}
public class GameManager{public static GameManager Instance;public World World;}
public class SingletonMonoBehaviour<T>{public static T Instance;}
public class Clients{public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender?.EntityId==id?Sender:null;}public class ConnectionManager{public bool IsServer=true;public Clients Clients=new();public int TerminalSends;public void SendPackage(NetPackageRebirthGearBoundSettled p,int _attachedToEntityId){TerminalSends++;}}
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public class NetPackageRebirthGearOfferFragment{}
public static class NetPackageManager{public static bool Missing;public static T GetPackage<T>() where T:new()=>new T();public static int GetPackageId(Type t){if(Missing)throw new Exception("mapping");return 1;}}
public class RebirthGearInventorySnapshot{public RebirthGearInventoryPlan.Stack[] Bag,Belt;public int OwnedBeltSlots=4;public bool IsUsableSource(bool b,int index)=>index>=0&&index<(b?Bag.Length:OwnedBeltSlots);}
public enum RebirthGearTransferPhase{Prepared,OwnerApplied,GearCommitted}public class RebirthGearTransferState{}
public static class RebirthRemoteGearPreparation{public static bool TryReplayBoundRetained(EntityPlayer p,ClientInfo s,string marker,out RebirthGearTransferState o,out RebirthGearTransferPhase phase){phase=RebirthGearTransferPhase.Prepared;return TryReplayBound(p,s,marker,out o);}
 public static bool Replay,Prepare=true;public static int Replays,Prepares;public static Action OnPrepare,OnReplay;
 public static bool TryReplayBound(EntityPlayer p,ClientInfo s,string marker,out RebirthGearTransferState offer){Replays++;OnReplay?.Invoke();offer=null;return Replay;}
 public static bool TryPrepareBound(EntityPlayer p,ClientInfo s,string marker,out RebirthGearTransferState offer){Prepares++;OnPrepare?.Invoke();offer=null;return Prepare;}
}
public static class RebirthGearOfferServer{public static bool TerminalDelivery=true;public static int TerminalFragments;public static bool TrySendTerminalOriginal(EntityPlayer p,ClientInfo s,string m,RebirthGearSettlement t){TerminalFragments++;return TerminalDelivery;}public static int Sends;public static string Marker;public static bool TrySendRetainedBound(EntityPlayer p,ClientInfo s,string marker){Sends++;Marker=marker;return true;}}
public class RebirthGearSettlement {}
public class NetPackageRebirthGearBoundSettled { public NetPackageRebirthGearBoundSettled Setup(int id,RebirthGearSettlement value)=>this; }
public static class RebirthRemoteGearAppliedConfirmation {public static bool TryGetBoundTerminalOriginal(EntityPlayer p,ClientInfo s,string m,out RebirthGearTransferState o,out RebirthGearSettlement t){o=null;t=null;return HasOriginal;}public static bool HasOriginal;public static bool Ready;public static Action OnRead;public static bool TryGetBoundSettlement(EntityPlayer p,ClientInfo s,string marker,out RebirthGearSettlement value){OnRead?.Invoke();value=Ready?new():null;return Ready;}}
class Program{
 static int checks;static World world;static ConnectionManager manager;static ClientInfo sender;static string marker;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 static void Throws(Action a,string label){bool thrown=false;try{a();}catch{thrown=true;}Check(thrown,label);}
 static void Reset(){world=new();GameManager.Instance=new(){World=world};manager=new();SingletonMonoBehaviour<ConnectionManager>.Instance=manager;sender=new();manager.Clients.Sender=sender;ThreadManager.Main=true;NetPackageManager.Missing=false;RebirthRemoteGearAppliedConfirmation.HasOriginal=false;RebirthGearOfferServer.TerminalDelivery=true;RebirthGearOfferServer.TerminalFragments=0;RebirthRemoteGearAppliedConfirmation.Ready=false;RebirthRemoteGearAppliedConfirmation.OnRead=null;RebirthRemoteGearPreparation.Replay=false;RebirthRemoteGearPreparation.Prepare=true;RebirthRemoteGearPreparation.Replays=RebirthRemoteGearPreparation.Prepares=RebirthGearOfferServer.Sends=0;RebirthRemoteGearPreparation.OnPrepare=RebirthRemoteGearPreparation.OnReplay=null;
 var snap=new RebirthGearInventorySnapshot{Bag=Enumerable.Range(0,52).Select(_=>new RebirthGearInventoryPlan.Stack()).ToArray(),Belt=Enumerable.Range(0,4).Select(_=>new RebirthGearInventoryPlan.Stack()).ToArray()};snap.Bag[0]=new(){ItemData="AQ==",Count=1};
 Check(RebirthGearPreparationIntent.TryCreate(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,1,3,true,0,snap,out var intent)&&RebirthGearPreparationMarker.TryEncode(Guid.NewGuid(),4,intent,out marker),"actual original marker prepared");}
 static NetPackageRebirthGearPreparationRequest Request(){var p=new NetPackageRebirthGearPreparationRequest().Setup(7,marker);p.Sender=sender;return p;}
 static byte[] Encode(NetPackageRebirthGearPreparationRequest p){using var s=new MemoryStream();using(var w=new PooledBinaryWriter(s))p.write(w);return s.ToArray();}
 static NetPackageRebirthGearPreparationRequest Decode(byte[] b){using var s=new MemoryStream(b);using var r=new PooledBinaryReader(s);r.ReadUInt16();var p=new NetPackageRebirthGearPreparationRequest();p.read(r);Check(s.Position==s.Length,"exact wire consumed");p.Sender=sender;return p;}
 static void ThrottleChecks(){
 var w=new object();var m=new object();var gate=new RebirthGearPreparationRequestThrottle();
 Check(!gate.TryAdmit(w,m,1,double.NaN)&&!gate.TryAdmit(w,m,1,double.PositiveInfinity)&&!gate.TryAdmit(w,m,0,0),"invalid scheduling context/time refused");
 Check(gate.TryAdmit(w,m,1,10)&&!gate.TryAdmit(w,m,1,10.74)&&gate.TryAdmit(w,m,1,10.75),"bounded original owner retry");
 Check(!gate.TryAdmit(w,m,1,9),"reversed time cannot bypass retry");
 Check(gate.TryAdmit(w,new object(),1,10.75),"new native manager resets runtime scheduling");
 gate=new();for(int id=1;id<=128;id++)Check(gate.TryAdmit(w,m,id,20),"active owner admitted");
 Check(!gate.TryAdmit(w,m,129,20)&&gate.TryAdmit(w,m,1,20.75),"capacity refuses new owner but permits original retry");
 Check(gate.TryAdmit(w,m,129,25.75),"expired runtime entries do not impose lifetime admission cap");
 Check(gate.TryAdmit(new object(),m,129,25.75),"new world has separate scheduling");
 }
 static void Main(){
 ThrottleChecks();
 Reset();RebirthRemoteGearAppliedConfirmation.Ready=true;RebirthRemoteGearAppliedConfirmation.HasOriginal=true;Request().ProcessPackage(world,GameManager.Instance);Check(manager.TerminalSends==1&&RebirthGearOfferServer.TerminalFragments==1&&RebirthRemoteGearPreparation.Prepares==0,"original terminal plan delivery precedes outcome without reprepare");
 Reset();RebirthRemoteGearAppliedConfirmation.Ready=true;RebirthRemoteGearAppliedConfirmation.HasOriginal=true;RebirthGearOfferServer.TerminalDelivery=false;Request().ProcessPackage(world,GameManager.Instance);Check(manager.TerminalSends==0&&RebirthGearOfferServer.TerminalFragments==1,"uncertain terminal fragments withhold outcome");
 Reset();RebirthRemoteGearAppliedConfirmation.Ready=true;Request().ProcessPackage(world,GameManager.Instance);Check(manager.TerminalSends==1&&RebirthRemoteGearPreparation.Replays==0&&RebirthRemoteGearPreparation.Prepares==0&&RebirthGearOfferServer.Sends==0,"terminal response skips preparation and retained offer effects");
 Reset();RebirthRemoteGearAppliedConfirmation.Ready=true;RebirthRemoteGearAppliedConfirmation.OnRead=()=>manager.Clients.Sender=new();Request().ProcessPackage(world,GameManager.Instance);Check(manager.TerminalSends==0,"replaced native sender after terminal lookup refuses delivery");
 Reset();RebirthRemoteGearAppliedConfirmation.Ready=true;RebirthRemoteGearAppliedConfirmation.OnRead=()=>GameManager.Instance.World=new();Request().ProcessPackage(world,GameManager.Instance);Check(manager.TerminalSends==0,"changed original world after terminal lookup refuses delivery");
 Reset();var p=Request();var wire=Encode(p);Check(p.PackageDirection==NetPackageDirection.ToServer&&wire.Length==p.GetLength(),"direction/length");
 Decode(wire).ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Prepares==1&&RebirthGearOfferServer.Sends==1&&RebirthGearOfferServer.Marker==marker,"original canonical marker reaches preparation/response");
 RebirthRemoteGearPreparation.Replay=true;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Prepares==1&&RebirthGearOfferServer.Sends==1,"immediate duplicate throttled before saved reads");System.Threading.Thread.Sleep(800);Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Prepares==1&&RebirthGearOfferServer.Sends==2,"saved replay skips reprepare");
 Reset();RebirthRemoteGearPreparation.Prepare=false;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthGearOfferServer.Sends==0,"uncertain/missing saved original withholds response");
 Reset();sender.EntityId=8;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0&&RebirthRemoteGearPreparation.Prepares==0,"foreign sender refused");
 Reset();world.Remote=true;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0,"remote world refused");
 Reset();GameManager.Instance.World=new();Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0,"old world refused");
 Reset();manager.IsServer=false;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0,"nonserver refused");
 Reset();ThreadManager.Main=false;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0,"nonmain native preparation refused");
 Reset();NetPackageManager.Missing=true;Request().ProcessPackage(world,GameManager.Instance);Check(RebirthRemoteGearPreparation.Replays==0&&RebirthGearOfferServer.Sends==0,"response mapping resolved before saved effect");
 Reset();RebirthRemoteGearPreparation.OnPrepare=()=>GameManager.Instance.World=new();Request().ProcessPackage(world,GameManager.Instance);Check(RebirthGearOfferServer.Sends==0,"changed world after save withholds response");
 Reset();RebirthRemoteGearPreparation.OnReplay=()=>SingletonMonoBehaviour<ConnectionManager>.Instance=new();Request().ProcessPackage(world,GameManager.Instance);Check(RebirthGearOfferServer.Sends==0,"changed session after replay withholds response");
 Reset();Throws(()=>new NetPackageRebirthGearPreparationRequest().Setup(0,marker),"invalid player refused");Throws(()=>new NetPackageRebirthGearPreparationRequest().Setup(7,marker.ToUpperInvariant()),"noncanonical marker refused");Throws(()=>Encode(new()),"uninitialized packet refused");
 wire=Encode(Request());Throws(()=>Decode(wire.Take(wire.Length-1).ToArray()),"truncated marker refused");var oversized=(byte[])wire.Clone();oversized[6]=1;oversized[7]=32;Throws(()=>Decode(oversized),"oversize declaration refused before marker allocation");var nonascii=(byte[])wire.Clone();nonascii[8]=255;Throws(()=>Decode(nonascii),"nonascii marker refused");var wrong=(byte[])wire.Clone();wrong[8]=(byte)'x';Throws(()=>Decode(wrong),"unknown marker family refused");
 var emptySnapshot=new RebirthGearInventorySnapshot{Bag=Enumerable.Range(0,52).Select(_=>new RebirthGearInventoryPlan.Stack()).ToArray(),Belt=Enumerable.Range(0,4).Select(_=>new RebirthGearInventoryPlan.Stack()).ToArray()};
 foreach(var slot in new[]{"backpack","belt","support","walkman"}){
 Check(RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,slot,emptySnapshot,out var remove)&&remove.IsUnequip&&remove.SourceIndex==-1&&remove.MatchesInventory(emptySnapshot),"explicit unequip accepts complete empty physical preimage "+slot);
 Check(RebirthGearPreparationIntent.TryRead(remove.Write(),out var readRemove)&&readRemove.IsUnequip&&readRemove.UnequipSlot==slot&&System.Xml.Linq.XNode.DeepEquals(remove.Write(),readRemove.Write()),"unequip schema roundtrip "+slot);
 var removeWorld=Guid.NewGuid();Check(RebirthGearPreparationMarker.TryEncode(removeWorld,4,remove,out var removeMarker)&&RebirthGearPreparationMarker.TryRead(removeMarker,1f,out var parsedWorld,out var owned,out var parsedRemove)&&parsedWorld==removeWorld&&owned==4&&parsedRemove.IsUnequip,"existing canonical marker carries explicit unequip "+slot);
 Check(RebirthGearPreparationRefusal.TryCreateStale(removeMarker,3,out var removeRefusal)&&removeRefusal.TransactionId==remove.TransactionId&&removeRefusal.SavedWorld==removeWorld,"refusal retains original unequip marker "+slot);
 var mixed=new System.Xml.Linq.XElement(remove.Write());mixed.SetAttributeValue("type",1);Check(!RebirthGearPreparationIntent.TryRead(mixed,out _),"mixed equip unequip fields refused "+slot);
 }
 Check(!RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,"Backpack",emptySnapshot,out _),"noncanonical unequip slot refused");
 Check(!RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,"armor",emptySnapshot,out _),"foreign gear slot refused");
 Check(!RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.Empty,2,"belt",emptySnapshot,out _),"empty unequip identity refused");
 Check(!RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.NewGuid(),long.MaxValue,"belt",emptySnapshot,out _),"exhausted revision refused");
 RebirthGearPreparationIntent.TryCreateUnequip(Guid.NewGuid().ToString("N"),Guid.NewGuid(),2,"belt",emptySnapshot,out var frozenRemove);emptySnapshot.Bag[1]=new(){ItemData="AQ==",Count=1};Check(!frozenRemove.MatchesInventory(emptySnapshot),"unequip full inventory digest detects later mutation");
 Reset();RebirthGearPreparationMarker.TryRead(marker,1f,out var refusalWorld,out _,out var refusalIntent);
 Check(RebirthGearPreparationRefusal.TryCreateStale(marker,9,out var refusal)&&refusal.ObservedRevision==9&&refusal.ExpectedRevision==2&&refusal.TransactionId==refusalIntent.TransactionId&&refusal.CreationId==refusalIntent.CreationId&&refusal.SavedWorld==refusalWorld&&refusal.RequestDigest.Length==64,"stale refusal binds exact full original world character transaction and separate observed revision");
 var refusalParent=new System.Xml.Linq.XElement("support",refusal.Write());Check(RebirthGearPreparationRefusal.TryRead(refusalParent,out var refusalCopy)&&refusalCopy.Write().ToString()==refusal.Write().ToString()&&refusalCopy.MatchesOriginal(marker),"canonical refusal data roundtrip");
 Check(!refusal.MatchesOriginal(marker+"x"),"changed original marker never matches refusal");
 Check(!RebirthGearPreparationRefusal.TryCreateStale(marker,2,out _)&&!RebirthGearPreparationRefusal.TryCreateStale(marker,1,out _),"equal or older authoritative revision cannot prove stale refusal");
 Check(!RebirthGearPreparationRefusal.TryCreateStale("invalid",9,out _),"malformed original never creates refusal");
 Check(RebirthGearPreparationRefusal.TryCreateStale(marker,long.MaxValue,out var terminalRevision)&&terminalRevision.ObservedRevision==long.MaxValue,"observed maximum revision does not overflow or increment");
 Check(RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support"),out var absentRefusal)&&absentRefusal==null,"legacy absent refusal state remains readable without invented outcome");
 var duplicateRefusal=new System.Xml.Linq.XElement("support",refusal.Write(),refusal.Write());Check(!RebirthGearPreparationRefusal.TryRead(duplicateRefusal,out _),"duplicate original refusal records rejected");
 foreach(var attr in new[]{("version","2"),("reason","unknown"),("observedRevision","02"),("observedRevision","+9"),("observedRevision","-1"),("observedRevision","2"),("marker","broken")}){var changed=refusal.Write();changed.SetAttributeValue(attr.Item1,attr.Item2);Check(!RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support",changed),out _),"strict refusal attribute "+attr.Item1+":"+attr.Item2);}
 var extraRefusal=refusal.Write();extraRefusal.SetAttributeValue("authority","invented");Check(!RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support",extraRefusal),out _),"unknown refusal authority attribute rejected");
 var childRefusal=refusal.Write();childRefusal.Add(new System.Xml.Linq.XElement("effect"));Check(!RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support",childRefusal),out _),"refusal cannot contain invented effect child");
 var textRefusal=refusal.Write();textRefusal.Add("effect");Check(!RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support",textRefusal),out _),"refusal nonblank text rejected"); var legacyCreation="legacy-"+new string('a',64);Check(RebirthGearPreparationIntent.TryCreateUnequip(legacyCreation,Guid.NewGuid(),2,"belt",emptySnapshot,out var legacyRemove)&&RebirthGearPreparationMarker.TryEncode(Guid.NewGuid(),4,legacyRemove,out var legacyMarker)&&RebirthGearPreparationRefusal.TryCreateStale(legacyMarker,5,out var legacyRefusal)&&RebirthGearPreparationRefusal.TryRead(new System.Xml.Linq.XElement("support",legacyRefusal.Write()),out var legacyCopy)&&legacyCopy.CreationId==legacyCreation,"full migrated legacy creation refusal remains canonical roundtrip"); Check(refusal.MatchesSupport(refusal.CreationId,9,false,null)&&refusal.MatchesSupport(refusal.CreationId,10,false,null),"refusal fits exact character revision and no competing custody");
 Check(!refusal.MatchesSupport(refusal.CreationId,8,false,null),"future observed refusal revision refuses saved state");
 Check(!refusal.MatchesSupport(Guid.NewGuid().ToString("N"),9,false,null),"foreign character refuses saved refusal state");
 Check(!refusal.MatchesSupport(refusal.CreationId,9,true,null),"pending custody cannot coexist unprepared refusal");
 Check(!refusal.MatchesSupport(refusal.CreationId,9,false,refusal.TransactionId.ToString("N")),"same original cannot be both prepared terminal and unprepared refusal"); Console.WriteLine("PASS "+checks+" actual preparation packet/marker/intent checks; native authentication/world, preparation and delivery boundaries doubled. No native save/network execution.");
 }
}