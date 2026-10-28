using System;
using System.Reflection;
namespace HarmonyLib {[AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}public HarmonyPatch(Type t,string n,Type[] a){}} public class HarmonyPriority:Attribute {public HarmonyPriority(int p){}} public static class Priority{public const int Last=0;}}
namespace UnityEngine {
 public class Object {public static void Destroy(Object o){}public static implicit operator bool(Object o)=>o!=null;}
 public class Transform:Object,System.Collections.IEnumerable {public string name; public System.Collections.Generic.List<Transform> Children=new System.Collections.Generic.List<Transform>();public MeshFilter[] Filters=Array.Empty<MeshFilter>();public System.Collections.IEnumerator GetEnumerator()=>Children.GetEnumerator();public Transform parent,Hull;public GameObject gameObject=new GameObject();public MeshCollider[] Colliders=Array.Empty<MeshCollider>();public Transform Find(string name)=>Hull;public T[] GetComponentsInChildren<T>(bool hidden) where T:class=>typeof(T)==typeof(MeshFilter)?Array.ConvertAll(Filters,c=>c as T):Array.ConvertAll(Colliders,c=>c as T);}
 public class Mesh:Object {public int[] triangles;public int Writes;public void SetTriangles(int[] a,int sub,bool bounds){triangles=a;Writes++;}} public class MeshFilter:Object {public string name;public Transform transform;public Mesh mesh;} public class MeshCollider:Object {public GameObject gameObject=new GameObject();public bool convex,isTrigger;}
 public class Rigidbody:Object {public Transform transform;}
 public class GameObject:Object {public string tag;public int layer;public ColliderHitCallForward Forward;public T GetComponent<T>() where T:class=>Forward as T;}
 public class Collision {public GameObject gameObject=new GameObject();public Transform transform=new Transform();public Rigidbody rigidbody;}
 public static class Mathf {public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v));public static float Max(float a,float b)=>Math.Max(a,b);}
}
public class TagGroup {public class Global{}}
public struct FastTags<T> {string tag;public static FastTags<T> Parse(string s)=>new FastTags<T>{tag=s};public bool Test_AnySet(FastTags<T> t)=>tag!=null&&Array.IndexOf(tag.Split(','),t.tag)>=0;}
public class ItemClass {public string Name;public string GetItemName()=>Name;}
public class ItemClassModifier:ItemClass {public FastTags<TagGroup.Global> ModifierTags;}
public class ItemValue {public ItemClass ItemClass;public ItemValue[] Mods=Array.Empty<ItemValue>();public int ModificationCount=>Mods.Length;public ItemValue GetModification(int i)=>Mods[i];public int MaxUseTimes=8000;public float UseTimes;}
public class HealthStat {public float BaseMax=8000,OriginalMax=8000;}
public class Stats {public HealthStat Health=new HealthStat();}
public class World {public Entity Attacker;public Entity GetEntity(int id)=>Attacker;}
public class Entity:UnityEngine.Object {public Stats Stats=new Stats();public int Health=6000;}
public class EntityZombie:Entity{}
public class EntityVehicle:Entity {public UnityEngine.Transform PhysicsTransform,ModelTransform;public void OnEntityUnload(){}public void PostInit(){}public void ReadSyncData(){}public Vehicle vehicle;public World world=new World();public bool isEntityRemote;public void ProcessDamageResponseLocal(DamageResponse d){}public void ApplyDamage(int damage){}public void OnCollisionForward(UnityEngine.Transform t,UnityEngine.Collision c,bool stay){}}
public class Vehicle {public enum Event{HealthChanged,SimulationUpdate}public void FireEvent(Event e){}public string Name="rebirthcamper80s";public EntityVehicle entity;public ItemValue itemValue=new ItemValue();public FastTags<TagGroup.Global> ModTags;public float EffectSelfDamagePer=1,EffectEntityDamagePer=1.6f,EffectStrongSelfDamagePer=.25f;public string GetName()=>Name;public void CalcMods(){}}
public class DamageSource {public int getEntityId()=>1;}
public struct DamageResponse {public DamageSource Source;}
public class ColliderHitCallForward:UnityEngine.Object {public Entity Entity;}
public static class CollisionCallForward {public static Entity FindEntity(UnityEngine.Transform t)=>null;}
class Fixture {
 static int passed;
 static object[] Call(Type t,string method,params object[] args){t.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);return args;}
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
 static void VisibilityChecks(){
 var root=new UnityEngine.Transform();var child=new UnityEngine.Transform();
 child.Children.Add(new UnityEngine.Transform{name="ArmorRange_1_95_0_3"});child.Children.Add(new UnityEngine.Transform{name="ArmorRange_54_5_3_3"});
 var mesh=new UnityEngine.Mesh{triangles=new[]{0,1,2,3,4,5}};root.Filters=new[]{new UnityEngine.MeshFilter{name="REBIRTHArmorSurface_plates",transform=child,mesh=mesh}};
 var entity=new EntityVehicle{ModelTransform=root,Health=24000};entity.Stats.Health.BaseMax=24000;var vehicle=new Vehicle{entity=entity,ModTags=FastTags<TagGroup.Global>.Parse("armor")};entity.vehicle=vehicle;
 RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==6,"load full health restores every triangle");
 entity.Health=23157;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==3&&mesh.triangles[0]==3,"exact threshold removes only assigned piece");
 int writes=mesh.Writes;entity.Health=23156;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.Writes==writes,"no mesh work between thresholds");
 entity.Health=8000;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==0,"all armor gone at final threshold");
 entity.Health=8001;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==3,"repair restores in reverse order");
 entity.Health=24000;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==6,"full repair restores original indices");
 vehicle.ModTags=default;entity.Health=8000;RebirthCamperArmorVisibility.Apply(vehicle);vehicle.ModTags=FastTags<TagGroup.Global>.Parse("armor");RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.triangles.Length==0,"mod reinstall respects current condition");
 foreach(int threshold in new[]{95,93,92,90,88,87,85,83,81,80,78,76,75,73,71,70,68,66,64,63,61,59,58,56,54,53,51,49,47,46,44,42,41,39,37,36,34,32,30,29,27,25,24,22,20,19,17,15,13,12,10,8,7,5}){
 if(RebirthCamperArmorVisibility.Visible((int)(24000L*(85+2*threshold)/285),24000,threshold)||!RebirthCamperArmorVisibility.Visible((int)(24000L*(85+2*threshold)/285)+1,24000,threshold))throw new Exception("Threshold "+threshold);
 }Check(true,"all 54 boundaries exact at native 24000 HP");
 var doorRoot=new UnityEngine.Transform();var panelTransform=new UnityEngine.Transform();panelTransform.Children.Add(new UnityEngine.Transform{name="ArmorRange_15_71_0_3"});var hardwareTransform=new UnityEngine.Transform();hardwareTransform.Children.Add(new UnityEngine.Transform{name="ArmorRange_15_71_0_3"});var panel=new UnityEngine.Mesh{triangles=new[]{0,1,2}};var hardware=new UnityEngine.Mesh{triangles=new[]{0,1,2}};doorRoot.Filters=new[]{new UnityEngine.MeshFilter{name="REBIRTHArmorSurface_door",transform=panelTransform,mesh=panel},new UnityEngine.MeshFilter{name="REBIRTHArmorSurface_doorHardware",transform=hardwareTransform,mesh=hardware}};var de=new EntityVehicle{ModelTransform=doorRoot,Health=19116};de.Stats.Health.BaseMax=24000;var dv=new Vehicle{entity=de};de.vehicle=dv;RebirthCamperArmorVisibility.Apply(dv);de.Health=19115;RebirthCamperArmorVisibility.Apply(dv);Check(panel.triangles.Length==0&&hardware.triangles.Length==0,"door grille and separately textured hardware hide together");de.Health=19116;RebirthCamperArmorVisibility.Apply(dv);Check(panel.triangles.Length==3&&hardware.triangles.Length==3,"repair restores both parts of one logical damage piece");
 vehicle.Name="vehicle4x4Truck";entity.Health=24000;writes=mesh.Writes;RebirthCamperArmorVisibility.Apply(vehicle);Check(mesh.Writes==writes,"other vehicle meshes untouched");
 }
 static void Main(){
 VisibilityChecks();
 var armor=new ItemValue{ItemClass=new ItemClassModifier{ModifierTags=FastTags<TagGroup.Global>.Parse("armor")}};
 var item=new ItemValue{ItemClass=new ItemClass{Name="rebirthCamper80sPlaceable"},Mods=new[]{armor}};
 Check((int)Call(typeof(RebirthCamperArmorDurability),"Postfix",item,8000)[1]==24000,"armor triples camper durability");
 item.ItemClass.Name="vehicle4x4Truck";Check((int)Call(typeof(RebirthCamperArmorDurability),"Postfix",item,8000)[1]==8000,"other vehicles unchanged");
 var e=new EntityVehicle();var v=new Vehicle{entity=e,ModTags=FastTags<TagGroup.Global>.Parse("armor")};e.vehicle=v;
 v.itemValue.MaxUseTimes=24000;Call(typeof(RebirthCamperArmorCapacity),"Postfix",v);Check(e.Health==18000&&e.Stats.Health.BaseMax==24000&&v.itemValue.UseTimes==6000,"install preserves 75 percent condition");
 v.itemValue.MaxUseTimes=8000;Call(typeof(RebirthCamperArmorCapacity),"Postfix",v);Check(e.Health==6000&&v.itemValue.UseTimes==2000,"remove preserves condition without repair");
 e.Health=1;v.itemValue.MaxUseTimes=24000;Call(typeof(RebirthCamperArmorCapacity),"Postfix",v);Check(e.Health==1,"broken vehicle remains broken after armor install");
 e.world.Attacker=new EntityZombie();var response=new DamageResponse{Source=new DamageSource()};var args=Call(typeof(RebirthCamperZombieDamageScope),"Prefix",e,response,null);int total=0;
 for(int i=0;i<1000;i++)total+=(int)Call(typeof(RebirthCamperZombieDamageReduction),"Prefix",e,1)[1];
 Check(total==50,"95 percent reduction retains fractional damage across small hits");
 Call(typeof(RebirthCamperZombieDamageScope),"Finalizer",null,args[2]);Check(RebirthCamperModProtection.ZombieDamageTarget==null,"damage context restored");
 e.world.Attacker=new Entity();args=Call(typeof(RebirthCamperZombieDamageScope),"Prefix",e,response,null);Check((int)Call(typeof(RebirthCamperZombieDamageReduction),"Prefix",e,100)[1]==100,"non-zombie damage unchanged");Call(typeof(RebirthCamperZombieDamageScope),"Finalizer",null,args[2]);
 var collision=new UnityEngine.Collision();collision.gameObject.Forward=new ColliderHitCallForward{Entity=new EntityZombie()};v.ModTags=FastTags<TagGroup.Global>.Parse("armor,plow");
 args=Call(typeof(RebirthCamperZombiePloughImpact),"Prefix",e,collision,null);Check(v.EffectSelfDamagePer==0&&v.EffectEntityDamagePer>=2,"plough removes zombie impact self-damage and boosts impact");
 var error=new Exception("test");var returned=typeof(RebirthCamperZombiePloughImpact).GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{(object)error,args[2]});Check(ReferenceEquals(returned,error)&&v.EffectSelfDamagePer==1&&v.EffectEntityDamagePer==1.6f&&v.EffectStrongSelfDamagePer==.25f,"impact effects restored even on exception");
 v.ModTags=FastTags<TagGroup.Global>.Parse("armor");args=Call(typeof(RebirthCamperZombiePloughImpact),"Prefix",e,collision,null);Check(Math.Abs(v.EffectSelfDamagePer-.05f)<1e-6&&v.EffectStrongSelfDamagePer==1,"armor-only zombie impact uses 5 percent self-damage");Call(typeof(RebirthCamperZombiePloughImpact),"Finalizer",null,args[2]);
 collision.gameObject.Forward.Entity=new Entity();v.ModTags=FastTags<TagGroup.Global>.Parse("plow");Call(typeof(RebirthCamperZombiePloughImpact),"Prefix",e,collision,null);Check(v.EffectSelfDamagePer==1,"animal/player impacts unchanged");
 var hull=new UnityEngine.Transform{Colliders=new[]{new UnityEngine.MeshCollider{isTrigger=true}}};var physics=new UnityEngine.Transform{Hull=hull};RebirthCamperModProtection.BindPloughHull(physics);Check(hull.Colliders[0].gameObject.tag=="E_VehicleStrong"&&hull.Colliders[0].gameObject.layer==21&&hull.Colliders[0].convex&&!hull.Colliders[0].isTrigger,"runtime native plough collider contract restored");
 Console.WriteLine("PASS "+passed+" policy checks; no gameplay simulation.");
 }
}




