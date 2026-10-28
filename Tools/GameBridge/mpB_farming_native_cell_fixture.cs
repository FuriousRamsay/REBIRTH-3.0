// Deferred production native-cell methods; native callbacks/storage are explicit doubles.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Security.Cryptography;
class ItemStack {public string Image;public int count;public ItemStack(string image,int amount){Image=image;count=amount;}public static ItemStack Empty=>new ItemStack("",0);public static ItemStack[] Clone(ItemStack[] rows)=>rows.Select(value=>new ItemStack(value.Image,value.count)).ToArray();}
class ItemStackGrid {public ItemStack[] items;}
class World{}
class EntityPlayerLocal {public ItemStack DragAndDropItem=ItemStack.Empty;}
static class RebirthProductiveEarnedObservationBudget {public static int Active;private sealed class Lease:IDisposable {bool ended;public void Dispose(){if(!ended){ended=true;Active--;}}}public static bool TryRetainDetached(long bytes,out IDisposable result){Active++;result=new Lease();return true;}}
static class RebirthDurableFileCommit {public static bool TryPublish(string a,string b,out string reason){reason="fixture";try{File.Move(a,b);return true;}catch{return false;}}}
static class AdvancedFarmingHarvestReceipt {public static string OutputDigest(string image){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(image)));}}
static partial class AdvancedFarmingHarvestOwnerDelta {
// PRODUCTION_TYPES
private static Permit permit;private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
private static bool Current(Attempt a)=>a.Current();
private static string Cells(ItemStack[] rows)=>new XElement("slots",rows.Select(value=>new XElement("slot",new XAttribute("count",value.count),value.Image))).ToString(SaveOptions.DisableFormatting);
// PRODUCTION_CONSUME
static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
static Attempt New(string root){var a=new Attempt{Plan=new Plan{Operation=Guid.NewGuid()},Directory=root,Image="full immutable original",World=new World(),Player=new EntityPlayerLocal(),Bag=new ItemStackGrid{items=new[]{new ItemStack("rich-original",1),ItemStack.Empty}},Belt=new ItemStackGrid{items=new[]{ItemStack.Empty}},Current=()=>true};Attempts.Add(a.Plan.Operation,a);return a;}
public static void Main(string[] args){
 var a=New(Path.Combine(args[0],"native-cells"));var value=new ItemStack("rich-original",3);
 Check(PrepareNativeCell(a,0,0,value,out var p),"original preparation refused");permit=p;Check(Consume(a.Bag,0,value),"native one-use permit refused");var state=EnterNativeCell(a.Bag,0,value);ObserveNativeCell(a.Bag,0,value,false,state);permit=null;ReleaseNativeCell(p);
 Check(PrepareNativeCell(a,0,0,value,out p)&&p.Invocation==1,"positively unused callback could not retry");permit=p;Check(Consume(a.Bag,0,value),"retry permit refused");state=EnterNativeCell(a.Bag,0,value);a.Bag.items[0]=value;ObserveNativeCell(a.Bag,0,value,false,state);Check(!PublishNativeCell(p),"skipped original with changed native postimage granted yield");permit=null;ReleaseNativeCell(p);a.Bag.items[0]=new ItemStack("rich-original",1);Check(!PrepareNativeCell(a,0,0,value,out p),"unknown callback replayed");
 a=New(Path.Combine(args[0],"native-cells"));Check(PrepareNativeCell(a,0,0,value,out p),"fresh source refused");permit=p;Check(Consume(a.Bag,0,value),"actual permit refused");state=EnterNativeCell(a.Bag,0,value);a.Bag.items[0]=value;ObserveNativeCell(a.Bag,0,value,true,state);Check(p.Observed&&PublishNativeCell(p)&&HasNativeCell(a,0,0),"actual native full-cell publication lost");permit=null;Check(!PrepareNativeCell(a,0,0,value,out p),"same original cell re-entered native setter");a.Image="another original";Check(!HasNativeCell(a,0,0),"foreign plan adopted cell publication");
 a=New(Path.Combine(args[0],"native-cells"));Check(PrepareNativeCell(a,0,0,value,out p),"next source refused");permit=p;Check(Consume(a.Bag,0,value),"next permit refused");state=EnterNativeCell(a.Bag,0,value);a.Bag.items[0]=value;a.Belt.items[0]=new ItemStack("foreign mutation",1);ObserveNativeCell(a.Bag,0,value,true,state);Check(!p.Observed&&!PublishNativeCell(p),"unrelated native grid mutation accepted");permit=null;ReleaseNativeCell(p);
 Check(RebirthProductiveEarnedObservationBudget.Active==0,"native cell retained uncharged/leaked observation memory");Console.WriteLine("PASS production native-cell attempt/unused/accepted/unknown/full grids and immutable original binding; native callbacks/storage doubled");
}
}
// PRODUCTION_NATIVE_CELL
