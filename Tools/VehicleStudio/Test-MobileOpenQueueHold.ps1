#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationOpenQueueHold.cs')).Replace('using HarmonyLib;','').Replace('using System.Reflection;','')
$camperFixture=@'
using System;
using System.Reflection;
public class HarmonyPatch:Attribute { public HarmonyPatch(Type type,string method){} }
public class HarmonyPriority:Attribute { public HarmonyPriority(int priority){} }
public static class AccessTools { public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic); }
public static class Priority { public const int First=800; }
public class Recipe { public bool Marked;public string Job="job"; }
public static class RebirthStationGridQueue { public static bool IsMarked(Recipe r)=>r!=null&&r.Marked;public static bool TryGetJobId(Recipe r,out string job){job=r?.Job;return r?.Marked==true;} }
public static class ThreadManager { public static bool IsMainThread()=>true; }
public class RecipeQueueItem { public Recipe Recipe; }
public class TileEntityWorkstation { }
public class World {public bool IsRemote()=>true;}
public class EntityVehicle {public World world=new World();}
public class EntityPlayerLocal {}
public class PlayerUI {public EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}
public class XUi {public PlayerUI playerUI=new PlayerUI();}
public static class RebirthCamperRemoteStationClient {public static int Requests;public static bool Cancel(EntityPlayerLocal p,RebirthCamperStationState s,string job){Requests++;return true;}}
public class RebirthCamperStationState:TileEntityWorkstation {public EntityVehicle BoundVehicle; public RecipeQueueItem[] Queue; }
public class Model { public TileEntityWorkstation TileEntity; }
public class XUiC_CraftingQueue { public XUiC_RecipeStack[] Cards;public XUiC_RecipeStack[] GetRecipesToCraft()=>Cards; }
public class XUiC_WorkstationWindowGroup {public XUi xui=new XUi(); public Model WorkstationData;public XUiC_CraftingQueue Cards=new XUiC_CraftingQueue();public T GetChildByType<T>() where T:class=>Cards as T; }
public static class RebirthCamperStationWindowSession { public static bool Current=true;public static bool Admitted(XUiC_WorkstationWindowGroup window,RebirthCamperStationState station)=>Current; }
public static class RebirthCamperStationQueuedAuthority { public class Binding{}public static bool TryGetCurrent(RebirthCamperStationState station,RecipeQueueItem row,out Binding binding){binding=new Binding();return true;} }
public static class RebirthCamperStationCancellationWatch { public static int Starts;public static bool TryWatch(RebirthCamperStationQueuedAuthority.Binding binding){Starts++;return true;} }
public class Window { public object Controller; }
public class XUiC_RecipeStack { public Window windowGroup;public Recipe recipe;public bool IsDirty;public int Refreshes;private void updateRecipeData(){Refreshes++;}public void Update(){} }
public static class OpenQueueHoldFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 private static bool Update(XUiC_RecipeStack stack)=>(bool)typeof(RebirthCamperStationOpenQueueUpdateHold).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stack});
 private static bool Output(XUiC_RecipeStack stack,out bool result){var args=new object[]{stack,true};bool run=(bool)typeof(RebirthCamperStationOpenQueueOutputHold).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);result=(bool)args[1];return run;}
 private static bool Cancel(XUiC_RecipeStack stack)=>(bool)typeof(RebirthCamperStationOpenQueueCancellationHold).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stack});
 public static void Run(){
  Check(Update(null)&&Output(null,out var result)&&result,"unrelated missing UI preserved");
  var window=new XUiC_WorkstationWindowGroup{WorkstationData=new Model{TileEntity=new TileEntityWorkstation()}};
  var stack=new XUiC_RecipeStack{windowGroup=new Window{Controller=window},recipe=new Recipe{Marked=true}};
  Check(Update(stack)&&Output(stack,out result)&&result&&Cancel(stack),"paid ground workstation unchanged");
  var mobile=new RebirthCamperStationState{Queue=new[]{new RecipeQueueItem{Recipe=stack.recipe}}};window.WorkstationData.TileEntity=mobile;window.Cards.Cards=new[]{stack};
  Check(!Update(stack),"mobile update/inventory-full retry held");Check(!Output(stack,out result)&&!result,"mobile direct output held with false result");
  Check(!Cancel(stack)&&RebirthCamperStationCancellationWatch.Starts==1,"selected paid card routes to original cancellation watch; native refunds held");
  mobile.BoundVehicle=new EntityVehicle();Check(!Cancel(stack)&&RebirthCamperRemoteStationClient.Requests==1&&RebirthCamperStationCancellationWatch.Starts==1,"Remote cancellation dispatched without local refund/watch");mobile.BoundVehicle=null;
  mobile.Queue[0].Recipe=new Recipe{Marked=true,Job="different"};Check(!Cancel(stack)&&RebirthCamperStationCancellationWatch.Starts==1,"stale projected card refused");mobile.Queue[0].Recipe=stack.recipe;
  RebirthCamperStationWindowSession.Current=false;Check(!Cancel(stack)&&RebirthCamperStationCancellationWatch.Starts==1,"expired window refused");RebirthCamperStationWindowSession.Current=true;
  stack.recipe.Marked=false;Check(!Update(stack)&&!Output(stack,out result)&&!result&&!Cancel(stack)&&stack.Refreshes==2,"ordinary mobile uses vehicle clock; native cancellation held; display refreshed");
  stack.recipe=null;Check(!Update(stack),"empty mobile UI uses projection");stack.recipe=new Recipe{Marked=true};stack.windowGroup.Controller=new object();Check(Update(stack),"personal crafting unchanged");
  Console.WriteLine("PASS actual mobile open-queue holds: update/inventory-full, direct output and native cancellation/refund denied for all mobile cards; ground/personal preserved; mobile display refreshed without a second clock. UI/Harmony/native adapters doubled; verified cancellation is separately integrated.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[OpenQueueHoldFixture]::Run()
