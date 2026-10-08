using System;
using HarmonyLib;

// Preserve native trader/respondent references; only this list's synchronous builder
// sees the owned-backpack context. State is restored even for nested calls or throws.
internal static class RebirthQuestBackpackActionContext
{
    [ThreadStatic] private static XUiM_Trader current;
    internal struct State { internal XUiM_Trader Previous; internal bool Entered; }
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
        return state;
    }
    internal static void Exit(State state) { current = state.Previous; }
}

[HarmonyPatch(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.SetCraftingActionList))]
internal static class RebirthQuestBackpackActionContextPatch
{
    private static void Prefix(XUiC_ItemActionList __instance,
        XUiC_ItemActionList.ItemActionListTypes __0, out RebirthQuestBackpackActionContext.State __state)
    { __state = RebirthQuestBackpackActionContext.Enter(__instance, __0); }
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

