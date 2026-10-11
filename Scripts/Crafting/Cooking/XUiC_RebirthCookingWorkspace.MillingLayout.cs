using UnityEngine;

// Mortar presentation uses the crafting arrangement; processing remains owned by the
// existing cooking station controller and its nine physical milling inputs.
public sealed partial class XUiC_RebirthCookingWorkspace
{
    private void MillingRect(string id, int x, int y, int width, int height)
    {
        var c = Find(id); if (c?.ViewComponent == null) return;
        Move(c, x, y);
        if (c is XUiC_RebirthReadableText readable) readable.SetBounds(width,height);
        else if (c.ViewComponent.Size.x != width || c.ViewComponent.Size.y != height)
            c.ViewComponent.Size = new Vector2i(width, height);
        c.ViewComponent.TryUpdatePosition();
    }
    private void LayoutMilling()
    {
        MillingRect("result",398,-70,956,300);
        MillingRect("resultBg",0,0,956,300);
        MillingRect("resultFrame",0,0,956,300);
        MillingRect("resultLine",0,-38,956,2);
        MillingRect("resultIcon",18,-54,112,112);
        MillingRect("resultHeroBg",14,-48,120,132);
        MillingRect("resultName",146,-52,426,32);
        MillingRect("resultDescriptionReader",146,-92,342,110);
        MillingRect("resultDescription",0,0,312,110);
        var reader = Find("resultDescriptionReader");
        var viewport = reader?.GetChildById("readableTextViewport")?.ViewComponent;
        if (viewport != null) viewport.Size = new Vector2i(318,110);
        MillingRect("nonFoodStats",590,-54,350,168);
        MillingRect("cook",16,-248,440,36);
        var action = Find("cook");
        foreach (var child in action.Children)
        {
            if (child.ViewComponent == null) continue;
            if (child == action.GetChildById("actionFill") || child == action.GetChildById("actionFrame")) child.ViewComponent.Size = new Vector2i(440,36);
            if (child == action.GetChildById("label")) { child.ViewComponent.Position = new Vector2i(220,-18); child.ViewComponent.Size = new Vector2i(370,24); }
        }
        MillingRect("cookShortcut",408,-18,24,24);
        MillingRect("cookStatus",16,-286,924,24);
        MillingRect("ingredients",398,-380,956,280);
        MillingRect("ingredientsBg",0,0,956,280);
        MillingRect("ingredientsFrame",0,0,956,280);
        MillingRect("ingredientsLine",0,-38,956,2);
        for (int i=0;i<9;i++)
        {
            MillingRect("ingredient"+i,14+(i%2)*464,-48-(i/2)*44,450,42);
            var slot=slots[i]?.ViewComponent;
            if(slot?.UiTransform!=null)slot.UiTransform.localScale=new Vector3(42f/86f,42f/86f,1);
            MillingRect("ghost"+i,21,-21,36,36);
            MillingRect("need"+i,384,-8,62,26);
            MillingRect("millingIngredientName"+i,48,-4,330,32);
        }
        MillingRect("batchLabel",148,90,100,26);
        MillingRect("batchMin",270,74,28,30);
        MillingRect("batchMinus",305,74,28,30);
        MillingRect("batchInput",327,90,68,26);
        MillingRect("batchPlus",418,74,28,30);
        MillingRect("batchMax",455,74,28,30);
        MillingRect("pullIngredients",488,62,344,34);
        MillingRect("ingredientGuide",14,-267,924,28);
        MillingRect("rebirthCraftingInventoryRegion",398,-670,956,222);
        MillingRect("rebirthCraftingInventoryRegionBg",0,0,956,222);
        MillingRect("rebirthCraftingInventoryHeaderRule",0,-38,956,2);
        MillingRect("rebirthCraftingInventoryScroll",12,-46,932,168);
        // A milling station has no fuel or tool requirement. Its station heading remains
        // above the processing queue, in the same place as tools on other workstations.
        MillingRect("rebirthCraftingQueueRegion",1364,-300,500,592);
        MillingRect("rebirthCraftingQueueRegionBg",0,0,500,592);
        MillingRect("rebirthCraftingQueueController",0,0,500,592);
        MillingRect("referenceTitle",980,-48,472,26);
        MillingRect("bookReferenceIcon",980,-80,52,52);
        MillingRect("referenceStatus",1040,-80,168,80);
        MillingRect("magazineReferenceIcon",1214,-80,52,52);
        MillingRect("magazineReferenceText",1274,-80,170,80);
        MillingRect("prepStatus",980,-156,472,26);
        MillingRect("prepare",980,-185,472,34);
        var preparation = Find("prepare");
        foreach (var id in new[] { "actionFill", "actionFrame" })
            if (preparation?.GetChildById(id)?.ViewComponent is XUiView fill) fill.Size = new Vector2i(472,34);
        if (preparation?.GetChildById("label")?.ViewComponent is XUiView caption)
        { caption.Position = new Vector2i(236,-17); caption.Size = new Vector2i(410,24); }
        MillingRect("prepTrack",980,-221,472,6);
        MillingRect("prepProgress",980,-221,472,6);
        MillingRect("referenceBenefits",980,-144,472,18);
        Text("resultTitle",Localization.Get("xuiRebirthSelectedRecipe"));
        Text("ingredientsTitle",Localization.Get("xuiRebirthRequirements"));
        Text("batchLabel",Localization.Get("xuiRebirthBatchSize"));
        Hide(Find("resultDivider"));
        (Find("rebirthCraftingInventoryScroll") as XUiC_RebirthCraftingInventoryScroll)?.ApplyLayoutGeometry();
        Text("rebirthCraftingTabCraftingLabel", Localization.Get("xuiCrafting"));
    }
}
