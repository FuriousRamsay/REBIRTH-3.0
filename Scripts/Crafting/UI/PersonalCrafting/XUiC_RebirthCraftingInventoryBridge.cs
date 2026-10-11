using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// UI coordinator for the Crafting inventory header. Storage and item movement remain owned by
/// XUiM_PlayerInventory/Bag; this controller only exposes real sort/slot-lock controls and status.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingInventoryBridge : XUiController
{
    private RebirthCraftingPresentation personalOwner;
    private XUiC_RebirthCraftingInventory inventory;
    private XUiController lockButton;
    private XUiController sortButton;
    private XUiController quickStackButton;
    private XUiController companionsButton;
    private XUiController libraryButton, theoryButton, saleButton;
    private XUiController lockActive;
    private XUiV_Label capacityLabel;
    private float nextRefresh;
    private int lastPhysical = -1;
    private int lastUnencumbered = -1;
    private int lastUsed = -1;
    private int lastEncumberedUsed = -1;
    private bool lastLockMode;

    public override void Init()
    {
        base.Init();
        personalOwner = RebirthCraftingPresentation.Resolve(this);
        Resolve();
        Wire();
        RefreshHeader(true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Resolve();
        Wire();
        if (inventory != null)
            inventory.SetUserLockMode(false);
        RefreshHeader(true);
    }

    public override void OnClose()
    {
        if (inventory != null)
        {
            inventory.PersistUserLockedSlots();
            inventory.SetUserLockMode(false);
        }
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (personalOwner != null && !personalOwner.State.IsOpen) return;
        if (Time.realtimeSinceStartup >= nextRefresh)
        {
            nextRefresh = Time.realtimeSinceStartup + 0.20f;
            // Optional controls are absent in some station templates. Retry discovery
            // with the header refresh rather than walking those trees every frame.
            Resolve();
            Wire();
            RefreshHeader(false);
        }
    }

    private void Resolve()
    {
        if (inventory == null)
            inventory = GetChildByType<XUiC_RebirthCraftingInventory>();
        if (lockButton == null)
            lockButton = GetChildById("btnRebirthCraftingInventoryLock");
        if (sortButton == null)
            sortButton = GetChildById("btnRebirthCraftingInventorySort");
        if (quickStackButton == null)
            quickStackButton = GetChildById("btnRebirthCraftingInventoryQuickStack");
        if (companionsButton == null)
            companionsButton = GetChildById("btnRebirthCraftingInventoryCompanions");
        if (lockActive == null)
            lockActive = GetChildById("rebirthCraftingInventoryLockActive");
        if (libraryButton == null) libraryButton = GetChildById("btnRebirthCraftingInventoryLibrary");
        if (theoryButton == null) theoryButton = GetChildById("rbBackpackTheorySection");
        if (saleButton == null) saleButton = GetChildById("rbBackpackSellSection");
        if (capacityLabel == null)
        {
            XUiController c = GetChildById("rebirthCraftingInventoryCapacity");
            capacityLabel = c != null ? c.ViewComponent as XUiV_Label : null;
        }
    }

    private bool wired;
    private void Wire()
    {
        if (wired || lockButton == null || sortButton == null || quickStackButton == null || companionsButton == null)
            return;
        lockButton.OnPress += Lock_OnPress;
        sortButton.OnPress += Sort_OnPress;
        quickStackButton.OnPress += QuickStack_OnPress;
        companionsButton.OnPress += Companions_OnPress;
        if (libraryButton != null)
            libraryButton.OnPress += delegate { xui.playerUI.windowManager.Open("rebirthBackpackLibrary", true); };
        wired = true;
    }

    private void Lock_OnPress(XUiController sender, int mouseButton)
    {
        if (inventory == null)
            return;
        inventory.ToggleUserLockMode();
        RefreshHeader(true);
    }

    private void Sort_OnPress(XUiController sender, int mouseButton)
    {
        if (inventory == null)
            return;
        inventory.SortAuthoritativeBackpack();
        RefreshHeader(true);
    }


    private void QuickStack_OnPress(XUiController sender, int mouseButton)
    {
        if (!QuickStackRuntimePolicy.Enabled)
            return;
        QuickStackRadialUiService.Open(xui);
    }

    private void Companions_OnPress(XUiController sender, int mouseButton)
    {
        RebirthCompanionUiService.Open(xui);
    }
    private void RefreshHeader(bool force)
    {
        if(GetChildById("rebirthCraftingInventoryTitle")?.ViewComponent is XUiV_Label title && title.Text!="BACKPACK")title.Text="BACKPACK";
        var scroll=GetChildByType<XUiC_RebirthCraftingInventoryScroll>();
        if(scroll?.ViewComponent!=null && scroll.CurrentViewportWidth>0)
        {
            int right=scroll.ViewComponent.Position.x+scroll.CurrentViewportWidth;
            XUiController[] buttons=theoryButton != null && saleButton != null
                ? new[]{theoryButton,saleButton,sortButton,lockButton,quickStackButton,companionsButton}
                : libraryButton != null
                ? new[]{libraryButton,sortButton,lockButton,quickStackButton,companionsButton}
                : new[]{sortButton,lockButton,quickStackButton,companionsButton};
            for(int i=0;i<buttons.Length;i++)
                if(buttons[i]?.ViewComponent!=null)
                {
                    var position=new Vector2i(right-16-(buttons.Length-1-i)*38,-20);
                    if(buttons[i].ViewComponent.Position!=position)buttons[i].ViewComponent.Position=position;
                }
            // Keep the text boundary aligned with the same viewport as the controls.
            if (capacityLabel != null)
            {
                int firstButtonX = right - 16 - (buttons.Length - 1) * 38;
                int labelWidth = System.Math.Max(1, firstButtonX - capacityLabel.Position.x - 18);
                if (capacityLabel.Size.x != labelWidth)
                    capacityLabel.Size = new Vector2i(labelWidth, capacityLabel.Size.y);
            }
            if(lockActive?.ViewComponent!=null)lockActive.ViewComponent.Position=new Vector2i(right-16-2*38-15,-36);
        }
        int physical = RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);
        int unencumbered = RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui);
        int used = RebirthCraftingInventoryBridge.GetUsedSlotCount(xui);
        int encumberedUsed = RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui);
        bool lockMode = inventory != null && inventory.UserLockMode;

        if (!force && physical == lastPhysical && unencumbered == lastUnencumbered &&
            used == lastUsed && encumberedUsed == lastEncumberedUsed && lockMode == lastLockMode)
            return;

        lastPhysical = physical;
        lastUnencumbered = unencumbered;
        lastUsed = used;
        lastEncumberedUsed = encumberedUsed;
        lastLockMode = lockMode;

        RebirthCraftingPresentation owner = RebirthCraftingPresentation.Resolve(this);
        owner?.Coordinator?.RecordBackpack(physical, unencumbered, used);

        if (capacityLabel != null)
            capacityLabel.Text = used + "/" + physical + "  •  " + encumberedUsed + " " + Localization.Get("xuiRebirthCraftingInventoryEncumbered");
        if (lockActive != null && lockActive.ViewComponent != null)
            lockActive.ViewComponent.IsVisible = lockMode;
        if (quickStackButton != null && quickStackButton.ViewComponent != null)
            quickStackButton.ViewComponent.IsVisible = QuickStackRuntimePolicy.Enabled;
        if (companionsButton != null && companionsButton.ViewComponent != null)
            companionsButton.ViewComponent.IsVisible = true;

        if (RebirthLogSettings.CraftingUiLoggingEnabled && force)
            Log.Out("[REBIRTH Crafting InventoryTrace] header physical=" + physical
                + " used=" + used
                + " unencumbered=" + unencumbered
                + " encumberedUsed=" + encumberedUsed
                + " lockMode=" + lockMode
                + " quickStackEnabled=" + QuickStackRuntimePolicy.Enabled
                + " controls={quick:" + (quickStackButton != null)
                + ",companions:" + (companionsButton != null)
                + ",lock:" + (lockButton != null)
                + ",sort:" + (sortButton != null) + "}");
    }
}
