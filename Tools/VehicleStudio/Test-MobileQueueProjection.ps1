#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueueProjection.cs'))
$camperSource=[regex]::Replace($camperSource,'(?m)^using [^;]+;\r?\n','')
$camperFixture=@'
using System;using System.Reflection;
public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n){} }
public static class AccessTools { public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic); }
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public static class RebirthCamperStationOpenQueueProcessing { public static bool Managed;public static bool OwnsQueue(RebirthCamperStationState s)=>Managed; }
public class Recipe { public bool Marked; }
public static class RebirthStationGridQueue { public static bool IsMarked(Recipe r)=>r!=null&&r.Marked; }
public class RecipeQueueItem { public Recipe Recipe=new Recipe{Marked=true};public int Multiplier=1,StartingEntityId=7,Quality=2,AmountToRepair;public object RepairItem;public float CraftingTimeLeft=3,OneItemCraftTime=4;public bool IsCrafting=true; }
public class RebirthCamperStationState { public RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()}; }
public class XUiC_RecipeStack { public bool IsCrafting,IsDirty;public Recipe Recipe;public int Actor,Count,Quality;public float Left,One;public bool SetRecipe(Recipe r,int c,float left,bool modify,int actor,int quality,float one){Recipe=r;Count=c;Left=left;Actor=actor;Quality=quality;One=one;return true;} }
public class XUiC_CraftingQueue { public XUiC_RecipeStack[] Cards=new[]{new XUiC_RecipeStack()};public XUiC_RecipeStack[] GetRecipesToCraft()=>Cards; }
public class XUiC_WorkstationWindowGroup { public XUiC_CraftingQueue Queue=new XUiC_CraftingQueue();public int Refreshes;private void syncUIfromTE(){Refreshes++;}public T GetChildByType<T>() where T:class=>Queue as T; }
public static class RebirthCamperStationWindowSession { public static bool Valid=true;public static bool Admitted(XUiC_WorkstationWindowGroup w,RebirthCamperStationState s)=>Valid; }
public class XUiM_Workstation { public object TileEntity;public void SetRecipeQueueItems(RecipeQueueItem[] rows){} }
public static class QueueProjectionFixture {
 private static void Check(bool b,string n){if(!b)throw new Exception(n);}
 private static bool Write(XUiM_Workstation m,RecipeQueueItem[] rows)=>(bool)typeof(RebirthCamperStationQueueWriteback).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{m,rows});
 public static void Run(){var w=new XUiC_WorkstationWindowGroup();var s=new RebirthCamperStationState();var row=s.Queue[0];var m=new XUiM_Workstation{TileEntity=s};
  RebirthCamperStationQueueProjection.Refresh(w,s);var card=w.Queue.Cards[0];Check(w.Refreshes==1&&ReferenceEquals(s.Queue[0],row)&&ReferenceEquals(card.Recipe,row.Recipe)&&card.Actor==7&&card.Count==1&&card.Quality==2&&card.Left==3&&card.One==4&&!card.IsCrafting&&card.IsDirty,"exact projection without paid row mutation or UI clock");
  Check(!Write(m,new RecipeQueueItem[0]),"stale empty UI cannot erase paid queue");row.Recipe.Marked=false;Check(!Write(m,new[]{new RecipeQueueItem()}),"ordinary UI cannot inject paid queue");Check(Write(m,s.Queue),"ordinary mobile queue sync preserved");RebirthCamperStationOpenQueueProcessing.Managed=true;Check(!Write(m,s.Queue),"managed ordinary queue cannot overwrite authoritative clock");RebirthCamperStationOpenQueueProcessing.Managed=false;m.TileEntity=new object();Check(Write(m,new[]{new RecipeQueueItem()}),"ground sync preserved");
  row.RepairItem=new object();try{RebirthCamperStationQueueProjection.Refresh(w,s);throw new Exception("expected refusal");}catch(InvalidOperationException){}Check(w.Refreshes==1,"unsupported repair refused before refresh");row.RepairItem=null;
  RebirthCamperStationWindowSession.Valid=false;try{RebirthCamperStationQueueProjection.Refresh(w,s);throw new Exception("expected stale refusal");}catch(InvalidOperationException){}
  Console.WriteLine("PASS actual queue projection/writeback: paid row/timer/actor/quality retained; no UI paid clock; stale erase/paid injection blocked; ordinary/ground sync preserved; stale lease/repair refused. Native UI adapters doubled; managed window writeback denied after paid completion.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[QueueProjectionFixture]::Run()
