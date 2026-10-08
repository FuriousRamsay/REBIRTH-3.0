$ErrorActionPreference='Stop'
$plan=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs')
$snapshot=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearInventorySnapshot.cs')
$snapshot=$snapshot.Replace('using System;','').Replace('using System.IO;','')
$planner=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearInventoryPlanner.cs')
$planner=$planner.Replace('using System;','').Replace('using Stack = RebirthGearInventoryPlan.Stack;','')
Add-Type -TypeDefinition ('using System.IO; using Stack = RebirthGearInventoryPlan.Stack;' + $plan + $snapshot + $planner + @"
public class ItemClass {}
public class ItemValue {
 public int Type=1,Quality=2,Mod=3; public bool Fail; public ItemClass ItemClass=new ItemClass();
 public bool IsEmpty(){return Type==0;}
 public static void Write(ItemValue v,BinaryWriter w){if(v.Fail)throw new IOException();w.Write(v.Type);w.Write(v.Quality);w.Write(v.Mod);}
}
public class ItemStack {public int count;public ItemValue itemValue;}
public static class SnapshotChecks {
 static ItemStack Item(int n){return new ItemStack{count=n,itemValue=new ItemValue()};}
 static void Check(bool value,string name){if(!value)throw new System.Exception(name);}
 public static void Run(){
  var bag=new ItemStack[52];var belt=new ItemStack[20];bag[0]=Item(1);belt[19]=Item(4);
  RebirthGearInventorySnapshot s;
  Check(RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"capture failed");
  Check(s.Belt.Length==20 && s.Belt[19].Count==4,"retired storage omitted");
  Check(s.IsUsableSource(false,3) && !s.IsUsableSource(false,4) && !s.IsUsableSource(false,19) && !s.IsUsableSource(true,-1),"locked source allowed");
  string original=s.Bag[0].ItemData;bag[0].itemValue.Quality=9;bag[0].itemValue.Mod=8;bag[0].count=2;
  Check(s.Bag[0].ItemData==original && s.Bag[0].Count==1,"snapshot aliased native items");
  RebirthGearInventorySnapshot next;Check(RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out next) && next.Bag[0].ItemData!=original,"quality/mod data lost");
  bag[1]=new ItemStack{count=2};Check(!RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s) && s==null,"invalid occupied cell discarded");
  bag[1]=Item(-1);Check(!RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"negative stack accepted");
  bag[1]=Item(1);bag[1].itemValue.Fail=true;Check(!RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"failed serialization accepted");
  bag[1]=null;Check(!RebirthGearInventorySnapshot.TryCapture(bag,belt,20,out s),"retired capacity owned");
  Check(!RebirthGearInventorySnapshot.TryCapture(bag,new ItemStack[11],12,out s),"owned exceeds backing");
  Check(RebirthGearInventorySnapshot.TryCapture(bag,new ItemStack[11],4,out s) && s.Belt.Length==11,"final real slot omitted");
  belt[19].itemValue.Fail=true;Check(!RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"final real slot serialization failure ignored");belt[19].itemValue.Fail=false;
  Check(RebirthGearInventorySnapshot.TryCapture(bag,new ItemStack[4],4,out s) && s.Belt.Length==4,"fully owned small grid refused");
  Check(!RebirthGearInventorySnapshot.TryCapture(bag,new ItemStack[21],4,out s),"oversized grid accepted");
  bag=new ItemStack[65];belt=new ItemStack[20];bag[64]=Item(2);belt[19]=Item(3);
  Check(RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"planner snapshot");
  var old=new Stack{ItemData=System.Convert.ToBase64String(new byte[]{9}),Count=1};RebirthGearInventoryPlan plan;
  Check(RebirthGearInventoryPlanner.TryPlan(s,old,true,64,52,4,out plan),"shrinking source plan");
  Check(plan.Recovery.Count==2 && plan.Recovery[0].Item.Count==1 && plan.Recovery[1].Item.Count==3,"tail remainder/recovery lost");
  Check(plan.Recovery[0].Origin==RebirthGearInventoryPlan.RecoveryOrigin.Backpack && plan.Recovery[0].SourceIndex==64 && plan.Recovery[1].Origin==RebirthGearInventoryPlan.RecoveryOrigin.Belt && plan.Recovery[1].SourceIndex==19,"recovery provenance lost");
  Check(plan.GearAfter.ItemData==s.Bag[64].ItemData && plan.GearAfter.Count==1,"wrong gear equipped");
  Check(s.Bag[64].Count==2 && s.Belt[19].Count==3 && bag[64].count==2,"planner mutated input");
  Check(plan.Changes[0].Index==0 && plan.Changes[0].After.ItemData==old.ItemData,"displaced item not returned");
  Check(!RebirthGearInventoryPlanner.TryPlan(s,old,false,19,52,4,out plan),"locked source accepted by planner");
  bag=new ItemStack[52];for(int i=0;i<bag.Length;i++)bag[i]=Item(1);
  Check(RebirthGearInventorySnapshot.TryCapture(bag,belt,4,out s),"full bag capture");
  Check(RebirthGearInventoryPlanner.TryPlan(s,old,true,-1,52,4,out plan) && plan.Recovery[plan.Recovery.Count-1].Item.ItemData==old.ItemData,"full bag unequip lost gear");
  Check(RebirthGearInventoryPlanner.TryPlan(s,old,true,-1,65,4,out plan) && plan.Changes[0].Index==52 && plan.Changes[0].After.ItemData==old.ItemData,"expansion return failed");
  Check(!RebirthGearInventoryPlanner.TryPlan(s,new Stack(),true,-1,52,4,out plan),"empty unequip accepted");

 }
}
"@)
[SnapshotChecks]::Run()
Write-Output 'PASS: production snapshot detachment, exact quality/mod bytes, owned versus retired slots, final real slot preservation, small native grids, fail-closed serialization, shrinking-source/full-bag/expansion plans and no input mutation (native writer stubbed).'
