#if !REBIRTH_DEBUG
// Production references retain their logging API without compiling the mutable
// Debug-folder implementation into Release or enabling diagnostic mutations.
public static class RebirthSurvivorDebug
{
    public static bool Enabled=>false;
    public static bool TraitUiLoggingEnabled=>RebirthLogSettings.TraitUiLoggingEnabled;
    public static bool TraitUiLoggingConfigured=>RebirthLogSettings.TraitUiLoggingConfigured;
    public static string TraitUiLoggingConfigPath=>RebirthLogSettings.ConfigPath;
    public static void SetEnabled(bool value)
    {
        if(value)Log.Warning("[REBIRTH Survivor] Mutating diagnostics are unavailable in this build.");
    }
    public static void SetTraitUiLoggingEnabled(bool value)=>RebirthLogSettings.SetTraitUiRuntimeOverride(value);
    public static void LoadFromConfigRoot(string root)=>RebirthLogSettings.LoadFromConfigRoot(root);
    public static void Reset()=>RebirthLogSettings.RestoreConfiguredTraitUi();
}
#endif
