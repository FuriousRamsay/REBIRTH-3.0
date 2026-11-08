using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Clicking a toolbelt item while a crafting or station window is open shows it in that window's Selected Item panel,
/// as a backpack click does (owner report 2026-11-08: a toolbelt weapon could not be selected at the Modifier Station).
/// The toolbelt lives outside the window, so its slots never reached the panel.
/// </summary>
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleClickComplete))]
internal static class RebirthToolbeltItemContextSelectPatch
{
    private static void Postfix(XUiC_ItemStack __instance)
    {
        if (__instance == null || __instance is XUiC_RebirthCraftingInventorySlot
            || __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.ToolBelt || __instance.xui == null) return;
        foreach (var group in __instance.xui.GetChildrenByType<XUiC_CraftingWindowGroup>())
        {
            if (group?.windowGroup?.isShowing != true) continue;
            var context = RebirthCraftingPresentation.For(group)?.GetChildByType<XUiC_RebirthCraftingItemContext>();
            if (context == null) continue;
            if (__instance.xui.DragAndDropWindow?.IsEmpty() == false) context.SelectSlot(__instance.xui.DragAndDropWindow.ItemStackControl);
            else if (__instance.ItemStack != null && !__instance.ItemStack.IsEmpty()) context.SelectSlot(__instance);
            return;
        }
    }
}

[Preserve]
public sealed class RebirthToolbeltItemContextSelectModApi : IModApi
{
    public void InitMod(Mod mod)
        => RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.toolbelt-item-context"), typeof(RebirthToolbeltItemContextSelectPatch));
}
