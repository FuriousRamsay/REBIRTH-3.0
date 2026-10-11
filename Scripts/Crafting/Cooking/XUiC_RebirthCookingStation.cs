using UnityEngine.Scripting;

#nullable disable

/// <summary>Retains native station locking, fuel, output, persistence and queue transactions.</summary>
[Preserve]
public sealed class XUiC_RebirthCookingStation : XUiC_WorkstationWindowGroup
{
    public bool IsMilling => Workstation == "WorkbenchMortarPestle001_FR";
    public bool UsesSharedMillingPresentation => GetChildById("rebirthMillingProcessor") != null;
    private RebirthCraftingPresentation sharedPresentation;
    private float cookingSync;
    private XUiC_RecipeStack[] cookingEntries;
    private int displayedBurnSeconds = -1;
    private string displayedBurnTime;
    public override void Init()
    {
        if(UsesSharedMillingPresentation) sharedPresentation=RebirthCraftingPresentation.For(this);
        // The cooking screen deliberately has no native RecipeList/CraftingInfo topology.
        if (viewComponent != null) viewComponent.InitView();
        for (int i = 0; i < children.Count; i++)
        {
            children[i].AutoBindComponents();
            children[i].Init();
            children[i].AutoBindEvents();
        }
        curInputStyle = Platform.PlatformManager.NativePlatform.Input.CurrentInputStyle;
        toolWindow = GetChildByType<XUiC_WorkstationToolGrid>();
        fuelWindow = GetChildByType<XUiC_WorkstationFuelGrid>();
        outputWindow = GetChildByType<XUiC_WorkstationOutputGrid>();
        craftingQueue = GetChildByType<XUiC_CraftingQueue>();
        // Native queue children remain stable; recipes move between those controllers.
        // GetRecipesToCraft allocates a copy, so take it once after queue initialization.
        cookingEntries = craftingQueue?.GetRecipesToCraft();
        ((XUiC_CraftingWindowGroup)this).craftingQueue = craftingQueue;
        burnTimeLeft = GetChildById("burnTimeLeft")?.ViewComponent as XUiV_Label;
        if(sharedPresentation!=null)
        {
            recipeList=GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();
            craftCountControl=GetChildByType<XUiC_RecipeCraftCount>();
            (recipeList as XUiC_RebirthCraftingRecipeCatalogue)?.AttachSearchInput(GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput,
                GetChildById("rebirthCraftingRecipeSearchPlaceholder")?.ViewComponent as XUiV_Label);
        }
    }
    public override void OnOpen()
    {
        sharedPresentation?.AdvanceCraftIntentEpoch();sharedPresentation?.Coordinator.Open();
        displayedBurnSeconds = -1;
        RebirthCookingNavigation.Attach(this);
        if(IsMilling && WorkstationData?.TileEntity?.Output is ItemStack[] saved && saved.Length<28)
        {var expanded=ItemStack.CreateArray(28);System.Array.Copy(saved,expanded,saved.Length);WorkstationData.SetOutputStacks(expanded);}
        if(IsMilling)XUiC_RebirthStationWorkspace.ExpandQueue(WorkstationData);
        base.OnOpen();
        // Previous versions exposed this hidden grid as a Shift-click destination. Recover its
        // contents through native inventory overflow handling, and unregister the destination.
        if(!IsMilling)xui.CurrentWorkstationOutputGrid=null;
        if (IsMilling) { xui.CurrentWorkstationFuelGrid = null; xui.CurrentWorkstationToolGrid = null; }
        if(outputWindow!=null && !IsMilling)
        {
            var stranded=outputWindow.GetSlots();
            var empty=new ItemStack[stranded.Length];
            for(int i=0;i<empty.Length;i++)empty[i]=ItemStack.Empty.Clone();
            outputWindow.SetSlots(empty);WorkstationData.SetOutputStacks(empty);
            foreach(var item in stranded)
                if(item!=null&&!item.IsEmpty()&&!xui.PlayerInventory.AddItem(item,true)&&item.count>0)
                    GameManager.Instance.ItemDropServer(item,xui.playerUI.entityPlayer.position,UnityEngine.Vector3.zero,xui.playerUI.entityPlayer.entityId,120f,false);
        }
    }
    public override void Update(float dt)
    {
        long profile=RebirthCookingDiagnostics.Begin();
        base.Update(dt);
        var cursorStack=xui?.DragAndDropWindow;
        if(windowGroup.isShowing && UIInput.selection==null && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            && cursorStack!=null && !cursorStack.IsEmpty() && xui.playerUI.playerInput.GUIActions.DPad_Down.WasPressed)
            cursorStack.DropCurrentItem();
        if(burnTimeLeft!=null&&WorkstationData!=null)
        {
            int seconds=System.Math.Max(0,(int)WorkstationData.GetTotalBurnTimeLeft());
            if (seconds != displayedBurnSeconds)
            {
                displayedBurnSeconds = seconds;
                displayedBurnTime=(seconds/3600).ToString("00")+"H "+(seconds/60%60).ToString("00")+"M "+(seconds%60).ToString("00")+"S";
            }
            // Native bindings can overwrite the text between updates; retain formatting
            // without rebuilding the string every frame.
            if (burnTimeLeft.Text != displayedBurnTime) burnTimeLeft.Text = displayedBurnTime;
        }
        var entries=cookingEntries;
        if(entries!=null&&entries.Length>0)
        {
            var current=System.Array.FindLast(entries,e=>e?.recipe!=null);
            var recipe=current?.recipe;
            if(windowGroup.isShowing&&RebirthCookingHeat.Ready(recipe)&&UIInput.selection==null&&!RebirthConsoleInputGuardRuntime.BlocksGameplayInput()&&xui.playerUI.playerInput.PermanentActions.Reload.WasPressed)
                RebirthCookingHeat.Take(current);
            if(RebirthCookingBatch.IsBatch(recipe)&&!RebirthCookingBatch.NeedsHeat(recipe))craftingQueue.ResumeCrafting();
            if(RebirthCookingHeat.Managed(recipe)&&(cookingSync-=dt)<=0)
            {
                cookingSync=1;
                // Only queue timing changes here. Full native sync also reassigns tools, fuel
                // and output and can trigger station model/network work for an unchanged inventory.
                var queue=new RecipeQueueItem[entries.Length];
                for(int i=0;i<entries.Length;i++)
                {
                    var e=entries[i];
                    queue[i]=new RecipeQueueItem{Recipe=e.recipe,Multiplier=(short)e.recipeCount,CraftingTimeLeft=e.craftingTimeLeft,IsCrafting=e.IsCrafting,Quality=(byte)e.OutputQuality,StartingEntityId=e.StartingEntityId,OneItemCraftTime=e.GetOneItemCraftTime()};
                }
                WorkstationData.SetRecipeQueueItems(queue);
            }
        }
        RebirthCookingDiagnostics.Frame(profile);
    }
    public override void OnClose()
    {
        sharedPresentation?.AdvanceCraftIntentEpoch();sharedPresentation?.Coordinator.Close();
        // Cancel before base teardown/synchronization; no late Update may complete preparation.
        GetChildByType<XUiC_RebirthCookingWorkspace>()?.Leave();
        base.OnClose();
        if(xui?.DragAndDropWindow!=null)
            xui.DragAndDropWindow.InMenu=xui.playerUI.windowManager.IsWindowOpen("windowpaging");
    }
    public override bool CraftingRequirementsValid(Recipe recipe)
    {
        return recipe != null && RebirthRecipeDiscoveryRules.Allows(xui?.playerUI?.entityPlayer,recipe.GetName())
            && RebirthCapabilityService.EvaluateRecipeForDiscovery(xui?.playerUI?.entityPlayer,recipe.GetName()).IsAllowed
            && !(GetChildByType<XUiC_RebirthCookingWorkspace>()?.Preparation.IsPreparing ?? false)
            && (toolWindow == null || toolWindow.HasRequirement(recipe))
            && (inputWindow == null || inputWindow.HasRequirement(recipe))
            && (outputWindow == null || outputWindow.HasRequirement(recipe))
            && (!RebirthCookingBatch.NeedsHeat(recipe) || fuelWindow == null || fuelWindow.HasRequirement(recipe));
    }
    public override string CraftingRequirementsInvalidMessage(Recipe recipe)
    {
        if (recipe != null && !RebirthRecipeDiscoveryRules.Allows(xui?.playerUI?.entityPlayer, recipe.GetName()))
            return Localization.Get("xuiRebirthRecipeReadingRequired");
        if (recipe != null)
        {
            var capability = RebirthCapabilityService.EvaluateRecipeForDiscovery(xui?.playerUI?.entityPlayer, recipe.GetName());
            if (!capability.IsAllowed) return capability.FirstMissingReason;
        }
        if (recipe != null)
        {
            if (toolWindow != null && !toolWindow.HasRequirement(recipe)) return Localization.Get("ttMissingCraftingTools");
            if (inputWindow != null && !inputWindow.HasRequirement(recipe)) return Localization.Get("ttMissingCraftingResources");
            if (outputWindow != null && !outputWindow.HasRequirement(recipe)) return Localization.Get("xuiRebirthStationOutputSpaceRequired");
            if (!RebirthCookingBatch.NeedsHeat(recipe)) return "";
        }
        return base.CraftingRequirementsInvalidMessage(recipe);
    }
}

[Preserve]
public sealed class XUiC_RebirthCookingSlot : XUiC_ItemStack
{
    public string SuggestedTooltip;
    private XUiV_Label nativeCount;
    public override void OnHovered(bool over){RebirthCharacterItemStatsTooltip.Hover(this,over);base.OnHovered(over);}

    public override void Update(float dt)
    {
        base.Update(dt);
        var cursorStack=xui?.DragAndDropWindow;
        if(windowGroup.isShowing && UIInput.selection==null && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            && cursorStack!=null && !cursorStack.IsEmpty() && xui.playerUI.playerInput.GUIActions.DPad_Down.WasPressed)
            cursorStack.DropCurrentItem();
        RebirthCookingSlotStyle.Apply(this,true);
        // The workspace owns required/available quantities, so suppress the overlapping native count.
        nativeCount = nativeCount ?? GetChildById("stackValue")?.ViewComponent as XUiV_Label;
        if (nativeCount != null && nativeCount.IsVisible) nativeCount.IsVisible = false;
    }
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "tooltip" && ItemStack.IsEmpty() && !string.IsNullOrEmpty(SuggestedTooltip))
        { value = SuggestedTooltip; return true; }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
    public override bool CanSwap(ItemStack stack)
    {
        var owner = windowGroup?.Controller?.GetChildByType<XUiC_RebirthCookingWorkspace>();
        if (owner != null && owner.Preparation.IsPreparing) return false;
        return base.CanSwap(stack) && (SlotNumber >= 9 ? stack == null || stack.IsEmpty() || RebirthCookingCatalogue.IsHerb(stack) : owner?.AcceptsIngredient(stack) ?? RebirthCookingCatalogue.IsIngredient(stack));
    }
    public override void updateItemInfoWindow(XUiC_ItemStack stack) { if (InfoWindow != null) base.updateItemInfoWindow(stack); }
}
