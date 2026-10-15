using System;
using HarmonyLib;

// New-item default only. Native ItemValue cloning/loading restores serialized cosmetics,
// so an intentionally empty cosmetic slot remains empty after removal or reload.
[HarmonyPatch(typeof(ItemValue), MethodType.Constructor, new Type[] {
    typeof(int), typeof(int), typeof(int), typeof(bool), typeof(string[]), typeof(float) })]
internal static class RebirthCamperInitialDye
{
    private static readonly string[] Palette = {
        "modDyeBrown", "modDyeRed", "modDyeOrange", "modDyeYellow", "modDyeGreen",
        "modDyeBlue", "modDyePurple", "modDyeBlack", "modDyePink"
    };

    private static void Postfix(ItemValue __instance)
    {
#if REBIRTH_CAMPER_DIAGNOSTICS
        if (GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") return;
#endif
        if (!ThreadManager.IsMainThread() || __instance == null || __instance.IsEmpty()) return;
        ItemClass item = __instance.ItemClass;
        if (item == null || item.GetItemName() != "rebirthCamper80sPlaceable"
            || !item.Properties.GetBool("RebirthCamperRandomInitialDye")) return;
        // Preserve explicit loot/trader cosmetics rather than replacing an existing selection.
        for (int slot = 0; slot < __instance.CosmeticModCount; slot++)
        {
            ItemValue cosmetic = __instance.GetCosmeticMod(slot);
            if (cosmetic != null && !cosmetic.IsEmpty()) return;
        }
        if (__instance.CosmeticModCount != 1) return;
        var random = new System.Random(__instance.Seed);
        int start = random.Next(Palette.Length);
        for (int index = 0; index < Palette.Length; index++)
        {
            ItemClass dye = ItemClass.GetItemClass(Palette[(start + index) % Palette.Length]);
            if (dye == null) continue;
            __instance.SetCosmeticMod(0, new ItemValue(dye.Id, false));
            return;
        }
    }
}
