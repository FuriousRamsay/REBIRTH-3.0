using UnityEngine.Scripting;
using HarmonyLib;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthNativeCharacterEditorGuard : XUiController
{
    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (xui == null || xui.playerUI == null) return;
        if (!XUiUtils.HotkeysAllowedFor(viewComponent)) return;
        if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            XUiC_RebirthSurvivorCreator.TryHandleNativeCharacterEditorEscape();
    }
}

// The stock controller schedules Apply directly even when its button is hidden.
// Intercept that scheduling boundary while the owned child-editor session exists.
[HarmonyPatch(typeof(XUiC_CustomCharacterWindowGroup),nameof(XUiC_CustomCharacterWindowGroup.Update))]
public static class RebirthOwnedNativeCharacterEditorApplyPatch
{
    private static bool Prefix(XUiC_CustomCharacterWindowGroup __instance)
    {
        var ui=__instance?.xui;
        if(ui?.playerUI==null||!XUiUtils.HotkeysAllowedFor(__instance.ViewComponent))return true;
        if(ui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            return !XUiC_RebirthSurvivorCreator.TryHandleNativeCharacterEditorEscape(ui);
        if(!ui.playerUI.playerInput.GUIActions.Apply.WasReleased)return true;
        return !XUiC_RebirthSurvivorCreator.TryHandleNativeCharacterEditorApply(ui);
    }
}
