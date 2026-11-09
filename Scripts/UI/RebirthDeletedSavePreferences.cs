using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Platform;
using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthDeletedSavePreferencesModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        if (GameManager.IsDedicatedServer) return; // There is no native save-selection menu on a dedicated host.
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.local-save-deletion"), typeof(RebirthDeletedSavePreferences));
    }
}

[HarmonyPatch]
internal static class RebirthDeletedSavePreferences
{
    private static FieldInfo saveEntry;
    private static MethodBase confirmation;
    internal static string Diagnostic = "no confirmed save deletion observed";
    internal static string Status()
    {
        var patches = confirmation == null ? null : Harmony.GetPatchInfo(confirmation);
        bool prefix = patches?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(RebirthDeletedSavePreferences)) == true;
        bool postfix = patches?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(RebirthDeletedSavePreferences)) == true;
        return "installed=" + (prefix && postfix) + ", last=" + Diagnostic;
    }
    private static MethodBase TargetMethod()
    {
        // Bind the actual confirmation callback, not list selection or opening
        // the confirmation dialog. Cancel must never touch preferences.
        foreach (var type in typeof(XUiC_ContinueGame).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            var method = AccessTools.DeclaredMethod(type, "<deleteSaveWithConfirmation>g__Confirm|0");
            var field = AccessTools.DeclaredField(type, "saveEntry");
            if (method == null || field?.FieldType != typeof(XUiC_SavegamesList.ListEntry)) continue;
            saveEntry = field; confirmation = method; return method;
        }
        throw new MissingMethodException("Native confirmed save-deletion callback unavailable");
    }
    private static void Prefix(object __instance, out RebirthSaveLocalDataCleanup.Plan __state)
    {
        __state = null;
        try
        {
            var entry = saveEntry.GetValue(__instance) as XUiC_SavegamesList.ListEntry;
            if (entry == null || !Guid.TryParse(entry.WorldState?.Guid, out var id)) return;
            string directory = entry.GetSaveDir();
            __state = RebirthSaveLocalDataCleanup.Capture(GameIO.GetUserGameDataDir(), directory, id, SdDirectory.Exists(directory));
        }
        catch (Exception e) { Diagnostic = "capture refused: " + e.Message; Log.Warning("[REBIRTH save deletion] " + Diagnostic); }
    }
    private static void Postfix(RebirthSaveLocalDataCleanup.Plan __state)
    {
        if (__state == null) return;
        try
        {
            bool deleted = RebirthSaveLocalDataCleanup.DeleteAfterNativeConfirmation(__state, SdDirectory.Exists(__state.SaveDirectory), key => {
                RebirthHudTrackingPreferenceService.RetireDeletedWorld(key);
                RebirthJournalGuideService.RetireDeletedWorld(key);
            }, out int files);
            Diagnostic = deleted ? "confirmed cleanup complete; files=" + files : "native save still present; local data retained";
            RebirthDebug.Log("ui", "[REBIRTH save deletion] " + Diagnostic);
        }
        catch (Exception e) { Diagnostic = "cleanup failed: " + e.Message; Log.Warning("[REBIRTH save deletion] " + Diagnostic); }
    }
}
