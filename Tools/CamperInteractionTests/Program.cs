using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

class Program
{
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static Vector3 V(JsonElement p)=>new Vector3(p.GetProperty("x").GetSingle(),p.GetProperty("y").GetSingle(),p.GetProperty("z").GetSingle());
    static void Main(string[] args)
    {
        // Synthetic cases isolate geometry edge conditions. The optional input
        // below is exported from the actual approved Unity prefab and its mesh.
        var vertices=new[]{new Vector3(1,0,0),new Vector3(1,3,0),new Vector3(1,0,3),new Vector3(-1,0,0),new Vector3(-1,3,0),new Vector3(-1,0,3)};
        var triangles=new[]{0,1,2,3,4,5};
        foreach(int side in new[]{-1,1})
        {
            Check(RebirthCamperRelayGeometry.Surface(vertices,triangles,new Vector3(side*3,1,1),new Vector3(-side,0,0),out var p,out var n),"Skin hit");
            Check(Math.Abs(p.x-side)<.00001f&&Math.Abs(n.x-side)<.00001f,"Nearest skin and outward normal");
        }
        Check(!RebirthCamperRelayGeometry.Surface(vertices,triangles,new Vector3(3,4,1),new Vector3(-1,0,0),out _,out _),"Missing skin must fail closed");
        Check(!RebirthCamperRelayGeometry.Surface(vertices,triangles,new Vector3(3,1,1),Vector3.up,out _,out _),"Parallel ray");
        var bounds=new Bounds(new Vector3(.2f,.4f,-.3f),new Vector3(.1f,.6f,.2f));
        Check(Math.Abs(RebirthCamperRelayGeometry.RedStripeCenter-2.345f)<.000001f,"Exact authored red stripe midpoint");
        Check(Math.Abs((RebirthCamperRelayGeometry.RedStripeCenter-RebirthCamperRelayGeometry.RedStripeBottom)-
            (RebirthCamperRelayGeometry.RedStripeTop-RebirthCamperRelayGeometry.RedStripeCenter))<.000001f,"Equal stripe margins");
        var offset=RebirthCamperRelayGeometry.SeatOffset(bounds);
        Check(Math.Abs(bounds.min.y+offset.y-.002f)<.00001f,"Plate seated two millimetres above skin");
        Check(Math.Abs(bounds.center.x+offset.x)<.00001f&&Math.Abs(bounds.center.z+offset.z)<.00001f,"Plate centered tangentially");
        if(args.Length>0)
        {
            var input=JsonDocument.Parse(System.IO.File.ReadAllText(args[0])).RootElement;
            vertices=input.GetProperty("vertices").EnumerateArray().Select(V).ToArray();
            triangles=input.GetProperty("triangles").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            float y=RebirthCamperRelayGeometry.RedStripeCenter,z=input.GetProperty("mountZ").GetSingle();
            if(input.TryGetProperty("batteryTop",out var batteryTop))Check(y>batteryTop.GetSingle(),"Stripe-centred relay remains above bank");
            foreach(int side in new[]{-1,1})
            {
                Check(RebirthCamperRelayGeometry.Surface(vertices,triangles,new Vector3(side*3,y,z),new Vector3(-side,0,0),out var p,out var n),"Actual camper skin hit");
                Check(side*p.x>.5f&&side*p.x<1.5f&&side*n.x>.9f,"Actual side skin, not broad hull");
                Console.WriteLine("Actual relay skin side="+side+" position="+p+" normal="+n);
            }
        }
        var world=new World();GameManager.Instance.World=world;
        var actor=new GameObject("Player").AddComponent<EntityPlayerLocal>();actor.entityId=1;actor.world=world;actor.RootTransform=actor.transform;world.Entities[1]=actor;
        var vehicle=new GameObject("Model").AddComponent<EntityVehicle>();vehicle.entityId=2;vehicle.world=world;vehicle.RootTransform=vehicle.ModelTransform=vehicle.transform;world.Entities[2]=vehicle;
        var proxy=vehicle.gameObject.AddComponent<BoxCollider>();
        // Actual native contract: visual shell siblings do not inherit the
        // forwarding on Physics; tag alone cannot resolve an entity.
        var physics=new GameObject("Physics");physics.transform.SetParent(vehicle.transform);
        physics.AddComponent<CollisionCallForward>().Entity=vehicle;
        var nativeHull=new GameObject("Native Physics hull");nativeHull.transform.SetParent(physics.transform);nativeHull.AddComponent<BoxCollider>();
        var plough=new GameObject("Native strong plough hull");plough.transform.SetParent(physics.transform);plough.tag="E_VehicleStrong";plough.AddComponent<BoxCollider>();
        var shell=new GameObject("REBIRTHCampWalkInColliders");shell.transform.SetParent(vehicle.transform);shell.activeSelf=false;
        foreach(string name in new[]{"Camp left wall","Camp right wall","Camp rear jamb","Travel CamperHull","Rear door"})
        {
            var surface=new GameObject(name);surface.transform.SetParent(shell.transform);surface.layer=21;
            var solid=surface.AddComponent<BoxCollider>();solid.enabled=false;
        }
        var passenger=new GameObject("Seated actor");passenger.transform.SetParent(vehicle.transform);passenger.AddComponent<EntityPlayer>();var passengerCollider=passenger.AddComponent<BoxCollider>();
        var authoredTrigger=new GameObject("Authored trigger");authoredTrigger.transform.SetParent(shell.transform);authoredTrigger.AddComponent<BoxCollider>().isTrigger=true;
        var wheel=new GameObject("Wheel");wheel.transform.SetParent(physics.transform);wheel.AddComponent<WheelCollider>();
        RebirthCamperNativeTargets.Bind(vehicle);
        RebirthCamperNativeTargets.Bind(vehicle);
        foreach(var solid in shell.GetComponentsInChildren<BoxCollider>(true).Where(c=>!c.isTrigger))
        {
            Check(solid.gameObject.tag=="E_Vehicle"&&CollisionCallForward.FindEntity(solid.transform)==vehicle,"Native entity selection for "+solid.name);
            Check(!solid.enabled&&solid.gameObject.layer==21,"Native binding preserves physical geometry state for "+solid.name);
        }
        Check(passengerCollider.GetComponent<CollisionCallForward>()==null,"Seated player collider is not rebound to vehicle");
        Check(authoredTrigger.GetComponent<CollisionCallForward>()==null,"Special trigger remains separate");
        Check(wheel.GetComponent<CollisionCallForward>()==null,"Wheel retains native parent forwarding");
        Check(nativeHull.GetComponent<CollisionCallForward>()==null&&CollisionCallForward.FindEntity(nativeHull.transform)==vehicle,"Existing Physics forwarding reused without duplicate collision callback");
        Check(plough.tag=="E_VehicleStrong","Native strong plough impact tag preserved");
        var body=new GameObject("Closed rear door");body.transform.SetParent(vehicle.transform);var door=body.AddComponent<BoxCollider>();
        var station=new GameObject("REBIRTHInteraction_rear_door");station.transform.SetParent(vehicle.transform);station.transform.position=new Vector3(0,0,2);var target=station.AddComponent<BoxCollider>();target.isTrigger=true;
        var relay=new GameObject("Relay");relay.transform.SetParent(vehicle.transform);relay.transform.position=new Vector3(0,0,2);var wire=relay.AddComponent<RebirthCamperWireTarget>();wire.Vehicle=vehicle;var wireBox=relay.AddComponent<BoxCollider>();wireBox.isTrigger=true;
        var wall=new GameObject("World wall").AddComponent<BoxCollider>();
        var other=new GameObject("Other model").AddComponent<EntityVehicle>();other.ModelTransform=other.transform;var otherProxy=other.gameObject.AddComponent<BoxCollider>();
        Check(RebirthCamperPhysicalTargets.SelectionProxy(proxy,vehicle),"Exact native model-root proxy");
        Check(!RebirthCamperPhysicalTargets.SelectionProxy(door,vehicle),"Real door is not a proxy");
        Check(!RebirthCamperPhysicalTargets.SelectionProxy(otherProxy,vehicle),"Other vehicle is not a proxy");
        Physics.Hits=new[]{Hit(proxy,1),Hit(target,2)};
        Check(RebirthCamperPhysicalTargets.TryGetFocused(actor,out var found,out var id)&&ReferenceEquals(found,vehicle)&&id=="rear_door","Physical focus through broad proxy");
        Physics.Hits=new[]{Hit(proxy,1),Hit(wireBox,2)};
        Check(ReferenceEquals(RebirthCamperWireTarget.Focus(actor),wire),"Wire focus through broad proxy");
        foreach(var solid in new Collider[]{door,wall,otherProxy})
        {
            Physics.Hits=new[]{Hit(proxy,1),Hit(solid,1.5f),Hit(target,2)};
            Check(!RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out _),"Physical focus blocked by "+solid.name);
            Physics.Hits=new[]{Hit(proxy,1),Hit(solid,1.5f),Hit(wireBox,2)};
            Check(RebirthCamperWireTarget.Focus(actor)==null,"Wire focus blocked by "+solid.name);
        }
        Physics.Hits=new[]{Hit(proxy,1),Hit(target,2),Hit(wall,3)};
        Check(RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out _),"Wall beyond target does not block");
        Physics.Hits=new[]{Hit(proxy,1),Hit(door,2),Hit(target,2)};
        Check(RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out _),"Coincident closed-door face does not hide its own interaction trigger");
        foreach(var targetId in (HashSet<string>)typeof(RebirthCamperPhysicalTargets).GetField("ids",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null))
        {
            station.name="REBIRTHInteraction_"+targetId;
            Check(RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out var focusedId)&&focusedId==targetId,"Physical focus for "+targetId);
        }
        station.name="REBIRTHInteraction_rear_door";
        station.activeSelf=false;Check(!RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out _),"Hidden station is unavailable");station.activeSelf=true;
        RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Travel;
        Check(!RebirthCamperPhysicalTargets.TryGetFocused(actor,out _,out _),"Physical station/door policy still Camp-only");
        var ladderObject=new GameObject("REBIRTHRearLadderClimbVolume");ladderObject.transform.SetParent(vehicle.RootTransform);
        var ladder=ladderObject.AddComponent<BoxCollider>();ladder.center=Vector3.zero;ladder.size=new Vector3(2,2,2);ladder.enabled=false;
        actor.RootTransform=actor.transform;actor.transform.position=Vector3.zero;
        RebirthCamperRearLadders.Bind(vehicle);
        RebirthCamperBodyAttachment.Body=true;
        Check(RebirthCamperRearLadders.Contains(actor),"Body-fitted Travel admits authored ladder");
        RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Camp;
        Check(RebirthCamperRearLadders.Contains(actor),"Camp retains native ladder admission");
        RebirthCamperBodyAttachment.Body=false;Check(!RebirthCamperRearLadders.Contains(actor),"Bare pickup has no camper ladder admission");RebirthCamperBodyAttachment.Body=true;
        foreach(var mode in new[]{RebirthCamperEnergyMode.Deploying,RebirthCamperEnergyMode.Packing})
        {RebirthCamperEnergyRuntime.Mode=mode;Check(!RebirthCamperRearLadders.Contains(actor),"Transition refuses ladder admission");}
        RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Travel;
        ladderObject.activeSelf=false;Check(!RebirthCamperRearLadders.Contains(actor),"Hidden authored ladder refuses admission");ladderObject.activeSelf=true;
        actor.AttachedToEntity=vehicle;Check(!RebirthCamperRearLadders.Contains(actor),"Seated player cannot climb");actor.AttachedToEntity=null;
        Console.WriteLine("PASS "+checks+" production geometry, physical-focus and wire-focus checks");
    }
    static RaycastHit Hit(Collider c,float distance)=>new RaycastHit{collider=c,distance=distance};
}

// Native shapes and Unity physics results are stubbed. Production algorithms
// above are linked directly; these tests do not claim Unity or game execution.
namespace UnityEngine
{
    public class Object {public string name;public static implicit operator bool(Object x)=>x!=null;}
    public class GameObject:Object
    {
        readonly List<Component> components=new List<Component>();public readonly Transform transform;public bool activeSelf=true;public string tag="Untagged";public int layer;
        public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
        public GameObject(string n){name=n;transform=new Transform{gameObject=this};components.Add(transform);}
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
        public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>components.OfType<T>().Concat(transform.children.SelectMany(t=>t.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
    }
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;public new string name=>gameObject.name;
        public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
        public T GetComponentInParent<T>()where T:class{for(var t=transform;t!=null;t=t.parent){var c=t.GetComponent<T>();if(c!=null)return c;}return null;}
        public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>gameObject.GetComponentsInChildren<T>(inactive);
    }
    public class MonoBehaviour:Component{}
    public class Transform:Component
    {
        public Transform parent;public Vector3 position;public List<Transform> children=new List<Transform>();
        public void SetParent(Transform p){parent=p;p.children.Add(this);}public bool IsChildOf(Transform t)=>parent==t||parent!=null&&parent.IsChildOf(t);
        public Vector3 InverseTransformPoint(Vector3 point)=>point-position;
    }
    public class Collider:Component{public bool enabled=true,isTrigger;public Vector3 ClosestPoint(Vector3 p)=>transform.position;}
    public class BoxCollider:Collider{public Vector3 center,size;}
    public class WheelCollider:Collider{}
    public struct Bounds{public Vector3 center,size;public Bounds(Vector3 c,Vector3 s){center=c;size=s;}public Vector3 min=>center-size*.5f;public bool Contains(Vector3 p)=>Math.Abs(p.x-center.x)<=size.x*.5f&&Math.Abs(p.y-center.y)<=size.y*.5f&&Math.Abs(p.z-center.z)<=size.z*.5f;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3(0,1,0);
        public float sqrMagnitude=>Dot(this,this);public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude<1e-12?zero:this*(1/magnitude);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator -(Vector3 a,Vector3 b)=>a+b*(-1);public static Vector3 operator -(Vector3 a)=>a*(-1);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);public static Vector3 operator /(Vector3 a,float b)=>a*(1/b);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public override string ToString()=>$"({x:F5},{y:F5},{z:F5})";
    }
    public struct Ray{public Vector3 origin,direction;public Ray(Vector3 o,Vector3 d){origin=o;direction=d;}}
    public struct RaycastHit{public Collider collider;public float distance;}
    public enum QueryTriggerInteraction{Collide,Ignore}
    public static class Physics{public const int AllLayers=-1;public static RaycastHit[] Hits=Array.Empty<RaycastHit>();public static RaycastHit[] RaycastAll(Ray r,float d,int mask,QueryTriggerInteraction q)=>Hits.Where(h=>h.distance<=d&&(q==QueryTriggerInteraction.Collide||!h.collider.isTrigger)).ToArray();}
    public static class Mathf{public static float Abs(float f)=>Math.Abs(f);}
}
class Entity:MonoBehaviour{public int entityId;public World world;public Transform RootTransform;public Vector3 position;public bool IsDead()=>false;}
class EntityAlive:Entity{public bool bInElevator;public void CalcIfInElevator(){}}
class EntityPlayer:EntityAlive{public float GetEyeHeight()=>0;}
class Flag{public bool Value;}
class EntityPlayerLocal:EntityPlayer{public Entity AttachedToEntity;public Flag IsFlyMode=new Flag();public Ray GetLookRay()=>new Ray(Vector3.zero,new Vector3(0,0,1));}
class EntityVehicle:Entity{public object vehicle=new object();public Transform ModelTransform;}
class CollisionCallForward:MonoBehaviour{public Entity Entity;public static Entity FindEntity(Transform t)=>t.GetComponent<Entity>()??t.GetComponentInParent<CollisionCallForward>()?.Entity;}
class World{public readonly Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();public Entity GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class ThreadManager{public static bool IsMainThread()=>true;}
static class Origin{public static Vector3 position;}
static class Constants{public static float cCollectItemDistance=4,cDigAndBuildDistance=4;}
static class RebirthCamperRadial{public static bool IsCamper(EntityVehicle v)=>true;public static bool IsDriver(EntityVehicle v,EntityPlayer p)=>false;}
enum RebirthCamperEnergyMode{Travel,Camp,Deploying,Packing}
static class RebirthCamperEnergyRuntime{public static RebirthCamperEnergyMode Mode=RebirthCamperEnergyMode.Camp;public static bool TryObserveMode(EntityVehicle v,out RebirthCamperEnergyMode mode,out long revision,out float remaining){mode=Mode;revision=0;remaining=0;return true;}public static bool TryObservePresentation(EntityVehicle v,out RebirthCamperEnergyMode mode,out long revision,out float range,out float remaining){range=0;return TryObserveMode(v,out mode,out revision,out remaining);}}
static class RebirthCamperBodyAttachment{public static bool Body=true;public static bool HasBody(EntityVehicle v)=>Body;}
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string method){}}}
