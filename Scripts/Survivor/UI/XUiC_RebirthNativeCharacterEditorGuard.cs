using UnityEngine.Scripting;

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
