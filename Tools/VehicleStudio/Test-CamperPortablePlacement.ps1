$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperPortablePlacementGuard.cs')).Replace('using HarmonyLib;','')
$doubles=@'
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace HarmonyLib {public sealed class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}}}
public sealed class ItemValue {public Dictionary<string,string> Metadata;}
public sealed class ItemInventoryData {public ItemValue itemValue;public object holdingEntity;}
public sealed class ItemActionData {public ItemInventoryData invData;}
public sealed class EntityPlayerLocal {}
public sealed class ItemActionPlaceAsBlock {public void ExecuteAction(ItemActionData data,bool released){}}
public static class RebirthCamperPortableStation {public const string MetadataKey="RebirthCamperStation";}
public static class Localization {public static string Get(string key){return key;}}
public static class GameManager {public static int Prompts;public static void ShowTooltip(EntityPlayerLocal p,string text){if(text!="xuiRebirthCamperPortableInstall")throw new Exception("Wrong prompt");Prompts++;}}
public static class PortablePlacementChecks {
 static int Placed,Debited;
 static bool Execute(ItemActionData data,bool release){
  bool run=(bool)typeof(RebirthCamperPortablePlacementGuard).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{data,release});
  if(run&&release){Placed++;Debited++;}return run;
 }
 public static void Run(){
  var item=new ItemValue{Metadata=new Dictionary<string,string>()};var data=new ItemActionData{invData=new ItemInventoryData{itemValue=item,holdingEntity=new EntityPlayerLocal()}};
  if(!Execute(data,true)||Placed!=1||Debited!=1)throw new Exception("Ordinary station placement changed.");
  item.Metadata[RebirthCamperPortableStation.MetadataKey]="encoded native contents";
  if(Execute(data,false)||Execute(data,true)||Placed!=1||Debited!=1||GameManager.Prompts!=1)throw new Exception("Portable state placed/debited or press spammed prompt.");
  item.Metadata[RebirthCamperPortableStation.MetadataKey]="";if(Execute(data,true))throw new Exception("Empty reserved payload was discarded.");
  item.Metadata[RebirthCamperPortableStation.MetadataKey]=null;if(Execute(data,true))throw new Exception("Malformed reserved payload was discarded.");
  item.Metadata.Clear();item.Metadata["unrelated"]="value";if(!Execute(data,true)||Placed!=2||Debited!=2)throw new Exception("Unrelated metadata changed native behavior.");
 }
}
'@
Add-Type -TypeDefinition ($doubles+"`n"+$source)
[PortablePlacementChecks]::Run()
Write-Output 'PASS: actual preplacement prefix prevents portable/malformed state conversion and debit; release-only cue; ordinary station/unrelated metadata retain native placement. Native action is an explicit adapter; real pre-debit call order verified separately from installed IL.'
