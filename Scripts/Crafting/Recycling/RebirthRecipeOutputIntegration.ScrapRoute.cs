using System;

public static partial class RebirthRecipeOutputIntegration
{
    internal static bool HasNativeScrapOutputs(Recipe recipe)
        =>IsFrozenNativeScrapOutput(recipe)&&RebirthRecipeOutputs.HasOutputs(recipe);
    internal static bool IsFrozenNativeScrapOutput(Recipe recipe)
        =>recipe?.IsScrap==true&&recipe.ingredients!=null&&recipe.ingredients.Count>0&&
            recipe.ingredients[0]!=null&&!recipe.ingredients[0].IsEmpty()&&recipe.ingredients[0].itemValue?.ItemClass!=null&&
            !string.IsNullOrEmpty(recipe.ingredients[0].itemValue.ItemClass.GetItemName());
    internal static bool MatchesNativeOutputScrappedName(Recipe recipe,string scrapped)
        =>recipe!=null&&(recipe.IsScrap?HasNativeScrapOutputs(recipe)&&scrapped==recipe.ingredients[0].itemValue.ItemClass.GetItemName():scrapped==string.Empty);
}
