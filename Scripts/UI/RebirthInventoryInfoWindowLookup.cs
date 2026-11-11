using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Native grid SetStacks searches every UI group, including hidden custom grids.
// Cache structural lookup only; item contents and eligibility remain live.
[HarmonyPatch(typeof(XUiC_ItemStackGrid), "SetStacks")]
internal static class RebirthInventoryInfoWindowLookup
{
    private sealed class Snapshot
    {
        internal XUiController[] Roots;
        internal bool ItemWindowResolved;
        internal XUiC_ItemInfoWindow ItemWindow;
        internal List<XUiC_InfoWindow> Windows;
    }

    private static readonly ConditionalWeakTable<XUi, Snapshot> Cache = new ConditionalWeakTable<XUi, Snapshot>();
    private static bool topologyHooksInstalled;

    private static Snapshot For(XUi ui)
    {
        var snapshot = Cache.GetValue(ui, _ => new Snapshot());
        var groups = ui.WindowGroups;
        bool changed = snapshot.Roots == null || snapshot.Roots.Length != groups.Count;
        for (int i = 0; !changed && i < groups.Count; i++)
            changed = !ReferenceEquals(snapshot.Roots[i], groups[i].Controller);
        if (changed)
        {
            snapshot.Roots = new XUiController[groups.Count];
            for (int i = 0; i < groups.Count; i++) snapshot.Roots[i] = groups[i].Controller;
            snapshot.ItemWindowResolved = false;
            snapshot.ItemWindow = null;
            snapshot.Windows = null;
        }
        return snapshot;
    }

    internal static XUiC_ItemInfoWindow Find(XUi ui)
    {
        if (ui == null) return null;
        if (!topologyHooksInstalled) return ui.GetChildByType<XUiC_ItemInfoWindow>();
        var snapshot = For(ui);
        if (!snapshot.ItemWindowResolved)
        {
            snapshot.ItemWindow = ui.GetChildByType<XUiC_ItemInfoWindow>();
            snapshot.ItemWindowResolved = true;
        }
        return snapshot.ItemWindow;
    }

    internal static IReadOnlyList<XUiC_InfoWindow> FindAll(XUi ui)
    {
        if (ui == null) return Array.Empty<XUiC_InfoWindow>();
        if (!topologyHooksInstalled) return ui.GetChildrenByType<XUiC_InfoWindow>();
        var snapshot = For(ui);
        return snapshot.Windows ?? (snapshot.Windows = ui.GetChildrenByType<XUiC_InfoWindow>());
    }

    private static void Invalidate(XUi ui)
    {
        if (ui != null) Cache.Remove(ui);
    }

    public static void Install()
    {
        var harmony = new Harmony("rebirth.inventory.infowindowlookup");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(ControllerParentChange));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(UiCleanup));
        topologyHooksInstalled = true;
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthInventoryInfoWindowLookup));
    }

    // XUiFromXml attaches both normal and empty controllers through this
    // setter. Changing a child can leave the ordered window roots identical.
    [HarmonyPatch(typeof(XUiController), nameof(XUiController.Parent), MethodType.Setter)]
    private static class ControllerParentChange
    {
        private static void Prefix(XUiController __instance, out XUiController __state)
            => __state = __instance.Parent;

        private static void Postfix(XUiController __instance, XUiController __state)
        {
            var parent = __instance.Parent;
            if (ReferenceEquals(parent, __state)) return;
            var previousUi = __state?.xui;
            var currentUi = parent?.xui;
            Invalidate(previousUi);
            if (!ReferenceEquals(previousUi, currentUi)) Invalidate(currentUi);
        }
    }

    [HarmonyPatch(typeof(XUi), nameof(XUi.Cleanup))]
    private static class UiCleanup
    {
        private static void Postfix(XUi __instance) => Invalidate(__instance);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(typeof(RebirthInventoryInfoWindowLookup), nameof(Find));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(XUi) &&
                method.Name == "GetChildByType" && method.IsGenericMethod &&
                method.GetGenericArguments()[0] == typeof(XUiC_ItemInfoWindow))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }
}
