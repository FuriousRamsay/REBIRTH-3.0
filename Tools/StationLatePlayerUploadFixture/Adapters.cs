using System.Xml.Linq;
using System.Text;
using System.IO;
class NativeId {public string CombinedString="owner";}
class ClientInfo {public int entityId=1; public NativeId InternalId=new();public PlayerDataFile latestPlayerData;public bool loginDone=true;}
class NetPackage {public ClientInfo Sender;public virtual void ProcessPackage(World w,GameManager g){}protected bool ValidEntityIdForSender(int id)=>Sender.entityId==id;}
class EntityPlayer {public int entityId=1;public World world;public object QuestJournal;public Observer ChunkObserver=new();}
class Observer {public Map mapDatabase=>null;}
interface IMapChunkDatabase {class DirectoryPlayerId {public DirectoryPlayerId(string s,string p){}}}
class EntityPlayerLocal:EntityPlayer {public Buffs Buffs=new();public Slots bag=new(),inventory=new();public bool IsSpawned()=>true;public bool IsDead()=>false;}
class Slots {public Grid ItemGrid=new();}class Grid {public RebirthGearInventoryPlan.Stack[] items=new[]{new RebirthGearInventoryPlan.Stack{Count=1,ItemData="full"}};}
class Buffs {public Dictionary<string,float> CVars=new();public float GetCustomVar(string k)=>CVars.GetValueOrDefault(k);public void RemoveCustomVar(string k)=>CVars.Remove(k);}
class World {public EntityPlayerLocal Player;public object GetEntity(int i)=>Player?.entityId==i?Player:null;public bool IsRemote()=>true;public EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class PersistentPlayers {public Dictionary<PlatformUserIdentifierAbs,PersistentPlayerData> Players=new();}class PersistentPlayerData {public int EntityId;public Vector3i Position;}
class Vector3i {public Vector3i(object p){}}class Ecd {public object pos;}
partial class GameManager {public static GameManager Instance;public World World;public World m_World=>World;public PersistentPlayers persistentPlayers;public int LocalSaves;public NativeId getPersistentPlayerID(object p)=>new();public void SaveLocalPlayerData(){LocalSaves++;FixtureIo.WriteLive(World.Player);} }
static class ThreadManager {public static bool IsMainThread()=>true;public static void AddSingleTask(object a,object b){}}
static class ModEvents {public struct SSavePlayerDataData {public SSavePlayerDataData(ClientInfo c,PlayerDataFile d){}}public static Event SavePlayerData=new();public class Event {public int Count;public void Invoke(ref SSavePlayerDataData d){Count++;}}}
static class GameIO {public static string Root;public static string GetPlayerDataDir()=>Root;}
static class Log {public static int Errors;public static void Error(string s){Errors++;}}
static class SdDirectory {public static bool Exists(string p)=>Directory.Exists(p);public static void CreateDirectory(string p)=>Directory.CreateDirectory(p);}
static class SdFile {public static bool FailFinal;public static bool Exists(string p)=>File.Exists(p);public static void Copy(string a,string b,bool overwrite){if(FailFinal&&!b.EndsWith(".bak"))throw new IOException("injected final copy failure");File.Copy(a,b,overwrite);}public static void Delete(string p)=>File.Delete(p);public static Stream Open(string p,FileMode m,FileAccess a,FileShare s)=>File.Open(p,m,a,s);}
enum StreamModeWrite{Persistency}
class PooledBinaryWriter:IDisposable {BinaryWriter w;public void SetBaseStream(Stream s){w=new BinaryWriter(s,Encoding.UTF8,true);}public void Write(char c)=>w.Write(c);public void Write(byte c)=>w.Write(c);public void Write(int c)=>w.Write(c);public void Write(string c)=>w.Write(c);public void Dispose()=>w.Dispose();}
static class MemoryPools {public static Pool poolBinaryWriter=new();public class Pool {public PooledBinaryWriter AllocSync(bool _bReset)=>new();}}
class Metadata {public void Write(string p){File.WriteAllText(p,"metadata");}}
partial class PlayerDataFile {public const string EXT="ttp";public int id=1,Count,Receipt=1;public bool Marker=true,bModifiedSinceLastSave=true;public MemoryStream buffData;public object questJournal;public Ecd ecd=new();public Metadata metadata=new();public void Write(PooledBinaryWriter w,StreamModeWrite m){w.Write(Count);w.Write("rbGear_tx="+Receipt+(Marker?";_rbgearintent_original=1":""));}}
class RebirthStablePlayerIdentity {public string CanonicalId="owner";public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity o){o=new();return true;}}
class RebirthGearInventoryPlan {public class Stack {public int Count;public string ItemData;}public int BagSlotsBefore=1,BagSlotsAfter=1;public object GearBefore;public enum ApplicationState{Conflict,Compatible}public bool MatchesAppliedInventory(Stack[] bag,Stack[] belt)=>bag[0].Count==1&&bag[0].ItemData=="full";public bool MatchesBefore(Stack[] b,Stack[] belt,object gear,int bn,int tn)=>b[0].Count==0;public ApplicationState InspectApplication(Stack[] b,Stack[] t,bool p)=>b[0].Count==0||b[0].Count==1?ApplicationState.Compatible:ApplicationState.Conflict;}
class RebirthGearTransferState {public string TransactionId="tx",CreationId="creation",PreparationRequestDigest="digest";public long ExpectedRevision=0;public bool TryGetPlan(out RebirthGearInventoryPlan p){p=new();return true;}}
enum RebirthGearOwnerReceipt{Rejected=-1,Applied=1,Applying=2}
static class RebirthGearReceiptReader {public static bool Contains(byte[] b,string t,RebirthGearOwnerReceipt s)=>Encoding.UTF8.GetString(b).Contains("rbGear_"+t+"="+(int)s);}
static class RebirthGearPreparationMarkerReader {public static bool HasNoOriginal(byte[] b)=>!Encoding.UTF8.GetString(b).Contains("_rbgearintent_");}
static class RebirthPlayerDataInventory {public static RebirthGearInventoryPlan.Stack[] ReadSlots(PlayerDataFile d,bool b)=>new[]{new RebirthGearInventoryPlan.Stack{Count=b?d.Count:0,ItemData=b?"full":""}};}
class RebirthGearInventorySnapshot {public RebirthGearInventoryPlan.Stack[] Bag,Belt;public static bool TryCapture(RebirthGearInventoryPlan.Stack[] b,RebirthGearInventoryPlan.Stack[] t,int n,out RebirthGearInventorySnapshot s){s=new(){Bag=b,Belt=t};return b!=null&&t!=null;}}
static class RebirthGearNativePlayerFile {public static bool TryRead(RebirthStablePlayerIdentity o,out PlayerDataFile d){d=null;try{using var r=new BinaryReader(File.OpenRead(Path.Combine(GameIO.Root,o.CanonicalId+".ttp")));if(r.ReadChar()!='t'||r.ReadChar()!='t'||r.ReadChar()!='p'||r.ReadChar()!=0||r.ReadByte()!=62)return false;int count=r.ReadInt32();string marker=r.ReadString();if(r.BaseStream.Position!=r.BaseStream.Length)return false;d=new(){Count=count,buffData=new MemoryStream(Encoding.UTF8.GetBytes(marker))};return true;}catch{return false;}}}
static class RebirthGearOwnerReservation {public static bool Held=true;public static object Session;public static bool Matches(EntityPlayerLocal p,object s,RebirthGearTransferState o)=>Held&&ReferenceEquals(s,Session);}
class Conn {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
class ConnectionManager {public bool IsServer;public Conn[] connectionToServer;}
class SingletonMonoBehaviour<T> {public static T Instance;}
enum EnumGamePrefs{GameGuidClient}
static class GamePrefs {public static string GetString(EnumGamePrefs p)=>"world";}
class RebirthGearSettlement {public bool Applied=true;public string TransactionId="tx",CreationId="creation",PreparationRequestDigest="digest";public long GearRevision=1;public bool MatchesOriginalMarker(string s)=>s=="_rbgearintent_original";public XElement Write()=>new XElement("terminal",TransactionId);}
static class RebirthGearTerminalClient {public static bool Present=true;public static bool TryGetCurrent(EntityPlayerLocal p,out string m,out RebirthGearSettlement t){m="_rbgearintent_original";t=new();return Present;}}
static class FixtureIo {public static void WriteLive(EntityPlayerLocal p){new PlayerDataFile{Count=p.bag.ItemGrid.items[0].Count,Marker=p.Buffs.CVars.ContainsKey("_rbgearintent_original")}.Save(GameIO.Root,"owner");}}
class Map {public object SaveAsync; }
class PlatformUserIdentifierAbs {}

