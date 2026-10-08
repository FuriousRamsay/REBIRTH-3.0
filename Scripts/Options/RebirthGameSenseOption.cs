using HarmonyLib;

#nullable disable

/// <summary>
/// Global (not per save) setting for the game's SteelSeries GameSense integration: keyboard/mouse lighting that reacts to health, ammo and so on.
/// It is only ever active when SteelSeries Engine is installed, and it costs performance (a worker thread posting web requests and steady garbage),
/// so REBIRTH ships it OFF. Stored in the player prefs; read once at game start, so a change applies the next time the game starts.
/// The vanilla launch argument <c>-nogamesense</c> still switches it off regardless of this option.
/// </summary>
public static class RebirthGameSenseOption
{
    public const string PrefKey = "RebirthGameSenseEnabled";

    public static bool Enabled
    {
        get { return SdPlayerPrefs.GetInt(PrefKey, 0) != 0; }
    }

    public static void SetEnabled(bool value)
    {
        SdPlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
        SdPlayerPrefs.Save();
    }
}

/// <summary>Skips <c>GameSenseManager.Init</c> (and with it every GameSense event, because the game calls the manager through a null-conditional) unless the option is on.</summary>
[HarmonyPatch(typeof(GameSenseManager), nameof(GameSenseManager.Init))]
public static class RebirthGameSenseOptionPatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (RebirthGameSenseOption.Enabled)
            return true;
        GameSenseManager.Instance = null;
        Log.Out("[REBIRTH] SteelSeries GameSense integration is off (Sandbox Options > Misc, takes effect at game start).");
        return false;
    }
}
