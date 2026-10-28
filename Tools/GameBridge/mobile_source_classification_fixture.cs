using System;
using System.Linq;
using System.Collections.Generic;
// Explicit endpoint adapters. Only Blocks below is extracted production code.
class TileEntityWorkstation { public virtual int ToWorldPos()=>17; }
class RebirthCamperStationState:TileEntityWorkstation {
    public override int ToWorldPos()=>throw new InvalidOperationException("Detached station has no world position.");
}
class TileEntityComposite { public int ToWorldPos()=>18; }
class EntityVehicle { public int entityId=19; }
class EntityDrone { public int entityId=20; }
class ItemStackGrid { public object holder; }
class Bag { public ItemStackGrid ItemGrid; }
class TEFeatureStorage { public object Parent; }
static class ThreadManager { public static bool IsMainThread()=>true; }
static class RebirthCookingNativeReturn { public static object Held; public static bool Blocks(object value)=>ReferenceEquals(value,Held)&&value!=null; }
static class RemoteResourceIdentity {
    public static string Workstation(int p)=>"station:"+p;
    public static string Static(int p)=>"static:"+p;
    public static string Vehicle(int p)=>"vehicle:"+p;
    public static string Drone(int p)=>"drone:"+p;
}
class RebirthRemoteResourceNativeBinding {
    public object Endpoint;
    public static object Candidate;
    public static bool TryResolve(object world,string id,int kind,string incarnation,out RebirthRemoteResourceNativeBinding source) {
        source=new RebirthRemoteResourceNativeBinding{Endpoint=Candidate}; return Candidate!=null;
    }
}
public static class MobileSourceClassificationFixture {
    class Original { public string SourceId,Incarnation="birth"; public int Kind; }
    class Lease {
        public bool Own=true;
        public Lease(){}
        public Lease(Original original,RebirthRemoteResourceNativeBinding source){}
        public bool OwnsEndpoint()=>Own;
    }
    static bool installed=true,ready=true;
    static object world=new object();
    static Dictionary<object,Lease> held=new Dictionary<object,Lease>();
    static List<Original> originals=new List<Original>();
    static void Bootstrap(){}
    // ACTUAL_BLOCKS
    static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Run(){
        var mobile=new RebirthCamperStationState();
        Require(!Blocks(mobile),"Detached snapshot must not ask for terrain position");
        Require(!Blocks(new ItemStackGrid{holder=mobile}),"Holder normalization must preserve mobile classification");
        held[mobile]=new Lease();
        Require(Blocks(mobile),"Existing exact mobile endpoint hold must remain enforced");
        held.Clear();RebirthCookingNativeReturn.Held=mobile;
        Require(Blocks(mobile),"Cooking return hold must remain enforced");
        RebirthCookingNativeReturn.Held=null;
        object[] placed={new TileEntityWorkstation(),new TileEntityComposite(),new EntityVehicle(),new EntityDrone()};
        string[] ids={"station:17","static:18","vehicle:19","drone:20"};
        for(int i=0;i<placed.Length;i++){
            originals.Clear();originals.Add(new Original{SourceId=ids[i]});
            RebirthRemoteResourceNativeBinding.Candidate=placed[i];
            Require(Blocks(placed[i]),"Existing source admission lost: "+ids[i]);held.Clear();
        }
        Require(ready,"Unexpected multiple binding");
        Console.WriteLine("PASS actual Blocks: detached mobile, normalized holder, exact mobile/cooking holds, placed station/storage/vehicle/drone; native gameplay NOT RUN");
    }
}
