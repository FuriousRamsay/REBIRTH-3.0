using System;
using System.Collections.Generic;
using System.Linq;

// Detached original paid-fragment row. Total fragments are NOT per-batch
// ingredients; only the purpose-specific cancellation reader may refund them.
internal static class RebirthRemoteResourcePlacedStationPaidRow
{
    internal const string JobKey="rebirth.remote.placed.job",SourceKey="rebirth.remote.placed.source",BindingKey="rebirth.remote.placed.binding";
    private const string BatchesKey="rebirth.remote.placed.batches",ImageKey="rebirth.remote.placed.image";
    internal static bool TryCreate(Guid job,Guid source,string binding,Recipe nativeTemplate,IReadOnlyList<ItemStack> paid,
        int batches,int tier,int entityId,bool isCrafting,out RecipeQueueItem row,string frozenOutputImage=null)
    {
        row=null;
        try
        {
            if(job==Guid.Empty||source==Guid.Empty||binding?.Length!=64||binding.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||
                nativeTemplate==null||paid==null||paid.Count<1||paid.Count>short.MaxValue||batches<1||batches>short.MaxValue||
                tier<0||tier>byte.MaxValue||nativeTemplate.count<1||nativeTemplate.count>short.MaxValue||
                float.IsNaN(nativeTemplate.craftingTime)||float.IsInfinity(nativeTemplate.craftingTime)||nativeTemplate.craftingTime<0)return false;
            // Reuse the exact native recipe codec, then retain native-only
            // runtime fields explicitly. No live recipe or stack is modified.
            if(!RebirthRemoteResourceCraftRecipe.TryRead(RebirthRemoteResourceOwnerAction.RecipeImage(nativeTemplate),out var recipe))return false;
            recipe.craftingToolType=nativeTemplate.craftingToolType;recipe.craftingTier=tier;recipe.tags=nativeTemplate.tags;
            recipe.UseIngredientModifier=false;recipe.unlockExpGain=nativeTemplate.unlockExpGain;recipe.Effects=nativeTemplate.Effects;
            recipe.materialBasedRecipe=nativeTemplate.materialBasedRecipe;recipe.tooltip=nativeTemplate.tooltip;
            recipe.wildcardForgeCategory=nativeTemplate.wildcardForgeCategory;recipe.wildcardCampfireCategory=nativeTemplate.wildcardCampfireCategory;
            recipe.IsLearnable=nativeTemplate.IsLearnable;recipe.IsTrackable=nativeTemplate.IsTrackable;
            recipe.isQuest=nativeTemplate.isQuest;recipe.isChallenge=nativeTemplate.isChallenge;recipe.IsTracked=nativeTemplate.IsTracked;
            recipe.ingredients.Clear();
            foreach(var fragment in paid)
            {
                if(fragment==null||fragment.IsEmpty()||fragment.count<1||fragment.count>ushort.MaxValue)return false;
                var clone=fragment.Clone();string original=RebirthNativeItemCodec.Encode(clone.itemValue);
                // An input's inherited frozen recipe output marker is item
                // metadata, never authority for this new recipe's outputs.
                if(recipe.ingredients.Count==0)RebirthRecipeOutputs.ClearFrozen(clone.itemValue);
                int additional=recipe.ingredients.Count==0?6:1;
                if(!RebirthNativeItemCodec.TryDecode(original,out _)||clone.itemValue.Metadata?.Count>byte.MaxValue-additional||
                    new[]{JobKey,SourceKey,BindingKey,BatchesKey,ImageKey}.Any(key=>clone.itemValue.HasMetadata(key)))return false;
                clone.itemValue.SetMetadata(ImageKey,original);
                if(recipe.ingredients.Count==0)
                {clone.itemValue.SetMetadata(JobKey,job.ToString("N"));clone.itemValue.SetMetadata(SourceKey,source.ToString("N"));clone.itemValue.SetMetadata(BindingKey,binding);clone.itemValue.SetMetadata(BatchesKey,batches);}
                recipe.ingredients.Add(clone);
            }
            if(frozenOutputImage==null?!RebirthRecipeOutputs.Freeze(recipe):!RebirthRecipeOutputs.TryApplyFrozenOutputs(recipe,frozenOutputImage))return false;
            row=new RecipeQueueItem{Recipe=recipe,Multiplier=(short)batches,Quality=(byte)tier,StartingEntityId=entityId,
                CraftingTimeLeft=recipe.craftingTime,OneItemCraftTime=recipe.craftingTime,IsCrafting=isCrafting};return true;
        }
        catch{row=null;return false;}
    }
    internal static bool TryRemaining(RecipeQueueItem row,Guid job,Guid source,string binding,out List<ItemStack> remaining)
    {
        remaining=null;
        try
        {
            var fragments=row?.Recipe?.ingredients;if(fragments==null||fragments.Count<1||fragments.Count>short.MaxValue)return false;
            var marker=fragments[0].itemValue;
            if(!marker.TryGetMetadata(JobKey,out string actualJob)||actualJob!=job.ToString("N")||!marker.TryGetMetadata(SourceKey,out string actualSource)||actualSource!=source.ToString("N")||
                !marker.TryGetMetadata(BindingKey,out string actualBinding)||actualBinding!=binding||!marker.TryGetMetadata(BatchesKey,out int batches)||batches<1||
                row.Multiplier<1||row.Multiplier>batches)return false;
            var totals=fragments.GroupBy(item=>item.itemValue.type).ToDictionary(group=>group.Key,group=>group.Sum(item=>(long)item.count));
            if(totals.Any(pair=>pair.Value<1||pair.Value%batches!=0))return false;
            var consumed=totals.ToDictionary(pair=>pair.Key,pair=>(pair.Value/batches)*(batches-row.Multiplier));var result=new List<ItemStack>();
            foreach(var fragment in fragments)
            {
                if(fragment.count<1||fragment.count>ushort.MaxValue||!fragment.itemValue.TryGetMetadata(ImageKey,out string original)||!RebirthNativeItemCodec.TryDecode(original,out var value)||value.type!=fragment.itemValue.type)return false;
                int used=(int)Math.Min(fragment.count,consumed[value.type]);consumed[value.type]-=used;
                if(used<fragment.count)result.Add(new ItemStack(value,fragment.count-used));
            }
            remaining=result;return true;
        }
        catch{return false;}
    }
    // Cancellation must supply the immutable fully validated original paid
    // row, not merely its marker/counts. Only actual native progress fields may
    // differ. This is a detached plan; it grants no refund or slot permission.
    internal static bool TryRemainingFromOriginal(RecipeQueueItem actual,RecipeQueueItem savedOriginal,Guid job,Guid source,string binding,out List<ItemStack> remaining)
    {
        remaining=null;
        try
        {
            if(actual?.Recipe==null||savedOriginal?.Recipe==null||actual.RepairItem!=null||savedOriginal.RepairItem!=null||
                actual.AmountToRepair!=0||savedOriginal.AmountToRepair!=0||savedOriginal.Multiplier<1||actual.Multiplier<1||actual.Multiplier>savedOriginal.Multiplier||
                float.IsNaN(actual.CraftingTimeLeft)||float.IsInfinity(actual.CraftingTimeLeft)||actual.CraftingTimeLeft<0||
                actual.CraftingTimeLeft>savedOriginal.OneItemCraftTime||savedOriginal.OneItemCraftTime<0||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(RebirthRemoteResourcePlacedStationQueue.Encode(new[]{actual}),out var detached))return false;
            var normalized=detached[0];normalized.Multiplier=savedOriginal.Multiplier;normalized.CraftingTimeLeft=savedOriginal.CraftingTimeLeft;normalized.IsCrafting=savedOriginal.IsCrafting;
            if(RebirthRemoteResourcePlacedStationQueue.Encode(new[]{normalized})!=RebirthRemoteResourcePlacedStationQueue.Encode(new[]{savedOriginal})||
                !savedOriginal.Recipe.ingredients[0].itemValue.TryGetMetadata(BatchesKey,out int originalBatches)||originalBatches!=savedOriginal.Multiplier)return false;
            return TryRemaining(actual,job,source,binding,out remaining);
        }
        catch{remaining=null;return false;}
    }
}
