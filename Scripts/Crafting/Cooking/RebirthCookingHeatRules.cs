using System;
using System.Collections.Generic;
using System.Linq;

public static class RebirthCookingHeatRules
{
    public static float Grace { get; private set; } = 120;
    private static float debugBurn;
    public static float BurnAfter(string method)=>debugBurn>0?debugBurn:method=="Soup"?900:600;
    public static void SetDebugTimes(float grace,float burn) { Grace=grace;debugBurn=burn; }
    public static void ResetDebugTimes() { Grace=120;debugBurn=0; }
    public static float Retention(float overdue,string method)=>Math.Max(.05f,1-.95f*Math.Max(0,overdue-Grace)/(BurnAfter(method)-Grace));
    public static float FoodStat(string stat,float original,float overdue,string method)
    {
        float retained=original>0?original*Retention(overdue,method):original;
        if(overdue<BurnAfter(method))return retained;
        switch(stat)
        {
            case "nutrition":return Math.Min(2f,Math.Max(0f,retained));
            case "water":case "energy":return 0f;
            case "comfort":return -5f;
            default:return retained;
        }
    }
    public static float Duration(string method,int types)=> (method=="Baked"?90:method=="Soup"?75:45)+Math.Max(0,types-3)*10;
    public static void Advance(float duration,ref float elapsed,ref float overdue,float delta,bool heated,bool cold,string method)
    {
        if(delta<=0||overdue>=BurnAfter(method)||!heated&&!cold)return;
        float used=Math.Min(delta,Math.Max(0,duration-elapsed));
        elapsed=Math.Min(duration,elapsed+used);
        if(heated&&!cold&&delta>used)overdue=Math.Min(BurnAfter(method),overdue+delta-used);
    }
    public static string Method(Recipe recipe)=>recipe.craftingToolType==ItemClass.GetItem("FuriousRamsayBakingPan").type?"Baked":recipe.craftingToolType==ItemClass.GetItem("rebirthCookingFryingPan").type?"Pan":"Soup";
    public static string BurnMethod(Recipe recipe)=>recipe.craftingToolType==ItemClass.GetItem("toolCookingGrill").type?"Pan":Method(recipe);
    public static bool ValidIngredients(List<ItemStack> inputs,string method,out string message)
    {
        message="";
        if(method=="Soup")return true;
        float liquid=0,solid=0;
        foreach(var input in inputs)
        {
            if(!RebirthConsumableResolver.TryResolve(input.itemValue,out var food)) { solid+=input.count;continue; }
            bool watery=food.IsDrink || food.FoodWaterMl>=200&&food.FoodWaterMl>food.NutritionUnits*15;
            if(watery)liquid+=input.count;else solid+=input.count;
        }
        if(method=="Pan"&&liquid>0){message="Use a cooking pot for liquids. A skillet accepts solid ingredients.";return false;}
        if(method=="Baked"&&liquid>solid/4f){message="Baking mixtures need at least four solid ingredients per liquid measure.";return false;}
        return true;
    }
}
