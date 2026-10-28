#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationNativeRewardDelivery.cs'))
$camperFixture=@'
using System;
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public static class RebirthCharacterCreationHoldService { public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held; }
public class CraftCompleteData {}
public class Buffs { public float Stamp;public float GetCustomVar(string key)=>Stamp;public void SetCustomVar(string key,float value,bool sync){Stamp=value;} }
public class EntityPlayer { public World world;public int entityId=7;public Buffs Buffs=new Buffs();public bool IsDead()=>false; }
public class EntityPlayerLocal:EntityPlayer
{
 public int Calls;public bool Throw;
 public void GiveExp(CraftCompleteData data){Calls++;if(Throw)throw new InvalidOperationException("partial native reward failure");}
}
public class World { public EntityPlayer Player;public EntityPlayerLocal GetPrimaryPlayer()=>Player as EntityPlayerLocal;public bool IsRemote()=>false;public EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null; }
public class GameManager
{
 public static GameManager Instance=new GameManager();public World World=new World();public int Saves;
 public void TriggerSendOfLocalPlayerDataFile(float delay){Saves++;}
}
public class Binding { public EntityPlayer Player;public object Identity=new object();public Admission Admission=new Admission();public bool IsOwnerCurrent()=>true; }
public class Admission { public string JobId="0123456789abcdef0123456789abcdef"; }
public enum RebirthCamperRemoteOwnerEffectKind {Reward,Refund}
public static class RebirthCamperStationRemoteRewardPayload {public static bool TryCreate(RebirthCamperStationTrainingPlan plan,CraftCompleteData reward,out byte[] payload){payload=new byte[]{1};return plan.Valid;}}
public static class RebirthCamperRemoteOwnerApplication {
 public static bool IsRegistered=true,Send=true;public static int Sends;public static Action<bool> Callback;
 public static bool SendOwnerApplication(EntityPlayer player,Guid operation,RebirthCamperRemoteOwnerEffectKind kind,byte[] payload,Action<bool> ack){Sends++;Callback=ack;return Send;}
}
public class Completed { public bool Allowed=true;public bool TryGetNativeReward(RebirthCamperStationTrainingPlan p,out CraftCompleteData reward){reward=new CraftCompleteData();return Allowed;} }
public class RebirthCamperStationTrainingPlan
{
 public Binding Binding=new Binding();public Completed Completed=new Completed();public bool Valid=true;public bool IsCurrent()=>Valid;
}
public static class RebirthCamperStationNativeRewardReceipt
{
 public static bool Saved;public static string Key(object a)=>"receipt";
 public static bool HasSaved(object i,object a,bool applied)=>Saved;
}
public static class Log { public static int Warnings;public static void Warning(string message){Warnings++;} }
SOURCE_BODY
public static class DeliveryFixture
{
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 private static RebirthCamperStationTrainingPlan Plan(out EntityPlayerLocal player)
 {
  player=new EntityPlayerLocal{world=GameManager.Instance.World};GameManager.Instance.World.Player=player;
  return new RebirthCamperStationTrainingPlan{Binding=new Binding{Player=player}};
 }
 public static void Run()
 {
  var plan=Plan(out var player);ThreadManager.Main=false;
  Check(!RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out _),"wrong-thread admission refused");ThreadManager.Main=true;
  RebirthCharacterCreationHoldService.Held=true;Check(!RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out _),"creation hold refuses delivery");RebirthCharacterCreationHoldService.Held=false;
  plan.Completed.Allowed=false;Check(!RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out _),"unconfirmed handoff refused");plan.Completed.Allowed=true;
  Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out var op),"original delivery admitted");
  Check(!op.TryDeliver()&&player.Calls==1&&player.Buffs.Stamp==1&&GameManager.Instance.Saves==1,"one invocation awaits native save");
  Check(!op.TryDeliver()&&player.Calls==1&&GameManager.Instance.Saves==1,"poll throttles native save without replay");
  UnityEngine.Time.realtimeSinceStartup=2;RebirthCamperStationNativeRewardReceipt.Saved=true;
  Check(op.TryDeliver()&&player.Calls==1,"saved acknowledgement completes without repeat");
  UnityEngine.Time.realtimeSinceStartup=4;Check(op.TryDeliver()&&player.Calls==1,"repeat completion does not repeat native reward");
  plan=Plan(out player);player.Throw=true;Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out op),"failure operation admitted");
  Check(!op.TryDeliver()&&player.Calls==1&&player.Buffs.Stamp==2,"partial exception retains in-flight stamp");
  Check(!op.TryDeliver()&&player.Calls==1&&Log.Warnings==1,"same operation never replays partial failure");
  Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out var replacement)&&!replacement.TryDeliver()&&player.Calls==1,"new operation cannot replay retained in-flight stamp");
  plan=Plan(out player);Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out op),"stale operation admitted before change");plan.Valid=false;
  Check(!op.TryDeliver()&&player.Calls==0&&player.Buffs.Stamp==0,"stale owner sends no effects");
  var remote=new EntityPlayer{world=GameManager.Instance.World};GameManager.Instance.World.Player=remote;
  plan=new RebirthCamperStationTrainingPlan{Binding=new Binding{Player=remote}};RebirthCamperStationNativeRewardReceipt.Saved=false;
  Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out op),"server remote owner original admitted");
  Check(!op.TryDeliver()&&RebirthCamperRemoteOwnerApplication.Sends==1,"remote send waits native owner acknowledgement");
  Check(!op.TryDeliver()&&RebirthCamperRemoteOwnerApplication.Sends==1,"remote poll never resends");
  remote.Buffs.Stamp=1;RebirthCamperRemoteOwnerApplication.Callback(true);
  Check(!op.TryDeliver(),"outer callback alone cannot prove native reward player file");
  RebirthCamperStationNativeRewardReceipt.Saved=true;Check(op.TryDeliver()&&op.IsConfirmedFor(plan),"remote requires callback and inner saved native receipt1");
  RebirthCamperRemoteOwnerApplication.Callback(false);Check(op.TryDeliver()&&RebirthCamperRemoteOwnerApplication.Sends==1,"duplicate callback cannot regress acknowledged original or resend");
  plan.Valid=false;Check(op.IsConfirmedFor(plan),"confirmed remote delivery remains acknowledged after original native receipt retirement invalidates live output");
  remote=new EntityPlayer{world=GameManager.Instance.World};GameManager.Instance.World.Player=remote;plan=new RebirthCamperStationTrainingPlan{Binding=new Binding{Player=remote}};
  Check(RebirthCamperStationNativeRewardDelivery.TryBegin(plan,out op)&&!op.TryDeliver(),"remote callback failure setup");
  RebirthCamperRemoteOwnerApplication.Callback(false);remote.Buffs.Stamp=1;
  Check(!op.TryDeliver()&&!op.IsConfirmedFor(plan)&&RebirthCamperRemoteOwnerApplication.Sends==2,"failed remote callback never authorizes receipt retirement or replay");
  Console.WriteLine("PASS actual native delivery: original handoff/thread gates, one reward through delayed save polls, throttling, acknowledgement, partial exception/no replay and stale owner. Native effects/file witnesses doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('SOURCE_BODY',[regex]::Replace($camperSource,'(?m)^using [^;]+;\r?\n',''))
[DeliveryFixture]::Run()
