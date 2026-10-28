// Native rich item/stack limits, XML extras and frozen owner receipt are doubles.
// The combined planner and candidate insertion methods are actual source.
using System;using System.Linq;using System.Collections.Generic;
class ItemClass{internal int MaxCount=10;}
class ItemValue
{
 internal int Type;internal string Metadata;internal ItemClass ItemClass=new ItemClass();
 internal ItemValue(int type,string metadata){Type=type;Metadata=metadata;}
 internal bool IsEmpty()=>Type==0;
 internal ItemValue Clone()=>new ItemValue(Type,Metadata){ItemClass=ItemClass};
}
class ItemStack
{
 internal ItemValue itemValue;internal int count;
 internal ItemStack(ItemValue item,int n){itemValue=item;count=n;}
 internal bool IsEmpty()=>count==0||itemValue.IsEmpty();
 internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);
 internal static ItemStack[] Clone(ItemStack[] rows)=>rows.Select(x=>x.Clone()).ToArray();
 internal bool CanStackPartlyWith(ItemStack other,out int amount){amount=itemValue.Type==other.itemValue.Type&&itemValue.Metadata==other.itemValue.Metadata?Math.Min(itemValue.ItemClass.MaxCount-count,other.count):0;return amount>0;}
}
class Recipe{internal int count=2;internal bool Prepared;internal IReadOnlyList<ItemStack> Frozen;}
static class RebirthCookingOutputDelivery
{
 internal static bool HasPrepared(Recipe r)=>r.Prepared;
 internal static bool TryReadPreparedOutputs(Recipe r,out IReadOnlyList<ItemStack> outputs){outputs=r.Frozen;return outputs!=null;}
}
static class RebirthRecipeOutputs
{
 internal static bool Available=true;
 internal static bool Freeze(Recipe recipe)=>Available;
 internal static bool TryPlan(Recipe r,int portions,out IReadOnlyList<ItemStack> extras,out string reason){reason=null;extras=new[]{new ItemStack(new ItemValue(2,"jar"),portions),new ItemStack(new ItemValue(3,"can"),portions)};return true;}
}
static class RebirthCookingHeat
{
 private static int previews;
 private static ItemValue Output(Recipe recipe){previews++;return new ItemValue(1,"cooked-seed-"+previews);}
 // ACTUAL_COMBINED
 // ACTUAL_INSERT
 private static void Check(bool value,string why){if(!value)throw new Exception(why);}
 private static ItemStack Empty()=>new ItemStack(new ItemValue(0,""),0);
 private static string Image(ItemStack[] rows)=>string.Join(";",rows.Select(x=>x.itemValue.Type+":"+x.itemValue.Metadata+":"+x.count));
 static void Main()
 {
  var recipe=new Recipe();IReadOnlyList<ItemStack> combined;
  Check(TryCombinedOutput(recipe,3,out combined),"three portions");
  Check(combined.Count==3&&combined[0].count==6&&combined[1].count==3&&combined[2].count==3,"primary count and extras scale independently");
  Check(combined[0].itemValue.Metadata.StartsWith("cooked-seed-")&&combined[1].itemValue.Metadata=="jar"&&combined[2].itemValue.Metadata=="can","each rich item remains separate");
  recipe.Prepared=true;recipe.Frozen=combined;int prior=previews;
  Check(TryCombinedOutput(recipe,3,out var retry)&&ReferenceEquals(retry,combined)&&previews==prior,"prepared retry never creates another primary");
  ItemStack[] output;var onlyPrimary=new[]{Empty()};string before=Image(onlyPrimary);
  Check(!TryMillingOutput(onlyPrimary,recipe,3,out output)&&output==null&&Image(onlyPrimary)==before,"partial candidate cannot leak primary into live output");
  var slots=new[]{Empty(),Empty(),Empty(),Empty()};before=Image(slots);
  Check(TryMillingOutput(slots,recipe,3,out output)&&Image(slots)==before,"complete parcel uses detached candidate");
  Check(output.Where(x=>x.itemValue.Type==1).Sum(x=>x.count)==6&&output.Where(x=>x.itemValue.Type==2).Sum(x=>x.count)==3&&output.Where(x=>x.itemValue.Type==3).Sum(x=>x.count)==3,"full parcel conservation");
  var incompatible=new[]{new ItemStack(new ItemValue(1,"different-rich-item"),9),Empty(),Empty()};before=Image(incompatible);
  Check(!TryMillingOutput(incompatible,recipe,3,out output)&&Image(incompatible)==before,"incompatible native metadata cannot absorb primary");
  recipe.Frozen=null;Check(!TryCombinedOutput(recipe,3,out combined)&&previews==prior,"invalid prepared evidence cannot fall back to fresh preview");
  recipe.Prepared=false;recipe.count=int.MaxValue;Check(!TryCombinedOutput(recipe,2,out combined),"primary count overflow refuses");
  Check(!TryCombinedOutput(recipe,0,out combined),"zero portions refuse");
  Console.WriteLine("PASS extracted combined planner/candidate insertion; native/XML/receipt adapters are doubles");
 }
}
