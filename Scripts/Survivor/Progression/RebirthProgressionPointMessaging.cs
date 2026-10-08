using HarmonyLib;

// Native level XP still controls level/game stage. Rebirth skills are learned
// through practice, so none of the native spending instructions apply here.
[HarmonyPatch(typeof(Localization), nameof(Localization.Get),
    new[] { typeof(string), typeof(bool), typeof(string) })]
public static class RebirthProgressionPointMessaging
{
    public static void Prefix(ref string _key)
    {
        // Filter before consulting world state: localization is a hot path.
        if (_key != "ttLevelUp" && _key != "skillPointTip" && _key != "skillPointTip_title"
            && _key != "armorNerdOutfitDesc" && _key != "drinkJarGrandpasForgettingElixirDesc"
            && _key != "drinkJarGrandpasForgettingElixirTooltip") return;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (_key == "ttLevelUp") _key = "rebirthLevelUpPractice";
        else if (_key == "skillPointTip") _key = "rebirthPracticeTip";
        else if (_key == "skillPointTip_title") _key = "rebirthPracticeTipTitle";
        else if (_key == "armorNerdOutfitDesc") _key = "rebirthNerdOutfitDescription";
        else if (_key == "drinkJarGrandpasForgettingElixirDesc") _key = "rebirthForgettingElixirDescription";
        else _key = "rebirthForgettingElixirTooltip";
    }
}
