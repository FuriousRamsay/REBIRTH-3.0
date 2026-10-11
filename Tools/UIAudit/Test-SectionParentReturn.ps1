$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$stub=@"
using System;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Runtime.CompilerServices;using HarmonyLib;using UnityEngine;
namespace HarmonyLib {public static class AccessTools {public static FieldInfo Field(Type t,string n)=>t.GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);}public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,Type[] a){}}}
namespace UnityEngine {public enum KeyCode{Escape};public static class Input {public static bool Escape;public static bool GetKey(KeyCode k)=>Escape;}public class WaitForEndOfFrame{}}
public struct Vector {public float sqrMagnitude;public static Vector operator-(Vector a,Vector b)=>new Vector();}
public struct Position{public Vector ToVector3()=>new Vector();}
public class World {public Tile Tile=new Tile();public BlockValue Value=new BlockValue();public Tile GetTileEntity(Position p)=>Tile;public BlockValue GetBlock(Position p)=>Value;}
public class Tile {public Position ToWorldPos()=>new Position();}
public class BlockValue {public Block Block=new Block();}
public class Block {public int Activations;public bool OnBlockActivated(World w,Position p,BlockValue b,Player x){Activations++;return true;}}
public class Player {public World world;public Vector position;public bool Dead;public bool IsDead()=>Dead;}
public class ActionState {public bool IsPressed,WasReleased;}
public class ActionSet {public ActionState Cancel=new ActionState();}
public class PlayerInput {public ActionSet PermanentActions=new ActionSet(),GUIActions=new ActionSet();}
public class PlayerUI {public GUIWindowManager windowManager;public Player entityPlayer;public PlayerInput playerInput=new PlayerInput();}
public class XUi {public PlayerUI playerUI;}
public class XUiController {public XUi xui;}
public class XUiC_WorkstationWindowGroup:XUiController {public Data WorkstationData=new Data();}
public class Data {public Tile TileEntity;}
public class GUIWindow {public string Id,openWindowOnEsc;public bool isShowing,isModal=true;}
public class XUiWindowGroup:GUIWindow {public XUiController Controller;}
public static class RebirthContextNavigationService {public static object Session;public static bool Visible,Suspended,Returned;public static bool IsContextVisible(XUi ui)=>Visible;public static bool SuspendForExternalRoute(XUi ui){Visible=false;Suspended=true;return true;}public static bool ReturnToContext(XUi ui){Returned=true;return true;}}
public class GameManager {
 public static GameManager Instance=new GameManager();public World World;public Stack<IEnumerator> Running=new Stack<IEnumerator>();
 public void StartCoroutine(IEnumerator e){Running.Push(e);}
 public void Frame(){while(Running.Count>0){var e=Running.Peek();if(!e.MoveNext()){Running.Pop();continue;}if(e.Current is IEnumerator nested){Running.Push(nested);continue;}break;}}
 public void Finish(){for(int n=0;n<12&&Running.Count>0;n++)Frame();if(Running.Count>0)throw new Exception("Return stuck");}
}
public class GUIWindowManager {
 public GUIWindow modalWindow;public List<GUIWindow> windowsToOpen=new List<GUIWindow>();public Dictionary<string,GUIWindow> Windows=new Dictionary<string,GUIWindow>();
 public GUIWindow GetWindow(string id)=>Windows[id];public bool TryGetWindow(string id,out GUIWindow w)=>Windows.TryGetValue(id,out w);public bool IsWindowOpen(string id)=>Windows.ContainsKey(id)&&Windows[id].isShowing;
 public bool IsModalWindowOpen(){foreach(var w in Windows.Values)if(w.isShowing&&w.isModal)return true;return false;}
 public void Open(string id,bool modal){foreach(var w in Windows.Values)if(w.isShowing&&w.isModal)w.isShowing=false;Windows[id].isShowing=true;modalWindow=Windows[id];}
 public void CloseAllOpenModalWindows(GUIWindow except=null,bool fromEsc=false){foreach(var w in Windows.Values){if(!w.isShowing||w==except||!w.isModal)continue;w.isShowing=false;if(fromEsc&&!string.IsNullOrEmpty(w.openWindowOnEsc))Open(w.openWindowOnEsc,true);}}
}
public static class ParentReturnFixture {
 static int checks;static void A(bool b,string m){checks++;if(!b)throw new Exception(m);}
 public static string Run(){
 var prefix=typeof(RebirthBackpackSectionEscapeReturnPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);var postfix=typeof(RebirthBackpackSectionEscapeReturnPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
 foreach(string section in new[]{"rebirthBackpackLibrary","rebirthBackpackSellStash"})foreach(string mode in new[]{"ordinary","escape","released","replacement","world","dead","context","station"}){
  var manager=new GUIWindowManager();var world=new World();var player=new Player{world=world};var ui=new XUi{playerUI=new PlayerUI{windowManager=manager,entityPlayer=player}};
  var child=new XUiWindowGroup{Id=section,Controller=new XUiController{xui=ui}};var parent=new XUiWindowGroup{Id="parent",isShowing=true,Controller=mode=="station"?new XUiC_WorkstationWindowGroup{xui=ui,WorkstationData=new Data{TileEntity=world.Tile}}:new XUiController{xui=ui}};
  manager.Windows.Add(section,child);manager.Windows.Add("parent",parent);manager.Windows.Add("other",new GUIWindow{Id="other"});manager.modalWindow=parent;GameManager.Instance=new GameManager{World=world};RebirthContextNavigationService.Visible=mode=="context";RebirthContextNavigationService.Session=new object();RebirthContextNavigationService.Returned=false;
  RebirthBackpackSectionEscapeReturnPatch.Open(manager,section,"incorrect");child.openWindowOnEsc="";
  bool esc=mode!="ordinary";object[] args={manager,esc,null};prefix.Invoke(null,args);manager.CloseAllOpenModalWindows(null,esc);postfix.Invoke(null,new[]{(object)manager,args[2]});
  A(!manager.IsWindowOpen("parent"),"Returned before coroutine");
  if(!esc){A(GameManager.Instance.Running.Count==0,"ordinary close returns");continue;}
  Input.Escape=true;ui.playerUI.playerInput.GUIActions.Cancel.IsPressed=true;
  for(int n=0;n<3;n++)GameManager.Instance.Frame();A(!manager.IsWindowOpen("parent"),"returned while Escape held");
  Input.Escape=false;ui.playerUI.playerInput.GUIActions.Cancel.IsPressed=false;ui.playerUI.playerInput.GUIActions.Cancel.WasReleased=true;
  for(int n=0;n<3;n++)GameManager.Instance.Frame();A(!manager.IsWindowOpen("parent"),"returned during Escape release");
  ui.playerUI.playerInput.GUIActions.Cancel.WasReleased=false;
  if(mode=="replacement")manager.Open("other",true);if(mode=="world")GameManager.Instance.World=new World();if(mode=="dead")player.Dead=true;
  GameManager.Instance.Finish();
  if(mode=="context")A(RebirthContextNavigationService.Returned&&!manager.IsWindowOpen("parent"),"context must resume live session");
  else if(mode=="station")A(world.Value.Block.Activations==1&&!manager.IsWindowOpen("parent"),"station must reacquire native lock");
  else A(manager.IsWindowOpen("parent")==!(mode=="replacement"||mode=="world"||mode=="dead"),"incorrect parent admission");
 }
 return "PASS: "+checks+" production Escape/coroutine checks for both compartments; held/released gesture blocked, later return admitted, alternate route/world/death suppressed, context resumes and station reactivates. Native input/window/tile APIs are explicitly doubled.";
 }
}
"@
$production=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/UI/RebirthBackpackSectionEscapeReturnPatch.cs')).Replace('using System;','').Replace('using HarmonyLib;','').Replace('using System.Runtime.CompilerServices;','')
$delay=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthWindowReturn.cs')).Replace('using System.Collections;','').Replace('using UnityEngine;','')
Add-Type -TypeDefinition ($stub+$delay+$production)
[ParentReturnFixture]::Run()