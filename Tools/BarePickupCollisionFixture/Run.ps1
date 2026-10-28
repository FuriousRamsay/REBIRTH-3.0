param([switch]$NegativeControl)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$source=Get-Content "$root/Scripts/Vehicles/Camper/RebirthCamperOwnCollision.cs" -Raw
$baseline=Get-Content "$root/_Documentation/Camper/BareCollision_20261028/RebirthCamperOwnCollision.cs.baseline" -Raw
$inventory=Get-Content "$root/_Documentation/Camper/BareCollision_20261028/installed-bundle-colliders.json" -Raw|ConvertFrom-Json
$rails=@($inventory.colliders|Where-Object path -like '*/Physics/Turret-facing guardrail collision/*')
if($rails.Count-ne34 -or @($rails|Where-Object {$_.type-ne'CapsuleCollider'-or!$_.enabled-or$_.trigger-or!$_.active}).Count){throw 'Installed guardrail inventory changed'}
$method=[regex]::Match($source,'    internal static void ApplyBodyGuardrails\(bool hasBody,GameObject guardrails\)\s*\{[^}]*\}').Value
if(!$method){throw 'Actual policy missing'}
$removed=$source.Replace("        internal GameObject BodyGuardrails;`r`n",'').Replace("        internal GameObject BodyGuardrails;`n",'')
$removed=$removed.Replace("        ApplyBodyGuardrails(hasBody,state.BodyGuardrails);`r`n",'').Replace("        ApplyBodyGuardrails(hasBody,state.BodyGuardrails);`n",'')
$binding=[regex]::Match($removed,'        // These native compound solids[^\r\n]*\r?\n(?:[^\r\n]*\r?\n){4}').Value
if(!$binding.Contains('state.BodyGuardrails=')){throw 'Exact binding block missing'}
$removed=$removed.Replace($binding,'').Replace($method+"`r`n",'').Replace($method+"`n",'')
if($removed.Replace("`r`n","`n")-cne$baseline.Replace("`r`n","`n")){throw 'Unrelated collision code changed'}
if($source.IndexOf('ApplyBodyGuardrails(hasBody,state.BodyGuardrails)')-lt$source.IndexOf('RebirthCamperBodyAttachment.HasBody(vehicle)')){throw 'Must observe live addon state first'}
$fixture=@'
using System;
public class GameObject {
 public bool activeSelf=true; public GameObject parent; public int writes;
 public bool activeInHierarchy { get { return activeSelf&&(parent==null||parent.activeInHierarchy); } }
 public void SetActive(bool value){activeSelf=value;writes++;}
}
public static class Checks {
/*ACTUAL*/
 static int n;
 static void Assert(bool value,string message){n++;if(!value)throw new Exception(message);}
 public static string Run(bool negative){
  var physics=new GameObject();var rails=new GameObject{parent=physics};
  var solids=new GameObject[34];for(int i=0;i<34;i++)solids[i]=new GameObject{parent=rails};
  var cab=new GameObject{parent=physics};var wheel=new GameObject{parent=physics};var query=new GameObject();
  bool[] states={false,false,true,true,false,true,false};
  foreach(bool body in states){
   if(!negative)ApplyBodyGuardrails(body,rails);
   foreach(var solid in solids){Assert(solid.activeInHierarchy==body,"invisible addon rail remains solid without body");Assert(solid.activeSelf,"authored child state changed");}
   Assert(cab.activeInHierarchy&&wheel.activeInHierarchy&&query.activeInHierarchy,"native cab/wheel/query changed");
  }
  int writes=rails.writes;ApplyBodyGuardrails(false,rails);Assert(rails.writes==writes,"unchanged state should not reactivate");
  ApplyBodyGuardrails(false,null);ApplyBodyGuardrails(true,null);
  solids[0].activeSelf=false;ApplyBodyGuardrails(true,rails);Assert(!solids[0].activeInHierarchy,"restore must preserve authored disabled child");
  physics.activeSelf=false;ApplyBodyGuardrails(true,rails);Assert(!rails.activeInHierarchy,"must not activate native Physics parent");
  return "PASS "+n+" actual-policy checks; installed 34-rail inventory; body removal/reinstall/repeated observation; unrelated solids/query and child flags preserved";
 }
}
'@
Add-Type -TypeDefinition $fixture.Replace('/*ACTUAL*/',$method)
[Checks]::Run([bool]$NegativeControl)
