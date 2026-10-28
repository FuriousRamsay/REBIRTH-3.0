$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper'
function Get-CamperRegistrationBody([string]$file,[string]$signature){
 $source=[IO.File]::ReadAllText((Join-Path $root $file));$start=$source.IndexOf($signature)
 if($start-lt0){throw 'Registration method missing'}
 $open=$source.IndexOf('{',$start);$depth=1;$end=$open+1
 while($depth-gt0-and$end-lt$source.Length){if($source[$end]-eq'{'){$depth++};if($source[$end]-eq'}'){$depth--};$end++}
 if($depth-ne0){throw 'Registration method incomplete'}
 return $source.Substring($start,$end-$start)
}
$module=Get-CamperRegistrationBody 'RebirthCamperRemoteTransport.cs' 'public static void Register('
$station=Get-CamperRegistrationBody 'RebirthCamperRemoteStationTransport.cs' 'public static void Register('
$owner=Get-CamperRegistrationBody 'RebirthCamperRemoteOwnerApplication.cs' 'public static void RegisterOwnerApplication('
$fixture=@'
using System;
public class EntityPlayer {}public class EntityPlayerLocal:EntityPlayer {}
public class RebirthCamperStationState {}public class RebirthCamperStationAddress {}public class RebirthCamperRemoteStationCommand {}
public enum RebirthCamperRemoteOwnerEffectKind {Reward,Refund}
// Deliberately no NetPackageManager type or mocked registry. ModAPI startup
// installs adapters before native connection mappings exist.
public static class RebirthCamperRemoteTransport {
 private static Func<EntityPlayer,string> creationProvider;
 public static bool IsRegistered=>creationProvider!=null;
 MODULE
}
public static class RebirthCamperRemoteStationTransport {
 private static Func<EntityPlayer,string> creationProvider;
 private static Func<EntityPlayer,RebirthCamperStationAddress,string,object,RebirthCamperRemoteStationCommand,bool> command;
 private static Action<EntityPlayer,RebirthCamperStationState> observeTerminal;
 public static bool IsRegistered=>creationProvider!=null&&command!=null;
 public static bool HasObserver=>observeTerminal!=null;
 STATION
}
public static class RebirthCamperRemoteOwnerApplication {
 private static Func<EntityPlayer,string> creationProvider;
 private static Func<EntityPlayerLocal,Guid,RebirthCamperRemoteOwnerEffectKind,byte[],bool> apply;
 public static bool IsRegistered=>creationProvider!=null&&apply!=null;
 OWNER
}
public static class RegistrationFixture {
 public static void Run(){
  RebirthCamperRemoteOwnerApplication.RegisterOwnerApplication(p=>"creation",(p,id,k,bytes)=>true);
  RebirthCamperRemoteStationTransport.Register(p=>"creation",(p,a,c,l,command)=>true,(p,s)=>{});
  RebirthCamperRemoteTransport.Register(p=>"creation");
  if(!RebirthCamperRemoteOwnerApplication.IsRegistered||!RebirthCamperRemoteStationTransport.IsRegistered||!RebirthCamperRemoteStationTransport.HasObserver||!RebirthCamperRemoteTransport.IsRegistered)throw new Exception("Startup adapters were disabled");
  try{RebirthCamperRemoteTransport.Register(p=>"different");throw new Exception("Duplicate registration accepted");}catch(InvalidOperationException){}
 }
}
'@
$fixture=$fixture.Replace(' MODULE',$module).Replace(' STATION',$station).Replace(' OWNER',$owner)
Add-Type -TypeDefinition $fixture
[RegistrationFixture]::Run()
Write-Output 'PASS actual registration methods install owner/station/module adapters in startup order without any NetPackageManager type, preserve complete readiness/observer and refuse duplicate installation. Native mapping lifecycle separately verified from installed assembly IL; startup/gameplay rerun still required.'
