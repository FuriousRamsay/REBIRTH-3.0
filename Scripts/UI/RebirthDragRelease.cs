#nullable disable
/// <summary>
/// Owner rule (2026-11-07), every window: dragging a stack picks it up onto the cursor and releasing the mouse button
/// keeps it there, so the right button can then split it (drop one). Placing is a separate click on a slot.
/// Every REBIRTH drag-release relay checks this before it drops the held stack on release.
/// </summary>
public static class RebirthDragRelease
{
    public const bool DropOnRelease = false;
}

/// <summary>Native slots: a drag picks the stack up (SwapItem / HandlePartialStackPickup while a button is held), but the
/// source slot kept lastClicked, so releasing over it (NGUI keeps hover on the pressed source) swapped the stack straight
/// back. Clearing it after a drag pickup keeps the stack on the cursor; a later click on a slot sets it again to place.</summary>
[HarmonyLib.HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.SwapItem))]
internal static class RebirthDragPickupKeepsCursorPatch
{
    private static void Postfix(XUiC_ItemStack __instance) => RebirthDragPickup.Release(__instance);
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandlePartialStackPickup))]
internal static class RebirthDragPartialPickupKeepsCursorPatch
{
    private static void Postfix(XUiC_ItemStack __instance) => RebirthDragPickup.Release(__instance);
}

internal static class RebirthDragPickup
{
    internal static void Release(XUiC_ItemStack slot)
    {
        if (RebirthDragRelease.DropOnRelease || slot?.xui?.DragAndDropWindow == null || slot.xui.DragAndDropWindow.IsEmpty()) return;
        var pointer = slot.xui.playerUI?.CursorController;
        if (pointer == null) return;
        bool held = pointer.GetMouseButton(UICamera.MouseButton.LeftButton) || pointer.GetMouseButton(UICamera.MouseButton.RightButton);
        bool pressedNow = pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton) || pointer.GetMouseButtonDown(UICamera.MouseButton.RightButton);
        // Only a pickup made while dragging (button held, not this frame's press).
        if (held && !pressedNow) slot.lastClicked = false;
    }
}

[UnityEngine.Scripting.Preserve]
public sealed class RebirthDragReleaseModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new HarmonyLib.Harmony("rebirth.drag-release");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDragPickupKeepsCursorPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDragPartialPickupKeepsCursorPatch));
    }
}
