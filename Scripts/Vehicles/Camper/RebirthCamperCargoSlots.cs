using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;

// Native BagContainer owns inventory callbacks, locks, sorting and scrolling.
// Its XML creates only 150 cells at startup; changing grid rows cannot create
// more controllers. Grow only the current camper's view before native binding.
[HarmonyPatch(typeof(XUiC_BagContainer), nameof(XUiC_BagContainer.SetBag))]
public static class RebirthCamperCargoSlots
{
    private static void Prefix(XUiC_BagContainer __instance, Bag __0)
    {
        EntityVehicle vehicle = __instance.xui?.Vehicle?.CurrentVehicle;
        if (!RebirthCamperRadial.IsCamper(vehicle) || !ReferenceEquals(vehicle.bag, __0)) return;
        if (__0.ItemGrid == null || __instance.grid == null || __instance.itemControllers == null)
            throw new InvalidOperationException("Native camper cargo view is unavailable.");
        int count = __0.ItemGrid.Length;
        if (count < 0 || count > RebirthCamperStorageCapacity.MaximumNativeSlots)
            throw new InvalidOperationException("Native camper cargo dimensions are invalid.");
        if (__instance.itemControllers.Length >= count) return;
        var parent = __instance.grid.Controller;
        if (parent == null) throw new InvalidOperationException("Native cargo grid controller is missing.");
        // Reuse cells on subsequent opens. Each new cell follows the exact native
        // repeater template and initialization sequence; no inventory is cloned.
        for (int slot = __instance.itemControllers.Length; slot < count; slot++)
        {
            var parameters = new Dictionary<string, object> {
                { "repeat_i", slot }, { "repeat_count", count }
            };
            __instance.grid.SetRepeatContentTemplateParams(parameters, slot);
            var template = new XElement("item_stack",
                new XAttribute("name", slot),
                new XAttribute("cell_size", __instance.GridCellSize.x));
            var view = XUiFromXml.parseViewComponents(template, __instance.WindowGroup,
                parent, slot.ToString(), parameters);
            var controller = view?.Controller as XUiC_ItemStack;
            if (controller == null) throw new InvalidOperationException("Native cargo item template is unavailable.");
            controller.AutoBindComponents();
            controller.Init();
            controller.AutoBindEvents();
        }
        __instance.itemControllers = __instance.GetChildrenByType<XUiC_ItemStack>();
        if (__instance.itemControllers.Length < count)
            throw new InvalidOperationException("Native cargo view did not expose every position.");
    }

    private static void Postfix(XUiC_BagContainer __instance, Bag __0)
    {
        EntityVehicle vehicle = __instance.xui?.Vehicle?.CurrentVehicle;
        if (!RebirthCamperRadial.IsCamper(vehicle) || !ReferenceEquals(vehicle.bag, __0)) return;
        int columns = __instance.grid.Columns;
        if (columns <= 0) throw new InvalidOperationException("Native cargo columns are invalid.");
        __instance.grid.Rows = (__0.ItemGrid.Length + columns - 1) / columns;
        __instance.SetAllChildrenDirty();
    }
}
