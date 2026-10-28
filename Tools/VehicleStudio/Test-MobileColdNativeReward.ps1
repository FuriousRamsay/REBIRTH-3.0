#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationTrainingRecovery.cs'))
$camperStart=$camperSource.IndexOf('    private bool TryNativeReward(')
$camperEnd=$camperSource.IndexOf('    private void TryBeginRetirement(',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Cold native reward method missing.'}
$camperFixture=@'
using System;
using System.Collections.Generic;
public class ItemValue { public Dictionary<string,object> Metadata=new Dictionary<string,object>();public ItemValue Clone(){var c=new ItemValue();foreach(var p in Metadata)c.Metadata.Add(p.Key,p.Value);return c;} }
public class ItemStack { public ItemValue itemValue=new ItemValue();public ItemStack Clone()=>new ItemStack{itemValue=itemValue.Clone()}; }
public class CraftCompleteData
{
 public int CrafterEntityID,CraftExpGain,RecipeUsedCount;public ItemStack CraftedItemStack;public string RecipeName,ItemScrapped;
 public CraftCompleteData(int actor,ItemStack stack,string recipe,string scrap,int xp,int count){CrafterEntityID=actor;CraftedItemStack=stack;RecipeName=recipe;ItemScrapped=scrap;CraftExpGain=xp;RecipeUsedCount=count;}
}
public class Buffs { public float Phase;public float GetCustomVar(string k)=>Phase;public void SetCustomVar(string k,float p,bool sync){Phase=p;} }
public class EntityPlayer { public Buffs Buffs=new Buffs();public int entityId=99;public World world; }
public class EntityPlayerLocal:EntityPlayer { public int Calls;public bool Throw;public CraftCompleteData Last;public void GiveExp(CraftCompleteData data){Calls++;Last=data;if(Throw)throw new Exception("native partial failure");} }
public class World { public EntityPlayer Primary;public EntityPlayer GetPrimaryPlayer()=>Primary;public bool IsRemote()=>false; }
public class GameManager { public static GameManager Instance=new GameManager();public World World=new World();public int Saves;public void TriggerSendOfLocalPlayerDataFile(float time){Saves++;} }
public static class RebirthCharacterCreationHoldService { public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held; }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public class Admission { public string JobId="0123456789abcdef0123456789abcdef"; }
public enum RebirthCamperRemoteOwnerEffectKind{Reward,Refund}
public static class RebirthCamperStationRemoteRewardPayload{public static bool TryCreate(Admission admission,int actor,CraftCompleteData data,out byte[] bytes){bytes=new byte[]{1};return true;}}
public static class RebirthCamperRemoteOwnerApplication{public static bool IsRegistered=true;public static int Sends;public static Action<bool> Callback;public static bool SendOwnerApplication(EntityPlayer player,Guid op,RebirthCamperRemoteOwnerEffectKind kind,byte[] payload,Action<bool> ack){Sends++;Callback=ack;return true;}}
public class Station { public List<CraftCompleteData> CraftCompleteList=new List<CraftCompleteData>(); }
public static class RebirthStationCompletionReceipt
{
 public const string Prefix="reserved.";public class Identity { public string Job="0123456789abcdef0123456789abcdef"; }
 public static bool TryReadIdentity(CraftCompleteData row,out Identity i){i=new Identity();return true;}
}
public static class RebirthCamperStationNativeRewardReceipt
{
 public static bool Readable=true;public static float SavedPhase;
 public static string Key(Admission a)=>"key";public static bool TryReadSaved(object i,Admission a,out float p){p=SavedPhase;return Readable;}
 public static bool HasSaved(object i,Admission a,bool applied)=>Readable&&SavedPhase==(applied?1:2);
}
public class ColdFixture
{
 private EntityPlayer player;private object identity=new object();private Admission admission=new Admission();private Station station=new Station();
 private bool intentPending,nativeInvoked,nativeUncertain,remoteCallback,remoteAcknowledged;private float nextNativeObservation;private bool Valid=true;
 private bool IsCurrent()=>Valid;
METHOD_BODY
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 private static ColdFixture Create(out EntityPlayerLocal local)
 {
  local=new EntityPlayerLocal{world=GameManager.Instance.World};GameManager.Instance.World.Primary=local;var f=new ColdFixture{player=local};
  var stack=new ItemStack();stack.itemValue.Metadata.Add("reserved.version",1);stack.itemValue.Metadata.Add("rebirth.cooking.token","old");stack.itemValue.Metadata.Add("food",42);
  f.station.CraftCompleteList.Add(new CraftCompleteData(7,stack,"recipe","",10,1));return f;
 }
 public static void Run()
 {
  var f=Create(out var p);RebirthCamperStationNativeRewardReceipt.Readable=false;
  Check(!f.TryNativeReward()&&p.Calls==0,"unreadable file is not absence");RebirthCamperStationNativeRewardReceipt.Readable=true;UnityEngine.Time.realtimeSinceStartup=2;
  Check(!f.TryNativeReward()&&p.Calls==1&&p.Buffs.Phase==1,"saved never-applied phase permits one delivery");
  Check(p.Last.CrafterEntityID==99&&p.Last.CraftExpGain==10&&p.Last.RecipeUsedCount==1,"current stable owner's actor and native totals retained");
  Check(!p.Last.CraftedItemStack.itemValue.Metadata.ContainsKey("reserved.version")&&!p.Last.CraftedItemStack.itemValue.Metadata.ContainsKey("rebirth.cooking.token")&&p.Last.CraftedItemStack.itemValue.Metadata.ContainsKey("food"),"detached delivery metadata only");
  Check(f.station.CraftCompleteList[0].CraftedItemStack.itemValue.Metadata.ContainsKey("reserved.version"),"held receipt unchanged");
  UnityEngine.Time.realtimeSinceStartup=4;RebirthCamperStationNativeRewardReceipt.SavedPhase=1;
  Check(f.TryNativeReward()&&p.Calls==1,"saved acknowledgement without repeat");
  f=Create(out p);p.Buffs.Phase=1;Check(f.TryNativeReward()&&p.Calls==0,"already-applied reload does not reissue");
  f=Create(out p);p.Buffs.Phase=2;RebirthCamperStationNativeRewardReceipt.SavedPhase=2;Check(!f.TryNativeReward()&&p.Calls==0,"in-flight reload held");
  f=Create(out p);RebirthCamperStationNativeRewardReceipt.SavedPhase=0;p.Throw=true;Check(!f.TryNativeReward()&&p.Calls==1&&p.Buffs.Phase==2,"partial failure retained");
  UnityEngine.Time.realtimeSinceStartup=6;Check(!f.TryNativeReward()&&p.Calls==1,"partial failure never replayed");
  f=Create(out p);f.intentPending=true;Check(!f.TryNativeReward()&&p.Calls==0,"unsaved choice cannot deliver");f.intentPending=false;f.Valid=false;
  Check(!f.TryNativeReward()&&p.Calls==0,"stale owner cannot deliver");
  f=Create(out p);var remote=new EntityPlayer{world=GameManager.Instance.World};f.player=remote;
  RebirthCamperStationNativeRewardReceipt.SavedPhase=0;Check(!f.TryNativeReward()&&RebirthCamperRemoteOwnerApplication.Sends==1,"cold pending server remote owner sends original native reward once");
  UnityEngine.Time.realtimeSinceStartup=8;Check(!f.TryNativeReward()&&RebirthCamperRemoteOwnerApplication.Sends==1,"cold remote poll never resends");
  RebirthCamperRemoteOwnerApplication.Callback(true);remote.Buffs.Phase=1;UnityEngine.Time.realtimeSinceStartup=10;
  Check(!f.TryNativeReward(),"cold remote outer ack insufficient without inner native player file");
  RebirthCamperStationNativeRewardReceipt.SavedPhase=1;UnityEngine.Time.realtimeSinceStartup=12;Check(f.TryNativeReward(),"cold remote requires actual inner saved reward receipt1");
  RebirthCamperRemoteOwnerApplication.Callback(false);UnityEngine.Time.realtimeSinceStartup=14;Check(f.TryNativeReward()&&RebirthCamperRemoteOwnerApplication.Sends==1,"cold remote duplicate callback cannot replay or regress first ack");
  Console.WriteLine("PASS actual cold native delivery: readable absence required, current owner actor, detached metadata, one delivery through save acknowledgement, already-applied/in-flight states, partial failure and unsaved/stale authority. Native/file/identity adapters doubled; no reload gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[ColdFixture]::Run()
