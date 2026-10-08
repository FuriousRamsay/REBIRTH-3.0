#nullable disable

/// <summary>
/// Single explicit gate for mutating Survivor diagnostics. Read-only automatic logging is owned
/// by RebirthLogSettings so those gates are available even before a world exists.
/// </summary>
public static class RebirthSurvivorDebug
{
    public static bool Enabled { get; private set; }
    public static bool TraitUiLoggingEnabled { get { return RebirthLogSettings.TraitUiLoggingEnabled; } }
    public static bool TraitUiLoggingConfigured { get { return RebirthLogSettings.TraitUiLoggingConfigured; } }
    public static string TraitUiLoggingConfigPath { get { return RebirthLogSettings.ConfigPath; } }

    public static void SetEnabled(bool value)
    {
        Enabled = value;
        Log.Out("[REBIRTH Survivor] debug mutation gate=" + Enabled);
    }

    public static void SetTraitUiLoggingEnabled(bool value)
    {
        RebirthLogSettings.SetTraitUiRuntimeOverride(value);
    }

    public static void LoadFromConfigRoot(string configRoot)
    {
        RebirthLogSettings.LoadFromConfigRoot(configRoot);
    }

    public static void Reset()
    {
        Enabled = false;
        RebirthLogSettings.RestoreConfiguredTraitUi();
    }
}
