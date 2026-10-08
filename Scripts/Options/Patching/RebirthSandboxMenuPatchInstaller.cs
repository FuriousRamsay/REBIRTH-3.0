using HarmonyLib;
using System;

#nullable disable

public static class RebirthSandboxMenuPatchInstaller
{
    private const string HarmonyId = "rebirth.fresh.sandbox.menu";
    private static readonly HarmonyLib.Harmony Harmony = new HarmonyLib.Harmony(HarmonyId);
    private static bool installed;

    public static void Install()
    {
        if (installed)
            return;

        Harmony.CreateClassProcessor(typeof(RebirthSandboxSaveOptionsPatch)).Patch();
        Harmony.CreateClassProcessor(typeof(RebirthSandboxRefreshOptionsPatch)).Patch();
        RebirthScrollbarPagingInstaller.Install();
        installed = true;
    }

    internal static void PostfixSaveGameOptions(bool _saveAsLastUsed)
    {
        try
        {
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                "base SaveGameOptions postfix saveAsLastUsed=" + _saveAsLastUsed
                + " before=" + RebirthSandboxUiSession.CharacterProgressionDebugContext); }
            RebirthSandboxUiSession.Commit(_saveAsLastUsed);
        }
        catch (Exception ex)
        {
            Log.Error("[RebirthSandbox] Failed to persist menu settings: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void PostfixUpdateOptionValuesFromGamePrefs()
    {
        try
        {
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                "base UpdateOptionValuesFromGamePrefs postfix BEFORE reload"); }
            RebirthSandboxUiSession.ReloadForCurrentContext();
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                "base UpdateOptionValuesFromGamePrefs postfix AFTER reload "
                + RebirthSandboxUiSession.CharacterProgressionDebugContext); }
        }
        catch (Exception ex)
        {
            Log.Warning("[RebirthSandbox] Failed to refresh menu context: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(XUiC_NewContinueGameSettings), nameof(XUiC_NewContinueGameSettings.SaveGameOptions))]
internal static class RebirthSandboxSaveOptionsPatch
{
    private static void Postfix(bool _saveAsLastUsed)
    { RebirthSandboxMenuPatchInstaller.PostfixSaveGameOptions(_saveAsLastUsed); }
}

[HarmonyPatch(typeof(XUiC_NewContinueGameSettings), nameof(XUiC_NewContinueGameSettings.UpdateOptionValuesFromGamePrefs))]
internal static class RebirthSandboxRefreshOptionsPatch
{
    private static void Postfix()
    { RebirthSandboxMenuPatchInstaller.PostfixUpdateOptionValuesFromGamePrefs(); }
}

