$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthMusicLibraryService.cs')
$start = $source.IndexOf('    internal static string Encode(')
if ($start -lt 0) { throw 'Production return helpers not found' }
$methods = $source.Substring($start).TrimEnd()
$methods = $methods.Substring(0, $methods.LastIndexOf('}'))
Add-Type -TypeDefinition (@'
using System;
using System.IO;
public class ItemValue {
 public int type, seed;
 public static void Write(ItemValue v, BinaryWriter w){w.Write(v.type);w.Write(v.seed);}
}
public class ItemStack {
 public ItemValue itemValue; public int count;
 public ItemStack(ItemValue v,int n){itemValue=v;count=n;}
 public bool IsEmpty(){return count<=0;}
 public bool CanStackWith(ItemStack other){return itemValue.type==other.itemValue.type && count+other.count<=10;}
 public ItemStack Clone(){return new ItemStack(itemValue,count);}
}
public static class MusicReturnChecks {
'@ + $methods + @'
 public static void Run(){
  var value=new ItemValue{type=1,seed=7};ItemStack result;
  var partial=new ItemStack(value,9);
  if(FindReturnSlot(new[]{partial},value,out result)!=0||result.count!=10||partial.count!=9)
   throw new Exception("partial-stack return or nonmutation failed");
  if(FindReturnSlot(new ItemStack[]{null,partial},value,out result)!=1)
   throw new Exception("did not prefer existing stack");
  var full=new ItemStack(value,10);
  if(FindReturnSlot(new[]{full},value,out result)!=-1||result!=null)
   throw new Exception("accepted full bag");
  var different=new ItemStack(new ItemValue{type=1,seed=8},2);
  if(FindReturnSlot(new[]{different},value,out result)!=-1)
   throw new Exception("merged distinct metadata");
  if(FindReturnSlot(new ItemStack[]{different,null},value,out result)!=1||result.count!=1||result.itemValue.seed!=7)
   throw new Exception("empty-slot fallback lost exact value");
  if(FindReturnSlot(null,value,out result)!=-1||FindReturnSlot(new ItemStack[0],value,out result)!=-1)
   throw new Exception("missing bag accepted");
 }
}
'@)
[MusicReturnChecks]::Run()
Write-Output 'PASS: 6 production-helper cases with inventory stubs; native inventory and multiplayer not exercised.'
