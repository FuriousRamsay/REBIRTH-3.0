#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Get-HoldMethod([string]$file,[string]$signature){
 $code=[IO.File]::ReadAllText((Join-Path $camperRoot $file))
 $match=[regex]::Match($code,'(?s)    '+$signature+'.*?\r?\n    }')
 if(!$match.Success){throw 'Current hold method not found.'};return $match.Value
}
$camperCode=@'
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// Native item/station records are explicit doubles; hold/filter methods are actual.
class ItemValue{public Dictionary<string,object> Metadata;}
class ItemStack{public ItemValue itemValue;}
class CraftCompleteData{public ItemStack CraftedItemStack;}
class RebirthCamperStationState{public List<CraftCompleteData> CraftCompleteList;}
static class RebirthStationCompletionReceipt{
 internal const string Prefix="rebirth.station.receipt.";
'@
$camperCode+=Get-HoldMethod 'Scripts/Crafting/UI/RebirthStationCompletionReceipt.cs' 'internal static bool HasReservedMarker\('
$camperCode+=Get-HoldMethod 'Scripts/Crafting/UI/RebirthStationCompletionReceipt.cs' 'internal static bool CanUseNativeRewards\('
$camperCode+=@'
}
static class RebirthCamperStationCompletionCallsite{
 class Retained{internal bool Uncertain;internal List<object> Events=new List<object>();}
 static ConditionalWeakTable<RebirthCamperStationState,Retained> retained=new ConditionalWeakTable<RebirthCamperStationState,Retained>();
 static ConditionalWeakTable<RebirthCamperStationState,object> recoveryHolds=new ConditionalWeakTable<RebirthCamperStationState,object>();
 internal static void HoldRecovery(RebirthCamperStationState station){recoveryHolds.Add(station,new object());}
 internal static void Remember(RebirthCamperStationState station,bool uncertain){var r=retained.GetValue(station,_=>new Retained());r.Uncertain=uncertain;if(!uncertain)r.Events.Add(new object());}
'@
$camperCode+=Get-HoldMethod 'Scripts/Vehicles/Camper/RebirthCamperStationCompletionCallsite.cs' 'internal static bool IsHeld\('
$camperCode+=@'
}
public static class CompletionHoldFixture{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){
 var cold=new RebirthCamperStationState();Check(!RebirthCamperStationCompletionCallsite.IsHeld(cold),"empty native station unchanged");
 cold.CraftCompleteList=new List<CraftCompleteData>{new CraftCompleteData{CraftedItemStack=new ItemStack{itemValue=new ItemValue{Metadata=new Dictionary<string,object>{{"ordinary",1}}}}}};
 Check(!RebirthCamperStationCompletionCallsite.IsHeld(cold),"ordinary native row unchanged");
 cold.CraftCompleteList[0].CraftedItemStack.itemValue.Metadata.Add("rebirth.station.receipt.job","saved-job");
 Check(RebirthCamperStationCompletionCallsite.IsHeld(cold),"saved marker holds without original event");
 cold.CraftCompleteList[0].CraftedItemStack.itemValue.Metadata.Clear();cold.CraftCompleteList[0].CraftedItemStack.itemValue.Metadata.Add("rebirth.station.receipt.malformed",1);
 Check(RebirthCamperStationCompletionCallsite.IsHeld(cold),"malformed reserved marker holds");
 var original=new RebirthCamperStationState();RebirthCamperStationCompletionCallsite.Remember(original,false);Check(RebirthCamperStationCompletionCallsite.IsHeld(original),"original event holds");
 var recovering=new RebirthCamperStationState();RebirthCamperStationCompletionCallsite.HoldRecovery(recovering);Check(RebirthCamperStationCompletionCallsite.IsHeld(recovering),"recovery holds after original row removed");
 var uncertain=new RebirthCamperStationState();RebirthCamperStationCompletionCallsite.Remember(uncertain,true);Check(RebirthCamperStationCompletionCallsite.IsHeld(uncertain),"uncertain native insertion holds");
 Console.WriteLine("PASS actual completion hold/reserved filter: cold saved marker, malformed custody, original event, uncertainty and ordinary rows. Native station persistence/input release not qualified.");}}
'@
Add-Type -TypeDefinition $camperCode
[CompletionHoldFixture]::Run()
