using System;using System.Collections.Generic;using System.IO;
class Program{
 static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Main(){
 var store=new RebirthPoiWorldStore();RebirthPoiWorldLifecycle.Instance.Store=store;
 var id=new RebirthPoiIdentity();var discovered=new RebirthPoiWorldSnapshot();var cleared=new RebirthPoiWorldSnapshot{Record=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.Cleared,Clear=new RebirthPoiClearEvidence()}};
 RebirthPurgeClearNotification.Pulse();Check(GameManager.Notices==0,"subscription does not replay saved clears");
 store.Emit(discovered,cleared,id);Check(GameManager.Notices==1&&Audio.Manager.Sounds==1&&ConnectionManager.Sent==1,"live clear produces local notice sound and remote package once");
 store.Emit(cleared,cleared,id);Check(GameManager.Notices==1,"unchanged clear publication is silent");
 var reset=new RebirthPoiWorldSnapshot{Record=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.ResetPending,BeforeResetClear=new RebirthPoiClearEvidence()}};
 store.Emit(reset,cleared,id);Check(GameManager.Notices==1,"cancelled reset restoring prior clear is silent");
 store.Emit(discovered,cleared,id);Check(GameManager.Notices==2,"actual reclear produces feedback");
 RebirthPurgeClearNotification.Reset();store.Emit(discovered,cleared,id);Check(GameManager.Notices==2,"world teardown removes subscription");
 var old=new World{Remote=true};new NetPackageRebirthPurgeClearNotice().Setup("house","forest",3).ProcessPackage(old,null);Check(GameManager.Notices==2,"late package from previous world is ignored");
 GameManager.Instance.World.Remote=true;new NetPackageRebirthPurgeClearNotice().Setup("house","forest",3).ProcessPackage(GameManager.Instance.World,null);Check(GameManager.Notices==3,"current remote client displays server notice");
 var savedWorld=RebirthPoiMapSync.LocalWorld;var savedSession=RebirthPoiMapSync.LocalSession;
 var early=new NetPackageRebirthPurgeClearNotice().Setup("house","forest",3);
 RebirthPoiMapSync.LocalWorld=Guid.Empty;early.ProcessPackage(GameManager.Instance.World,null);Check(GameManager.Notices==3,"notice waits for map handshake");
 RebirthPoiMapSync.LocalWorld=savedWorld;RebirthPoiMapSync.LocalSession=savedSession;RebirthPurgeNoticeDelivery.Pulse();Check(GameManager.Notices==4,"matching delayed notice delivers once after handshake");
 RebirthPurgeNoticeDelivery.Pulse();Check(GameManager.Notices==4,"drained notice is not replayed");
 var stale=new NetPackageRebirthPurgeClearNotice().Setup("house","forest",3);RebirthPoiMapSync.LocalSession=Guid.NewGuid();stale.ProcessPackage(GameManager.Instance.World,null);Check(GameManager.Notices==4,"previous session notice rejected in same world object");
 RebirthPoiMapSync.LocalSession=savedSession;
 var expired=new NetPackageRebirthPurgeClearNotice().Setup("house","forest",3);RebirthPoiMapSync.LocalWorld=Guid.Empty;expired.ProcessPackage(GameManager.Instance.World,null);UnityEngine.Time.realtimeSinceStartup=61;RebirthPoiMapSync.LocalWorld=savedWorld;RebirthPurgeNoticeDelivery.Pulse();Check(GameManager.Notices==4,"expired prehandshake notice withheld");
 var actors=new Observations();var volume=new Volume();actors.Volumes.Add(0,volume);
 volume.Actors.Add(1,new Actor{Dead=true,TerminalKind=RebirthPoiActorTerminalKind.Death,Contributor="offline"});
 volume.Actors.Add(2,new Actor{Dead=true,TerminalKind=RebirthPoiActorTerminalKind.Death,Contributor="online"});
 volume.Actors.Add(3,new Actor{Dead=true,TerminalKind=RebirthPoiActorTerminalKind.Death,Contributor="online"});
 volume.Actors.Add(4,new Actor{Dead=false,TerminalKind=RebirthPoiActorTerminalKind.Death,Contributor="unearned"});
 GameManager.Instance.Saved.Players.Add(1,new Persistent{PrimaryId=new Identity{CombinedString="offline"},PlayerName=new Display{DisplayName="Ada"}});
 GameManager.Instance.World.aiDirector.Players.trackedPlayers.list.Add(new Entry{Player=new Player{Key="online",Name="Bea"}});
 GameManager.Instance.World.aiDirector.Players.trackedPlayers.list.Add(new Entry{Player=new Player{Key="unearned",Name="Not credited"}});
 Check(RebirthPurgeClearNotification.ContributorNames(new RebirthPoiClearanceRecord{Observations=actors})=="Ada\nBea","current death witnesses include saved offline contributor once and exclude living actor");
 RebirthSandboxOptionManager.Current.IsPurge=false;RebirthPurgeClearNotification.Show("house","forest",3);Check(GameManager.Notices==4,"None mode suppresses Purge feedback");
 Console.WriteLine("RESULT "+checks+" PASS; production notification code; store events, network and audio doubled; no in-game playback claim.");}}
class RebirthPoiWorldStore{public event Action<RebirthPoiWorldSnapshot,RebirthPoiWorldSnapshot,IReadOnlyList<RebirthPoiIdentity>> PublicationChanged;public void Emit(RebirthPoiWorldSnapshot a,RebirthPoiWorldSnapshot b,RebirthPoiIdentity id)=>PublicationChanged?.Invoke(a,b,new[]{id});}
class RebirthPoiWorldSnapshot{public Binding Binding=new Binding();public RebirthPoiClearanceRecord Record;public bool TryGet(RebirthPoiIdentity id,out RebirthPoiClearanceRecord record){record=Record;return record!=null;}}
class Binding{public bool IsCurrent=true;}class RebirthPoiIdentity{public string Prefab="house",Biome="forest";}enum RebirthPoiClearanceState{Discovered,Cleared,ResetPending}class RebirthPoiClearEvidence{public int SpawnedParticipants=3;}class RebirthPoiClearanceRecord{public RebirthPoiClearanceState State;public RebirthPoiClearEvidence Clear,BeforeResetClear;public Observations Observations;}
class RebirthPoiWorldLifecycle{public static RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();public RebirthPoiWorldStore Store;public bool TryGetEvidenceStore(out RebirthPoiWorldStore s){s=Store;return s!=null;}}
class RebirthPurgeReleasePolicy{public static bool Enabled=true;}class RebirthSandboxOptionManager{public static RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();public bool IsPurge=true;}
public class World{public bool Remote;public bool IsRemote()=>Remote;public object GetPrimaryPlayer()=>new object();internal Director aiDirector=new();}public class GameManager{public static GameManager Instance=new GameManager();public World World=new World();public static int Notices;internal PersistentList GetPersistentPlayerList()=>Saved;internal PersistentList Saved=new();public static void ShowTooltip(object player,string text){Notices++;}}
class ConnectionManager{public bool IsServer=true;public static int Sent;public void SendPackage(NetPackage p){Sent++;}}class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}class NetPackageManager{public static T GetPackage<T>()where T:new()=>new T();}
class Localization{public static string Get(string key)=>key=="xuiRebirthPurgePoiCleared"?"{0} {1} {2}":key;}
namespace Audio{class Manager{public static int Sounds;public static void PlayInsidePlayerHead(string name){if(name!="purge_discovered")throw new Exception("wrong legacy sound");Sounds++;}}}
public enum NetPackageDirection{ToClient}public class NetPackage{public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}}public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
namespace UnityEngine.Scripting{class PreserveAttribute:Attribute{}}

// Native adapters only. Production clear event and notice delivery code is linked;
// display sanitization, paging, history IO and codec are qualified separately.
class Observations{public Dictionary<int,Volume> Volumes=new();}
class Volume{public Dictionary<int,Actor> Actors=new();}
class Actor{public bool Dead;public RebirthPoiActorTerminalKind TerminalKind;public string Contributor;}
enum RebirthPoiActorTerminalKind{Death,Removed}
class Director{internal AIDirectorPlayerManagementComponent Players=new();public T GetComponent<T>()where T:class=>Players as T;}
class AIDirectorPlayerManagementComponent{public Tracked trackedPlayers=new();}
class Tracked{public List<Entry> list=new();}
class Entry{public Player Player;}
class Player{public string Key,Name;}
class PersistentList{public Dictionary<int,Persistent> Players=new();}
class Persistent{public Identity PrimaryId;public Display PlayerName;}
class Identity{public string CombinedString;}
class Display{public string DisplayName;}
static class RebirthStablePlayerIdentity{public static string ComputeStorageKey(string s)=>s;}
static class RebirthPurgeKillContributor{public static string Identify(Player p,World w)=>p?.Key;}
static class RebirthPurgeDiscoveryNotification{public static string CleanDisplayName(string s)=>s??"";public static string DisplayName(Player p)=>p?.Name??"";}
static class RebirthPurgePoiHistory{public static int Writes;public static void Purged(RebirthPoiClearanceRecord r,string names){Writes++;}}
static class XUiC_RebirthMessagePopup{public static IEnumerable<string> ClearNoticePages(string names,Func<string,string> format){yield return format(names);}}
static class RebirthPoiMapSync{public static Guid LocalWorld=Guid.NewGuid(),LocalSession=Guid.NewGuid();}
static class RebirthSurvivorNetworkCodec{public static string ReadBoundedString(BinaryReader r,int max){var s=r.ReadString();if(s.Length>max)throw new InvalidDataException();return s;}public static void WriteString(BinaryWriter w,string s,int max){if(s.Length>max)throw new InvalidDataException();w.Write(s);}}
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;}}
