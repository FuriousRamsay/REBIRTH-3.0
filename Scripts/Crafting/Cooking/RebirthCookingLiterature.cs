using HarmonyLib;

public static class RebirthCookingLiterature
{
    public static bool IsCard(ItemValue item) => IsCard(item?.ItemClass);
    public static bool IsCard(ItemClass item) => item != null &&
        (RebirthConsumableResolver.Get(item,"RebirthCookingLiteratureKind","")=="card" ||
         RebirthConsumableResolver.Get(item,"RebirthInstantRecipe","")=="true");
    public static string SourceLiterature(ItemStack stack)
    {
        string id = stack != null && !stack.IsEmpty() ? stack.itemValue?.ItemClass?.GetItemName() : null;
        if (string.IsNullOrEmpty(id)) return null;
        RebirthAudiobookDefinition audio;
        if (RebirthProgressionRuntimeConfig.TryGetAudiobook(id, out audio) && audio != null)
            id = audio.SourceLiteratureId;
        return RebirthProgressionRuntimeConfig.TryGetLiterature(id, out _) ? id : null;
    }
    public static bool IsLiterature(ItemStack stack) => SourceLiterature(stack) != null;
}
[HarmonyPatch(typeof(XUiBindingHelper),nameof(XUiBindingHelper.GetItemTypeIconValue))]
public static class RebirthCookingLiteratureIcon
{
    static void Postfix(XUi xui,ItemStack itemStack,ref string __result)
    {
        string sourceId=RebirthCookingLiterature.SourceLiterature(itemStack);
        if(sourceId!=null)
        {
            var player=xui?.playerUI?.entityPlayer;
            ItemClass sourceItem=ItemClass.GetItemClass(sourceId,false);
            bool recipeReference=RebirthCookingLiterature.IsCard(itemStack.itemValue)||RebirthCookingLiterature.IsCard(sourceItem);
            bool learned=RebirthProgressionRuntimeConfig.TryGetLiterature(sourceId,out var definition)&&RebirthLiteratureService.IsAlreadyCompleted(player,definition);
            if(!learned && recipeReference && definition!=null &&
                string.Equals(definition.Kind,"discovery",System.StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(definition.KnowledgeId))
                learned=RebirthKnowledgeService.HasKnowledge(player,definition.KnowledgeId);
            // Known recipes display learned without fabricating actual-reading markers.
            if(!learned && recipeReference)
            {
                // Learned recipe icons do not fabricate actual-study markers.
                string covered=RebirthConsumableResolver.Get(sourceItem??itemStack.itemValue.ItemClass,"RebirthCookingCoveredRecipes","");
                if(!string.IsNullOrEmpty(covered))
                {
                    learned=true;
                    foreach(string name in covered.Split(','))
                    {
                        var recipe=RebirthCookingRecipeLookup.Find(name.Trim());
                        if(recipe==null||!RebirthCookingCatalogue.Known(player,recipe)){learned=false;break;}
                    }
                }
            }
            __result=learned?"book_read":"book";
        }
    }
}
[HarmonyPatch(typeof(XUiBindingHelper),nameof(XUiBindingHelper.GetHasItemTypeIconValueInternal))]
public static class RebirthCookingLiteratureHasIcon
{
    static void Postfix(ItemStack itemStack,ref bool __result)
    {if(RebirthCookingLiterature.IsLiterature(itemStack))__result=true;}
}

[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.Update))]
public static class RebirthCookingLiteratureIconRefresh
{
    private sealed class State { public float Next; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,State> states=new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,State>();
    static void Postfix(XUiC_ItemStack __instance)
    {
        var state=states.GetValue(__instance,_=>new State());
        if(UnityEngine.Time.realtimeSinceStartup<state.Next)return;
        state.Next=UnityEngine.Time.realtimeSinceStartup+.5f;
        var stack=__instance.ItemStack;
        if(!RebirthCookingLiterature.IsLiterature(stack))return;
        if(__instance.GetChildById("itemtypeicon")?.ViewComponent is XUiV_Sprite icon)
        {
            string sprite="ui_game_symbol_"+XUiBindingHelper.GetItemTypeIconValue(__instance.xui,stack);
            if(icon.SpriteName!=sprite)icon.SpriteName=sprite;
            if(!icon.IsVisible)icon.IsVisible=true;
        }
    }
}
