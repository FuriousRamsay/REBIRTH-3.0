using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthStationWorkspace : XUiC_WorkstationWindowGroup
{
    internal RebirthCraftingPresentation Presentation;
    internal static XUiC_RebirthStationWorkspace ActiveInstance;
    public override void Init()
    {
        Presentation=RebirthCraftingPresentation.For(this);
        base.Init();
        var list=GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();
        list?.AttachSearchInput(GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput,
            GetChildById("rebirthCraftingRecipeSearchPlaceholder")?.ViewComponent as XUiV_Label);
    }
    public override void OnOpen()
    {
        // Native SetTileEntity selects the physical block name. Repairable POI
        // aliases keep that identity for saves, but share the authored recipe family.
        // Do not use native WorkstationName: it also overwrites the global registry.
        var stationBlock = WorkstationData?.TileEntity?.block;
        string craftingStation = stationBlock?.Properties.GetString("RebirthCraftingStation");
        if (!string.IsNullOrEmpty(craftingStation) && CraftingManager.GetWorkstationData(craftingStation) != null)
            Workstation = craftingStation;
        Presentation.AdvanceCraftIntentEpoch();Presentation.Coordinator.Open();
        // The output panel exposes 28 real native slots. Preserve every existing stack;
        // native workstation serialization already stores the output array's length.
        var output = WorkstationData?.TileEntity?.Output;
        if (output != null && output.Length < 28)
        {
            var expanded = ItemStack.CreateArray(28);
            Array.Copy(output, expanded, output.Length);
            WorkstationData.SetOutputStacks(expanded);
        }
        ExpandQueue(WorkstationData);
        base.OnOpen();
        ActiveInstance = this;
        RefreshToolPreviews();
        new RebirthPersonalCraftingLayoutService(Presentation).Apply(true);
        var header=GetChildById("windowNonPagingHeader")?.ViewComponent;if(header!=null)header.IsVisible=false;
        xui.playerUI.windowManager.Open("toolbelt",false);
        xui.playerUI.windowManager.Open("dragAndDrop",false);
    }
    internal static void ExpandQueue(XUiM_Workstation data)
    {
        var saved = data?.GetRecipeQueueItems();
        if (saved == null || saved.Length >= 16) return;
        var expanded = new RecipeQueueItem[16];
        for (int i=0;i<expanded.Length;i++) expanded[i]=new RecipeQueueItem();
        // Native queues execute from the last slot; retain job order and active position.
        Array.Copy(saved,0,expanded,expanded.Length-saved.Length,saved.Length);
        data.SetRecipeQueueItems(expanded);
    }
    private void RefreshToolPreviews()
    {
        var grid = GetChildByType<XUiC_WorkstationToolGrid>();
        if (grid == null) return;
        var tools = new System.Collections.Generic.List<ItemClass>();
        switch (Workstation)
        {
            case "workbench": grid.requiredTools="FuriousRamsayHammerPliers,FuriousRamsayScrewdriver"; break;
            case "chemistryStation": grid.requiredTools="toolBeaker"; break;
            case "cementMixer": grid.requiredTools="carBattery"; break;
            case "WorkbenchResearchTable001_FR": grid.requiredTools="FuriousRamsayFountainPen"; break;
            case "WorkbenchDistiller001_FR": grid.requiredTools="toolCookingPot"; break;
            case "forge": case "WorkbenchToolbox001_FR": break;
            default: grid.requiredTools=string.Empty; break;
        }
        // Retain authored slot order (forge/tools), then expose requirements from this station's recipes.
        foreach (var name in (grid.requiredTools ?? "").Split(','))
        {
            var tool = ItemClass.GetItemClass(name);
            if (tool != null && !tools.Contains(tool)) tools.Add(tool);
        }
        foreach (var recipe in XUiM_Recipes.GetRecipes())
            if (recipe != null && string.Equals(recipe.craftingArea, Workstation, StringComparison.OrdinalIgnoreCase) && recipe.craftingToolType > 0)
            {
                var tool = ItemClass.GetForId(recipe.craftingToolType);
                if (tool != null && !tools.Contains(tool)) tools.Add(tool);
            }
        var slots = grid.GetItemStackControllers();
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is XUiC_RequiredItemStack slot)
            {
                if (i < tools.Count) slot.SetAllowedItemClassSingle(tools[i]);
                else slot.ClearAllowedItemClasses();
                slot.RequiredType = XUiC_RequiredItemStack.RequiredTypes.ItemClass;
                slot.RequiredItemOnly = true;
                slot.IsDirty = true;
            }
    }
    public override void OnClose()
    {
        Presentation.AdvanceCraftIntentEpoch();Presentation.Coordinator.Close();
        if (ActiveInstance == this) ActiveInstance = null;
        if (xui?.DragAndDropWindow != null) xui.DragAndDropWindow.InMenu = xui.playerUI.windowManager.IsWindowOpen("windowpaging");
        base.OnClose();
    }
}

[Preserve]
public sealed class XUiC_RebirthStationWorkspaceLayout : XUiController
{
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    private RebirthPersonalCraftingLayoutService layout;
    private float nextLayoutCheck;
    public override void Init(){base.Init();layout=new RebirthPersonalCraftingLayoutService(RebirthCraftingPresentation.Resolve(this));}
    public override void OnOpen(){base.OnOpen();ViewComponent.Position=new Vector2i(0,0);ViewComponent.TryUpdatePosition();layout.Apply(true);hud.Maintain(xui);}
    public override void Update(float dt){base.Update(dt);if(IsOpen && Time.unscaledTime >= nextLayoutCheck){nextLayoutCheck=Time.unscaledTime+.25f;layout.Apply(false);}}
    public override void OnClose(){hud.Restore();base.OnClose();}
}


// Keep native source-slot transfer/sounds; restrict this destination to its matching tool cell.
[HarmonyLib.HarmonyPatch(typeof(XUiC_WorkstationToolGrid), nameof(XUiC_WorkstationToolGrid.TryAddTool))]
internal static class RebirthStationMatchingToolTransfer
{
    private static bool Prefix(XUiC_WorkstationToolGrid __instance,ItemClass newItemClass,ItemStack newItemStack,ref bool __result)
    {
        if(!(__instance.windowGroup?.Controller is XUiC_RebirthStationWorkspace))return true;
        __result=false;if(__instance.isLocked||newItemStack==null||newItemStack.IsEmpty())return false;
        foreach(var cell in __instance.GetItemStackControllers())
            if(cell is XUiC_RequiredItemStack slot && slot.allowedItemClasses.Contains(newItemClass))
            {slot.TryStack(newItemStack);if(newItemStack.count==0){__result=true;break;}}
        return false;
    }
}
