$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/GameBridge/RebirthGameBridgePoiStorage.cs')
$start = $source.IndexOf('    private static Dictionary<string, long> CombinedCounts')
$end = $source.IndexOf('    private bool HasCargo', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Production conservation methods not found' }
$methods = $source.Substring($start, $end - $start)
Add-Type -TypeDefinition (@'
using System;
using System.Collections.Generic;
using System.IO;
public class PooledBinaryWriter : BinaryWriter {public PooledBinaryWriter():base(new MemoryStream()){}public void SetBaseStream(Stream stream){OutStream=stream;}}
public static class MemoryPools {public class Pool {public PooledBinaryWriter AllocSync(bool reset = true){return new PooledBinaryWriter();}}public static Pool poolBinaryWriter=new Pool();}
public class ItemClass { public int MaxCount=100; }
public class ItemValue {
 public int type,quality; public string mods=""; public bool broken;
 public int Quality {get{return quality;}} public float UseTimes; public int Meta,Flags,SelectedAmmoTypeIndex;
 public class Texture {public bool IsDefault=true;} public Texture TextureFullArray=new Texture();
 public int[] Stats; public Dictionary<string,string> Metadata; public ItemValue[] modifications,cosmeticMods;
 public ItemClass ItemClass=new ItemClass();
 public static void Write(ItemValue value,BinaryWriter writer){if(value.broken)throw new IOException();writer.Write(value.type);writer.Write(value.quality);writer.Write(value.mods);writer.Write(value.Meta);writer.Write(value.Flags);}
}
public class ItemStack {
 public ItemValue itemValue; public int count;
 public ItemStack(int type,int count) {itemValue=new ItemValue{type=type};this.count=count;}
 public bool IsEmpty(){return count<=0;}
}
public static class StorageConservationChecks {
'@ + $methods + @'
 public static void Run() {
  var bag=new[]{new ItemStack(1,10),new ItemStack(2,3)};
  var crate=new[]{new ItemStack(1,4)};
  var before=CombinedCounts(bag,crate);
  if(CombinedCounts(bag,null)!=null||CombinedCounts(null,crate)!=null||SameCounts(before,null,crate))throw new Exception("missing inventory certified");
  if(!SameCounts(before,new[]{new ItemStack(1,7),new ItemStack(2,3)},new[]{new ItemStack(1,7)}))throw new Exception("valid partial transfer rejected");
  if(SameCounts(before,new[]{new ItemStack(1,7),new ItemStack(2,3)},new[]{new ItemStack(1,6),new ItemStack(3,1)}))throw new Exception("equal-total substitution accepted");
  if(SameCounts(before,bag,new[]{new ItemStack(1,5)}))throw new Exception("duplication accepted");
  if(SameCounts(before,bag,new[]{new ItemStack(1,3)}))throw new Exception("loss accepted");
  if(!SameCounts(before,new ItemStack[]{null,new ItemStack(1,0)},new[]{new ItemStack(1,14),new ItemStack(2,3)}))throw new Exception("empty bag transfer rejected");
  var weapon=new ItemStack(7,1);weapon.itemValue.ItemClass.MaxCount=1;weapon.itemValue.quality=6;weapon.itemValue.mods="scope";
  string original=RestoreKey(weapon);
  if(original==null||RestoreKey(weapon)!=original)throw new Exception("unchanged restore identity rejected");
  if(RestoreKey(null)!=null||RestoreKey(new ItemStack(7,0))!=null)throw new Exception("empty restore target accepted");
  before=CombinedCounts(new[]{weapon},new ItemStack[0]);
  if(!SameCounts(before,new ItemStack[0],new[]{weapon}))throw new Exception("exact equipment move rejected");
  weapon.itemValue.quality=5;
  if(RestoreKey(weapon)==original)throw new Exception("different-quality restoration accepted");
  if(SameCounts(before,new ItemStack[0],new[]{weapon}))throw new Exception("equipment quality loss accepted");
  weapon.itemValue.quality=6;weapon.itemValue.mods="";
  if(RestoreKey(weapon)==original)throw new Exception("different-mod restoration accepted");
  if(SameCounts(before,new ItemStack[0],new[]{weapon}))throw new Exception("equipment mod loss accepted");
  var special=new ItemStack(8,4);special.itemValue.Meta=3;
  var specialBefore=CombinedCounts(new[]{special},new ItemStack[0]);
  if(!SameCounts(specialBefore,new ItemStack[0],new[]{special}))throw new Exception("exact stackable metadata move rejected");
  special.itemValue.Meta=0;
  if(SameCounts(specialBefore,new ItemStack[0],new[]{special}))throw new Exception("stackable metadata loss accepted");
  special.itemValue.Flags=1;specialBefore=CombinedCounts(new[]{special},new ItemStack[0]);special.itemValue.Flags=0;
  if(SameCounts(specialBefore,new ItemStack[0],new[]{special}))throw new Exception("stackable flags loss accepted");
  weapon.itemValue.broken=true;
  if(RestoreKey(weapon)!=null)throw new Exception("unreadable restoration accepted");
  if(CombinedCounts(new[]{weapon},new ItemStack[0])!=null||SameCounts(before,new ItemStack[0],new[]{weapon}))throw new Exception("serialization failure accepted");
 }
}
'@)
[StorageConservationChecks]::Run()
Write-Output 'PASS: conservation and restoration identity checks, including quality/mod changes, empty slots and failed serialization; native inventory/UI not exercised.'
