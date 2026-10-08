using System;using System.Collections;using System.Collections.Generic;
struct Vector3i {public int x,y,z;}
class World {}
class EntityPlayerLocal {public World world=new World();public bool IsDead(){return false;}}
class PrefabInstance {}
class TileEntityComposite {}
class TEFeatureStorage {public TileEntityComposite Parent=new TileEntityComposite();}
class TEFeatureRebirthPoiCrateIdentity {public Guid PlacementId;public TileEntityComposite Parent;public bool Owned=true;public bool IsUnbound{get{return PlacementId!=Guid.Empty&&RebirthPoiCrateIdentity.Bound==Guid.Empty;}}public bool MatchesPoi(PrefabInstance poi){return RebirthPoiCrateIdentity.Bound==PlacementId;}public bool HasLocalOwner(EntityPlayerLocal p){return Owned;}}
static class Time {public static float realtimeSinceStartup;}
static class RebirthGameBridgeCombat {public static List<int> AwakeThreats(EntityPlayerLocal p,float radius,bool unused){return new List<int>();}}
static class RebirthPoiCrateIdentity {public static TEFeatureRebirthPoiCrateIdentity Marker;public static Guid Bound;public static TEFeatureRebirthPoiCrateIdentity Resolve(EntityPlayerLocal p,Vector3i site){return Marker;}public static bool RequestBinding(EntityPlayerLocal p,PrefabInstance poi,Vector3i site,Guid placement){Bound=placement;return true;}}
class Storage {
 public string Failure;private readonly Dictionary<Vector3i,TEFeatureStorage> placedCrateFeatures=new Dictionary<Vector3i,TEFeatureStorage>();private readonly Dictionary<Vector3i,Guid> placedCrateIds=new Dictionary<Vector3i,Guid>();private readonly List<Vector3i> crates=new List<Vector3i>();private readonly HashSet<Guid> publishedPlacementIds=new HashSet<Guid>();public static TEFeatureStorage Current;
 private static TEFeatureStorage Loot(EntityPlayerLocal p,Vector3i site){return Current;}private TEFeatureStorage PlacedLoot(EntityPlayerLocal p,Vector3i site){return RebirthPoiCrateIdentity.Bound==Guid.Empty?null:Current;}
 // PRODUCTION_WITNESS
 public IEnumerator Bind(EntityPlayerLocal p,PrefabInstance pi,Action<bool> done){Vector3i site=new Vector3i();Vector3i? chosen=null;Func<bool> sameContext=()=>true;while(true){
 // PRODUCTION_CLASS
 break;
 }done(Failure==null);}
}
class Checks {
 static void A(bool c,string m){if(!c)throw new Exception(m);}
 static Storage Test(Action<TEFeatureStorage,TEFeatureRebirthPoiCrateIdentity> atYield,out bool result){Time.realtimeSinceStartup=0;RebirthPoiCrateIdentity.Bound=Guid.Empty;var original=new TEFeatureStorage();Storage.Current=original;var marker=new TEFeatureRebirthPoiCrateIdentity{Parent=original.Parent};RebirthPoiCrateIdentity.Marker=marker;var storage=new Storage();bool delivered=false,called=false;var work=storage.Bind(new EntityPlayerLocal(),new PrefabInstance(),ok=>{called=true;delivered=ok;});A(work.MoveNext(),"captured original feature awaits marker");atYield(original,marker);Time.realtimeSinceStartup=1;while(work.MoveNext())Time.realtimeSinceStartup++;A(called,"completion callback");result=delivered;return storage;}
 static void Main(){
 bool result;var id=Guid.NewGuid();var s=Test((original,marker)=>{Storage.Current=new TEFeatureStorage();RebirthPoiCrateIdentity.Marker=new TEFeatureRebirthPoiCrateIdentity{PlacementId=id,Parent=Storage.Current.Parent};},out result);A(!result&&s.Failure!=null&&RebirthPoiCrateIdentity.Bound==Guid.Empty,"late replacement cannot bind");
 s=Test((original,marker)=>marker.PlacementId=id,out result);A(result&&s.Failure==null&&RebirthPoiCrateIdentity.Bound==id,"original late native marker can bind");
 s=Test((original,marker)=>{marker.PlacementId=id;marker.Parent=new TileEntityComposite();},out result);A(!result&&RebirthPoiCrateIdentity.Bound==Guid.Empty,"wrong native parent refused");
 s=Test((original,marker)=>{RebirthPoiCrateIdentity.Marker=new TEFeatureRebirthPoiCrateIdentity{PlacementId=id,Parent=original.Parent};},out result);A(!result&&RebirthPoiCrateIdentity.Bound==Guid.Empty,"same-parent replaced marker object refused");
 s=Test((original,marker)=>{marker.PlacementId=id;marker.Owned=false;},out result);A(!result&&RebirthPoiCrateIdentity.Bound==Guid.Empty,"owner change refused");
 Console.WriteLine("PASS5 actual placement-bind iterator/witness cases: replacement, original late marker, foreign parent, replaced marker object and owner change. Captured feature remains original custody; native world/marker/input doubled.");
 }
}