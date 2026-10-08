using UnityEngine.Scripting;
using HarmonyLib;

[Preserve]
public sealed class XUiC_RebirthQuestTurnInWorkspace : XUiC_QuestTurnInWindowGroup
{
    public static XUiC_RebirthQuestTurnInWorkspace ActiveInstance { get; private set; }
    public XUiC_ItemInfoWindow Details { get; private set; }
    private XUiC_InfoWindow empty;
    private XUiC_ItemStack lastToolbeltSelection;
    public void InspectToolbelt(XUiC_ItemStack slot, bool force)
    {
        if (!force && lastToolbeltSelection == slot && !slot.IsDirty) return;
        lastToolbeltSelection = slot;
        Details.SetItemStack(slot, true);
    }
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();

    public override void Init()
    {
        base.Init();
        Details = GetChildById("questItemInfo") as XUiC_ItemInfoWindow;
        empty = GetChildById("questEmptyInfo") as XUiC_InfoWindow;
        BindDetails();
    }

    private void BindDetails()
    {
        if (Details == null) return;
        Details.emptyInfoWindow = empty;
        if (rewardsWindow != null) rewardsWindow.InfoWindow = Details;
        foreach (var slot in GetChildrenByType<XUiC_RebirthQuestInventorySlot>())
            slot.InfoWindow = Details;
    }

    public override void OnOpen()
    {
        BindDetails();
        ActiveInstance = this;
        lastToolbeltSelection = null;
        Details.ViewComponent.IsVisible = false;
        empty.ViewComponent.IsVisible = true;
        // Native lifecycle still owns the respondent, pending quest and claim transition.
        base.OnOpen();
        BindDetails();
        xui.playerUI.windowManager.Open("toolbelt", false);
        xui.playerUI.windowManager.Open("dragAndDrop", false);
        hud.Maintain(xui);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (windowGroup.isShowing) hud.Maintain(xui);
    }

    public override void OnClose()
    {
        if (ActiveInstance == this) ActiveInstance = null;
        hud.Restore();
        base.OnClose();
    }
}

[Preserve]
public sealed class XUiC_RebirthQuestTurnInRewards : XUiC_QuestTurnInRewardsWindow
{
    public override void OnOpen()
    {
        // The native group resolves a global item-info window before opening descendants.
        // Replace that binding before native SelectedEntry=null calls ShowEmptyInfo.
        var owner = windowGroup.Controller as XUiC_RebirthQuestTurnInWorkspace;
        if (owner?.Details != null) InfoWindow = owner.Details;
        base.OnOpen();
    }
}

[Preserve]
public sealed class XUiC_RebirthQuestInventorySlot : XUiC_RebirthCraftingInventorySlot
{
    private void Bind()
    {
        var owner = windowGroup.Controller as XUiC_RebirthQuestTurnInWorkspace;
        if (owner?.Details != null) InfoWindow = owner.Details;
    }
    public override void Update(float dt) { Bind(); base.Update(dt); }
    public override void updateItemInfoWindow(XUiC_ItemStack slot)
    {
        Bind();
        InfoWindow?.SetItemStack(slot, true);
    }
    public override void HandleClickComplete() { Bind(); base.HandleClickComplete(); }
}
[Preserve]
public sealed class XUiC_RebirthQuestDetails : XUiC_ItemInfoWindow
{
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "rebirthtraderhasmods")
        {
            bool present = false;
            var mods = itemStack?.itemValue?.modifications;
            if (mods != null) foreach (var mod in mods)
                if (mod != null && !mod.IsEmpty()) { present = true; break; }
            var cosmetics = itemStack?.itemValue?.cosmeticMods;
            if (cosmetics != null) foreach (var mod in cosmetics)
                if (mod != null && !mod.IsEmpty()) { present = true; break; }
            value = (present && CompareStack.IsEmpty()).ToString();
            return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
}

// Quest turn-in retains the trader respondent, but its items are rewards or owned
// backpack items. A trader context must not create buy/sell actions for either.
[HarmonyPatch(typeof(XUiC_ItemInfoWindow), "isOpenAsTrader", MethodType.Getter)]
internal static class RebirthQuestDetailsTradeContextPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance, ref bool __result)
    {
        if (__instance is XUiC_RebirthQuestDetails) __result = false;
    }
}

[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.ShowEmptyInfo))]
internal static class RebirthQuestDetailsEmptyPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance)
    {
        if (__instance is XUiC_RebirthQuestDetails)
            __instance.ViewComponent.IsVisible = false;
    }
}
[Preserve]
public sealed class XUiC_RebirthQuestTurnInSurface : XUiController
{
    private Vector2i lastScreen = new Vector2i(-1, -1);
    private Vector2i lastBoundsPosition = new Vector2i(int.MinValue, int.MinValue);
    private Vector2i lastAvailable = new Vector2i(-1, -1);
    public override void OnOpen()
    {
        base.OnOpen();
        lastScreen = new Vector2i(-1, -1);
        Layout();
    }
    public override void Update(float dt) { base.Update(dt); Layout(); }
    private void Layout()
    {
        Vector2i screen = xui.GetXUiScreenSize();
        Vector2i position, available;
        RebirthScreenLayout.GetScreenBounds(xui, out position, out available);
        if (screen.x == lastScreen.x && screen.y == lastScreen.y &&
            position.x == lastBoundsPosition.x && position.y == lastBoundsPosition.y &&
            available.x == lastAvailable.x && available.y == lastAvailable.y) return;
        lastScreen = screen;
        lastBoundsPosition = position;
        lastAvailable = available;
        float scale = System.Math.Min(System.Math.Max(1, available.x - 32) / 1680f, available.y / 850f);
        ViewComponent.Position = new Vector2i(
            position.x + UnityEngine.Mathf.RoundToInt((available.x - 1680f * scale) / 2f),
            position.y);
        ViewComponent.TryUpdatePosition();
        if (ViewComponent.UiTransform != null)
            ViewComponent.UiTransform.localScale = new UnityEngine.Vector3(scale, scale, 1);
    }
}