using System;

// Current authored water jar grades, shared by recipe eligibility and refill plans.
// No distilled item ID is fabricated: mineralWater is the highest authored grade.
internal static class RebirthWaterQualityPolicy
{
    internal static bool IsWaterJar(string name)=>name=="drinkJarRiverWater"||name=="drinkJarBoiledWater"||name=="drinkJarPureMineralWater";
    private static int ClassGrade(string name)=>name=="drinkJarRiverWater"?0:name=="drinkJarBoiledWater"?1:name=="drinkJarPureMineralWater"?2:-1;
    internal static bool SatisfiesRecipeGrade(ItemValue actual,ItemValue required)
    {
        int need=ClassGrade(required?.ItemClass?.GetItemName());if(need<0)return true;
        int label=ClassGrade(actual?.ItemClass?.GetItemName());
        if(label<0||!RebirthConsumableResolver.TryResolve(actual,out var definition)||!definition.IsDrink)return false;
        string profile=RebirthLiquidContainerService.GetLiquidProfile(actual,definition);
        int purity=profile=="unsafeWater"?0:profile=="cleanWater"?1:profile=="mineralWater"?2:-1;
        return purity>=0&&Math.Min(label,purity)>=need;
    }
}
