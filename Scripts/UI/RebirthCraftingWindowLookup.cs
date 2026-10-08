using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

/// <summary>Cache the structural crafting-window lookup, never recipe eligibility or visibility.</summary>
[HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.RefreshEnabled))]
internal static class RebirthCraftingWindowLookup
{
    private sealed class Snapshot
    {
        internal XUiController[] Roots;
        internal readonly List<XUiC_CraftingWindowGroup> Windows = new List<XUiC_CraftingWindowGroup>();
    }
    private static readonly ConditionalWeakTable<XUi, Snapshot> Cache = new ConditionalWeakTable<XUi, Snapshot>();

    public static void Install() => RebirthHarmonyBootstrap.PatchClassOnce(
        new Harmony("rebirth.crafting.windowlookup"), typeof(RebirthCraftingWindowLookup));

    private static List<XUiC_CraftingWindowGroup> Find(XUi xui)
    {
        Snapshot snapshot = Cache.GetValue(xui, CreateSnapshot);
        var groups = xui.WindowGroups;
        bool changed = snapshot.Roots == null || snapshot.Roots.Length != groups.Count;
        for (int i = 0; !changed && i < groups.Count; i++)
            changed = !ReferenceEquals(snapshot.Roots[i], groups[i].Controller);
        if (changed)
        {
            snapshot.Roots = new XUiController[groups.Count];
            snapshot.Windows.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                snapshot.Roots[i] = groups[i].Controller;
                snapshot.Roots[i]?.GetChildrenByType(snapshot.Windows);
            }
        }
        return snapshot.Windows;
    }

    private static Snapshot CreateSnapshot(XUi xui) => new Snapshot();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(typeof(RebirthCraftingWindowLookup), nameof(Find));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(XUi) &&
                method.Name == "GetChildrenByType" && method.IsGenericMethod &&
                method.GetGenericArguments()[0] == typeof(XUiC_CraftingWindowGroup))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }
}
