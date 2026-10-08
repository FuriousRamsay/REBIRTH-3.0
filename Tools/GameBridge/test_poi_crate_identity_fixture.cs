using System;using System.Collections.Generic;
struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class Block {public string Name;public string GetBlockName()=>Name;}
struct BlockValue {public Block Block;}
class TEFeatureStorage{public object Parent;}
class Lootable:TEFeatureStorage{}
class PrefabInstance {public Vector3i boundingBoxPosition,boundingBoxSize=new Vector3i(1,1,1);}
class TEFeatureRebirthPoiCrateIdentity {public object Parent;public Guid PlacementId;public bool Bound,Owned=true;public bool MatchesPoi(PrefabInstance p){return p!=null&&Bound&&PlacementId!=Guid.Empty;}public bool HasLocalOwner(EntityPlayerLocal p){return Owned&&p!=null;}}
class TileEntity {public TEFeatureStorage Loot;public TEFeatureRebirthPoiCrateIdentity Marker;public bool TryGetSelfOrFeature<T>(out T feature) where T:class {feature=(typeof(T)==typeof(TEFeatureStorage)?(object)Loot:Marker) as T;return feature!=null;}public Vector3i ToWorldPos(){return default(Vector3i);}}
class TileList {public List<TileEntity> list=new List<TileEntity>();}
class Chunk {public TileList Tiles=new TileList();public TileList GetTileEntities(){return Tiles;}}
class GameManager {public static GameManager Instance=new GameManager();public World World;} class World {public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer(){return Primary;}public BlockValue Block;public TileEntity Tile;public int TileReads;public bool Loaded=true;public BlockValue GetBlock(Vector3i p)=>Block;public TileEntity GetTileEntity(Vector3i p){TileReads++;return Tile;}public static int toChunkXZ(int p){return p>>4;}public object GetChunkSync(int x,int z){if(!Loaded)return null;var c=new Chunk();if(Tile!=null)c.Tiles.list.Add(Tile);return c;}}
class EntityPlayerLocal {public World world;}
class RebirthPoiCrateIdentity {public static TEFeatureRebirthPoiCrateIdentity Resolve(EntityPlayerLocal p,Vector3i pos){return p?.world?.Tile?.Marker;}}
class Storage {private World custodyWorld;public Storage(World w){custodyWorld=w;}public const string CrateName="cntWoodWritableCrate";
private readonly Dictionary<Vector3i,TEFeatureStorage> placedCrateFeatures=new Dictionary<Vector3i,TEFeatureStorage>();
private readonly Dictionary<Vector3i,Guid> placedCrateIds=new Dictionary<Vector3i,Guid>();private readonly HashSet<Guid> publishedPlacementIds=new HashSet<Guid>();
private readonly List<Vector3i> crates=new List<Vector3i>();private PrefabInstance boundPoi;private bool expectationsLoaded=true;private string expectationFailure,recoveryFailure;public void Expect(Guid id){publishedPlacementIds.Add(id);placedCrateIds[new Vector3i()]=id;crates.Add(new Vector3i());}
public void Remember(TEFeatureStorage feature){placedCrateFeatures[new Vector3i()]=feature;if(!crates.Contains(new Vector3i()))crates.Add(new Vector3i());}
public TEFeatureStorage Tracked(EntityPlayerLocal player){return PlacedLoot(player,new Vector3i());}
public int Count=>crates.Count;
// PRODUCTION_CLASS
public static bool Current(EntityPlayerLocal p,TEFeatureStorage expected,TEFeatureStorage opened)=>IsDepositTarget(p,new Vector3i(),expected,opened);
public static TEFeatureStorage Resolve(EntityPlayerLocal p)=>Loot(p,new Vector3i());
}
class Check {static void A(bool v,string reason){if(!v)throw new Exception(reason);}static void Main(){
var loot=new Lootable();var w=new World{Tile=new TileEntity{Loot=loot},Block=new BlockValue{Block=new Block{Name=Storage.CrateName}}};var p=new EntityPlayerLocal{world=w};GameManager.Instance.World=w;w.Primary=p;
A(Storage.Resolve(p)==loot,"current crate feature");var tracked=new Storage(w);A(tracked.Tracked(p)==null,"untracked refused");tracked.Remember(loot);A(tracked.Tracked(p)==null&&!tracked.Contains(p,new Vector3i()),"unbound session original refuses cargo admission");A(tracked.CustodyFailure(p)!=null,"unpublished original custody remains unresolved");A(Storage.Current(p,loot,loot),"UI/current target");A(!Storage.Current(p,loot,new Lootable()),"substitute UI refused");
var original=w.Tile;w.Tile=new TileEntity{Loot=new Lootable()};A(!Storage.Current(p,loot,loot)&&tracked.Tracked(p)==null,"same-type replacement refused");A(!tracked.Contains(p,new Vector3i())&&tracked.CustodyFailure(p)!=null,"replacement cannot be excluded with certified custody");w.Tile=original;A(tracked.CustodyFailure(p)!=null,"same original feature restored still requires published binding");
w.Block=new BlockValue{Block=new Block{Name="otherContainer"}};int reads=w.TileReads;A(Storage.Resolve(p)==null&&w.TileReads==reads,"different block before tile lookup");A(!tracked.Contains(p,new Vector3i()),"other container not certified storage");w.Block=new BlockValue();A(Storage.Resolve(p)==null,"removed block");w.Block=new BlockValue{Block=new Block{Name=Storage.CrateName}};w.Tile=null;A(Storage.Resolve(p)==null&&tracked.CustodyFailure(p)!=null,"unloaded tile unresolved");w.Tile=new TileEntity();A(Storage.Resolve(p)==null,"missing storage feature");
var id=Guid.NewGuid();w.Tile=new TileEntity{Loot=new Lootable(),Marker=new TEFeatureRebirthPoiCrateIdentity{PlacementId=id,Bound=true}};var recovered=new Storage(w);recovered.Expect(id);var poi=new PrefabInstance();var unrecorded=new Storage(w);unrecorded.Recover(p,poi);A(unrecorded.Count==0&&unrecorded.CustodyFailure(p)!=null,"unrecorded native marker fails closed");A(recovered.Recover(p,poi)&&recovered.Count==1&&recovered.Contains(p,new Vector3i()),"native marker recovers once despite overlapping scans");
w.Tile=new TileEntity{Loot=new Lootable(),Marker=new TEFeatureRebirthPoiCrateIdentity{PlacementId=id,Bound=true}};A(recovered.Contains(p,new Vector3i())&&recovered.CustodyFailure(p)==null,"same durable identity accepts new feature after reload");
w.Tile.Marker.PlacementId=Guid.NewGuid();recovered.Recover(p,poi);A(!recovered.Contains(p,new Vector3i())&&recovered.CustodyFailure(p)!=null,"recovery cannot overwrite remembered placement with replacement ID");
w.Tile.Marker.PlacementId=id;w.Tile.Marker.Owned=false;A(!recovered.Contains(p,new Vector3i()),"owner change refused");var stranger=new Storage(w);stranger.Recover(p,poi);A(stranger.Count==0,"foreign marker not adopted");w.Tile.Marker.Owned=true;w.Tile.Marker.Bound=false;var unbound=new Storage(w);unbound.Recover(p,poi);A(unbound.Count==0,"unbound marker not adopted");w.Tile.Marker.Bound=true;w.Tile.Marker.PlacementId=Guid.Empty;unbound.Recover(p,poi);A(unbound.Count==0,"legacy empty marker not adopted");
var savedWorld=p.world;p.world=new World();A(recovered.CustodyFailure(p)!=null,"world transition invalidates custody");p.world=savedWorld;
w.Loaded=false;A(!recovered.Recover(p,poi),"missing recovery chunk explicitly incomplete");
Console.WriteLine("PASS actual POI crate resolver/recovery/custody predicates: session replacement refusal, native marker reload reuse, immutable remembered ID, owner/unbound/legacy refusal and incomplete chunk scan. Native world/features/marker adapters doubled.");}}
