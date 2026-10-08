using System;
using System.Collections;
using System.Collections.Generic;
struct Vector2 { }
class WaitForSeconds { public float Seconds; public WaitForSeconds(float seconds){Seconds=seconds;} }
static class Time { public static float realtimeSinceStartup; }
class World {public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer(){return Primary;}} class GameManager {public static GameManager Instance=new GameManager();public World World;}
struct Vector3i {public int x,y,z;}
class PrefabInstance {public int id=1;public Vector3i boundingBoxPosition,boundingBoxSize;}
class EntityPlayerLocal { public int Crates; public World world=new World();public bool IsDead(){return false;} }
class ItemValue { public ItemClass ItemClass = new ItemClass(); }
class ItemClass { public static ItemValue GetItem(string name,bool unused){return new ItemValue();} public string GetLocalizedItemName(){return "crate";} }
static class RebirthGameBridgeNeeds { public static int CountItem(EntityPlayerLocal p,string name){return p.Crates;} }
static class RebirthGameBridgeInput {
 public static int Tabs,Crafts;
 public static IEnumerable<string> FindByKey(string key){yield return key;}
 public static void HoldFrames(string action,int frames){if(action=="Tab")Tabs++;else if(action=="W")Crafts++;}
}
static class RebirthGameBridgeUi {
 public static int Clicks,Types,Closes;
 public static IEnumerator CloseMenus(){Closes++;yield return "close";}
 public static bool TypeInput(string window,string id,string value){Types++;return true;}
 public static bool TryFindRecipe(string window,string name,out Vector2 point){point=new Vector2();return true;}
 public static IEnumerator ClickAt(Vector2 point,Func<bool> beforePress=null){yield return "glide";if(beforePress!=null&&!beforePress())yield break;Clicks++;}
}
class Storage {
 const string CrateName="cntWoodWritableCrate";
 private World custodyWorld;private PrefabInstance boundPoi;
 // PRODUCTION_CONTEXT
 public IEnumerator BoundCraft(EntityPlayerLocal p,PrefabInstance poi){custodyWorld=p.world;boundPoi=poi;GameManager.Instance.World=p.world;p.world.Primary=p;World expected=custodyWorld;int id=poi.id;Vector3i origin=poi.boundingBoxPosition,size=poi.boundingBoxSize;Func<bool> same=()=>TripContextFailure(p,poi,expected,id,origin,size)==null;return CraftCrate(p,s=>{},same,same);}
 // PRODUCTION_CLASS
 public static IEnumerator Craft(EntityPlayerLocal p,Func<bool> canAct){return CraftCrate(p,s=>{},canAct);}
}
class Checks {
 static void A(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Run(IEnumerator root,Action<object> onYield){var stack=new Stack<IEnumerator>();stack.Push(root);while(stack.Count>0){var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}var child=current.Current as IEnumerator;if(child!=null){stack.Push(child);continue;}onYield(current.Current);Time.realtimeSinceStartup+=1;}}
 static void Reset(){Time.realtimeSinceStartup=0;RebirthGameBridgeInput.Tabs=RebirthGameBridgeInput.Crafts=RebirthGameBridgeUi.Clicks=RebirthGameBridgeUi.Types=RebirthGameBridgeUi.Closes=0;}
 static void Main(){
  Reset();var p=new EntityPlayerLocal();Run(Storage.Craft(p,()=>false),x=>{});A(RebirthGameBridgeInput.Tabs==0&&RebirthGameBridgeUi.Types==0,"initial refusal sends no crafting input");
  Reset();bool allowed=true;Run(Storage.Craft(p,()=>allowed),x=>{if(x as string=="close")allowed=false;});A(RebirthGameBridgeInput.Tabs==0,"context lost closing menus blocks Tab");
  Reset();allowed=true;Run(Storage.Craft(p,()=>allowed),x=>{var delay=x as WaitForSeconds;if(delay!=null&&delay.Seconds==.7f)allowed=false;});A(RebirthGameBridgeUi.Types==0&&RebirthGameBridgeUi.Clicks==0&&RebirthGameBridgeInput.Crafts==0,"context lost after Tab blocks search and crafting");
  Reset();allowed=true;Run(Storage.Craft(p,()=>allowed),x=>{if(x as string=="glide")allowed=false;});A(RebirthGameBridgeUi.Clicks==0&&RebirthGameBridgeInput.Crafts==0,"late glide refusal blocks click and Craft");
  Reset();allowed=true;Run(Storage.Craft(p,()=>allowed),x=>{var delay=x as WaitForSeconds;if(delay!=null&&delay.Seconds==.2f)allowed=false;});A(RebirthGameBridgeUi.Clicks==1&&RebirthGameBridgeInput.Crafts==0,"context lost after selection blocks Craft");
  Reset();Run(Storage.Craft(p,()=>true),x=>{if(RebirthGameBridgeInput.Crafts>0)p.Crates=1;});A(RebirthGameBridgeInput.Tabs==1&&RebirthGameBridgeUi.Clicks==1&&RebirthGameBridgeInput.Crafts==1,"valid flow uses native input once");
  Reset();p.Crates=1;Run(Storage.Craft(p,()=>true),x=>{});A(RebirthGameBridgeInput.Tabs==0,"existing owned crate skips crafting");
  Reset();p.Crates=0;var poi=new PrefabInstance();var bound=new Storage();Run(bound.BoundCraft(p,poi),x=>{var delay=x as WaitForSeconds;if(delay!=null&&delay.Seconds==.7f)p.world=new World();});A(RebirthGameBridgeUi.Types==0&&RebirthGameBridgeInput.Crafts==0&&RebirthGameBridgeUi.Closes==1,"actual context predicate blocks input/cleanup in replacement world after Tab yield");
  Reset();bound=new Storage();Run(bound.BoundCraft(p,poi),x=>{var delay=x as WaitForSeconds;if(delay!=null&&delay.Seconds==.2f)poi.boundingBoxSize=new Vector3i{x=9};});A(RebirthGameBridgeUi.Clicks==1&&RebirthGameBridgeInput.Crafts==0&&RebirthGameBridgeUi.Closes==1,"actual context predicate blocks Craft/cleanup after POI geometry changes at selection yield");
  Console.WriteLine("PASS 9 actual CraftCrate iterator checks: initial/close/Tab/glide/selection interruption, admitted real-input path and existing crate. Native UI/input/world doubled.");
 }
}
