using System;
using System.Reflection;
using HarmonyLib;

// Hire inspection hosts the native itemInfoPanel (the shared selected-item pane) read-only. The pane builds
// Drop/Scrap/Take/Wear/Modify entries from the selected stack controller, and re-builds them on every dirty
// update, so for hire preview stacks (ids "hire*Stack*") the action list is cleared right after each native build.
[HarmonyPatch]
internal static class RebirthHireInfoActionsPatch
{
    internal static bool Healthy { get; private set; }
    internal static void Install()
    {
        if (Healthy) return;
        try
        {
            if (AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.SetCraftingActionList)) == null)
            { Log.Error("[REBIRTH Hire UI] XUiC_ItemActionList.SetCraftingActionList not found; shared item info is disabled for hire."); return; }
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.hire.infoactions"), typeof(RebirthHireInfoActionsPatch));
            Healthy = true;
        }
        catch (Exception e) { Log.Error("[REBIRTH Hire UI] info action patch failed (" + e.GetType().Name + "); legacy read-only pane stays in use."); }
    }
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.SetCraftingActionList));
    private static void Postfix(XUiC_ItemActionList __instance, XUiC_ItemActionList.ItemActionListTypes _actionListType, XUiController itemController)
    {
        if (_actionListType != XUiC_ItemActionList.ItemActionListTypes.Item) return;
        if (!(itemController is XUiC_RebirthReadonlyItemSlot slot)) return;
        string id = slot.ViewComponent?.ID;
        if (id == null || !id.StartsWith("hire", StringComparison.Ordinal)) return;
        __instance.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None, null);
    }
}
