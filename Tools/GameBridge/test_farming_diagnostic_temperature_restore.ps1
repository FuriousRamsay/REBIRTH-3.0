$ErrorActionPreference='Stop'
$command=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/ConsoleCmdRebirthAdvancedFarmingFresh.cs')
$heat=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/AdvancedFarmingHeatQueryService.cs')
$start=$command.IndexOf('    private sealed class DiagnosticTemperatureLease')
$end=$command.IndexOf('    private static int s_autoBenchIntervalIndex;',$start)
$hs=$heat.IndexOf('    public static string SetTemperatureOverride(float value)')
$he=$heat.IndexOf('    public static string TemperatureStatus()',$hs)
if($start -lt 0 -or $end -le $start -or $hs -lt 0 -or $he -le $hs){throw 'Production temperature lease/policy methods missing'}
$fixture=@'
using System;
class World{}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class AdvancedFarmingHeatQueryService{
static bool s_temperatureOverrideEnabled;static float s_temperatureOverrideValue;static long s_temperatureOverrideRevision;
public static bool TemperatureOverrideEnabled=>s_temperatureOverrideEnabled;
public static float TemperatureOverrideValue=>s_temperatureOverrideValue;
public static long TemperatureOverrideRevision=>s_temperatureOverrideRevision;
static void InvalidateConditionCaches(string reason){} // Cache invalidation/broadcast are outside this fixture.
// POLICY
}
public static class DiagnosticTemperatureRestoreChecks{
// LEASE
static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
public static void Run(){
var world=GameManager.Instance.World;
AdvancedFarmingHeatQueryService.ClearTemperatureOverride();
var lease=DiagnosticTemperatureLease.Apply(world,-10);lease.Restore();Check(!AdvancedFarmingHeatQueryService.TemperatureOverrideEnabled,"disabled prior override not restored");
AdvancedFarmingHeatQueryService.SetTemperatureOverride(25);
lease=DiagnosticTemperatureLease.Apply(world,-10);lease.Restore();Check(AdvancedFarmingHeatQueryService.TemperatureOverrideValue==25,"prior temperature lost");
lease=DiagnosticTemperatureLease.Apply(world,-10);AdvancedFarmingHeatQueryService.SetTemperatureOverride(-10);lease.Restore();Check(AdvancedFarmingHeatQueryService.TemperatureOverrideValue==-10,"same-value explicit edit overwritten");
lease=DiagnosticTemperatureLease.Apply(world,-20);AdvancedFarmingHeatQueryService.ClearTemperatureOverride();lease.Restore();Check(!AdvancedFarmingHeatQueryService.TemperatureOverrideEnabled,"explicit clear overwritten");
lease=DiagnosticTemperatureLease.Apply(world,-30);GameManager.Instance.World=new World();lease.Restore();Check(AdvancedFarmingHeatQueryService.TemperatureOverrideValue==-30,"replacement world touched");
Check(DiagnosticTemperatureLease.Apply(world,12)==null,"stale world accepted");
world=GameManager.Instance.World;Check(DiagnosticTemperatureLease.Apply(world,float.NaN)==null&&DiagnosticTemperatureLease.Apply(world,float.PositiveInfinity)==null,"nonfinite temperature accepted");
ThreadManager.Main=false;Check(DiagnosticTemperatureLease.Apply(world,12)==null,"worker accepted");
Console.WriteLine("PASS actual temperature lease and override setter/clear; native lifecycle/cache/broadcast NOT covered.");}
}
'@
$fixture=$fixture.Replace('// POLICY',$heat.Substring($hs,$he-$hs)).Replace('// LEASE',$command.Substring($start,$end-$start))
Add-Type -TypeDefinition $fixture
[DiagnosticTemperatureRestoreChecks]::Run()
