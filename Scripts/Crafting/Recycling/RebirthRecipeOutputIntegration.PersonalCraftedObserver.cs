using System;
using HarmonyLib;

// Actual native callback boundary, independently fenced from item insertion.
[HarmonyPatch(typeof(QuestEventManager),nameof(QuestEventManager.CraftedItem),new[]{typeof(ItemStack)})]
internal static class RebirthRecipeOutputPersonalCraftedItem
{
    private static bool Prefix(QuestEventManager __instance,ItemStack __0,bool __runOriginal,out object __state)
    {
        __state=null;
        if(RebirthRecipeOutputIntegration.RedirectPersonalOutputScrap(__instance,__0,__runOriginal))return false;
        __state=RebirthRecipeOutputIntegration.EnterPersonalOutputCraftedItem(__0,__runOriginal);return true;
    }
    private static Exception Finalizer(Exception __exception,bool __runOriginal,object __state)
    {
        try{RebirthRecipeOutputIntegration.ObservePersonalOutputCraftedItem(__state,__runOriginal,__exception);}
        catch{/* Observation failure retains the entered callback; native exception stays authoritative. */}
        return __exception;
    }
}

[HarmonyPatch(typeof(QuestEventManager),nameof(QuestEventManager.ScrappedItem),new[]{typeof(ItemStack)})]
internal static class RebirthRecipeOutputPersonalScrappedItem
{
    private static void Prefix(ItemStack __0,bool __runOriginal,out object __state)
        =>__state=RebirthRecipeOutputIntegration.EnterPersonalOutputScrappedItem(__0,__runOriginal);
    private static Exception Finalizer(Exception __exception,bool __runOriginal,object __state)
    {
        try{RebirthRecipeOutputIntegration.ObservePersonalOutputCraftedItem(__state,__runOriginal,__exception);}
        catch{/* Unknown native callback remains entered. */}
        return __exception;
    }
}
