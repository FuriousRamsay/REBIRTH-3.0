#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueuePolicy.cs')).Replace('using System;','')
$camperFixture=@'
using System;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class Player { public int Id=99; }
public class Recipe { public string Name="paid";public string GetName()=>Name; }
public class Row { public Recipe Recipe=new Recipe();public int Actor=7; }
public class RebirthCamperStationState { public Row[] Queue=new[]{new Row()}; }
public static class RebirthCamperStationQueuedAuthority {
 public class Binding { public Player Player=new Player(); }
 public static bool Ready=true,Current=true;public static Binding Owner=new Binding();
 public static bool CanProcessNativeQueue(RebirthCamperStationState s)=>Ready;
 public static bool TryGetCurrent(RebirthCamperStationState s,Row r,out Binding b){b=Owner;return Current;}
}
public static class RebirthStationGridQueue { public static bool Resolved=true;public static bool TryGetAdmittedSource(Recipe r,object definitions,out Recipe source){source=new Recipe{Name="original"};return Resolved;} }
public static class XUiM_Recipes { public static object GetRecipes()=>new object(); }
public static class RebirthSurvivorMode { public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled; }
public class Evaluation { public bool IsAllowed;public string FirstMissingReason="missing"; }
public static class RebirthCapabilityService { public static bool Allowed=true;public static Player Last;public static string Recipe;public static Evaluation EvaluateRecipe(Player p,string recipe){Last=p;Recipe=recipe;return new Evaluation{IsAllowed=Allowed};} }
public static class QueuePolicyFixture {
 private static void Check(bool b,string n){if(!b)throw new Exception(n);}
 public static void Run(){var s=new RebirthCamperStationState();
  Check(RebirthCamperStationQueuePolicy.Authorize(s,out var reason)&&reason==null&&ReferenceEquals(RebirthCapabilityService.Last,RebirthCamperStationQueuedAuthority.Owner.Player)&&RebirthCapabilityService.Recipe=="original","verified current owner and original source policy despite old native actor");
  RebirthCapabilityService.Allowed=false;Check(!RebirthCamperStationQueuePolicy.Authorize(s,out reason)&&reason=="crafting-policy:missing","current policy denial retained");
  RebirthSurvivorMode.Enabled=false;Check(RebirthCamperStationQueuePolicy.Authorize(s,out reason),"native mode retains verified paid custody");
  RebirthCamperStationQueuedAuthority.Current=false;Check(!RebirthCamperStationQueuePolicy.Authorize(s,out reason),"stale binding denied in native mode");RebirthCamperStationQueuedAuthority.Current=true;
  RebirthCamperStationQueuedAuthority.Ready=false;Check(!RebirthCamperStationQueuePolicy.Authorize(s,out reason),"save or completion hold denied");RebirthCamperStationQueuedAuthority.Ready=true;
  RebirthStationGridQueue.Resolved=false;Check(!RebirthCamperStationQueuePolicy.Authorize(s,out reason),"unresolved original definition denied");RebirthStationGridQueue.Resolved=true;
  ThreadManager.Main=false;Check(!RebirthCamperStationQueuePolicy.Authorize(s,out reason),"off-thread denied");
  Console.WriteLine("PASS actual mobile queue policy: current verified owner/original recipe, denial, native mode, stale binding, processing hold, unresolved source and thread. Policy/native authority adapters doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[QueuePolicyFixture]::Run()
