$ErrorActionPreference='Stop'
$production=Get-Content (Join-Path $PSScriptRoot '../../Scripts/Survivor/Support/RebirthBackpackSectionTransfer.cs') -Raw
# Production transfer logic; native codecs and item identity explicitly doubled.
$stubs=@"
using System;
using System.Collections.Generic;
public class FakeClass { public int MaxCount=125; }
public class ItemValue { public string Id,Variant; public bool Reading; public FakeClass ItemClass=new FakeClass(); public ItemStack[] Books,Sale; public ItemValue Clone(){return new ItemValue{Id=Id,Variant=Variant,Reading=Reading,Books=Copy(Books),Sale=Copy(Sale)};} static ItemStack[] Copy(ItemStack[] a){if(a==null)return null;var b=new ItemStack[a.Length];for(int i=0;i<a.Length;i++)b[i]=a[i].Clone();return b;} }
public class ItemStack { public ItemValue itemValue; public int count; public ItemStack(ItemValue v,int c){itemValue=v;count=c;} public bool IsEmpty(){return count<=0||itemValue==null;} public ItemStack Clone(){return new ItemStack(itemValue?.Clone(),count);} public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack(null,0);return a;} }
public static class RebirthBackpackLibraryPolicy { public static bool IsLearningMaterial(ItemValue v){return v.Reading;} }
public static class RebirthBackpackLibraryTransfer { public static bool CanMerge(ItemStack a,ItemStack b){return a.itemValue.Id==b.itemValue.Id&&a.itemValue.Variant==b.itemValue.Variant;} }
public static class RebirthBackpackLibraryContents { public static bool TryRead(ItemValue v,out ItemStack[] a){a=v.Books;return a!=null;} public static bool TryWrite(ItemValue v,ItemStack[] a,out ItemValue next){next=v.Clone();next.Books=a;return true;} }
public static class RebirthBackpackSellStashContents { public static bool TryRead(ItemValue v,out ItemStack[] a){a=v.Sale;return a!=null;} public static bool TryWrite(ItemValue v,ItemStack[] a,out ItemValue next){next=v.Clone();next.Sale=a;return true;} }
public static class TransferFixture {
 static ItemValue Pack(int n){return new ItemValue{Id="pack",Books=ItemStack.CreateArray(n),Sale=ItemStack.CreateArray(n)};}
 static ItemStack Item(string id,int n,bool reading=true,string variant="a"){return new ItemStack(new ItemValue{Id=id,Reading=reading,Variant=variant},n);}
 static Dictionary<string,int> Totals(params ItemStack[][] arrays){var t=new Dictionary<string,int>();foreach(var a in arrays)if(a!=null)foreach(var s in a)if(!s.IsEmpty()){string k=s.itemValue.Id+":"+s.itemValue.Variant;if(!t.ContainsKey(k))t[k]=0;t[k]+=s.count;}return t;}
 static void Equal(Dictionary<string,int> a,Dictionary<string,int> b){if(a.Count!=b.Count)throw new Exception("Identity lost");foreach(var p in a)if(!b.ContainsKey(p.Key)||b[p.Key]!=p.Value)throw new Exception("Quantity changed: "+p.Key);}
 public static string Run(){int passed=0;foreach(int target in new[]{0,1,2,4,8}){var old=Pack(4);old.Books[0]=Item("book",2);old.Books[3]=Item("recipe",1);old.Sale[0]=Item("ammo",120,false);old.Sale[3]=Item("ammo",70,false,"b");var next=target==0?null:Pack(target);if(next!=null)next.Sale[0]=Item("ammo",10,false);var before=Totals(old.Books,old.Sale,next?.Books,next?.Sale);ItemValue empty,filled;List<ItemStack> excess;if(!RebirthBackpackSectionTransfer.TryPrepare(old,next,out empty,out filled,out excess))throw new Exception("Prepare failed");Equal(before,Totals(empty.Books,empty.Sale,filled?.Books,filled?.Sale,excess.ToArray()));if(old.Books[0].count!=2)throw new Exception("Source mutated");if(target==0&&excess.Count!=4)throw new Exception("Removal missing stacks");if(target<4&&excess.Count<2)throw new Exception("Tail missing");passed++;}var legacy=Pack(1);legacy.Books[0]=Item("tool",1,false);ItemValue e,d;List<ItemStack> r;if(!RebirthBackpackSectionTransfer.TryPrepare(legacy,Pack(2),out e,out d,out r)||!d.Books[0].IsEmpty()||r.Count!=1)throw new Exception("Invalid admission");return (passed+1)+" conservation/admission cases passed";}
}
"@
$production=$production.Replace('using System.Collections.Generic;','')
Add-Type -TypeDefinition ($stubs+"`n"+$production)
[TransferFixture]::Run()
