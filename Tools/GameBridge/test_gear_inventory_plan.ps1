$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs')
$sequence=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearOwnerApplySequence.cs')
$sequence=$sequence.Replace('using System;','')
Add-Type -TypeDefinition ($source + $sequence + @"
public static class GearPlanChecks {
 static RebirthGearInventoryPlan.Stack S(string data,int n){return new RebirthGearInventoryPlan.Stack{ItemData=data,Count=n};}
 static RebirthGearInventoryPlan P(){return new RebirthGearInventoryPlan{BagSlotsBefore=52,BagSlotsAfter=52,BeltSlotsBefore=4,BeltSlotsAfter=4};}
 static void Check(bool v,string name){if(!v)throw new System.Exception(name);}
 static RebirthGearInventoryPlan.Stack[] Empty(int count){var a=new RebirthGearInventoryPlan.Stack[count];for(int i=0;i<count;i++)a[i]=S("",0);return a;}
 static RebirthGearInventoryPlan.RecoveryEntry R(string data,int n,RebirthGearInventoryPlan.RecoveryOrigin origin,int index){return new RebirthGearInventoryPlan.RecoveryEntry{Item=S(data,n),Origin=origin,SourceIndex=index};}
 public static void Run(){
  var p=P(); p.GearAfter=S("AQ==",1); p.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=0,Before=S("AQ==",1)});
  Check(p.IsConserved(),"valid equip");
  p.GearAfter=S("Ag==",1);Check(!p.IsConserved(),"altered item accepted");
  p.GearAfter=S("AQ==",2);Check(!p.IsConserved(),"multiple gear accepted");
  p.GearAfter=S("AQ==",1);p.Changes.Add(p.Changes[0]);Check(!p.IsConserved(),"duplicate position accepted");
  p=P();p.BeltSlotsBefore=6;p.Changes.Add(new RebirthGearInventoryPlan.Change{Index=5,Before=S("AQ==",4)});p.Recovery.Add(R("AQ==",4,RebirthGearInventoryPlan.RecoveryOrigin.Belt,5));Check(p.IsConserved(),"valid shrink recovery");
  p.Recovery[0].Item.Count=3;Check(!p.IsConserved(),"lost recovery count accepted");
  p.Recovery[0].Item.Count=4;p.Changes[0].After=S("AQ==",1);Check(!p.IsConserved(),"occupied removed slot accepted");
  p=P();p.GearBefore=S("AQ==",1);p.Recovery.Add(R("AQ==",1,RebirthGearInventoryPlan.RecoveryOrigin.DisplacedGear,-1));Check(p.IsConserved(),"unequip recovery");
  p.GearBefore.ItemData="A Q==";Check(!p.IsConserved(),"noncanonical bytes accepted");
  p.GearBefore.ItemData="invalid";Check(!p.IsConserved(),"invalid encoding accepted");
  p=P();p.GearBefore=S("AQ==",0);Check(!p.IsConserved(),"empty stack with data accepted");
  p=P();p.GearBefore.Count=-1;Check(!p.IsConserved(),"negative gear accepted");
  p=P();p.BagSlotsAfter=170;Check(!p.IsConserved(),"oversize capacity accepted");
  p=P();p.Changes.Add(new RebirthGearInventoryPlan.Change{Index=4});Check(!p.IsConserved(),"dummy slot accepted");
  p=P();p.BeltSlotsBefore=6;var bag=Empty(52);var belt=Empty(6);belt[5]=S("AQ==",4);
  Check(!p.MatchesBefore(bag,belt,S("",0),52,6),"unplanned tail loss accepted");
  p.Changes.Add(new RebirthGearInventoryPlan.Change{Index=5,Before=S("AQ==",4)});p.Recovery.Add(R("AQ==",4,RebirthGearInventoryPlan.RecoveryOrigin.Belt,5));
  Check(p.MatchesBefore(bag,belt,S("",0),52,6),"complete shrink rejected");
  belt[5].Count=3;Check(!p.MatchesBefore(bag,belt,S("",0),52,6),"stale source count accepted");
  belt[5]=S("Ag==",4);Check(!p.MatchesBefore(bag,belt,S("",0),52,6),"stale item bytes accepted");
  belt[5]=S("AQ==",4);Check(!p.MatchesBefore(bag,belt,S("Ag==",1),52,6),"stale equipped gear accepted");
  Check(!p.MatchesBefore(bag,Empty(7),S("",0),52,6),"dummy/extra physical tail accepted");
  Check(!p.MatchesBefore(bag,belt,S("",0),52,4),"stale capacity accepted");
  p=P();p.BeltSlotsAfter=6;p.GearBefore=S("AQ==",1);p.Changes.Add(new RebirthGearInventoryPlan.Change{Index=5,After=S("AQ==",1)});
  Check(p.MatchesBefore(bag,Empty(4),S("AQ==",1),52,4),"expansion empty preimage rejected");
  p=P();string big=System.Convert.ToBase64String(new byte[196608]);
  for(int n=0;n<9;n++)p.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=n,Before=S(big,1),After=S(big,1)});
  Check(!p.IsConserved(),"aggregate payload bound bypassed");
  p=P();p.BagSlotsBefore=65;p.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=52,Before=S("AQ==",int.MaxValue)});p.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=53,Before=S("AQ==",int.MaxValue)});p.Recovery.Add(R("AQ==",int.MaxValue,RebirthGearInventoryPlan.RecoveryOrigin.Backpack,52));p.Recovery.Add(R("AQ==",int.MaxValue,RebirthGearInventoryPlan.RecoveryOrigin.Backpack,53));Check(p.IsConserved(),"count overflow");
  p.Recovery[1].SourceIndex=52;Check(!p.IsConserved(),"duplicate recovery source accepted");
  p.Recovery[1].SourceIndex=53;p.Recovery[1].Origin=(RebirthGearInventoryPlan.RecoveryOrigin)99;Check(!p.IsConserved(),"unknown recovery origin accepted");
  p.Recovery[1].Origin=RebirthGearInventoryPlan.RecoveryOrigin.Backpack;
  bag=Empty(65);belt=Empty(4);bag[52]=S("AQ==",int.MaxValue);bag[53]=S("AQ==",int.MaxValue);
  Check(p.InspectApplication(bag,belt,false)==RebirthGearInventoryPlan.ApplicationState.Before,"untouched plan misclassified");
  bag[52]=S("",0);
  Check(p.InspectApplication(bag,belt,false)==RebirthGearInventoryPlan.ApplicationState.Conflict,"partial writes accepted without applying receipt");
  Check(p.InspectApplication(bag,belt,true)==RebirthGearInventoryPlan.ApplicationState.Partial,"partial progress not recognized");
  bag[53]=S("",0);Check(p.InspectApplication(bag,belt,true)==RebirthGearInventoryPlan.ApplicationState.After,"completed changes not recognized");
  bag[53]=S("Ag==",1);Check(p.InspectApplication(bag,belt,true)==RebirthGearInventoryPlan.ApplicationState.Conflict,"foreign item overwritten");
  bag[53]=S("",0);bag[54]=S("AQ==",1);Check(p.InspectApplication(bag,belt,true)==RebirthGearInventoryPlan.ApplicationState.Conflict,"unlisted shrinking tail ignored");
  Check(p.InspectApplication(Empty(52),belt,true)==RebirthGearInventoryPlan.ApplicationState.Conflict,"premature backing shrink accepted");
  bag=Empty(65);bag[52]=S("AQ==",int.MaxValue);bag[53]=S("AQ==",int.MaxValue);
  bool intent=false,receipt=false;int writes=0;
  Action<RebirthGearInventoryPlan.Change> writer=c=>{Check(intent,"write before durable intent");bag[c.Index]=S(c.After.ItemData,c.After.Count);writes++;if(writes==1)throw new Exception("setter failed after applying");};
  var outcome=RebirthGearOwnerApplySequence.Execute(p,false,()=>bag,()=>belt,()=>{intent=true;return true;},()=>{},writer,()=>{receipt=true;return true;});
  Check(outcome==RebirthGearOwnerApplySequence.Result.Pending && intent && !receipt && writes==1,"partial setter failure acknowledged or refunded");
  outcome=RebirthGearOwnerApplySequence.Execute(p,true,()=>bag,()=>belt,()=>true,()=>{},writer,()=>{receipt=true;return true;});
  Check(outcome==RebirthGearOwnerApplySequence.Result.Applied && receipt && writes==2,"resume repeated completed write or lost remaining write");
  outcome=RebirthGearOwnerApplySequence.Execute(p,true,()=>bag,()=>belt,()=>true,()=>{},writer,()=>false);
  Check(outcome==RebirthGearOwnerApplySequence.Result.Pending && writes==2,"failed final receipt repeated writes");
  bag[53]=S("Ag==",1);outcome=RebirthGearOwnerApplySequence.Execute(p,true,()=>bag,()=>belt,()=>true,()=>{},writer,()=>true);
  Check(outcome==RebirthGearOwnerApplySequence.Result.NeedsReconciliation && writes==2,"foreign item on resume treated as cancellable");
  outcome=RebirthGearOwnerApplySequence.Execute(p,false,()=>bag,()=>belt,()=>{throw new Exception("conflict persisted intent");},()=>{},writer,()=>true);
  Check(outcome==RebirthGearOwnerApplySequence.Result.Conflict && writes==2,"untouched conflict not distinguished");
  Check(RebirthGearOwnerApplySequence.Execute(null,true,()=>bag,()=>belt,()=>true,()=>{},writer,()=>true)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation,"invalid pending plan treated as cancellable");
  bag[52]=S("AQ==",int.MaxValue);bag[53]=S("AQ==",int.MaxValue);
  outcome=RebirthGearOwnerApplySequence.Execute(p,false,()=>bag,()=>belt,()=>false,()=>{throw new Exception("grew backing before intent saved");},writer,()=>true);
  Check(outcome==RebirthGearOwnerApplySequence.Result.Pending && writes==2 && bag[52].Count==int.MaxValue,"failed intent changed inventory");
  outcome=RebirthGearOwnerApplySequence.Execute(p,false,()=>bag,()=>belt,()=>true,()=>{bag[53]=S("Ag==",1);},writer,()=>{throw new Exception("conflict acknowledged");});
  Check(outcome==RebirthGearOwnerApplySequence.Result.NeedsReconciliation && writes==2,"conflict after new durable intent treated as cancellable");
  bag=Empty(65);bag[52]=S("AQ==",int.MaxValue);bag[53]=S("AQ==",int.MaxValue);
  int callbackWrites=0;
  outcome=RebirthGearOwnerApplySequence.Execute(p,false,()=>bag,()=>belt,()=>true,()=>{},c=>{bag[c.Index]=S(c.After.ItemData,c.After.Count);callbackWrites++;bag[53]=S("Ag==",1);},()=>{throw new Exception("foreign callback item acknowledged");});
  Check(outcome==RebirthGearOwnerApplySequence.Result.NeedsReconciliation && callbackWrites==1 && bag[53].ItemData=="Ag==","setter callback foreign item overwritten by stale snapshot");

 }
}
"@)
[GearPlanChecks]::Run()
Write-Output 'PASS: gear conservation, exact bytes, bounds, partial-write retry, receipt failures, and pre/post-intent conflict classification.'
