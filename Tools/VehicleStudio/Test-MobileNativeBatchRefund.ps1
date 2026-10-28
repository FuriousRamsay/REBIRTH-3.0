#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$nativeSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationNativeCrafting.cs'))
$nativeMethod=$nativeSource.Substring($nativeSource.IndexOf('    internal static bool TryRemainingRefund('))
$nativeMethod=$nativeMethod.Substring(0,$nativeMethod.LastIndexOf('}'))
$nativeFixture=@'
using System;using System.Collections.Generic;
public class ItemValue {public int type;public Dictionary<string,object> Metadata=new Dictionary<string,object>();public bool IsEmpty()=>type==0;public bool HasMetadata(string k)=>Metadata.ContainsKey(k);public bool TryGetMetadata<T>(string k,out T value){value=default(T);if(!Metadata.TryGetValue(k,out var o)||!(o is T))return false;value=(T)o;return true;}public ItemValue Clone()=>new ItemValue{type=type,Metadata=new Dictionary<string,object>(Metadata)};}
public class ItemStack {public ItemValue itemValue;public int count;public ItemStack Clone()=>new ItemStack{itemValue=itemValue.Clone(),count=count};}
public class Recipe {public List<ItemStack> ingredients=new List<ItemStack>();}
public class RecipeQueueItem {public Recipe Recipe;public int Multiplier;}
public static class NativeRefundFixture {
const string JobKey="rebirth.camper.native.job",BatchesKey="rebirth.camper.native.batches",UnitsKey="rebirth.camper.native.units",MaterialKey="rebirth.camper.native.material",SlotKey="rebirth.camper.native.slot";
static bool TryJob(Recipe r,out Guid job){job=Guid.Empty;return r?.ingredients?.Count>0&&r.ingredients[0].itemValue.TryGetMetadata(JobKey,out string s)&&Guid.TryParseExact(s,"N",out job)&&job!=Guid.Empty;}
static void Check(bool v,string n){if(!v)throw new Exception(n);}
static ItemStack Cell(int type,int count,string data)=>new ItemStack{itemValue=new ItemValue{type=type,Metadata=new Dictionary<string,object>{{"original",data}}},count=count};
public static void Run(){var row=new RecipeQueueItem{Multiplier=2,Recipe=new Recipe()};row.Recipe.ingredients.Add(Cell(1,5,"first"));row.Recipe.ingredients.Add(Cell(1,7,"second"));row.Recipe.ingredients.Add(Cell(2,6,"other"));var marker=row.Recipe.ingredients[0].itemValue;marker.Metadata[JobKey]=Guid.NewGuid().ToString("N");marker.Metadata[BatchesKey]=3;
 Check(TryRemainingRefund(row,out var refund)&&refund.Count==3&&refund[0].count==1&&refund[1].count==7&&refund[2].count==4,"remaining two batches refunded completed batch or lost exact fragments");Check((string)refund[0].itemValue.Metadata["original"]=="first"&&!refund[0].itemValue.HasMetadata(JobKey)&&marker.HasMetadata(JobKey)&&row.Recipe.ingredients[0].count==5,"refund altered original or leaked marker");
 row.Multiplier=1;Check(TryRemainingRefund(row,out refund)&&refund.Count==2&&refund[0].count==4&&(string)refund[0].itemValue.Metadata["original"]=="second"&&refund[1].count==2,"final remaining batch provenance wrong");row.Multiplier=0;Check(!TryRemainingRefund(row,out _),"completed job refunded");row.Multiplier=4;Check(!TryRemainingRefund(row,out _),"multiplier beyond paid batches refunded");row.Multiplier=1;marker.Metadata[BatchesKey]="3";Check(!TryRemainingRefund(row,out _),"malformed batch marker accepted");marker.Metadata.Remove(BatchesKey);row.Recipe.ingredients.Clear();row.Recipe.ingredients.Add(Cell(1,3,"legacy"));row.Recipe.ingredients[0].itemValue.Metadata[JobKey]=Guid.NewGuid().ToString("N");Check(TryRemainingRefund(row,out refund)&&refund[0].count==3,"existing collapsed single batch lost");row.Multiplier=2;Check(!TryRemainingRefund(row,out _),"legacy recipe invented batches");
 var molten=new RecipeQueueItem{Multiplier=2,Recipe=new Recipe()};var unit=Cell(3,1,"counter");unit.itemValue.Metadata[JobKey]=Guid.NewGuid().ToString("N");unit.itemValue.Metadata[BatchesKey]=3;unit.itemValue.Metadata[UnitsKey]=300000;unit.itemValue.Metadata[MaterialKey]="unit_iron";unit.itemValue.Metadata[SlotKey]=3;molten.Recipe.ingredients.Add(unit);
 Check(TryRemainingRefund(molten,out refund)&&refund.Count==1&&refund[0].count==200000&&unit.count==1,"Int32 counter refund clipped to native UInt16 fragment");unit.itemValue.Metadata[UnitsKey]=-1;Check(!TryRemainingRefund(molten,out _),"negative units accepted");unit.itemValue.Metadata[UnitsKey]=300000;unit.itemValue.Metadata.Remove(SlotKey);Check(!TryRemainingRefund(molten,out _),"counter without exact slot accepted");
Console.WriteLine("PASS actual remaining-batch refund method: native remaining multiplier, exact fragment suffix metadata, partial/all completion, Int32 counter metadata, malformed markers, legacy single-batch compatibility. Native model adapters doubled.");}
'@
Add-Type -TypeDefinition ($nativeFixture+"`n"+$nativeMethod+"`n}")
[NativeRefundFixture]::Run()
if($nativeSource -notmatch 'Multiplier=batches' -or $nativeSource -notmatch 'OneItemCraftTime=seconds' -or $nativeSource -notmatch 'count=recipe.count,') {throw 'Native queue no longer preserves batch multiplier/time/output'}

