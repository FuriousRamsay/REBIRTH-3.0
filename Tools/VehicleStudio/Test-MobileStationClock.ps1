#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationState.cs'))
$camperStart=$camperSource.IndexOf('    public void TickMobile(')
$camperEnd=$camperSource.IndexOf('    public RebirthCamperStationState(',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Production mobile tick source missing.'}
$camperTick=$camperSource.Substring($camperStart,$camperEnd-$camperStart)
$camperFixture=@'
using System;
public static class ThreadManager { public static bool IsMainThread()=>true; }
public class World { public EntityVehicle Owner;public bool IsRemote()=>false;public EntityVehicle GetEntity(int id)=>Owner; }
public class EntityVehicle { public World world;public int entityId=1;public UnityEngine.Vector3 position; }
namespace UnityEngine { public struct Vector3 { public float x,y,z; } public static class Mathf { public static float Min(float a,float b)=>Math.Min(a,b);public static int FloorToInt(float n)=>(int)Math.Floor(n); } }
public struct Vector3i { public Vector3i(int x,int y,int z){} }
public enum EnumAIDirectorChunkEvent { None }
public class GameTimer { public static GameTimer Instance=new GameTimer();public ulong ticks; }
public static class RebirthCamperStationSaveEvidence { public static bool Pending;public static bool IsPending(RebirthCamperStationState s)=>Pending; }
public static class RebirthCamperStationCompletionCallsite { public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held; }
public static class RebirthCamperStationQueuedAuthority { public static bool Allowed;public static bool CanProcessNativeQueue(RebirthCamperStationState s)=>Allowed; }
public static class RebirthCamperStationHeat { public static bool IsInstalled=true; }
public static class RebirthCamperStationOpenQueueProcessing { public static int Begun,Disposed;public static IDisposable Begin(RebirthCamperStationState s){Begun++;return new Scope();}private sealed class Scope:IDisposable { public void Dispose(){Disposed++;} } }
public class Block { public float HeatMapStrength; }
public class BlockValue { public Block Block=new Block(); }
public class Slot { public RebirthCamperStationState State; }
public class Stations { public Slot Current=new Slot();public Slot Find(Guid id)=>Current; }
public class RebirthCamperModuleRuntime { public static RebirthCamperModuleRuntime Instance=new RebirthCamperModuleRuntime();public Stations Stations=new Stations();public static RebirthCamperModuleRuntime For(EntityVehicle v)=>Instance; }
public class RebirthCamperStationState
{
 public Guid InstallationId=Guid.NewGuid();public EntityVehicle BoundVehicle,MobileOwner;public ulong lastTickTime;
 public bool[] isModuleUsed=new[]{false,false,false,true};public bool isBurning=true,isBesideWater;public float BurnTotalTimeLeft=100;public bool IsCrafting=true;public BlockValue InstalledBlock=new BlockValue();
 public int Fuel,Recipes,Materials;public float Elapsed;
 public bool hasRecipeInQueue()=>true;public bool IsByWater(World w,Vector3i p)=>false;
 public void HandleFuel(World w,float elapsed){Fuel++;Elapsed=elapsed;}
 public void HandleRecipeQueue(float elapsed){Recipes++;}public void HandleMaterialInput(float elapsed){Materials++;}
 public void emitHeatMapEvent(World w,EnumAIDirectorChunkEvent e){}public void setModified(){}
TICK_BODY
}
public static class ClockFixture
{
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Run()
 {
  var world=new World();var vehicle=new EntityVehicle{world=world};world.Owner=vehicle;
  var station=new RebirthCamperStationState{BoundVehicle=vehicle,lastTickTime=20};RebirthCamperModuleRuntime.Instance.Stations.Current.State=station;
  GameTimer.Instance.ticks=200;station.TickMobile(vehicle);
  Check(station.lastTickTime==0&&station.Fuel==0&&station.Recipes==0&&station.Materials==0,"unconfirmed publication pauses all processing");
  RebirthCamperStationQueuedAuthority.Allowed=true;station.TickMobile(vehicle);
  Check(station.lastTickTime==200&&station.Fuel==0,"resume establishes baseline without catch-up");
  GameTimer.Instance.ticks=205;station.TickMobile(vehicle);
  Check(station.Fuel==1&&station.Recipes==1&&station.Materials==1&&Math.Abs(station.Elapsed-.25f)<.0001f&&station.MobileOwner==null&&RebirthCamperStationOpenQueueProcessing.Begun==1&&RebirthCamperStationOpenQueueProcessing.Disposed==1,"normal native elapsed tick resumes inside one presentation scope");
  RebirthCamperStationSaveEvidence.Pending=true;GameTimer.Instance.ticks=400;station.TickMobile(vehicle);
  Check(station.lastTickTime==0&&station.Fuel==1&&RebirthCamperStationOpenQueueProcessing.Begun==1,"pending snapshot pauses clock before presentation/processing");RebirthCamperStationSaveEvidence.Pending=false;
  station.TickMobile(vehicle);Check(station.lastTickTime==400&&station.Fuel==1,"pending-save release establishes baseline without catch-up");
  GameTimer.Instance.ticks=405;station.TickMobile(vehicle);Check(station.Fuel==2&&Math.Abs(station.Elapsed-.25f)<.0001f,"pending-save release resumes normal interval");
  RebirthCamperStationCompletionCallsite.Held=true;station.TickMobile(vehicle);
  Check(station.lastTickTime==0&&station.Fuel==2&&station.Recipes==2&&station.Materials==2,"held completion pauses all processing");
  Console.WriteLine("PASS actual mobile tick: unpublished/held work consumes no fuel or material, resume skips catch-up, normal interval and pending snapshot no-catch-up and scoped presentation. Native adapters doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('TICK_BODY',$camperTick)
[ClockFixture]::Run()
