$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Close game before offline validation'}
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Foundation/RebirthNpcLifecycle.cs'))
$signature='private static void QualifyWholeBundleAdmission()'
$start=$source.IndexOf($signature);if($start -lt 0){throw 'Admission qualifier missing'}
$open=$source.IndexOf('{',$start);$end=$open+1;$depth=1
while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
if($depth -ne 0){throw 'Incomplete admission qualifier'}
$method=$source.Substring($start,$end-$start).Replace('private static','internal static')
$code=@"
using System;
class World {}
class GameManager {internal static GameManager Instance;internal World World;}
static class RebirthNpcSaveQuarantineBundle {
 internal static bool AdmissionEnabled=true,Allow=true;internal static int Retires,Admissions;
 internal static World Original,Incoming;internal static string Directory;internal static Action OnRetire;
 internal static void RetireEndedClientProjections(){Retires++;OnRetire?.Invoke();}
 internal static bool TryAdmitWorld(World original,string directory,World incoming,out string error){Admissions++;Original=original;Incoming=incoming;Directory=directory;error="fixture refusal";return Allow&&ReferenceEquals(incoming,GameManager.Instance?.World);}
}
static class ActualLifecycle {
 private static readonly object Sync=new object();internal static World activeWorldReference;internal static string activeSaveDirectory;
 $method
}
public static class AdmissionBootstrapFixture {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static string Run(){
  GameManager.Instance=null;ActualLifecycle.QualifyWholeBundleAdmission();
  GameManager.Instance=new GameManager();ActualLifecycle.QualifyWholeBundleAdmission();
  Need(RebirthNpcSaveQuarantineBundle.Retires==0&&RebirthNpcSaveQuarantineBundle.Admissions==0,"process bootstrap entered world retirement/admission");
  var original=new World();var incoming=new World();GameManager.Instance.World=incoming;
  ActualLifecycle.activeWorldReference=original;ActualLifecycle.activeSaveDirectory="remembered-original";
  ActualLifecycle.QualifyWholeBundleAdmission();
  Need(ReferenceEquals(RebirthNpcSaveQuarantineBundle.Original,original)&&ReferenceEquals(RebirthNpcSaveQuarantineBundle.Incoming,incoming)&&RebirthNpcSaveQuarantineBundle.Directory=="remembered-original","qualifier substituted incoming scope for outgoing original");
  RebirthNpcSaveQuarantineBundle.Allow=false;bool refused=false;try{ActualLifecycle.QualifyWholeBundleAdmission();}catch(InvalidOperationException){refused=true;}
  Need(refused&&ReferenceEquals(ActualLifecycle.activeWorldReference,original),"refused admission changed remembered original");
  RebirthNpcSaveQuarantineBundle.Allow=true;RebirthNpcSaveQuarantineBundle.OnRetire=()=>GameManager.Instance.World=new World();refused=false;
  try{ActualLifecycle.QualifyWholeBundleAdmission();}catch(InvalidOperationException){refused=true;}
  Need(refused&&ReferenceEquals(RebirthNpcSaveQuarantineBundle.Incoming,incoming),"world change during observer work borrowed new world authority");
  return "PASS actual admission qualifier: no-world process bootstrap has no world effects; exact remembered outgoing scope; refusal and intervening World change retain originals. Explicit admission/World adapters; native execution NOT RUN.";
 }
}
"@
Add-Type -TypeDefinition $code
[AdmissionBootstrapFixture]::Run()
