#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source='using System;using System.Collections.Generic;'
foreach($name in @('RebirthStationInputLayout.cs','RebirthStationInputPaymentImage.cs')){$source+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $taskRoot ('Scripts/Crafting/UI/'+$name))),'(?m)^using [^\r\n]+;\r?\n','')}
$doubles=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'test_station_input_layout_fixture.cs'))
$source+=$doubles.Substring(0,$doubles.IndexOf('public static class LayoutFixture'))
$source+=@"
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&a.itemValue.Data==b.itemValue.Data;}
public static class RebirthStationNativeInputCodec{public static bool Valid=true;public static bool IsRoundTrippable(IList<ItemStack> x)=>Valid;}
public static class PaymentImageFixture{
 static int checks;static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;}
 public static string Run(){
 foreach(int physical in new[]{3,9}){
 var current=ItemStack.CreateArray(physical+2);for(int i=0;i<current.Length;i++){current[i].count=5;current[i].itemValue.Data="payload"+i;}
 ItemStack[] before;RebirthStationInputLayout.TryReadPhysical(current,physical,2,out before);var after=new ItemStack[physical];for(int i=0;i<physical;i++){after[i]=before[i].Clone();after[i].count=3;}
 ItemStack[] merged;Check(RebirthStationInputPaymentImage.TryMerge(current,physical,2,before,after,out merged),"valid merge");
 Check(merged.Length==current.Length&&merged[0].count==3&&current[0].count==5,"physical detached");
 Check(merged[physical].count==5&&merged[physical].itemValue.Data==current[physical].itemValue.Data&&!ReferenceEquals(merged[physical].itemValue,current[physical].itemValue),"material totals preserved detached");
 current[0].count=4;Check(!RebirthStationInputPaymentImage.TryMerge(current,physical,2,before,after,out merged)&&merged==null,"changed custody");current[0].count=5;
 after[0].itemValue.Data="foreign";Check(!RebirthStationInputPaymentImage.TryMerge(current,physical,2,before,after,out merged),"substituted remainder");after[0].itemValue.Data=before[0].itemValue.Data;
 after[0].count=6;Check(!RebirthStationInputPaymentImage.TryMerge(current,physical,2,before,after,out merged),"increased remainder");after[0].count=3;
 RebirthStationNativeInputCodec.Valid=false;Check(!RebirthStationInputPaymentImage.TryMerge(current,physical,2,before,after,out merged),"lossy native image");RebirthStationNativeInputCodec.Valid=true;
 }
 return "PASS "+checks+" actual payment-image/layout checks with native clone/snapshot/codec doubles; no station mutation or custody publication";
 }
}
"@
Add-Type -TypeDefinition $source
[PaymentImageFixture]::Run()