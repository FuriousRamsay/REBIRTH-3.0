$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStorageContainer.cs'))
$doubles=@'
using System;
using System.Collections.Generic;
public sealed class DynamicProperties {
 public Dictionary<string,DynamicProperties> Classes=new Dictionary<string,DynamicProperties>();
 public Dictionary<string,string> Values=new Dictionary<string,string>();
 public string GetString(string key){string value;return Values.TryGetValue(key,out value)?value:string.Empty;}
}
public sealed class Block {public DynamicProperties Properties=new DynamicProperties();}
public sealed class BlockValue {public Block Block;}
public class ItemClass {}
public sealed class ItemClassBlock:ItemClass {public Block Block;public BlockValue GetBlockValueFromItemValue(ItemValue item){return new BlockValue{Block=Block};}}
public sealed class ItemValue {public ItemClass ItemClass;}
public sealed class ItemStack {public int count;public ItemValue itemValue;public bool IsEmpty(){return count==0;}}
public static class TEFeatureStorage {public const string PropLootList="LootList";}
public static class RebirthCamperStorageCapacity {public const int MaximumNativeSlots=32767;}
public struct Vector2i {public int x,y;}
public sealed class LootContainer {
 public Vector2i size;public static Dictionary<string,LootContainer> Entries=new Dictionary<string,LootContainer>();
 public static LootContainer GetLootContainer(string name,bool fallback){LootContainer value;return Entries.TryGetValue(name,out value)?value:null;}
}
'@
$checks=@'
public static class CamperContainerChecks {
 static ItemStack Module(Block block){return new ItemStack{count=1,itemValue=new ItemValue{ItemClass=new ItemClassBlock{Block=block}}};}
 static void Capacity(ItemStack item,int wanted){int actual;if(!RebirthCamperStorageContainer.TryGetCapacity(item,out actual)||actual!=wanted)throw new Exception("Wrong configured storage capacity.");}
 static void Refuse(ItemStack item){int actual;if(RebirthCamperStorageContainer.TryGetCapacity(item,out actual))throw new Exception("Invalid container admitted.");}
 public static void Run(){
  LootContainer.Entries["safe"]=new LootContainer{size=new Vector2i{x=5,y=4}};
  LootContainer.Entries["fridge"]=new LootContainer{size=new Vector2i{x=7,y=6}};
  var safe=new Block();var features=new DynamicProperties();var storage=new DynamicProperties();
  safe.Properties.Classes["CompositeFeatures"]=features;features.Classes["TEFeatureStorage"]=storage;storage.Values["LootList"]="safe";
  Capacity(Module(safe),20);safe.Properties.Values["LootList"]="fridge";Capacity(Module(safe),20); // native feature wins
  storage.Values["LootList"]="missing";Refuse(Module(safe)); // no accidental legacy fallback
  storage.Values.Remove("LootList");Refuse(Module(safe));
  var legacy=new Block();legacy.Properties.Values["LootList"]="fridge";Capacity(Module(legacy),42);
  var plural=Module(legacy);plural.count=2;Refuse(plural);
  Refuse(new ItemStack{count=1,itemValue=new ItemValue{ItemClass=new ItemClass()}});
  LootContainer.Entries["fridge"].size=new Vector2i{x=32768,y=1};Refuse(Module(legacy));
 }
}
'@
Add-Type -TypeDefinition ($doubles+"`n"+$source+"`n"+$checks)
[CamperContainerChecks]::Run()
Write-Output 'PASS: actual resolver uses native nested storage feature, feature precedence, legacy root fallback, configured dimensions and malformed/noncontainer/over-limit refusal. Native definitions are explicit adapters; actual game item resolution remains unqualified.'
