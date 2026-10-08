using HarmonyLib;

// Run after the native manager has removed Creative's action set, not inside its OnClose.
[HarmonyPatch(typeof(GUIWindowManager),nameof(GUIWindowManager.Close),new[]{typeof(GUIWindow),typeof(bool)})]
internal static class RebirthCreativeCloseInputPatch
{
    private static void Postfix(GUIWindowManager __instance,GUIWindow _w)
    {
        if(_w?.Id!="creative"||_w.isShowing||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        __instance.ResetActionSets();
    }
}