using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;

// Native BagContainer owns inventory callbacks, locks, sorting and scrolling.
// Its XML creates only 150 cells at startup; changing grid rows cannot create
// more controllers. Grow only the current camper's view before native binding.
[HarmonyPatch(typeof(XUiC_BagContainer), nameof(XUiC_BagContainer.SetBag))]
public static class RebirthCamperCargoSlots
{
    private static readonly FieldInfo TemplatesField = AccessTools.Field(typeof(XUiFromXml), "templateData");
    private static readonly FieldInfo DefaultsField = AccessTools.Field(typeof(XUiFromXml), "templateDefaults");
    private static readonly FieldInfo UsedField = AccessTools.Field(typeof(XUiFromXml), "usedTemplates");
    private static readonly FieldInfo ExpressionsField = AccessTools.Field(typeof(XUiFromXml), "expressionCache");
    private static XElement itemStackTemplate;
    private static Dictionary<string, object> itemStackDefaults;

    // XUi.postLoadInit releases its XML templates. Runtime parseViewComponents
    // otherwise dereferences a null templateData as soon as cargo exceeds the
    // 150 cells created by the stock window. Retain only this one native template.
    internal static void CaptureNativeTemplate()
    {
        var templates = TemplatesField?.GetValue(null) as Dictionary<string, XElement>;
        if (templates == null || !templates.TryGetValue("item_stack", out var template)) return;
        itemStackTemplate = new XElement(template);
        var defaults = DefaultsField?.GetValue(null) as Dictionary<string, Dictionary<string, object>>;
        itemStackDefaults = defaults != null && defaults.TryGetValue("item_stack", out var values)
            ? new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

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
        if (TemplatesField == null || DefaultsField == null || UsedField == null || ExpressionsField == null
            || itemStackTemplate == null || itemStackDefaults == null)
            throw new InvalidOperationException("Native cargo item template was not captured before UI startup completed.");
        // The parser is used only on the game thread. Restore its minimal native
        // template state for this expansion, then release it again even on error.
        object previousTemplates = TemplatesField.GetValue(null);
        object previousDefaults = DefaultsField.GetValue(null);
        object previousUsed = UsedField.GetValue(null);
        object previousExpressions = ExpressionsField.GetValue(null);
        bool restore = previousTemplates == null;
        try
        {
            if (restore)
            {
                TemplatesField.SetValue(null, new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase)
                    { { "item_stack", itemStackTemplate } });
                DefaultsField.SetValue(null, new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
                    { { "item_stack", itemStackDefaults } });
                UsedField.SetValue(null, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    { { "item_stack", 0 } });
                ExpressionsField.SetValue(null, Activator.CreateInstance(ExpressionsField.FieldType));
            }
            // Reuse cells on subsequent opens. Each new cell follows the exact
            // native repeater template and initialization sequence.
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
                if (controller?.ViewComponent == null)
                    throw new InvalidOperationException("Native cargo item template did not create a usable cell.");
                controller.AutoBindComponents();
                controller.Init();
                controller.AutoBindEvents();
            }
            __instance.itemControllers = __instance.GetChildrenByType<XUiC_ItemStack>();
        }
        finally
        {
            if (restore)
            {
                TemplatesField.SetValue(null, previousTemplates);
                DefaultsField.SetValue(null, previousDefaults);
                UsedField.SetValue(null, previousUsed);
                ExpressionsField.SetValue(null, previousExpressions);
            }
        }
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

[HarmonyPatch(typeof(XUiFromXml), nameof(XUiFromXml.ClearLoadingData))]
internal static class RebirthCamperCargoTemplateCapture
{
    private static void Prefix() => RebirthCamperCargoSlots.CaptureNativeTemplate();
}
