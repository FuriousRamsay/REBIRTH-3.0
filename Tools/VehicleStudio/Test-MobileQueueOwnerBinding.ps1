#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueuedAuthority.cs'))
$camperStart=$camperSource.IndexOf('    internal static bool TryGetCurrent(')
$camperEnd=$camperSource.IndexOf('    // A fresh vehicle-save observation',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Queue binding lookup missing.'}
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class Recipe { public string Job="job"; }
public class RecipeQueueItem { public Recipe Recipe=new Recipe();public int StartingEntityId=7,AmountToRepair,Multiplier=1,Quality=2;public object RepairItem; }
public class RebirthCamperStationState { public RecipeQueueItem[] Queue; }
public static class RebirthStationGridQueue { public static bool TryGetJobId(Recipe r,out string j){j=r.Job;return j!=null;} }
public class Player { public int entityId=99; }
public class Admission { public bool MatchesQueuedQuality(Recipe r,int multiplier,int quality)=>r.Job=="job"&&multiplier==1&&quality==2; }
public static class QueueOwnerFixture {
 internal class Binding { public Player Player=new Player();public int QueuedOwner=7;public bool Valid=true;public Admission Admission=new Admission();public bool IsCurrent()=>Valid;public bool IsProcessingCurrent()=>Valid; }
 private static readonly ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>> bindings=new ConditionalWeakTable<RebirthCamperStationState,Dictionary<string,Binding>>();
METHOD_BODY
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
  var row=new RecipeQueueItem();var s=new RebirthCamperStationState{Queue=new[]{row}};var original=new Binding();bindings.Add(s,new Dictionary<string,Binding>{{"job",original}});
  Check(TryGetCurrent(s,row,out var current)&&ReferenceEquals(current,original)&&current.Player.entityId!=row.StartingEntityId,"current owner can differ from saved native recipe actor");
  row.StartingEntityId=99;Check(!TryGetCurrent(s,row,out _),"rewritten recipe actor denied");row.StartingEntityId=7;
  original.Valid=false;Check(!TryGetCurrent(s,row,out _),"stale stable-owner authority denied");original.Valid=true;
  row.Quality=3;Check(!TryGetCurrent(s,row,out _),"paid quality still checked");row.Quality=2;
  s.Queue=new[]{row,new RecipeQueueItem()};Check(!TryGetCurrent(s,row,out _),"duplicate job object denied");s.Queue=new[]{row,row};Check(!TryGetCurrent(s,row,out _),"duplicate exact row denied");s.Queue=new[]{row};
  row.RepairItem=new object();Check(!TryGetCurrent(s,row,out _),"repair substitution denied");row.RepairItem=null;
  ThreadManager.Main=false;Check(!TryGetCurrent(s,row,out _),"off-thread denied");
  Console.WriteLine("PASS actual queue lookup: immutable saved native actor, distinct current owner, stale authority, paid quality, duplicate jobs, repair and thread checks. Stable-owner predicate/native adapters doubled; no identity-recovery gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[QueueOwnerFixture]::Run()
