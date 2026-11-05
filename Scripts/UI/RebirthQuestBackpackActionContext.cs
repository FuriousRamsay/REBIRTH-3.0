using System;
using HarmonyLib;

// Preserve native trader/respondent references; only this list's synchronous builder
// sees the owned-backpack context. State is restored even for nested calls or throws.
internal static class RebirthQuestBackpackActionContext
{
    [ThreadStatic] private static XUiM_Trader current;
    internal struct State { internal XUiM_Trader Previous; internal bool Entered, TraderSale; }
    internal static bool Active => current != null;
    internal static bool Matches(XUiM_Trader model) => ReferenceEquals(current, model) && current != null;
    internal static State Enter(XUiC_ItemActionList list, XUiC_ItemActionList.ItemActionListTypes type)
    {
        var state = new State { Previous = current };
        current = null;
        if (type == XUiC_ItemActionList.ItemActionListTypes.Item &&
            list?.windowGroup?.Controller is XUiC_RebirthQuestTurnInWorkspace &&
            list.ViewComponent?.ID == "itemActions" && list.xui?.Trader != null)
        {
            current = list.xui.Trader;
            state.Entered = true;
        }
        // The trader workspace shows the normal item actions too; the patch below restores Sell.
        else if (type == XUiC_ItemActionList.ItemActionListTypes.Item &&
            list?.windowGroup?.Controller is XUiC_RebirthTraderWorkspace &&
            list.ViewComponent?.ID == "vendorItemActions" && list.xui?.Trader?.TraderData != null)
        {
            state.TraderSale = list.xui.Trader.TraderData.TraderInfo != null;
            current = list.xui.Trader;
            state.Entered = true;
        }
        return state;
    }
    internal static void Exit(State state) { current = state.Previous; }
    // Same conditions as the native trader branch that this context bypasses. Inserting first
    // keeps Sell in front, with the W shortcut it has always had.
    internal static void AddTraderSale(XUiC_ItemActionList list, XUiController controller)
    {
        var stack = controller as IItemStackController;
        var trader = list?.xui?.Trader;
        var info = trader?.TraderData?.TraderInfo;
        if (stack == null || info == null || stack.ItemStack == null || stack.ItemStack.IsEmpty() ||
            stack.ItemStack.itemValue.ItemClass == null || stack is XUiC_Creative2Stack || stack is XUiC_RequiredItemStack) return;
        if ((stack.StackLocation == XUiC_ItemStack.StackLocationTypes.Backpack || stack.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt) &&
            stack == list.xui.AssembleItem.CurrentItemStackController) return;
        bool allowed = info.AllowSell;
        if (trader.Trader is TileEntityVendingMachine machine)
            allowed = allowed || machine.LocalPlayerIsOwner() || machine.IsUserAllowed(Platform.PlatformManager.InternalLocalUserIdentifier);
        if (!allowed) return;
        var sell = new ItemActionEntrySell(controller);
        sell.ShortCut = BaseItemActionEntry.GamepadShortCut.DPadUp;
        sell.ParentActionList = list;
        list.itemActionEntries.Insert(0, sell);
    }
}

[HarmonyPatch(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.SetCraftingActionList))]
internal static class RebirthQuestBackpackActionContextPatch
{
    private static void Prefix(XUiC_ItemActionList __instance,
        XUiC_ItemActionList.ItemActionListTypes __0, out RebirthQuestBackpackActionContext.State __state)
    { __state = RebirthQuestBackpackActionContext.Enter(__instance, __0); }
    private static void Postfix(XUiC_ItemActionList __instance, XUiController __1, RebirthQuestBackpackActionContext.State __state)
    {
        if (!__state.TraderSale) return;
        // The sale check needs the real trader data, which this context hides from the native builder.
        RebirthQuestBackpackActionContext.Exit(__state);
        RebirthQuestBackpackActionContext.AddTraderSale(__instance, __1);
    }
    private static Exception Finalizer(Exception __exception, RebirthQuestBackpackActionContext.State __state)
    {
        RebirthQuestBackpackActionContext.Exit(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(XUiM_Trader), nameof(XUiM_Trader.TraderData), MethodType.Getter)]
internal static class RebirthQuestBackpackTraderDataPatch
{
    private static void Postfix(XUiM_Trader __instance, ref TraderData __result)
    { if (RebirthQuestBackpackActionContext.Matches(__instance)) __result = null; }
}

