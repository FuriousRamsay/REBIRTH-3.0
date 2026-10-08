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
        internal XUiC_ItemInfoWindow Window;
    }
    private static readonly ConditionalWeakTable<XUi, Snapshot> Cache = new ConditionalWeakTable<XUi, Snapshot>();
    internal static XUiC_ItemInfoWindow Find(XUi ui)
    {
        if (ui == null) return null;
        var snapshot = Cache.GetValue(ui, _ => new Snapshot());
        var groups = ui.WindowGroups;
        bool changed = snapshot.Roots == null || snapshot.Roots.Length != groups.Count;
        for (int i = 0; !changed && i < groups.Count; i++)
            changed = !ReferenceEquals(snapshot.Roots[i], groups[i].Controller);
        if (changed)
        {
            snapshot.Roots = new XUiController[groups.Count];
            for (int i = 0; i < groups.Count; i++) snapshot.Roots[i] = groups[i].Controller;
            // Preserve native search order, including a cached missing result.
            snapshot.Window = ui.GetChildByType<XUiC_ItemInfoWindow>();
        }
        return snapshot.Window;
    }
    public static void Install() => RebirthHarmonyBootstrap.PatchClassOnce(
        new Harmony("rebirth.inventory.infowindowlookup"), typeof(RebirthInventoryInfoWindowLookup));
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
