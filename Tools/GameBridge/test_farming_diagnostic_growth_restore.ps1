$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/ConsoleCmdRebirthAdvancedFarmingFresh.cs')
$start=$source.IndexOf('    private static void RestoreFarmingSuiteGrowth()')
$end=$source.IndexOf('    private static void ExecuteHoverTrace(',$start)
if($start -lt 0 -or $end -le $start){throw 'Production growth cleanup methods missing'}
$methods=$source.Substring($start,$end-$start)
$fixture=@'
using System;
class World{}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class Log{public static void Out(string message){}}
static class AdvancedFarmingRuntimePolicy{
public static int GrowthSeconds=5400,PolicyVersion;
public static void SetGrowthSeconds(int value){GrowthSeconds=value;PolicyVersion++;}}
public static class DiagnosticGrowthRestoreChecks{
class DiagnosticTemperatureLease{public void Restore(){}}
static DiagnosticTemperatureLease s_autoBenchTemperature,s_farmingSuiteTemperature;
class DiagnosticSamplingLease{}
static DiagnosticSamplingLease s_autoBenchSampling,s_farmingSuiteSampling;
static void RestoreSampling(ref DiagnosticSamplingLease lease){lease=null;} // Sampling ownership has its own fixture.
static bool s_farmingSuiteOwnsGrowth,s_autoBenchOwnsGrowth;
static World s_farmingSuiteWorld,s_autoBenchWorld;
static int s_farmingSuitePriorGrowth,s_farmingSuiteAppliedGrowth,s_farmingSuitePolicyVersion;
static int s_autoBenchPriorGrowthSeconds,s_autoBenchGrowthSeconds,s_autoBenchGrowthPolicyVersion;
// METHODS
static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
static void Bind(){s_farmingSuiteWorld=s_autoBenchWorld=GameManager.Instance.World;
s_farmingSuitePriorGrowth=s_autoBenchPriorGrowthSeconds=5400;
AdvancedFarmingRuntimePolicy.SetGrowthSeconds(10);
s_farmingSuiteAppliedGrowth=s_autoBenchGrowthSeconds=10;
s_farmingSuitePolicyVersion=s_autoBenchGrowthPolicyVersion=AdvancedFarmingRuntimePolicy.PolicyVersion;
s_farmingSuiteOwnsGrowth=s_autoBenchOwnsGrowth=true;}
public static void Run(){
Bind();RestoreAutoBenchGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==5400&&!s_autoBenchOwnsGrowth,"benchmark restore omitted");
Bind();RestoreFarmingSuiteGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==5400&&!s_farmingSuiteOwnsGrowth,"suite restore omitted");
Bind();AdvancedFarmingRuntimePolicy.SetGrowthSeconds(10);RestoreFarmingSuiteGrowth();RestoreAutoBenchGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==10,"same-value later edit overwritten");
Bind();AdvancedFarmingRuntimePolicy.SetGrowthSeconds(120);RestoreAutoBenchGrowth();RestoreFarmingSuiteGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==120,"later edit overwritten");
Bind();GameManager.Instance.World=new World();RestoreAutoBenchGrowth();RestoreFarmingSuiteGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==10,"replacement world mutated");
Bind();ThreadManager.Main=false;RestoreAutoBenchGrowth();RestoreFarmingSuiteGrowth();Check(AdvancedFarmingRuntimePolicy.GrowthSeconds==10,"worker mutated policy");
Console.WriteLine("PASS actual growth cleanup methods; command lifecycle, native policy broadcast and temperature restoration NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHODS',$methods)
[DiagnosticGrowthRestoreChecks]::Run()
