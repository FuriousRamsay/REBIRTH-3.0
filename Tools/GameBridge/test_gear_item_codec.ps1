$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearItemCodec.cs')
$shared=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Inventory/RebirthNativeItemCodec.cs')
$shared=$shared.Replace('using System;','').Replace('using System.IO;','')
$plan=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs')
$plan=$plan.Replace('using System;','').Replace('using System.Collections.Generic;','')
$native=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthGearNativePlan.cs')
$native=$native.Replace('using System.Collections.Generic;','')
Add-Type -TypeDefinition ('using System.Collections.Generic;' + $source + $shared + $plan + $native + @"
public class ItemStack{public int count;public ItemValue itemValue;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack Empty=new ItemStack(null,0);public ItemStack Clone(){return new ItemStack(itemValue,count);}}
public class ItemClass{public string Name;public string GetItemName(){return Name;}}
public class ItemValue{
 public ItemClass ItemClass;public bool Empty;
 public static void Write(ItemValue value,BinaryWriter writer){writer.Write((byte)(value==null?0:value.ItemClass.Name=="pack"?1:2));}
 public bool IsEmpty(){return Empty;}
 public static ItemValue ReadOrNull(BinaryReader r){int tag=r.ReadByte();if(tag==0)return null;return new ItemValue{Empty=tag==9,ItemClass=tag==8?null:new ItemClass{Name=tag==1?"pack":tag==2?"belt":"unknown"}};}
}
public class RebirthTraitSupportProfileDefinition{public string Kind, GearSlotId;}
public static class RebirthSurvivorDefinitionRegistry{
 public static bool TryGetSupportByGearItem(string id,out RebirthTraitSupportProfileDefinition p){p=id=="unknown"?null:new RebirthTraitSupportProfileDefinition{Kind="survivor_gear",GearSlotId=id=="pack"?"backpack":"belt"};return p!=null;}
}
public static class GearNativeChecks{
 static void Check(bool v,string name){if(!v)throw new Exception(name);}
 public static void Run(){
  Check(RebirthGearItemCodec.ResolveGearItemId("AQ==","backpack")=="pack","matching slot rejected");
  Check(RebirthGearItemCodec.ResolveGearItemId("Ag==","backpack")==null,"wrong slot accepted");
  Check(RebirthGearItemCodec.ResolveGearItemId("Aw==","backpack")==null,"unregistered item accepted");
  ItemValue value;
  var encoded=RebirthNativeItemCodec.Encode(new ItemValue{ItemClass=new ItemClass{Name="pack"}});
  Check(encoded=="AQ==" && RebirthNativeItemCodec.TryDecode(encoded,out value) && value.ItemClass.Name=="pack","native serialization delegation changed");
  foreach(string data in new[]{"", "bad", "AQAB", "AA==", "CA==", "CQ==", "A Q=="})Check(!RebirthGearItemCodec.TryDecode(data,out value)&&value==null,"invalid payload accepted: "+data);
  Check(RebirthGearItemCodec.ResolveGearItemId("AQ==","armor")==null,"unknown gear slot accepted");
  var plan=new RebirthGearInventoryPlan{BagSlotsBefore=65,BagSlotsAfter=52,BeltSlotsBefore=20,BeltSlotsAfter=4};
  plan.GearAfter=new RebirthGearInventoryPlan.Stack{ItemData="AQ==",Count=1};
  plan.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=0,Before=new RebirthGearInventoryPlan.Stack{ItemData="AQ==",Count=1}});
  plan.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=64,Before=new RebirthGearInventoryPlan.Stack{ItemData="Ag==",Count=4}});
  plan.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry{Origin=RebirthGearInventoryPlan.RecoveryOrigin.Backpack,SourceIndex=64,Item=new RebirthGearInventoryPlan.Stack{ItemData="Ag==",Count=4}});
  RebirthGearNativePlan native;
  Check(RebirthGearNativePlan.TryDecode(plan,out native) && native.GearAfter.itemValue.ItemClass.Name=="pack" && native.Recovery[0].count==4 && native.Changes.Count==2,"native plan decode lost data");
  Check(!object.ReferenceEquals(native.Changes[0].After,native.Changes[1].After) && !object.ReferenceEquals(native.GearBefore,ItemStack.Empty),"shared empty stack exposed");
  plan.Changes[1].Before.ItemData="AgAA";plan.Recovery[0].Item.ItemData="AgAA";
  Check(plan.IsConserved() && !RebirthGearNativePlan.TryDecode(plan,out native) && native==null,"partial decoded plan escaped on bad tail item");

 }
}
"@)
[GearNativeChecks]::Run()
Write-Output 'PASS: production gear decoder rejects wrong-slot/unregistered/corrupt/trailing/empty data (native reader and registry stubbed).'
