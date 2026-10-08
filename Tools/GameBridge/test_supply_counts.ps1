$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/GameBridge/RebirthGameBridgeNeeds.cs')
$start=$source.IndexOf('    private static int CountMatching(')
$end=$source.IndexOf('    // ------------------------------------------------------------------ toolbelt',$start)
if($start -lt 0 -or $end -le $start){throw 'Production count helper not found'}
$method=$source.Substring($start,$end-$start)
Add-Type -TypeDefinition (@'
using System;
public class ItemClass { public string Name; }
public class ItemValue { public ItemClass ItemClass; }
public class ItemStack {
 public ItemValue itemValue; public int count;
 public bool IsEmpty(){return count<=0;}
 public ItemStack(string name,int n){count=n;itemValue=new ItemValue{ItemClass=name==null?null:new ItemClass{Name=name}};}
}
public static class SupplyCountChecks {
'@ + $method + @'
 public static void Run(){
  var slots=new[]{new ItemStack("food",2),new ItemStack("water",3),new ItemStack("food",90),new ItemStack("food",2)};
  Func<ItemClass,bool> food=ic=>ic.Name=="food";
  if(CountMatching(slots,2,food)!=2)throw new Exception("locked/dummy slots counted");
  if(CountMatching(slots,3,food)!=92)throw new Exception("expanded slot omitted");
  if(CountMatching(slots,int.MaxValue,food)!=94)throw new Exception("backpack count wrong");
  if(CountMatching(slots,0,food)!=0||CountMatching(slots,-1,food)!=0)throw new Exception("empty capacity counted");
  if(CountMatching(null,3,food)!=0)throw new Exception("null array counted");
  if(CountMatching(new[]{null,new ItemStack("food",0),new ItemStack(null,1)},3,food)!=0)throw new Exception("invalid slot counted");
 }
}
'@)
[SupplyCountChecks]::Run()
Write-Output 'PASS: production supply counter excludes unavailable slots; expanded capacity, backpack, empty and invalid slots checked.'
