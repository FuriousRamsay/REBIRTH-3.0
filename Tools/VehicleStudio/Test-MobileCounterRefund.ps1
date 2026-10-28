#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$counterSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationCounterRefund.cs'))
$counterSource=[regex]::Replace($counterSource,'(?m)^using [^;]+;\r?\n','')
$counterFixture=@'
using System;
public class ItemClass{public string Name;public string GetItemName()=>Name;}
public class ItemValue{public int type;public ItemClass ItemClass;public string Data;public bool IsEmpty()=>type==0;}
public class ItemStack{public ItemValue itemValue;public int count;public ItemStack Clone()=>new ItemStack{itemValue=new ItemValue{type=itemValue.type,ItemClass=itemValue.ItemClass,Data=itemValue.Data},count=count};}
public class RebirthCamperStationState{public ItemStack[] Input;public int InputSlotCount;public string[] MaterialNames;}
public class RebirthStationCancellationRefund{public bool IsMaterialReturn;public ItemStack[] Counters;public ItemStack[] CopyCounterRefunds()=>Counters;}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Data==b.itemValue.Data;}
public static class CounterRefundFixture{
static void Check(bool v,string n){if(!v)throw new Exception(n);}static ItemStack Cell(int type,int count,string name,string data="original")=>new ItemStack{count=count,itemValue=new ItemValue{type=type,ItemClass=new ItemClass{Name=name},Data=data}};
public static void Run(){var s=new RebirthCamperStationState{InputSlotCount=1,MaterialNames=new[]{"iron","brass"},Input=new[]{Cell(9,3,"wood"),Cell(1,0,"unit_iron"),Cell(2,7,"unit_brass")}};var refund=new RebirthStationCancellationRefund{IsMaterialReturn=true,Counters=new[]{Cell(1,300000,"unit_iron")}};
Check(RebirthCamperStationCounterRefund.TryBuild(s,refund,out var next)&&next[1].count==300000&&next[2].count==7&&next[0].count==3&&s.Input[1].count==0,"counter credit clipped, lost unrelated inventory, or mutated source");Check(RebirthCamperStationCounterRefund.TryBuild(s,refund,out var retry)&&retry[1].count==300000&&s.Input[1].count==0,"detached retry minted live credit");
refund.Counters[0].itemValue.Data="foreign";Check(!RebirthCamperStationCounterRefund.TryBuild(s,refund,out _),"different native payload accepted");refund.Counters[0].itemValue.Data="original";s.MaterialNames[0]="lead";Check(!RebirthCamperStationCounterRefund.TryBuild(s,refund,out _),"changed material definitions accepted");s.MaterialNames[0]="iron";s.Input[1].count=int.MaxValue;Check(!RebirthCamperStationCounterRefund.TryBuild(s,refund,out _),"counter overflow accepted");s.Input[1].count=0;refund.Counters[0].count=-1;Check(!RebirthCamperStationCounterRefund.TryBuild(s,refund,out _),"negative credit accepted");refund.IsMaterialReturn=false;Check(RebirthCamperStationCounterRefund.TryBuild(s,refund,out next)&&next[1].count==0,"physical refund mutated counters");Console.WriteLine("PASS actual detached counter return: Int32/typed zero, exact payload/material, unrelated cells, retry no mutation, overflow/negative/changed definitions refusal. Authority/native save adapters are outside this arithmetic fixture.");}
}
'@
Add-Type -TypeDefinition ($counterFixture+"`n"+$counterSource)
[CounterRefundFixture]::Run()
