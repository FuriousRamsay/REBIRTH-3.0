$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackLibraryContents.cs') -Raw
# Exercise production migration with an explicitly doubled native codec and item model.
$fixture=@"
using System;
using System.Collections.Generic;
public class FakeClass { public int MaxCount=125; public string GetItemName(){return "pack";} }
public class TypedMetadataValue { public enum TypeTag{String} public object Value;public TypeTag GetTypeTag(){return TypeTag.String;} public object GetValue(){return Value;} }
public class ItemValue { public FakeClass ItemClass=new FakeClass();public Dictionary<string,TypedMetadataValue> Metadata=new Dictionary<string,TypedMetadataValue>();public bool TryGetMetadata(string key,out string text){text=null;TypedMetadataValue value;if(!Metadata.TryGetValue(key,out value))return false;text=value.Value as string;return text!=null;}public ItemValue Clone(){return new ItemValue{Metadata=new Dictionary<string,TypedMetadataValue>(Metadata)};}public void SetMetadata(string key,string value){Metadata[key]=new TypedMetadataValue{Value=value};} }
public class ItemStack { public int count;public ItemValue itemValue;public bool IsEmpty(){return count<=0||itemValue==null;}public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;} }
public class EntityPlayer{}
public static class RebirthSurvivorGearService { public static bool TryGetEquippedBackpackItem(EntityPlayer p,out ItemValue v){v=null;return false;} }
public static class RebirthBackpackLibraryPolicy {public static int Capacity;public static int CapacityForBackpack(string id){return Capacity;}public static bool IsLearningMaterial(ItemValue value){return true;} }
public static class RebirthBackpackLibraryTransfer {public static bool CanMerge(ItemStack a,ItemStack b){return true;} }
public static class RebirthBackpackStoragePayload {public static bool Fits(ItemValue value){return true;} }
public static class RebirthNativeItemConformanceReader {
 public static ItemStack[] Image;
 public static bool TryDecodeStackArrayV1(string text,int capacity,int bytes,int chars,out ItemStack[] result){result=null;if(text!="fixture"||Image==null||Image.Length!=capacity)return false;result=ItemStack.CreateArray(capacity);for(int i=0;i<capacity;i++)result[i]=new ItemStack{count=Image[i].count,itemValue=Image[i].itemValue};return true;}
 public static bool TryEncodeStackArrayV1(ItemStack[] items,int bytes,int chars,out string text){Image=items;text="fixture";return true;}
}
public static class ReadingMigrationFixture {
 public static string Run(){int cases=0;for(int tier=1;tier<=8;tier++){
  RebirthBackpackLibraryPolicy.Capacity=tier*10;var image=ItemStack.CreateArray(tier*6);for(int i=0;i<image.Length;i++)image[i]=new ItemStack{count=i+1,itemValue=new ItemValue()};RebirthNativeItemConformanceReader.Image=image;var pack=new ItemValue();pack.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,"fixture");ItemStack[] result;
  if(!RebirthBackpackLibraryContents.TryRead(pack,out result)||result.Length!=tier*10)throw new Exception("Wrong capacity");
  for(int i=0;i<image.Length;i++)if(result[i].count!=image[i].count||result[i].itemValue!=image[i].itemValue)throw new Exception("Legacy item lost");
  for(int i=image.Length;i<result.Length;i++)if(!result[i].IsEmpty())throw new Exception("New slot occupied");
  ItemValue rewritten;if(!RebirthBackpackLibraryContents.TryWrite(pack,result,out rewritten)||!RebirthBackpackLibraryContents.TryRead(rewritten,out result))throw new Exception("Migrated write failed");cases++;
 }
 var malformed=new ItemValue();malformed.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,"bad");ItemStack[] invalid;if(RebirthBackpackLibraryContents.TryRead(malformed,out invalid))throw new Exception("Corrupt metadata silently reset");return (cases+1)+" migration/write/corruption cases passed";
 }
}
"@
$source=$source.Replace('using System;','').Replace('using System.IO;','')
Add-Type -TypeDefinition ($fixture+"`n"+$source)
[ReadingMigrationFixture]::Run()