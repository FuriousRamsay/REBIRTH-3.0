using System;
using System.Collections.Generic;

// Existing refund native receipt brackets these effects. This helper never
// authorizes an inventory/counter mutation or a parsed-record replay.
internal static class RebirthCamperStationPartialCancellationRewards
{
    internal static bool TryBuild(RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,int actor,
        out CraftCompleteData reward,out Recipe trainingRecipe,out int cycles)
    {
        reward=null;trainingRecipe=null;cycles=0;
        try
        {
            if(admission==null||intent==null||intent.IsCompletion||actor<=0||
                !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _))return false;
            if(!admission.IsPerBatch)return true;
            cycles=admission.OriginalBatches-intent.RemainingBatches;
            if(cycles<0||cycles>=admission.OriginalBatches)return false;
            if(cycles==0)return true;
            if(!admission.TryMaterialize(XUiM_Recipes.GetRecipes(),out var queued,out _,out _)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,XUiM_Recipes.GetRecipes(),out var source))return false;
            long outputs=(long)queued.count*cycles,xp=(long)queued.craftExpGain*cycles;
            if(outputs<1||outputs>int.MaxValue||xp<0||xp>int.MaxValue)return false;
            var output=new ItemValue(queued.itemValueType,false);output.Quality=(byte)queued.craftingTier;
            reward=new CraftCompleteData(actor,new ItemStack(output,(int)outputs),source.GetName(),string.Empty,(int)xp,checked((ushort)cycles));
            if(!TryBuildTrainingRecipe(admission,queued,source,out trainingRecipe))return false;
            return true;
        }
        catch{return false;}
    }
    internal static bool TryBuildTrainingRecipe(RebirthStationGridAdmission admission,Recipe queued,Recipe source,out Recipe trainingRecipe)
    {
        trainingRecipe=null;try{if(admission==null||!admission.IsPerBatch||admission.OriginalBatches<1||queued==null||source==null)return false;
            trainingRecipe=new Recipe{itemValueType=source.itemValueType,count=source.count,craftingTime=source.craftingTime,
                craftingArea=source.craftingArea,craftingTier=queued.craftingTier,UseIngredientModifier=false,tags=source.tags};
            var units=new Dictionary<int,ItemStack>();
            foreach(var fragment in queued.ingredients)
            {
                var paid=RebirthStationGridQueue.RefundSnapshot(fragment);
                if(units.TryGetValue(paid.itemValue.type,out var total))
                {if((long)total.count+paid.count>int.MaxValue)return false;total.count+=paid.count;}
                else units.Add(paid.itemValue.type,paid);
            }
            foreach(var paid in units.Values)
            {
                if(paid.count%admission.OriginalBatches!=0)return false;
                paid.count/=admission.OriginalBatches;trainingRecipe.ingredients.Add(paid);
            }
            return true;}catch{trainingRecipe=null;return false;}
    }
    internal static bool ApplyNative(EntityPlayerLocal player,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        string refundReceipt)
    {
        if(player?.Buffs==null||player.Buffs.GetCustomVar(refundReceipt)!=2f||
            !TryBuild(admission,intent,player.entityId,out var reward,out _,out _))return false;
        if(reward!=null)player.GiveExp(reward);
        return player.Buffs.GetCustomVar(refundReceipt)==2f;
    }
    // Call only after the existing primary native refund receipt1 witness.
    // Survivor's existing event receipt makes owner-flush retries idempotent.
    internal static bool TryTrain(EntityPlayer player,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,Guid delivery)
    {
        if(delivery==Guid.Empty||!TryBuild(admission,intent,player?.entityId??0,out _,out var recipe,out int cycles))return false;
        if(cycles==0)return true;
        string skill=RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
        if(string.IsNullOrEmpty(skill))return true;
        var model=RebirthCraftTrainingRules.BuildModel(player,recipe,skill);
        if(model.Valid&&model.Raw==0f&&string.IsNullOrEmpty(model.SkillId))
        {model.SkillId=skill;model.RecipeId=recipe.GetName();model.Outputs=recipe.count;}
        return RebirthSkillAwardService.TryAwardCraft(player,skill,model,cycles,RebirthCookingBatch.SkillMultiplier(recipe.GetName()),
            "craft:"+skill+":"+recipe.GetName(),"camper.cancel.v1:"+delivery.ToString("N"));
    }
}
