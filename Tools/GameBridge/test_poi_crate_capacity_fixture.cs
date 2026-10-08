using System;
using System.Collections.Generic;
using System.IO;
class Block {public const int ItemsStartHere=1000;}
class Utils {public static int FastMin(int a,int b){return Math.Min(a,b);}}
class ItemClass {public int MaxCount=100;public static ItemClass GetForId(int id){return new ItemClass();}}
class ItemValue {public string Metadata="";public int type=1001;public int TextureFullArray;public bool IsShapeHelperBlock;}
class ItemStack {public int count;public ItemValue itemValue=new ItemValue();public bool IsEmpty(){return count==0;}
// NATIVE
}
class ItemGrid {public ItemStack[] items;}
class TEFeatureStorage {public ItemGrid ItemGrid=new ItemGrid();}
class Test {
static string ConservationKey(ItemValue v)=>v.type+":"+v.Metadata;
// SOURCE
 static void Main(){
  long total;
  if(TryCount(null,out total)||TryCount(new[]{new ItemStack{count=-1}},out total)||total!=0)throw new Exception("invalid count evidence accepted");
  if(!TryCount(new[]{new ItemStack{count=int.MaxValue},new ItemStack{count=int.MaxValue}},out total)||total!=4294967294L)throw new Exception("storage aggregate count overflow");
  if(!TryCount(new ItemStack[]{null,new ItemStack()},out total)||total!=0)throw new Exception("valid empty count refused");
  var empty=new ItemStack[0];
  if(CombinedCounts(null,empty)!=null||CombinedCounts(empty,null)!=null)throw new Exception("missing inventory certified as empty");
  var emptyCounts=CombinedCounts(empty,empty);if(emptyCounts==null||emptyCounts.Count!=0||!SameCounts(emptyCounts,empty,empty))throw new Exception("valid empty inventories refused");
  if(SameCounts(emptyCounts,null,empty)||SameCounts(emptyCounts,empty,null))throw new Exception("missing after image conserved");
  if(CombinedCounts(new[]{new ItemStack{count=-1}},empty)!=null)throw new Exception("negative stack count certified");
  var before=CombinedCounts(new[]{new ItemStack{count=20}},empty);
  if(!SameCounts(before,new[]{new ItemStack{count=12}},new[]{new ItemStack{count=8}}))throw new Exception("valid split transfer refused");
  if(SameCounts(before,new[]{new ItemStack{count=12}},new[]{new ItemStack{count=7}}))throw new Exception("lost quantity conserved");
  var cargo=new ItemStack{count=20};var crate=new TEFeatureStorage{ItemGrid=new ItemGrid{items=new[]{new ItemStack{count=100},new ItemStack{count=100}}}};
  if(CanAccept(crate,cargo))throw new Exception("full crate accepted matching cargo");
  crate.ItemGrid.items[0].count=99;if(!CanAccept(crate,cargo))throw new Exception("partial stack space lost");
  crate.ItemGrid.items[0].itemValue.type=1002;if(CanAccept(crate,cargo))throw new Exception("incompatible partial stack accepted");
  crate.ItemGrid.items[0]=new ItemStack();if(!CanAccept(crate,cargo))throw new Exception("empty cell ignored");
  crate.ItemGrid.items[0]=null;if(!CanAccept(crate,cargo))throw new Exception("null empty cell ignored");
  if(CanAccept(null,cargo)||CanAccept(new TEFeatureStorage(),cargo)||CanAccept(new TEFeatureStorage{ItemGrid=null},cargo))throw new Exception("missing crate accepted");
  crate.ItemGrid.items=new[]{new ItemStack{count=99,itemValue=new ItemValue{Metadata="different"}},new ItemStack()};if(CanAccept(crate,cargo))throw new Exception("metadata-conflicting merge accepted despite empty slot");crate.ItemGrid.items[0].itemValue.Metadata="";if(!CanAccept(crate,cargo))throw new Exception("matching payload rejected");crate.ItemGrid.items[0].count=100;crate.ItemGrid.items[0].itemValue.Metadata="different";if(!CanAccept(crate,cargo))throw new Exception("full conflicting stack cannot merge and must not block empty slot");if(CanAccept(crate,null)||CanAccept(crate,new ItemStack()))throw new Exception("empty/missing cargo accepted");  Console.WriteLine("PASS actual POI capacity predicate with installed build25661859 stack quantity methods: full matching crate rejected; partial compatible, empty and null cells accepted; incompatible/missing crate rejected. Item registry/value representation substituted.");
 }
}
