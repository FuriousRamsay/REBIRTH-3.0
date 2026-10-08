using System;using System.Linq; static class ActualPublicationTail {    private static bool CanUse(TileEntityWorkstation station,Recipe recipe,EntityPlayer player)
    {
        // Reading policy is independent of native unlock state and applies again on retained retries.
        if (player == null || recipe == null || !RebirthRecipeDiscoveryRules.Allows(player,recipe.GetName())) return false;
        var block=station.block;if(block==null)return false;
        string areas=block.Properties.Contains("Workstation","CraftingAreaRecipes")
            ?block.Properties.GetString("Workstation","CraftingAreaRecipes"):block.GetBlockName();
        return !string.IsNullOrEmpty(areas)&&areas.Split(',').Any(a=>
            string.Equals(a.Trim()=="player"?string.Empty:a.Trim(),recipe.craftingArea??string.Empty,StringComparison.OrdinalIgnoreCase))&&
            (recipe.craftingToolType==0||station.Tools!=null&&station.Tools.Any(s=>
                s!=null&&!s.IsEmpty()&&s.itemValue.type==recipe.craftingToolType));
    } public static bool Current(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission attempted,Recipe source,TileEntityWorkstation current,RebirthStationSavedPreparationIntent savedIntent){        if(!savedIntent.MatchesCached(owner,attempted)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player))return false;
return true;} public static bool Candidate(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission attempted,Recipe source,TileEntityWorkstation current,RebirthStationSavedPreparationIntent savedIntent){if(!RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player)||!savedIntent.MatchesCached(owner,attempted))return false;return true;}}