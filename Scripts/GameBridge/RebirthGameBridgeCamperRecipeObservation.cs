using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Opt-in read-only qualification through the existing authenticated /console.
public sealed class ConsoleCmdRebirthCamperRecipeObservation : ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbcamperregrecipe"};
    public override string getDescription()=>"Observe current native recipe output and modifier-resolved ingredient payment.";
    public override string getHelp()=>"rbcamperregrecipe exactCanonicalRecipeId batches tier (CodexTest Bridge only).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        int batches,tier;
        if(player==null || sender.RemoteClientInfo!=null || !RebirthGameBridge.IsRunning
            || GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest" || args.Count!=3
            || !System.Text.RegularExpressions.Regex.IsMatch(args[0],@"\A[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}\z")
            || !int.TryParse(args[1],out batches)||batches<1||batches>600
            || !int.TryParse(args[2],out tier)||tier<0||tier>6)
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        try
        {
            Recipe recipe=null;
            foreach(var candidate in XUiM_Recipes.GetRecipes())
                if(candidate!=null&&candidate.GetName()==args[0])
                {if(recipe!=null)throw new InvalidOperationException("ambiguous native recipe");recipe=candidate;}
            if(recipe==null||recipe.count<=0||recipe.ingredients==null||recipe.ingredients.Count==0)
                throw new InvalidOperationException("complete native recipe unavailable");
            var ingredients=new JArray();
            foreach(var ingredient in recipe.ingredients)
            {
                int payment;
                if(ingredient==null||ingredient.itemValue?.ItemClass==null
                    || !RebirthCraftingIngredientQuantity.TryResolveTotal(player,recipe,ingredient,tier,batches,out payment))
                    throw new InvalidOperationException("native ingredient quantity unresolved");
                ingredients.Add(new JObject{["name"]=ingredient.itemValue.ItemClass.GetItemName(),
                    ["perBatch"]=ingredient.count,["payment"]=payment});
            }
            float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,
                _entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)
                throw new InvalidOperationException("native crafting time unresolved");
            var workbenchItem=ItemClass.GetItemClass("workbench") as ItemClassBlock;
            if(workbenchItem==null)throw new InvalidOperationException("current native workbench item unavailable");
            var workbenchBlock=workbenchItem.GetBlockValueFromItemValue(new ItemValue(workbenchItem.Id)).Block;
            if(workbenchBlock?.GetBlockName()!="workbench")throw new InvalidOperationException("current native workbench block unavailable");
            string workbenchAreas=workbenchBlock.Properties.Contains("Workstation","CraftingAreaRecipes")?
                workbenchBlock.Properties.GetString("Workstation","CraftingAreaRecipes"):workbenchBlock.GetBlockName();
            var normalized=new JArray();
            if(!string.IsNullOrEmpty(workbenchAreas))foreach(string area in workbenchAreas.Split(','))
                normalized.Add(area.Trim()=="player"?"":area.Trim());
            SdtdConsole.Instance.Output(new JObject{["schema"]=1,["observationOnly"]=true,
                ["recipe"]=recipe.GetName(),["area"]=recipe.craftingArea,["batches"]=batches,["tier"]=tier,
                ["requiredToolType"]=recipe.craftingToolType,
                ["requiredTool"]=recipe.craftingToolType>0?ItemClass.GetForId(recipe.craftingToolType)?.GetItemName():null,
                ["outputItem"]=ItemClass.GetForId(recipe.itemValueType).GetItemName(),
                ["localizedOutput"]=ItemClass.GetForId(recipe.itemValueType).GetLocalizedItemName(),
                ["outputPerBatch"]=recipe.count,["expectedOutput"]=checked(recipe.count*batches),
                ["ingredients"]=ingredients,["nativeSecondsPerBatch"]=seconds,
                ["workbenchAdmission"]=new JObject{["block"]=workbenchBlock.GetBlockName(),
                    ["rawAreas"]=workbenchAreas,["normalizedAreas"]=normalized}}.ToString(Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR recipe observation: "+error.Message);}
    }
}
