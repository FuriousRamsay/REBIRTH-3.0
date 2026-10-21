using UnityEngine;
class Program{
 static int checks;static void Check(bool yes,string name){if(!yes)throw new Exception(name);checks++;}
 static void Main(){var v=new EntityVehicle();var loads=RebirthCamperRelayWiring.MountedConsumers(v).ToArray();Check(loads.Length==4,"Four internally mounted consumers");
 for(int i=0;i<4;i++){Check(loads[i].DemandWatts==15,"Installed enabled turret demands15W without a relay link");loads[i].SetSupply(v,0);Check(!RebirthCamperRelayWiring.TurretPowered(v,i),"No source paid: not powered");loads[i].SetSupply(v,14);Check(!RebirthCamperRelayWiring.TurretPowered(v,i),"Underfunded15W: not powered");loads[i].SetSupply(v,15);Check(RebirthCamperRelayWiring.TurretPowered(v,i),"Fully funded without relay: powered");}
 Check(v.vehicle.Item.Text=="15","Four paid channels publish native mask");Time.realtimeSinceStartup=1.01f;Check(!RebirthCamperRelayWiring.TurretPowered(v,0),"Supply expires without another paid tick");
 var runtime=RebirthCamperTurretRuntime.For(v);runtime.Slots[0].Enabled=false;Check(loads[0].DemandWatts==0,"Disabled and unclaimed turret idle");runtime.Owner=3;Check(loads[0].DemandWatts==15,"Manual control pays15W even disabled-auto");runtime.Slots[0].HasTurret=false;Check(loads[0].DemandWatts==0,"Uninstalled turret no demand");loads[0].SetSupply(v,15);Check(!RebirthCamperRelayWiring.TurretPowered(v,0),"Uninstalled turret never granted power");
 v.world.Remote=true;Check(RebirthCamperRelayWiring.MountedConsumers(v).Count()==0,"Client creates no authoritative loads");Check(RebirthCamperRelayWiring.TurretPowered(v,1),"Client consumes paid replicated mask without wire links");Check(!RebirthCamperRelayWiring.TurretPowered(v,4),"Invalid corner refused");Console.WriteLine("PASS "+checks+" linked production mounted consumer checks; source spending/native circuit traversal are separate integration paths.");}
}
namespace UnityEngine{class Object{public static implicit operator bool(Object x)=>x!=null;}static class Time{public static float realtimeSinceStartup;}static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int RoundToInt(float x)=>(int)Math.Round(x);}}
class World{public bool Remote;public bool IsRemote()=>Remote;}
class Item{public string Text;public void SetMetadata(string key,string value){Text=value;}public bool TryGetMetadata(string key,out string text){text=Text;return text!=null;}}
class Vehicle{public readonly Item Item=new();public Item GetUpdatedItemValue()=>Item;}
class EntityVehicle:UnityEngine.Object{public readonly World world=new();public readonly Vehicle vehicle=new();public void SendSyncData(int flags){}}
interface IRebirthCamperEnergyConsumer{string Id{get;}int Priority{get;}float Distance{get;}int DemandWatts{get;}void SetSupply(EntityVehicle v,int watts);}
class Slot{public bool HasTurret=true,Enabled=true;}
class RebirthCamperTurretRuntime{static readonly Dictionary<EntityVehicle,RebirthCamperTurretRuntime> map=new();public Slot[] Slots=Enumerable.Range(0,4).Select(_=>new Slot()).ToArray();public int Owner=-1;public static RebirthCamperTurretRuntime For(EntityVehicle v){if(!map.TryGetValue(v,out var r))map[v]=r=new();return r;}public RebirthCamperTurretRuntime Observe()=>this;public int ControlOwner(int i)=>Owner;}
static class RebirthCamperTurretProfiles{public static object Block(Slot s)=>s;public static float Number(object b,string key,int fallback)=>15;}
internal static partial class RebirthCamperRelayWiring{private static bool TurretConnected(EntityVehicle v,int corner)=>throw new Exception("Built-in consumer must not inspect external wire graph");}
