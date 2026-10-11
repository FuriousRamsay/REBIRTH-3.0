using System;
using System.Collections.Generic;
class Program { static void Main(){
 foreach(bool rebirth in new[]{false,true})foreach(bool native in new[]{false,true}){RebirthSurvivorMode.Enabled=rebirth;SandboxOptions.SandboxOptionManager.Native=native;RebirthForgeCraftingMode.Apply();Check(XUiM_Recipes.DisableSmelter==(rebirth||native),"forge mode");}
 var w=new XUiC_RebirthCookingWorkspace();w.CraftShared(new Recipe(),1);Check(w.Cooks==1&&!w.Pending,"local submission immediate");w.Tick();Check(w.Cooks==1,"no double submit");
 w=new XUiC_RebirthCookingWorkspace{Remote=true};w.CraftShared(new Recipe(),1);Check(w.Cooks==0&&w.Pending,"remote waits");w.Reply();Check(w.Cooks==1&&!w.Pending,"remote completes");
 w=new XUiC_RebirthCookingWorkspace{Fail=true};w.CraftShared(new Recipe(),1);Check(w.Cooks==0&&w.Returns==1&&!w.Pending,"failure refunds");Console.WriteLine("PASS: 9 mode/submission assertions");}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}}
public class Recipe{}
public static class Localization{public static string Get(string s)=>s;}
public class Entry{public Recipe GetRecipe()=>null;}
public class Queue{public IEnumerable<Entry> GetRecipesToCraft()=>new[]{new Entry()};}
public class XUiC_RebirthCraftingQueue:Queue{public int ActiveCount;public int RuntimeCapacity=16;}
public class Station{public bool IsMilling=true;public Queue craftingQueue=new XUiC_RebirthCraftingQueue();public bool CraftingRequirementsValid(Recipe r)=>true;public string CraftingRequirementsInvalidMessage(Recipe r)=>null;}
public class Prep{public bool IsPreparing;}
public class XUiC_RebirthCraftingOutcome{public void RefreshNow(){}}
public class Controller{public T GetChildByType<T>() where T:new()=>new T();}
public class Window{public Controller Controller=new Controller();}
public sealed partial class XUiC_RebirthCookingWorkspace{
 bool open=true,pulling,submittingCook,preparingRequest;Station station=new Station();Prep Preparation=new Prep();Window windowGroup=new Window();string status;int batch;public bool Remote,Fail;public int Cooks,Returns;public bool Pending=>sharedCraftPending;
 object MissingTool(Recipe r)=>null;int Feasible(Recipe r)=>99;void Select(Recipe r){status="";}void SetBatch(int n){batch=n;}void Pull(){pulling=Remote;if(Fail)status="failed";}void Cook(){Cooks++;}void ReturnIngredients(){Returns++;}public void Tick()=>CompleteSharedCraft();public void Reply(){pulling=false;CompleteSharedCraft();}}
public static class RebirthSurvivorMode{public static bool Enabled;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public static class XUiM_Recipes{public static bool DisableSmelter;}
namespace SandboxOptions{public enum SandboxOptions{SmeltingType}public static class SandboxOptionManager{public static bool Native;public static bool GetBool(SandboxOptions o)=>Native;}}
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}}
