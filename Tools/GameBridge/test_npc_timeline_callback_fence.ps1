$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Close game before offline validation'}
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function Read-Method($relative,$signature){
 $source=[IO.File]::ReadAllText((Join-Path $root $relative));$start=$source.IndexOf($signature)
 if($start -lt 0){throw "Missing $signature"};$open=$source.IndexOf('{',$start);$depth=1;$end=$open+1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw 'Incomplete source method'}
 return $source.Substring($start,$end-$start).Replace('private static void','internal static void')
}
$update=Read-Method 'Scripts/NPC/Commands/RebirthNpcCommandSystem.cs' 'private static void OnGameUpdate('
$shutdown=Read-Method 'Scripts/Survivor/Progression/AdvancedDisciplines/RebirthBoundUndeadService.cs' 'private static void OnWorldShuttingDown('
$adapters=''
$calls=[regex]::Matches($update,'\b(Rebirth\w+)\.(\w+)\(\)')
foreach($group in ($calls | Group-Object {$_.Groups[1].Value})){
 $members=($group.Group | ForEach-Object {$_.Groups[2].Value} | Sort-Object -Unique | ForEach-Object {"internal static void $_(){Calls++;}"}) -join ''
 $adapters+="internal static class $($group.Name){internal static int Calls;$members}"
}
$code=@"
using System;
internal static class ModEvents {internal struct SGameUpdateData{} internal struct SWorldShuttingDownData{}}
internal static class RebirthNpcPersistenceTimelineService {internal static bool IsAlignmentFailed;}
internal static class RebirthNpcPersistenceCoordinator {internal static bool IsRecoveryInstalling=false;}
internal static class RebirthNpcWorkReleaseGate {internal static bool Enabled=true;}
internal static class Log {internal static int Warnings;internal static void Warning(string message){Warnings++;}}
$adapters
internal static class ActualCallbacks {
 internal static int Captures,Clears;
 internal static void CaptureAll(){Captures++;}internal static void ClearRuntime(){Clears++;}
 $update
 $shutdown
}
public static class TimelineCallbackFixture {
 public static string Run(){
  var update=new ModEvents.SGameUpdateData();var stop=new ModEvents.SWorldShuttingDownData();
  RebirthNpcPersistenceTimelineService.IsAlignmentFailed=true;
  ActualCallbacks.OnGameUpdate(ref update);ActualCallbacks.OnWorldShuttingDown(ref stop);
  if(RebirthNpcLiveRecoveryRuntime.Calls!=1||RebirthNpcSettlementPersistenceStore.Calls!=0||RebirthNpcCommandGateway.Calls!=0||ActualCallbacks.Captures!=0||ActualCallbacks.Clears!=0||Log.Warnings!=1)throw new Exception("unresolved timeline escaped callback fence");
  RebirthNpcPersistenceTimelineService.IsAlignmentFailed=false;
  ActualCallbacks.OnGameUpdate(ref update);ActualCallbacks.OnWorldShuttingDown(ref stop);
  if(RebirthNpcSettlementPersistenceStore.Calls!=1||RebirthNpcCommandGateway.Calls!=1||ActualCallbacks.Captures!=1||ActualCallbacks.Clears!=1)throw new Exception("healthy timeline callbacks suppressed");
  return "PASS actual callback bodies: unresolved timeline skips decisions/writers/shutdown capture and preserves runtime; healthy path runs. Explicit scheduler/store adapters; no native exit timing claim.";
 }
}
"@
Add-Type -TypeDefinition $code
[TimelineCallbackFixture]::Run()
