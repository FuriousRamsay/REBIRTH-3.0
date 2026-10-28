using System;
using System.Collections.Generic;
struct Vector3i {}
struct BlockValue { public int type; }
class WorldBase {public BlockValue GetBlock(Vector3i position)=>new BlockValue();public bool IsDaytime()=>true;}
class GameManager {public static GameManager Instance=new GameManager();public WorldBase World=new WorldBase();}
class GameTimer {public static GameTimer Instance=new GameTimer();public ulong ticks=300;}
class TileEntityPlantGrowingRebirth {
 public ulong GameTimerTicks=100,AccumulatedTicks=7;public bool bWatered=true;public int LastProcessedWorldSeconds=5,Water=100;
 public void SanitizeTimingBaselines(){} public bool SetAuthoritativeLightPass(bool value)=>false;public void setModified(){}
}
static class AdvancedFarmingRuntimePolicy {public static bool Enabled=true;public static int GetEffectiveGrowthSeconds(WorldBase w,Vector3i p)=>100;public static int GetActiveDirectMaxElapsedSeconds()=>100;}
enum AdvancedFarmingManagedMemoryPhase {LightEvaluate,ThermalEvaluate}
static class AdvancedFarmingPerfSnapshotService {public static bool SamplingEnabled=false;public static void RecordActiveBlockLightPrecheck(bool value){}public static void RecordLightEvaluate(long value){}public static void RecordThermalEvaluate(long value){}public static void RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase p,long value){}}
static class AdvancedFarmingLightService {public struct LightResult {public bool HasSunLight,HasBlockLight,HasOpenSky;}public static bool TryGetCurrentBlockLight(WorldBase w,Vector3i p,out byte light){light=15;return true;}public static LightResult Evaluate(WorldBase w,Vector3i p,int a,int b,bool c,bool d)=>new LightResult{HasSunLight=true};}
static class AdvancedFarmingTemperatureService {public static bool Complete;public static float Temperature=70;public struct Result{public bool ThermalEvaluationComplete;public float Temperature;}public static Result Evaluate(WorldBase w,Vector3i p,bool light,bool enabled)=>new Result{ThermalEvaluationComplete=Complete,Temperature=Temperature};}
static class AdvancedFarmingSyncService {public static void SendPlant(Vector3i p,int type,ulong ticks,bool watered){}}
static class Fixture {
 sealed class PlantCatchupEntry {public Vector3i Pos;public BlockValue BlockValue;public TileEntityPlantGrowingRebirth TileEntity=new TileEntityPlantGrowingRebirth();public bool IsMushroom,Deferred;public float Temperature;public int Depletion;public ulong StageRateTicks;}
 static bool s_catchupDebugEnabled=false,DiagnosticBypassLightEvaluation=false,DiagnosticBypassThermalEvaluation=false;
 static int Steps,Marked;
 sealed class WaterCatchupEntry {}
 static bool ProcessPlantSharedStep(WorldBase w,PlantCatchupEntry p,List<WaterCatchupEntry> entries)=>ProcessPlantSingleActiveStep(w,p);
 static void CommitSharedWater(List<WaterCatchupEntry> entries){}
 static bool IsFullyGrownPlant(BlockValue value)=>false;static string GetBiomeName(Vector3i p)=>"forest";
 static string SafeBlockName(BlockValue v)=>"test";static string SafePlantState(TileEntityPlantGrowingRebirth t)=>"test";static void LogCatchupDebug(string value){}
 static int GetWaterDepletion(BlockValue value,float temperature)=>1;
 static void MarkPlantChanged(Vector3i p,BlockValue v,TileEntityPlantGrowingRebirth t,ulong a,bool b,ulong c,int d){Marked++;}
 // The step double measures caller dispatch, not native growth implementation.
 static bool ProcessPlantSingleActiveStep(WorldBase w,PlantCatchupEntry plant){Steps++;plant.TileEntity.AccumulatedTicks++;plant.TileEntity.Water-=plant.Depletion;return true;}
 // PRODUCTION_METHODS
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){var plant=new PlantCatchupEntry();var world=GameManager.Instance.World;
  Check(ProcessSingleActivePlant(world,plant,15),"deferred dispatch refused");
  Check(plant.Deferred&&Steps==0&&Marked==0,"incomplete geometry consumed work");
  Check(plant.TileEntity.GameTimerTicks==100&&plant.TileEntity.LastProcessedWorldSeconds==5&&plant.TileEntity.AccumulatedTicks==7&&plant.TileEntity.Water==100,"incomplete geometry changed baselines");
  AdvancedFarmingTemperatureService.Complete=true;
  Check(ProcessSingleActivePlant(world,plant,15)&&!plant.Deferred,"complete retry remained deferred");
  Check(Steps==10&&plant.TileEntity.GameTimerTicks==300&&plant.TileEntity.AccumulatedTicks==17&&plant.TileEntity.Water==90,"retained elapsed work not dispatched once");
  ProcessSingleActivePlant(world,plant,15);Check(Steps==10,"same elapsed interval replayed");
  GameTimer.Instance.ticks=500;AdvancedFarmingTemperatureService.Temperature=20;
  ProcessSingleActivePlant(world,plant,25);Check(!plant.Deferred&&Steps==10&&plant.TileEntity.GameTimerTicks==500,"proven cold block must consume blocked interval");
  var listed=new PlantCatchupEntry();Steps=0;GameTimer.Instance.ticks=300;AdvancedFarmingTemperatureService.Complete=false;AdvancedFarmingTemperatureService.Temperature=70;
  var plants=new List<PlantCatchupEntry>{listed};var water=new List<WaterCatchupEntry>();
  Check(ProcessPlantsActiveDirect(world,plants,water,15)==1&&listed.Deferred&&Steps==0&&listed.TileEntity.GameTimerTicks==100&&listed.TileEntity.LastProcessedWorldSeconds==5&&listed.TileEntity.Water==100,"list caller discarded incomplete interval");
  AdvancedFarmingTemperatureService.Complete=true;ProcessPlantsActiveDirect(world,plants,water,15);
  Check(Steps==10&&listed.TileEntity.GameTimerTicks==300&&listed.TileEntity.Water==90,"list caller lost retained elapsed work");
  ProcessPlantsActiveDirect(world,plants,water,15);Check(Steps==10,"list caller repeated same interval");
  Console.WriteLine("PASS actual active prepare/direct/list callers: incomplete retains, retry once, cold consumes");
 }
}
