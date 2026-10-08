using System;
using System.Collections.Generic;
public class ItemValue {public int type,Variant;public bool HasModSlots,Modded;public bool HasMods(){return Modded;}}
public class ItemStack {public ItemValue itemValue;public int count;public bool IsEmpty(){return count<=0;}}
public static class RebirthCraftingIngredientQuantity {public static bool SameRecipeIdentity(ItemValue a,ItemValue b){return a.type==b.type&&a.Variant==b.Variant;}}
public static class EligibleSourceFixture {    public static int CountEligibleItems(List<ItemStack> stacks, ItemValue required, bool qualityIngredient)
    {
        long count = 0;
        for (int i = 0; stacks != null && i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue == null) continue;
            // Native quality admission matches item type, not quality/wear metadata.
            if (qualityIngredient ? stack.itemValue.type != required.type :
                !RebirthCraftingIngredientQuantity.SameRecipeIdentity(stack.itemValue, required)) continue;
            if (qualityIngredient)
            {
                if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
                count++;
            }
            else count += stack.count;
            if (count >= int.MaxValue) return int.MaxValue;
        }
        return (int)count;
    }

public static string Run(){
var required=new ItemValue{type=1,Variant=0};
var values=new List<ItemStack>{new ItemStack{count=9,itemValue=new ItemValue{type=1,Variant=2}},new ItemStack{count=3,itemValue=new ItemValue{type=1,Variant=3}},new ItemStack{count=1,itemValue=new ItemValue{type=1,HasModSlots=true,Modded=true}},new ItemStack{count=2,itemValue=new ItemValue{type=2}}};
if(CountEligibleItems(values,required,true)!=2)throw new Exception("quality admission mismatch");
if(CountEligibleItems(values,required,false)!=1)throw new Exception("non-quality identity changed");
if(CountEligibleItems(null,required,true)!=0)throw new Exception("null list");
values.Add(new ItemStack{count=0,itemValue=required});if(CountEligibleItems(values,required,true)!=2)throw new Exception("empty included");
return "PASS4 actual count-method checks with explicit item/identity doubles";
}}
