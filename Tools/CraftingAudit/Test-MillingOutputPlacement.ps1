$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Crafting/Cooking/RebirthCookingHeat.cs') -Raw
$a=$s.IndexOf('    private static bool TryMillingOutput(');$b=$s.IndexOf('    private static void ReportReady(', $a)
if($a -lt 0 -or $b -lt $a){throw 'Output method not found'}
$m=$s.Substring($a,$b-$a).Replace('private static bool','public static bool')
Add-Type -TypeDefinition @"
using System;
public class ItemClass{public int MaxCount=10;}
public class ItemValue{public string Key="same";public ItemClass ItemClass=new ItemClass();public bool IsEmpty(){return false;}public ItemValue Clone(){return new ItemValue{Key=Key,ItemClass=ItemClass};}}
public class Recipe{public int count=1;}
public class ItemStack {
 public ItemValue itemValue;public int count;
 public ItemStack(ItemValue v,int c){itemValue=v;count=c;}
 public bool IsEmpty(){return count==0;}
 public static ItemStack[] Clone(ItemStack[] s){var r=new ItemStack[s.Length];for(int i=0;i<s.Length;i++)r[i]=new ItemStack(s[i].itemValue.Clone(),s[i].count);return r;}
 public bool CanStackPartlyWith(ItemStack other,out int amount){amount=Math.Min(other.count,itemValue.ItemClass.MaxCount-count);return amount>0&&itemValue.Key==other.itemValue.Key;}
}
public static class OutputFixture {
 static ItemValue Output(Recipe r){return new ItemValue();}
$m
 public static void Run(){
  ItemStack[] output;
  var cells=new[]{new ItemStack(new ItemValue(),8),new ItemStack(new ItemValue(),7)};
  if(!TryMillingOutput(cells,new Recipe(),5,out output)||output[0].count!=10||output[1].count!=10||cells[0].count!=8||cells[1].count!=7)throw new Exception("Fragmented capacity/source mutation");
  if(TryMillingOutput(cells,new Recipe(),6,out output)||output!=null||cells[0].count!=8||cells[1].count!=7)throw new Exception("Partial failed commit");
  cells=new[]{new ItemStack(new ItemValue{Key="different"},8),new ItemStack(new ItemValue(),0)};
  if(!TryMillingOutput(cells,new Recipe(),7,out output)||output[0].count!=8||output[1].count!=7)throw new Exception("Incompatible metadata merged");
  cells=new[]{new ItemStack(new ItemValue(),0),new ItemStack(new ItemValue(),0),new ItemStack(new ItemValue(),0)};
  if(!TryMillingOutput(cells,new Recipe{count=7},3,out output)||output[0].count!=10||output[1].count!=10||output[2].count!=1)throw new Exception("Batch output/stack limits");
  if(TryMillingOutput(cells,new Recipe{count=int.MaxValue},2,out output))throw new Exception("Overflow accepted");
  Console.WriteLine("PASS fragmented capacity, atomic failure, incompatible metadata, batch output/limits, overflow");
 }
}
"@
[OutputFixture]::Run()
