#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationInstallations.cs'))
function Extract-Method([string]$signature){$start=$source.IndexOf($signature);if($start -lt 0){throw $signature};$open=$source.IndexOf('{',$start);$depth=1;$end=$open+1;while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++};$source.Substring($start,$end-$start)}
$methods=(Extract-Method 'internal void RetainUnchangedStates')+"`n"+(Extract-Method 'private static bool Equal')
$fixture=@'
using System;
public static class ThreadManager {public static bool Main=true;}
public class ItemStack {public byte Value;}
public class RebirthCamperStationState {public byte Stock;}
public static class RebirthCamperInventorySnapshot {public static byte[] Capture(ItemStack[] s)=>new[]{s[0].Value};}
public static class RebirthCamperStationSnapshot {public static byte[] Capture(Guid id,RebirthCamperStationState s)=>new[]{s.Stock};}
public class RebirthCamperStationInstallations {
 public class Installation {public Guid Id;public ItemStack InstalledItem=new ItemStack();public RebirthCamperStationState State=new RebirthCamperStationState();}
 public Guid VehicleIdentity;private Installation[] slots=new Installation[10];public long Revision;
 public Installation Find(Guid id){foreach(var s in slots)if(s!=null&&s.Id==id)return s;return null;}
 public Installation Get(int i)=>slots[i];public void Put(int i,Installation s){slots[i]=s;}
 private static void RequireGameThread(){if(!ThreadManager.Main)throw new InvalidOperationException();}
'@
$fixture+=$methods+@'
}
public static class RetainFixture {
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 static void Deny(Action a,string n){try{a();}catch(InvalidOperationException){return;}throw new Exception(n);}
 static RebirthCamperStationInstallations.Installation New(Guid id,byte stock=7)=>new RebirthCamperStationInstallations.Installation{Id=id,InstalledItem=new ItemStack{Value=4},State=new RebirthCamperStationState{Stock=stock}};
 public static void Run(){var vehicle=Guid.NewGuid();var id=Guid.NewGuid();var original=New(id);var current=new RebirthCamperStationInstallations{VehicleIdentity=vehicle};current.Put(0,original);var candidate=new RebirthCamperStationInstallations{VehicleIdentity=vehicle,Revision=8};var decoded=New(id);candidate.Put(9,decoded);var added=New(Guid.NewGuid());candidate.Put(1,added);candidate.RetainUnchangedStates(current,false);Check(ReferenceEquals(candidate.Get(9),decoded),"preflight never aliases before callback");candidate.RetainUnchangedStates(current);Check(ReferenceEquals(candidate.Get(9),original)&&ReferenceEquals(candidate.Get(1),added)&&candidate.Revision==8,"move retains original state and candidate revision/new install");
 var id2=Guid.NewGuid();var old2=New(id2);current.Put(1,old2);candidate=new RebirthCamperStationInstallations{VehicleIdentity=vehicle};var first=New(id);candidate.Put(0,first);candidate.Put(1,New(id2,8));Deny(()=>candidate.RetainUnchangedStates(current),"changed second snapshot denied");Check(ReferenceEquals(candidate.Get(0),first),"all matches validated before any alias");
 candidate.Put(1,New(id2));candidate.RetainUnchangedStates(current,false);old2.State.Stock=9;Deny(()=>candidate.RetainUnchangedStates(current),"listener changes after preflight denied");Check(ReferenceEquals(candidate.Get(0),first),"late callback refusal retains detached candidate");old2.State.Stock=7;candidate.Get(1).InstalledItem.Value=5;Deny(()=>candidate.RetainUnchangedStates(current),"different installed native item denied");
 var other=new RebirthCamperStationInstallations{VehicleIdentity=Guid.NewGuid()};Deny(()=>candidate.RetainUnchangedStates(other),"wrong vehicle denied");ThreadManager.Main=false;Deny(()=>candidate.RetainUnchangedStates(current),"offthread denied");ThreadManager.Main=true;
 var removed=new RebirthCamperStationInstallations{VehicleIdentity=vehicle};removed.Put(3,New(id2));removed.RetainUnchangedStates(current);Check(removed.Find(id)==null&&ReferenceEquals(removed.Get(3),old2),"removal preserves surviving original only");Console.WriteLine("PASS extracted actual station retention helper: original bindings survive module adoption, move/new/remove semantics, exact item/state validation, validate-only preflight and late listener refusal, all-or-none candidate alias, vehicle/thread refusal. Native snapshot adapters are explicit doubles; transfer/native durability not claimed.");
 }
}
'@
Add-Type -TypeDefinition $fixture
[RetainFixture]::Run()
