using System;using System.Collections;using System.Collections.Generic;
struct Vector3i {}
static class Time {public static float realtimeSinceStartup;}
class ItemValue {public bool IsEmpty(){return false;}}
static class ItemClass {public static ItemValue GetItem(string n){return new ItemValue();}}
class TEFeatureLockPickable {public string lockPickItem="resourceLockPick";public bool Locked=true;public bool NeedsLockpicking(){return Locked;}}
class TileEntityComposite {public TEFeatureLockPickable Feature=new TEFeatureLockPickable();public T GetFeature<T>() where T:class {return Feature as T;}}
class World {public TileEntityComposite Tile=new TileEntityComposite();public object GetTileEntity(Vector3i p){return Tile;}}
class EntityPlayerLocal {public World world=new World();public bool Dead;public bool IsDead(){return Dead;}}
class Inventory {public int GetItemCount(ItemValue p){return State.Picks;}}
class Xui {public Inventory PlayerInventory=new Inventory();}
class LocalPlayerUI {public Xui xui=new Xui();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p){return new LocalPlayerUI();}}
static class State {public static bool Timer,Threat,Denied;public static int Picks,Actions,Closes,TimerPresses,Breaks;public static float TimerUntil;public static EntityPlayerLocal Player;public static void Reset(){Time.realtimeSinceStartup=0;Timer=Threat=Denied=false;Picks=3;Actions=Closes=TimerPresses=Breaks=0;Player=new EntityPlayerLocal();}public static void Tick(){Time.realtimeSinceStartup+=.1f;if(Timer&&Time.realtimeSinceStartup>=TimerUntil){Timer=false;if(Breaks>0){Breaks--;Picks--;}else Player.world.Tile.Feature.Locked=false;}}}
static class RebirthGameBridgeCombat {public static List<int> AwakeThreats(EntityPlayerLocal p,float r,bool b){return State.Threat?new List<int>{1}:new List<int>();}}
static class RebirthGameBridgeUi {public static bool IsWindowOpen(string n){return State.Timer;}public static IEnumerator CloseMenus(){State.Closes++;State.Timer=false;yield return null;}}
static class RebirthGameBridgePlayer {public static IEnumerator ApproachAndActivate(EntityPlayerLocal p,Vector3i pos,Action<string> log,float reach,float wait){State.Actions++;if(State.Timer)State.TimerPresses++;if(!State.Denied){State.Timer=true;State.TimerUntil=Time.realtimeSinceStartup+1;}yield return null;}}
class Check {
 static void Beat(){}
// METHODS
 static void Require(bool b,string message){if(!b)throw new Exception(message);}
 static void Execute(){var stack=new Stack<IEnumerator>();stack.Push(PickLootLock(State.Player,new Vector3i(),x=>{}));int steps=0;while(stack.Count>0){if(++steps>10000)throw new Exception("Unbounded coroutine");var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}var nested=top.Current as IEnumerator;if(nested!=null)stack.Push(nested);else State.Tick();}}
 static void Main(){
 State.Reset();State.Picks=0;Execute();Require(State.Actions==0,"Missing picks activated");
 State.Reset();State.Breaks=2;Execute();Require(State.Actions==3&&State.Picks==1&&!State.Player.world.Tile.Feature.Locked,"Broken picks did not retry to success");Require(State.TimerPresses==0,"Cancelled active timer with E");
 State.Reset();State.Timer=true;State.TimerUntil=1;Execute();Require(State.Actions==0&&!State.Player.world.Tile.Feature.Locked,"Existing timer interrupted");
 State.Reset();State.Denied=true;Execute();Require(State.Actions==15,"Denied-lock retry bound");
 State.Reset();State.Timer=true;State.TimerUntil=1000;Execute();Require(State.Actions==0&&State.Closes==1&&Time.realtimeSinceStartup<181,"Timer deadline not cancelled");
 State.Reset();State.Threat=true;Execute();Require(State.Actions==0&&State.Closes==1,"Threat did not release UI");
 State.Reset();State.Player.Dead=true;Execute();Require(State.Actions==0,"Dead player activated");
 Console.WriteLine("PASS: extracted lockpick coroutine: missing picks, two breaks then success, existing timer, denied-lock retry cap, timer deadline, threat and death. Native inventory/network/timer replaced by controlled fixture.");
 }
}
