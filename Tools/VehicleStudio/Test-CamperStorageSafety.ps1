$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Join-Path $PSScriptRoot '../..'
$commit=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperTransferCommit.cs'))
# Exercise the production policy bodies, with explicit inventory/save adapters.
$policy=$commit.Substring($commit.IndexOf('    private static bool Equal(')).Trim()
$policy=$policy.Substring(0,$policy.LastIndexOf('}')).Replace('internal static void AssertCargoPolicy','public static void AssertCargoPolicy')
$prefix=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperModulePreferredTransfer.cs'))
$prefix=$prefix.Replace('using HarmonyLib;','')
$doubles=@'
using System;
using System.Reflection;
using HarmonyLib;
namespace HarmonyLib { public sealed class HarmonyPatch:Attribute { public HarmonyPatch(Type type,string method){} } }
public sealed class ItemStack { public int Type, Count; public ItemStack(int type,int count){Type=type;Count=count;} }
public sealed class PreferenceTracker { public bool AnyPreferences; }
public sealed class ItemStackGrid { public int Length; public bool[] SlotLocks; public PreferenceTracker Preferences; }
public static class RebirthCamperInventorySnapshot {
 public static byte[] Capture(ItemStack[] items) {
  var result=new byte[items.Length*8]; for(int i=0;i<items.Length;i++) {
   Array.Copy(BitConverter.GetBytes(items[i].Type),0,result,i*8,4);
   Array.Copy(BitConverter.GetBytes(items[i].Count),0,result,i*8+4,4);
  } return result;
 }
}
public class XUiC_BasePartStack { public void HandleMoveToPreferredLocation(){} }
public sealed class XUiC_RebirthCamperModuleSlot:XUiC_BasePartStack {
 public int Requests; internal void TransferPreferred(){Requests++;}
}
'@
$checks=@'
public static class CamperStorageSafetyChecks {
 static void Reject(Action action) {try{action();}catch(InvalidOperationException){return;}throw new Exception("Protected cargo mutation was admitted.");}
 public static void Run() {
  var old=new[]{new ItemStack(1,4),new ItemStack(0,0),new ItemStack(2,123456)};
  var shrunk=new[]{new ItemStack(1,4),new ItemStack(2,123456)};
  var grid=new ItemStackGrid{Length=3,SlotLocks=new[]{true,false,false}};
  CargoPolicy.AssertCargoPolicy(grid,old,shrunk);
  grid.SlotLocks=new[]{false,false,true}; Reject(()=>CargoPolicy.AssertCargoPolicy(grid,old,shrunk));
  grid.SlotLocks=new[]{false,true,false}; Reject(()=>CargoPolicy.AssertCargoPolicy(grid,old,shrunk));
  grid.SlotLocks=new[]{false,false,false}; grid.Preferences=new PreferenceTracker{AnyPreferences=true};
  Reject(()=>CargoPolicy.AssertCargoPolicy(grid,old,shrunk));
  CargoPolicy.AssertCargoPolicy(grid,old,old); // station-only edits preserve policy
  grid.Preferences=null; grid.SlotLocks=new[]{false,false}; Reject(()=>CargoPolicy.AssertCargoPolicy(grid,old,shrunk));
  grid.SlotLocks=new[]{true,false,false}; var changed=(ItemStack[])old.Clone(); changed[0]=new ItemStack(1,5);
  Reject(()=>CargoPolicy.AssertCargoPolicy(grid,old,changed));
  var prefix=typeof(RebirthCamperModulePreferredTransfer).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
  if(!(bool)prefix.Invoke(null,new object[]{new XUiC_BasePartStack()}))throw new Exception("Ordinary native parts were intercepted.");
  var slot=new XUiC_RebirthCamperModuleSlot();
  if((bool)prefix.Invoke(null,new object[]{slot}) || slot.Requests!=1)throw new Exception("Module shift click reached direct native backpack transfer.");
 }
}
'@
Add-Type -TypeDefinition ($doubles+"`npublic static class CargoPolicy {`n"+$policy+"`n}`n"+$prefix+"`n"+$checks)
[CamperStorageSafetyChecks]::Run()
$xml=[xml][IO.File]::ReadAllText((Join-Path $camperRoot 'Config/_Vehicles/camper_modules_windows.xml'))
$positions=@($xml.SelectNodes("//rect[starts-with(@name,'camperModule')]"))
if($positions.Count -ne 10){throw 'Management must contain exactly ten module positions.'}
foreach($slot in $positions){if(-not $slot.SelectSingleNode("sprite[@name='background']")){throw 'Native part-stack background binding is missing.'}}
$rows=@($positions|ForEach-Object {$_.pos.Split(',')[1]}|Select-Object -Unique)
if($rows.Count -ne 2){throw 'Management requires two rows.'}
foreach($row in $rows){if(@($positions|Where-Object {$_.pos.Split(',')[1] -eq $row}).Count -ne 5){throw 'Each management row requires five positions.'}}
Write-Output 'PASS: native shift bypass prevention; protected-cell, truncated-lock, preference and dimension refusal; unchanged-policy admission; ten native backgrounds in a 5x2 layout. Inventory/native-save adapters are doubles; no gameplay acceptance.'
