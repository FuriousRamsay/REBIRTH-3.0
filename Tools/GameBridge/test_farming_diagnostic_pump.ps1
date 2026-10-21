$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/ConsoleCmdRebirthAdvancedFarmingFresh.cs')
$start=$source.IndexOf('    internal static void PumpTemperatureTrack()');$end=$source.IndexOf('    private static void LogTemperatureTrackSampleLocked(',$start)
if($start -lt 0 -or $end -le $start){throw 'Production diagnostic pump missing'}
$method=$source.Substring($start,$end-$start)
$fixture=@'
using System;
class World{}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class AdvancedFarmingPerfSnapshotService{public static bool Sampling;public static void SetSampling(bool value){Sampling=value;}}
public static class DiagnosticPumpChecks{
static readonly object s_autoBenchLock=new object(),s_farmingSuiteLock=new object(),s_temperatureTrackLock=new object();
static bool s_autoBenchRunning,s_farmingSuiteRunning,s_temperatureTrackRunning;
static World s_autoBenchWorld,s_farmingSuiteWorld,s_temperatureTrackWorld;
static long s_autoBenchNextTicks,s_farmingSuiteNextTicks,s_temperatureTrackNextTicks;
static int s_autoBenchIntervalSeconds=1,s_farmingSuiteIntervalSeconds=1,s_temperatureTrackIntervalSeconds=1;
static int Auto,Suite,Temp;
static void AutoBenchTick(object ignored){Auto++;}static void FarmingSuiteTick(object ignored){Suite++;}static void TemperatureTrackTick(object ignored){Temp++;}
static void StopAutoBench(string ignored){s_autoBenchRunning=false;}static void StopTemperatureTrack(string ignored){s_temperatureTrackRunning=false;}
static void RestoreFarmingSuiteGrowth(){} // Cleanup policy is covered separately.
// METHOD
static void Bind(){s_autoBenchRunning=s_farmingSuiteRunning=s_temperatureTrackRunning=true;s_autoBenchWorld=s_farmingSuiteWorld=s_temperatureTrackWorld=GameManager.Instance.World;s_autoBenchNextTicks=s_farmingSuiteNextTicks=s_temperatureTrackNextTicks=0;}
static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
public static void Run(){
 Bind();ThreadManager.Main=false;PumpTemperatureTrack();Check(Auto+Suite+Temp==0,"worker touched diagnostic");
 ThreadManager.Main=true;PumpTemperatureTrack();Check(Auto==1&&Suite==1&&Temp==1,"due interval omitted");
 PumpTemperatureTrack();Check(Auto==1&&Suite==1&&Temp==1,"same interval repeated");
 Bind();GameManager.Instance.World=new World();PumpTemperatureTrack();Check(Auto==1&&Suite==1&&Temp==1&&!s_autoBenchRunning&&!s_farmingSuiteRunning&&!s_temperatureTrackRunning,"replacement world sampled");
 Bind();PumpTemperatureTrack();Check(Auto==2&&Suite==2&&Temp==2,"fresh run did not resume");
 Console.WriteLine("PASS actual pump: main thread, interval, original world, fresh run. Native sampling/lifecycle NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHOD',$method)
[DiagnosticPumpChecks]::Run()
