#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationQueuedAuthority.cs'))
$camperStart=$camperSource.IndexOf('    internal static bool CanProcessNativeQueue(')
$camperEnd=$camperSource.IndexOf('    internal static bool TryRegister(',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Production queue gate not found.'}
$camperGate=$camperSource.Substring($camperStart,$camperEnd-$camperStart)
$camperFixture=@'
using System;
public class Recipe { public bool Marked,PerBatch,Valid=true,Resolved=true; }
public class RecipeQueueItem { public Recipe Recipe;public int Multiplier=1;public bool Published; }
public class RebirthCamperStationState { public RecipeQueueItem[] Queue; }
public static class RebirthCamperStationProcessingRecoveryWatch {public static int Calls;public static bool TryObserve(RebirthCamperStationState station){Calls++;return true;}} public static class RebirthCamperStationCancellationRemoval { public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held; }
namespace Rebirth.Vehicles.Camper { public static class RebirthCamperModulePatchInstaller { public static bool IsInstalled; } }
public static class RebirthCamperStationCompletionCallsite { public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held; }
public static class RebirthCamperStationSaveEvidence { public static bool Pending;public static bool IsPending(RebirthCamperStationState s)=>Pending; }
public static class RebirthStationGridQueue
{
 public static bool IsMarked(Recipe r)=>r.Marked;
 public static bool HasValidMultiplier(Recipe r,int count)=>r.Valid&&(r.PerBatch?count>=1&&count<=3:count==1);
 public static bool HasResolvedDefinition(Recipe r)=>r.Resolved;
}
public static class QueueFixture
{
GATE_BODY
 private static bool TryGetCurrent(RebirthCamperStationState s,RecipeQueueItem q,out object binding){binding=null;return q.Published;}
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Run()
 {
  var ordinary=new RecipeQueueItem{Recipe=new Recipe()};var paid=new RecipeQueueItem{Recipe=new Recipe{Marked=true},Published=true};
  var state=new RebirthCamperStationState{Queue=new[]{ordinary}};
  Check(CanProcessNativeQueue(state),"ordinary native queue preserved");state.Queue=new[]{paid};
  RebirthCamperStationCancellationRemoval.Held=true;Check(!CanProcessNativeQueue(state),"cancellation removal holds queue");RebirthCamperStationCancellationRemoval.Held=false;
  Check(!CanProcessNativeQueue(state),"uninstalled paid processing refused");Rebirth.Vehicles.Camper.RebirthCamperModulePatchInstaller.IsInstalled=true;
  Check(CanProcessNativeQueue(state),"published mobile queue admitted");
  paid.Published=false;Check(!CanProcessNativeQueue(state),"unconfirmed publication refused");paid.Published=true;
  RebirthCamperStationSaveEvidence.Pending=true;Check(!CanProcessNativeQueue(state),"pending native save holds queue");RebirthCamperStationSaveEvidence.Pending=false;
  RebirthCamperStationCompletionCallsite.Held=true;Check(!CanProcessNativeQueue(state),"completion holds queue");RebirthCamperStationCompletionCallsite.Held=false;
  paid.Multiplier=2;Check(!CanProcessNativeQueue(state),"repeated payment multiplier refused");paid.Multiplier=1;
  paid.Recipe.Resolved=false;Check(!CanProcessNativeQueue(state),"unresolved definition refused");paid.Recipe.Resolved=true;
  paid.Recipe.Valid=false;Check(!CanProcessNativeQueue(state),"invalid recipe multiplier refused");paid.Recipe.Valid=true;
  var later=new RecipeQueueItem{Recipe=new Recipe{Marked=true}};state.Queue=new[]{paid,later};
  Check(!CanProcessNativeQueue(state),"every marked queue entry needs authority");
  Console.WriteLine("PASS actual mobile native queue gate: original publication, install/save/completion/definition/multiplier checks and all queued entries. Authority/native adapters doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('GATE_BODY',$camperGate)
[QueueFixture]::Run()
