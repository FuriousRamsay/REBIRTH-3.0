using System;
using System.Linq;

// Deferred native-context PURE planner checks. Caller supplies actual hydrated
// native owner/item fixtures. These checks confer no callback/source authority.
internal static class RemoteCraftPlanChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    internal static void RecipeRoundTrip(Recipe actualNativeRecipe)
    {
        string before=RebirthRemoteResourceOwnerAction.RecipeImage(actualNativeRecipe);
        Require(RebirthRemoteResourceCraftRecipe.TryRead(before,out var clone)&&RebirthRemoteResourceOwnerAction.RecipeImage(clone)==before,"native recipe roundtrip retains all ingredient metadata");
        Guid original=Guid.NewGuid();string binding=new string('a',64);
        Require(RebirthRemoteResourceCraftRecipe.TryMarked(actualNativeRecipe,original,binding,out var marked),"existing real ingredient supports bounded original marker");
        Require(RebirthRemoteResourceOwnerAction.RecipeImage(actualNativeRecipe)==before,"marker planning never mutates native source recipe");
        Require(marked.itemValueType==actualNativeRecipe.itemValueType&&marked.count==actualNativeRecipe.count&&marked.craftingTime==actualNativeRecipe.craftingTime&&
            marked.ingredients.Count==actualNativeRecipe.ingredients.Count,"no fake ingredient or changed output/count/time");
        Require(RebirthRemoteResourceOwnerAction.IsMarked(marked,original,binding),"one original real ingredient marked");
        Require(!RebirthRemoteResourceCraftRecipe.TryRead(before+"AAAA",out _),"trailing native recipe bytes refused");
    }
    internal static void Run(RebirthCamperEnergyActorImage before,int kitType,int nativeBeltCount)
    {
        string original=RebirthRemoteResourceClientOriginal.ActorImage(before);
        var local=before.Bag.Concat(before.Belt.Take(nativeBeltCount)).Where(stack=>!stack.IsEmpty()&&stack.itemValue.type==kitType&&
            (!stack.itemValue.HasModSlots||!stack.itemValue.HasMods())).ToArray();
        Require(local.Sum(stack=>stack.count)>=2,"actual native fixture requires two consumable local items");
        var exemplar=local[0].itemValue.Clone();
        var need=new[]{new ItemStack(exemplar.Clone(),2),new ItemStack(exemplar.Clone(),1)};
        var remote=new[]{new ItemStack(exemplar.Clone(),1)};
        Require(RebirthRemoteResourceCraftPlan.TryDebit(before,need,remote,nativeBeltCount,out var after,out var removed),"repeated requirement key with split local/remote payment");
        Require(RebirthRemoteResourceClientOriginal.ActorImage(before)==original,"pure original unchanged");
        int BeforeCount()=>before.Bag.Concat(before.Belt.Take(nativeBeltCount)).Where(stack=>stack.itemValue.type==kitType).Sum(stack=>stack.count);
        int afterCount=after.Bag.Concat(after.Belt.Take(nativeBeltCount)).Where(stack=>stack.itemValue.type==kitType).Sum(stack=>stack.count);
        Require(BeforeCount()-afterCount==2&&removed.Sum(stack=>stack.count)==3,"two native local debits plus one retained remote ingredient");
        Require(RebirthStationGridIngredients.IsSameStackSnapshot(before.Cursor,after.Cursor)&&before.BagLocks.SequenceEqual(after.BagLocks),"cursor and native slot locks retained");
        foreach(var stack in removed)Require(RebirthNativeItemCodec.Encode(stack.itemValue)==RebirthNativeItemCodec.Encode(exemplar)||
            local.Any(originalStack=>RebirthNativeItemCodec.Encode(originalStack.itemValue)==RebirthNativeItemCodec.Encode(stack.itemValue)),"removed quality and all metadata came from actual original items");
        Require(!RebirthRemoteResourceCraftPlan.TryDebit(before,need,new[]{new ItemStack(exemplar.Clone(),4)},nativeBeltCount,out _,out _),"paid payload cannot exceed original required amount");
        Require(!RebirthRemoteResourceCraftPlan.TryDebit(before,need,remote,before.Belt.Length+1,out _,out _),"unused native belt capacity cannot become a consumable slot");
    }
}
