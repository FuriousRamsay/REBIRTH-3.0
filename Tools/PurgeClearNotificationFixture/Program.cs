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
 RebirthSandboxOptionManager.Current.IsPurge=false;RebirthPurgeClearNotification.Show("house","forest",3);Check(GameManager.Notices==3,"None mode suppresses Purge feedback");
 Console.WriteLine("RESULT "+checks+" PASS; production notification code; store events, network and audio doubled; no in-game playback claim.");}}
class RebirthPoiWorldStore{public event Action<RebirthPoiWorldSnapshot,RebirthPoiWorldSnapshot,IReadOnlyList<RebirthPoiIdentity>> PublicationChanged;public void Emit(RebirthPoiWorldSnapshot a,RebirthPoiWorldSnapshot b,RebirthPoiIdentity id)=>PublicationChanged?.Invoke(a,b,new[]{id});}
class RebirthPoiWorldSnapshot{public Binding Binding=new Binding();public RebirthPoiClearanceRecord Record;public bool TryGet(RebirthPoiIdentity id,out RebirthPoiClearanceRecord record){record=Record;return record!=null;}}
class Binding{public bool IsCurrent=true;}class RebirthPoiIdentity{public string Prefab="house",Biome="forest";}enum RebirthPoiClearanceState{Discovered,Cleared,ResetPending}class RebirthPoiClearEvidence{public int SpawnedParticipants=3;}class RebirthPoiClearanceRecord{public RebirthPoiClearanceState State;public RebirthPoiClearEvidence Clear,BeforeResetClear;}
class RebirthPoiWorldLifecycle{public static RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();public RebirthPoiWorldStore Store;public bool TryGetEvidenceStore(out RebirthPoiWorldStore s){s=Store;return s!=null;}}
class RebirthPurgeReleasePolicy{public static bool Enabled=true;}class RebirthSandboxOptionManager{public static RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();public bool IsPurge=true;}
public class World{public bool Remote;public bool IsRemote()=>Remote;public object GetPrimaryPlayer()=>new object();}public class GameManager{public static GameManager Instance=new GameManager();public World World=new World();public static int Notices;public static void ShowTooltip(object player,string text){Notices++;}}
class ConnectionManager{public bool IsServer=true;public static int Sent;public void SendPackage(NetPackage p){Sent++;}}class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}class NetPackageManager{public static T GetPackage<T>()where T:new()=>new T();}
class Localization{public static string Get(string key)=>key=="xuiRebirthPurgePoiCleared"?"{0} {1} {2}":key;}
namespace Audio{class Manager{public static int Sounds;public static void PlayInsidePlayerHead(string name){if(name!="purge_discovered")throw new Exception("wrong legacy sound");Sounds++;}}}
public enum NetPackageDirection{ToClient}public class NetPackage{public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}}public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
namespace UnityEngine.Scripting{class PreserveAttribute:Attribute{}}
