$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthSurvivorGearService.cs')
$start = $source.IndexOf('    private static bool TryBuildStoredGearStack(')
$end = $source.IndexOf('    private static void AddToBackpackOrDrop(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Production gear decoder not found' }
$method = $source.Substring($start, $end - $start)
Add-Type -TypeDefinition (@'
using System;
using System.IO;
public class ItemClass {
 public string Name;
 public string GetItemName(){return Name;}
 public static ItemValue GetItem(string id,bool unused){return id=="gear"?new ItemValue{ItemClass=new ItemClass{Name=id},Quality=1}:null;}
}
public class ItemValue {
 public ItemClass ItemClass; public int Quality; public bool Empty;
 public bool IsEmpty(){return Empty;}
 public static ItemValue ReadOrNull(BinaryReader reader){
  int kind=reader.ReadInt32(); if(kind==0)return null;
  string name=reader.ReadString(); int quality=reader.ReadInt32();
  return new ItemValue{ItemClass=kind==3?null:new ItemClass{Name=name},Quality=quality,Empty=kind==2};
 }
}
public class ItemStack {
 public ItemValue itemValue; public int count;
 public ItemStack(ItemValue value,int quantity){itemValue=value;count=quantity;}
}
public static class GearDecodeChecks {
'@ + $method + @'
 static string Encode(int kind,string name,int quality,bool trailing){
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
   writer.Write(kind);writer.Write(name);writer.Write(quality);if(trailing)writer.Write((byte)1);
   return Convert.ToBase64String(stream.ToArray());
  }
 }
 static void Reject(string id,string data){ItemStack result;
  if(TryBuildStoredGearStack(id,data,out result)||result!=null)throw new Exception("Accepted invalid exact data: "+data);
 }
 public static void Run(){ItemStack result;
  if(!TryBuildStoredGearStack("gear",Encode(1,"gear",6,false),out result)||result.count!=1||result.itemValue.Quality!=6)
   throw new Exception("Lost exact quality");
  if(!TryBuildStoredGearStack("gear","",out result)||result.itemValue.Quality!=1)throw new Exception("Legacy migration failed");
  Reject("missing","");Reject("gear","invalid base64!");
  Reject("gear",Convert.ToBase64String(new byte[]{1}));
  Reject("gear",Encode(1,"other",6,false));Reject("gear",Encode(1,"gear",6,true));
  Reject("gear",Encode(0,"gear",6,false));Reject("gear",Encode(2,"gear",6,false));
  Reject("gear",Encode(3,"gear",6,false));
 }
}
'@)
[GearDecodeChecks]::Run()
Write-Output 'PASS: 10 production gear-decoder cases with serialization stubs. Native saves/inventory/runtime not exercised.'
