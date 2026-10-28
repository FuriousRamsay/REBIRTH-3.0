using System;
using System.Collections.Generic;
using System.Linq;
class Program
{
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 static EntityRebirthHumanoidNPC N(int seed=1){var n=new EntityRebirthHumanoidNPC();n.RebirthAppearance=new Appearance{Seed=seed};return n;}
 static void Main(){
  ItemClass.Add("hat",new ItemClassArmor{Slot=0});ItemClass.Add("coat",new ItemClassArmor{Slot=1});ItemClass.Add("gloves",new ItemClassArmor{Slot=2});ItemClass.Add("boots",new ItemClassArmor{Slot=3});
  ItemClass.Add("club",new ItemClass{Actions=new object[]{new ItemActionMelee()}});ItemClass.Add("pistol",new ItemClass{Actions=new object[]{new ItemActionRanged{MagazineItemNames=new[]{"ammo"}}}});ItemClass.Add("ammo",new ItemClass());
  EntityClass.list.Add(new EntityClass());var p=EntityClass.list[0].Properties;p.Values["ArmorItems"]="hat,coat,gloves,boots,missing-addon";p.Values["HeldWeapon"]="club,pistol,missing-addon";p.Values["RandomArmorItemsMissing"]="2";
  bool melee=false,ranged=false;var layouts=new HashSet<string>();
  for(int seed=1;seed<=100;seed++){var n=N(seed);RebirthHumanStartingLoadout.Apply(n);int count=n.Slots.Take(4).Count(s=>!s.IsEmpty());Check(count>=2&&count<=4,"armor missing budget");Check(n.Slots[4].IsEmpty()!=n.Slots[5].IsEmpty(),"one actual weapon");layouts.Add(string.Join(",",n.Slots.Take(4).Select(s=>s.IsEmpty()?"-":s.itemValue.type.ToString())));if(!n.Slots[4].IsEmpty())melee=true;else{ranged=true;Check(n.Slots[6].count==60,"real ranged ammunition");}var retained=n.Slots[4].IsEmpty()?5:4;n.Slots[retained]=ItemStack.Empty;RebirthHumanStartingLoadout.Apply(n);Check(n.Slots[retained].IsEmpty(),"birth marker prevents refills");}
  Check(melee&&ranged&&layouts.Count>1,"varied weapons and armor");
  var loaded=N();loaded.RebirthStartingLoadoutRead=true;RebirthHumanStartingLoadout.Apply(loaded);Check(loaded.Slots.All(s=>s.IsEmpty()),"native loaded NPC never rerolls");
  var remote=N();remote.world.Remote=true;RebirthHumanStartingLoadout.Apply(remote);Check(remote.Slots.All(s=>s.IsEmpty()),"client never generates items");
  var prepared=N();prepared.IsPreparedRestorationPending=true;RebirthHumanStartingLoadout.Apply(prepared);Check(prepared.Slots.All(s=>s.IsEmpty()),"prepared restoration never generates items");
  var authored=N();var item=new ItemStack(ItemClass.GetItem("club",false),1);authored.Slots[4]=item;RebirthHumanStartingLoadout.Apply(authored);Check(ReferenceEquals(authored.Slots[4],item),"authored actual weapon retained");
    var prefix=typeof(RebirthNpcWindowExclusivity).GetMethod("Prefix",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  var manager=new GUIWindowManager();manager.Open.Add("rebirthCompanions");prefix.Invoke(null,new object[]{manager,new GUIWindow{Id="creative"},true});Check(manager.Open.Count==0,"creative closes companion window");
  manager.Open.Add("rebirthCompanionInteraction");var next=new GUIWindow{Id="rebirthCompanions"};prefix.Invoke(null,new object[]{manager,next,true});Check(manager.Open.Count==0&&next.alwaysUsesMouseCursor,"NPC navigation closes card and requests cursor");
  manager.Open.Add("rebirthCompanions");prefix.Invoke(null,new object[]{manager,new GUIWindow{Id="hud",Actions=false},false});Check(manager.Open.Count==1,"HUD does not close NPC window");
  prefix.Invoke(null,new object[]{manager,new GUIWindow{Id="rebirthCompanions"},true});Check(manager.Open.Count==1,"already open window is not dismissed");
  prefix.Invoke(null,new object[]{manager,new GUIWindow{Id="inventory",Actions=true},false});Check(manager.Open.Count==0,"nonmodal action window closes NPC window");
  var q=RebirthHumanEyeGaze.Clamp(new UnityEngine.Quaternion{x=0.9f,y=-0.8f,z=0.3f,w=0.7f});Check(q.x==0.2f&&q.y==-0.4f&&q.z==0.3f&&q.w==0.7f,"legacy gaze limits preserve z/w");
  var postfix=typeof(RebirthHumanEyeGaze).GetMethod("Postfix",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  var gaze=new CharacterGazeController{entityAlive=N(),currentLeftEyeRotation=new UnityEngine.Quaternion{x=-1,y=1,w=1},currentRightEyeRotation=new UnityEngine.Quaternion{x=1,y=-1,w=1}};postfix.Invoke(null,new object[]{gaze});Check(gaze.eyeMaterial.Values["_LeftEyeRotation"].x==0.2f&&gaze.eyeMaterial.Values["_RightEyeRotation"].y==-0.4f,"NPC shader receives constrained rotations");
  var playerGaze=new CharacterGazeController{entityAlive=new object()};postfix.Invoke(null,new object[]{playerGaze});Check(playerGaze.eyeMaterial.Values.Count==0,"player gaze untouched");
  Console.WriteLine("PASS "+checks+" production loadout, window-policy and gaze checks using native API stubs; no game execution.");
 }
}
class World{public bool Remote;public bool IsRemote()=>Remote;}
class Buffs{readonly Dictionary<string,float> values=new();public float GetCustomVar(string key)=>values.TryGetValue(key,out var v)?v:0;public void SetCustomVar(string key,float value,bool sync)=>values[key]=value;}
struct Appearance{public int Seed;}
class Inventory{public int Holding;public void SetHoldingItemIdx(int value)=>Holding=value;}
class EntityRebirthHumanoidNPC{public World world=new();public Buffs Buffs=new();public bool RebirthStartingLoadoutRead,IsPreparedRestorationPending;public int entityClass;public Appearance RebirthAppearance;public Inventory inventory=new();public ItemStack[] Slots=Enumerable.Range(0,10).Select(_=>ItemStack.Empty).ToArray();}
class EntityClass{public static List<EntityClass> list=new();public Props Properties=new();}
class Props{public Dictionary<string,string> Values=new();public string GetString(string key)=>Values.TryGetValue(key,out var v)?v:"";}
class ItemClass{public int Id;public object[] Actions;static Dictionary<string,ItemClass> names=new();static Dictionary<int,ItemClass> types=new();public static void Add(string name,ItemClass item){item.Id=names.Count+1;names[name]=item;types[item.Id]=item;}public static ItemValue GetItem(string name,bool ignored)=>names.TryGetValue(name,out var item)?new ItemValue(item.Id,1,1,false):new ItemValue(0,1,1,false);public static ItemClass Resolve(int id)=>types.TryGetValue(id,out var c)?c:null;}
class ItemClassArmor:ItemClass{public int Slot;}
class ItemValue{public int type,Quality;public ItemClass ItemClass=>ItemClass.Resolve(type);public ItemValue(int id,int min,int max,bool ignored){type=id;Quality=min;}public bool IsEmpty()=>type==0;}
class ItemStack{public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public static ItemStack Empty=>new(new ItemValue(0,1,1,false),0);public bool IsEmpty()=>count<=0||itemValue.IsEmpty();}
class ItemActionMelee{}
class ItemActionRanged{public string[] MagazineItemNames;}
static class RebirthHumanEquipment{public static ItemStack Get(EntityRebirthHumanoidNPC n,int i)=>n.Slots[i];public static void Put(EntityRebirthHumanoidNPC n,int i,ItemStack s)=>n.Slots[i]=s;public static bool Accept(int slot,ItemStack s)=>slot<4?s.itemValue.ItemClass is ItemClassArmor a&&a.Slot==slot:slot==4?s.itemValue.ItemClass.Actions?[0] is ItemActionMelee:slot==5&&s.itemValue.ItemClass.Actions?[0] is ItemActionRanged;}
class GUIWindow{public string Id;public bool alwaysUsesMouseCursor,Actions=true;public bool HasActionSet()=>Actions;}
class GUIWindowManager{public HashSet<string> Open=new();public bool IsWindowOpen(GUIWindow w)=>Open.Contains(w.Id);public bool IsWindowOpen(string id)=>Open.Contains(id);public bool IsFullHUDDisabled()=>false;public void Close(string id)=>Open.Remove(id);}
static class RebirthHarmonyBootstrap{public static bool PatchClassOnce(HarmonyLib.Harmony h,Type t)=>true;}
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string method){}}class Harmony{public Harmony(string id){}}}
class CharacterGazeController{public object entityAlive;public UnityEngine.Material eyeMaterial=new();public UnityEngine.Quaternion currentLeftEyeRotation,currentRightEyeRotation;}
namespace UnityEngine{struct Quaternion{public float x,y,z,w;}struct Vector4{public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}static class Mathf{public static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));}class Material{public Dictionary<string,Vector4> Values=new();public void SetVector(string key,Vector4 value)=>Values[key]=value;}}
