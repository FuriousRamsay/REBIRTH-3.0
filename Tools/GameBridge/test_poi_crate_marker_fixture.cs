using System;using System.IO;
using UnityEngine;
namespace UnityEngine { public struct Vector3 {public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static float Distance(Vector3 a,Vector3 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}} }
public struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public class Owner {public string CombinedString="owner";public override bool Equals(object o){return o is Owner&&((Owner)o).CombinedString==CombinedString;}public override int GetHashCode(){return CombinedString.GetHashCode();}}
public static class PlatformManager {public static Owner InternalLocalUserIdentifier=new Owner();}
public class Block {public string GetBlockName(){return "cntWoodWritableCrate";}}
public struct BlockValue {public Block Block;}
public class ConnectionManager {public bool IsServer=true;}
public class SingletonMonoBehaviour<T> where T:new() {public static T Instance=new T();}
public class PersistentPlayer {public Owner PrimaryId;}
public class PersistentPlayers {public Owner PrimaryId=new Owner();public PersistentPlayer GetPlayerDataFromEntityID(int id){return new PersistentPlayer{PrimaryId=PrimaryId};}}
public class GameManager {public static GameManager Instance=new GameManager();public PersistentPlayers Players=new PersistentPlayers();public PersistentPlayers GetPersistentPlayerList(){return Players;}public bool Edit;public bool IsEditMode(){return Edit;}}
public class World {public bool Remote;public bool IsRemote(){return Remote;}}
public class EntityPlayer {public int entityId=1;public World world=new World();public bool Dead;public Vector3 position;public bool IsDead(){return Dead;}}
public class EntityPlayerLocal:EntityPlayer {}
public class RebirthStablePlayerIdentity {public string CanonicalId="owner";public static string Current="owner";public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity{CanonicalId=Current};return true;}}
public class PrefabInstance {public int id=4;public Vector3i boundingBoxPosition=new Vector3i(1,2,3),boundingBoxSize=new Vector3i(10,10,10);}
public static class RebirthGameBridgeWorld {public static float PoiDistance;public static float DistanceTo(PrefabInstance p,Vector3 v){return PoiDistance;}}
public static class RebirthGameBridgePoiStorage {public const string CrateName="cntWoodWritableCrate";}
public class TileEntityComposite {public Owner Owner=new Owner();}
public class FastTags<T> {} public class TagGroup {public class Global{}}
public enum StreamModeRead {Persistency,FromServer,FromClient}
public enum StreamModeWrite {Persistency,ToServer,ToClient}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
public abstract class TEFeatureAbs {
public TileEntityComposite Parent=new TileEntityComposite();public BlockValue blockValue=new BlockValue{Block=new Block()};public int Modified;
public virtual void OnAdded(Vector3i p,BlockValue b){} public abstract void CopyFromInternal(TileEntityComposite other);public virtual void UpgradeDowngradeFrom(TileEntityComposite other){}
public virtual void OnBlockReset(Vector3i p,BlockValue b){}public virtual void Reset(FastTags<TagGroup.Global> tags){}public virtual void Read(PooledBinaryReader r,StreamModeRead m){}public virtual void Write(PooledBinaryWriter w,StreamModeWrite m){}
public Vector3i ToWorldPos(){return default(Vector3i);}public void SetModified(){Modified++;}public Vector3 ToWorldCenterPos(){return default(Vector3);}public virtual void OnLoad(){}
}
public static class RebirthPoiCrateLedgerService {public static bool Allow=true;public static bool Reserve(EntityPlayer p,PrefabInstance poi,Vector3i pos,Guid id){return Allow;}}
// PRODUCTION_CLASS
class Checks {
static void A(bool c,string m){if(!c)throw new Exception(m);}
static byte[] Save(TEFeatureRebirthPoiCrateIdentity marker){using(var s=new MemoryStream()){var w=new PooledBinaryWriter(s);marker.Write(w,StreamModeWrite.Persistency);w.Flush();return s.ToArray();}}
static void Read(TEFeatureRebirthPoiCrateIdentity marker,byte[] bytes,StreamModeRead mode=StreamModeRead.Persistency){using(var s=new MemoryStream(bytes))using(var r=new PooledBinaryReader(s))marker.Read(r,mode);}
static TEFeatureRebirthPoiCrateIdentity Create(){var m=new TEFeatureRebirthPoiCrateIdentity();m.OnAdded(default(Vector3i),default(BlockValue));return m;}
static void Main(){
var p=new EntityPlayerLocal();var poi=new PrefabInstance();var m=Create();var original=m.PlacementId;A(original!=Guid.Empty,"server placement ID");A(!m.MatchesPoi(poi)&&m.IsUnbound,"new placement unbound");A(m.HasLocalOwner(p),"local owner");A(m.TryBindServer(p,poi,original),"owner binds");A(m.MatchesPoi(poi)&&!m.IsUnbound,"bound scope cannot rebind");A(m.TryBindServer(p,poi,original),"same binding replay");A(!m.TryBindServer(p,new PrefabInstance{id=5},original),"no reassignment");A(!m.TryBindServer(p,poi,Guid.NewGuid()),"wrong placement rejected");
var denied=Create();RebirthPoiCrateLedgerService.Allow=false;A(!denied.TryBindServer(p,poi,denied.PlacementId)&&!denied.MatchesPoi(poi),"failed durable reservation cannot bind native marker");RebirthPoiCrateLedgerService.Allow=true;A(denied.TryBindServer(p,poi,denied.PlacementId),"retry durable reservation permits binding");
var bytes=Save(m);var reloaded=new TEFeatureRebirthPoiCrateIdentity();Read(reloaded,bytes);reloaded.OnLoad();A(reloaded.PlacementId==original&&reloaded.MatchesPoi(poi),"persist/read/load preserves placement scope");
var different=Save(Create());Read(reloaded,different,StreamModeRead.FromClient);A(reloaded.PlacementId==original&&reloaded.MatchesPoi(poi),"client cannot overwrite authoritative marker");
var bad=(byte[])bytes.Clone();bad[0]=255;try{Read(reloaded,bad,StreamModeRead.FromClient);throw new Exception("client invalid version accepted");}catch(InvalidDataException){}A(reloaded.PlacementId==original,"malformed client preserves server state");
for(int n=0;n<bytes.Length;n++){var truncated=new byte[n];Array.Copy(bytes,truncated,n);var target=Create();bool failed=false;try{Read(target,truncated);}catch(EndOfStreamException){failed=true;}catch(InvalidDataException){failed=true;}A(failed&&target.PlacementId==Guid.Empty,"truncated persisted marker must clear and refuse "+n);}
Read(reloaded,bytes);bad=(byte[])bytes.Clone();bad[17]=2;try{Read(reloaded,bad);throw new Exception("bad flag accepted");}catch(InvalidDataException){}A(reloaded.PlacementId==Guid.Empty,"invalid disk flag leaves unbound");
Read(reloaded,bytes);reloaded.CopyFromInternal(m.Parent);A(reloaded.PlacementId==Guid.Empty&&!reloaded.MatchesPoi(poi),"clone/copy cannot adopt custody");reloaded.OnAdded(default(Vector3i),default(BlockValue));A(reloaded.PlacementId!=Guid.Empty&&reloaded.PlacementId!=original,"new placement renews GUID");
Read(reloaded,bytes);reloaded.UpgradeDowngradeFrom(m.Parent);A(reloaded.PlacementId==Guid.Empty,"conversion clears identity");Read(reloaded,bytes);reloaded.OnBlockReset(default(Vector3i),default(BlockValue));A(reloaded.PlacementId==Guid.Empty,"block reset clears identity");Read(reloaded,bytes);reloaded.Reset(null);A(reloaded.PlacementId==Guid.Empty,"quest reset clears identity");
GameManager.Instance.Players.PrimaryId=new Owner{CombinedString="other"};A(!m.TryBindServer(p,poi,original),"other native PrimaryId refused");GameManager.Instance.Players.PrimaryId=new Owner();RebirthStablePlayerIdentity.Current="different-internal-platform-key";A(m.TryBindServer(p,poi,original),"native PrimaryId works independently of cross-platform internal key");GameManager.Instance.Players.PrimaryId=null;A(m.TryBindServer(p,poi,original),"local primary identity fallback");A(!m.TryBindServer(new EntityPlayer(),poi,original),"remote missing primary identity cannot use local fallback");GameManager.Instance.Players.PrimaryId=new Owner();PlatformManager.InternalLocalUserIdentifier=new Owner{CombinedString="other"};A(!m.HasLocalOwner(p),"other local owner refused");PlatformManager.InternalLocalUserIdentifier=new Owner();
p.Dead=true;A(!m.TryBindServer(p,poi,original),"dead actor refused");p.Dead=false;p.position=new Vector3(20,0,0);A(!m.TryBindServer(p,poi,original),"distant actor refused");p.position=default(Vector3);RebirthGameBridgeWorld.PoiDistance=13;A(!m.TryBindServer(p,poi,original),"distant POI refused");RebirthGameBridgeWorld.PoiDistance=0;
SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;A(Create().PlacementId==Guid.Empty,"client placement cannot mint");SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;GameManager.Instance.Edit=true;A(Create().PlacementId==Guid.Empty,"editor placement cannot mint");GameManager.Instance.Edit=false;
A(new TEFeatureRebirthPoiCrateIdentity().PlacementId==Guid.Empty,"old missing feature stays unmarked");Console.WriteLine("PASS actual POI marker class: server placement/bind, persisted roundtrip, all truncated prefixes, client-write refusal, clone/reset/renewal, owner/scope/distance checks. Native lifecycle, synchronization and identity adapters doubled.");
}}