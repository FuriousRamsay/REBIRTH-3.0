using System;
using System.Collections.Generic;

// Detached server pre-enqueue projection. This does not grant admission or claim completion.
internal static class RebirthTheorySoloNativeRecipeProjection
{
    internal static bool TryCreate(EntityPlayer player, Recipe original, int tier, int batches,
        IList<ItemStack> bag, IList<ItemStack> belt, out Recipe queued,
        out RebirthTheorySoloNativePaymentPlan payment, out RebirthCraftTrainingRules.Model training)
    {
        queued=null;payment=null;training=new RebirthCraftTrainingRules.Model();
        if(player==null||original==null||!string.IsNullOrEmpty(original.craftingArea)||tier<0||tier>255||batches<1||batches>10000||original.ingredients==null||original.ingredients.Count<1||original.ingredients.Count>64)return false;
        try
        {
            float modifier=XUiM_Recipes.GetCraftingInputModifier(original);
            if(float.IsNaN(modifier)||float.IsInfinity(modifier)||modifier<=0f)return false;
            var demands=new List<ItemStack>();bool quality=false;
            foreach(var ingredient in original.ingredients)
            {
                if(ingredient==null||ingredient.itemValue==null||ingredient.itemValue.type<=0||ingredient.itemValue.ItemClass==null||ingredient.count<=0)return false;
                int amount=ingredient.count,preModifier=amount;
                if(original.UseIngredientModifier)
                {
                    float adjusted=EffectManager.GetValue(PassiveEffects.CraftingIngredientCount,null,ingredient.count,player,original,FastTags<TagGroup.Global>.Parse(ingredient.itemValue.ItemClass.GetItemName()),calcEquipment:true,calcHoldingItem:true,calcProgression:true,calcBuffs:true,calcChallenges:true,tier);
                    if(float.IsNaN(adjusted)||float.IsInfinity(adjusted)||adjusted<0f||adjusted>=int.MaxValue)return false;
                    // Native hasItems truncates BEFORE multiplying by the input modifier.
                    amount=(int)adjusted;preModifier=amount;
                    if(amount>0)
                    {
                        double scaled=(double)amount*modifier;if(scaled>=int.MaxValue)return false;
                        amount=(int)((float)amount*modifier);
                        if(XUiM_Recipes.CraftingInputModifier>0f)amount=Math.Max(1,amount);
                    }
                }
                bool hasQuality=ingredient.itemValue.HasQuality;quality|=hasQuality;
                if(hasQuality&&preModifier==0)amount=1;
                // A free/malformed demand cannot be evidence of material work.
                if(amount<=0)return false;
                demands.Add(new ItemStack(ingredient.itemValue.Clone(),amount));
            }
            if(!RebirthTheorySoloNativePaymentPlan.TryCreate(demands,batches,bag,belt,out payment))return false;
            float outputModifier=original.tags.Test_AnySet(XUiM_Recipes.SandboxIgnoreTag)?1f:XUiM_Recipes.CraftingOutputModifier;
            float output=EffectManager.GetValue(PassiveEffects.CraftingOutputCount,null,original.count,player,original,original.tags)*outputModifier;
            float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,null,original.craftingTime,player,original,original.tags)*XUiM_Recipes.CraftingTimeModifier;
            if(float.IsNaN(output)||float.IsInfinity(output)||output>=int.MaxValue||float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
            queued=new Recipe{itemValueType=original.itemValueType,count=(int)Math.Max(output,1f),craftingArea=original.craftingArea,craftExpGain=original.craftExpGain,craftingTime=Math.Max(seconds,0f),craftingToolType=original.craftingToolType,craftingTier=tier,tags=original.tags};
            // Native rewrites the WHOLE paid batch for any quality-bearing ingredient.
            queued.AddIngredients(quality?payment.PaidSnapshot():demands);
            training=RebirthCraftTrainingRules.BuildModel(player,queued,RebirthServiceCraftSkillService.ClassifyRecipe(original));
            return training.Valid&&!float.IsNaN(training.Raw)&&!float.IsInfinity(training.Raw)&&training.Raw>0f&&!float.IsNaN(training.Difficulty)&&!float.IsInfinity(training.Difficulty)&&training.Difficulty>=0f&&training.Difficulty<=100f;
        }
        catch {queued=null;payment=null;training=new RebirthCraftTrainingRules.Model();return false;}
    }
}