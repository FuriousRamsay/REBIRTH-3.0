$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/ConsoleCmdRebirthAdvancedFarmingFresh.cs')
$start=$source.IndexOf('    private sealed class DiagnosticSamplingLease')
$end=$source.IndexOf('    private sealed class DiagnosticTemperatureLease',$start)
if($start -lt 0 -or $end -le $start){throw 'Production sampling lease missing'}
$fixture=@'
using System;
class World{}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class AdvancedFarmingPerfSnapshotService{
public static bool SamplingEnabled;public static long SamplingRevision;
public static void SetSampling(bool value){SamplingEnabled=value;SamplingRevision++;}
public static void ResetSampling(){SamplingRevision++;}}
public static class DiagnosticSamplingRestoreChecks{
// METHODS
static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
public static void Run(){
var world=GameManager.Instance.World;
var lease=DiagnosticSamplingLease.Begin(world);Check(lease.Current&&AdvancedFarmingPerfSnapshotService.SamplingEnabled,"lease did not start");lease.Restore();Check(!AdvancedFarmingPerfSnapshotService.SamplingEnabled,"prior disabled not restored");
AdvancedFarmingPerfSnapshotService.SetSampling(true);lease=DiagnosticSamplingLease.Begin(world);lease.Restart();Check(lease.Current,"own restart lost ownership");lease.Restore();Check(AdvancedFarmingPerfSnapshotService.SamplingEnabled,"prior enabled not restored");
lease=DiagnosticSamplingLease.Begin(world);AdvancedFarmingPerfSnapshotService.SetSampling(false);lease.Restore();Check(!AdvancedFarmingPerfSnapshotService.SamplingEnabled,"external disable overwritten");
lease=DiagnosticSamplingLease.Begin(world);AdvancedFarmingPerfSnapshotService.ResetSampling();Check(!lease.Current,"external reset not detected");
lease=DiagnosticSamplingLease.Begin(world);GameManager.Instance.World=new World();lease.Restore();Check(AdvancedFarmingPerfSnapshotService.SamplingEnabled,"replacement world sampling changed");
Console.WriteLine("PASS actual sampling lease ownership; native counters, timer commands and lifecycle NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHODS',$source.Substring($start,$end-$start))
[DiagnosticSamplingRestoreChecks]::Run()
