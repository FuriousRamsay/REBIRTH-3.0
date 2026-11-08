using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 item info "Bonuses" tab (ArmorEnchantingItemInfoTabRuntime, owner 2026-11-08). Item panels show a star tab when
/// the item has armor bonuses. It opens on the bonus list (a "BONUSES" header in the highest rarity's colour, then one
/// line per bonus in its rarity colour); pressing the star switches to the normal description and back.
/// The weapon bonus stays with the description, as in 2.6.
/// </summary>
public static class RebirthArmorBonusInfoTab
{
    public const string ButtonId = "rbBonusTab";
    private static readonly string[] Colors = { "c8c8c8", "88aaff", "ffaa55", "cc88ff" };

    public static bool HasBonuses(ItemStack stack) => stack != null && !stack.IsEmpty() && RebirthArmorBonusStorage.Read(stack.itemValue).Length > 0;

    public static string BonusText(ItemStack stack)
    {
        var bonuses = stack == null || stack.IsEmpty() ? null : RebirthArmorBonusStorage.Read(stack.itemValue);
        if (bonuses == null || bonuses.Length == 0) return string.Empty;
        int highest = bonuses.Max(b => (int)b.Definition.Rarity);
        var text = new StringBuilder();
        text.Append('[').Append(Colors[highest]).Append(']').Append(Localization.Get("xuiRebirthBonusesHeader")).Append("[-]\n");
        foreach (var bonus in bonuses)
            text.Append("\n[").Append(Colors[(int)bonus.Definition.Rarity]).Append(']').Append(RebirthArmorBonusText.Plain(bonus)).Append("[-]");
        return text.ToString();
    }

    /// <summary>Description with the weapon bonus below it (no armor bonuses: those are on the star tab).</summary>
    public static string WithWeapon(string description, ItemStack stack)
    {
        string weapon = stack == null || stack.IsEmpty() ? string.Empty : RebirthWeaponBonuses.Describe(stack.itemValue);
        if (weapon.Length == 0) return description ?? string.Empty;
        return string.IsNullOrEmpty(description) ? weapon : description + "\n\n" + weapon;
    }

    internal sealed class State { internal bool ShowBonuses = true; internal int ItemType; internal string Raw; }
    private static readonly ConditionalWeakTable<XUiController, State> States = new ConditionalWeakTable<XUiController, State>();
    internal static State For(XUiController owner) => States.GetValue(owner, _ => new State());

    /// <summary>A newly shown item with bonuses opens on its bonuses again.</summary>
    internal static void Track(State state, ItemStack stack)
    {
        int type = stack == null || stack.IsEmpty() ? 0 : stack.itemValue.type;
        string raw = null;
        if (type != 0) stack.itemValue.TryGetMetadata(RebirthArmorBonusStorage.MetadataKey, out raw);
        if (type != state.ItemType || raw != state.Raw) { state.ItemType = type; state.Raw = raw; state.ShowBonuses = true; }
    }
}

[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.Init))]
internal static class RebirthArmorBonusInfoTabInitPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance)
    {
        var button = __instance.GetChildById(RebirthArmorBonusInfoTab.ButtonId);
        if (button == null) return;
        button.OnPress += (sender, mouse) =>
        {
            var state = RebirthArmorBonusInfoTab.For(__instance);
            state.ShowBonuses = !state.ShowBonuses;
            // 2.6: the bonus tab shows in the description area.
            if (state.ShowBonuses) __instance.showStats = false;
            __instance.IsDirty = true;
            __instance.RefreshBindings();
        };
    }
}

[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValueInternal))]
internal static class RebirthArmorBonusInfoTabBindingPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance, ref string value, string bindingName, ref bool __result)
    {
        if (bindingName == "showbonusoption")
        {
            value = RebirthArmorBonusInfoTab.HasBonuses(__instance?.itemStack) ? "true" : "false";
            __result = true;
            return;
        }
        if (bindingName != "itemdescription" || __instance?.itemStack == null || __instance.itemStack.IsEmpty()) return;
        var state = RebirthArmorBonusInfoTab.For(__instance);
        RebirthArmorBonusInfoTab.Track(state, __instance.itemStack);
        value = RebirthArmorBonusInfoTab.HasBonuses(__instance.itemStack) && state.ShowBonuses
            ? RebirthArmorBonusInfoTab.BonusText(__instance.itemStack)
            : RebirthArmorBonusInfoTab.WithWeapon(value, __instance.itemStack);
    }
}

[Preserve]
public sealed class RebirthArmorBonusInfoTabModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.armor-bonus-info-tab");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorBonusInfoTabInitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorBonusInfoTabBindingPatch));
    }
}
