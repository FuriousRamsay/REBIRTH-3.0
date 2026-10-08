using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthItemEditorHeader : XUiC_WindowNonPagingHeader
{
    public static XUiC_RebirthItemEditorHeader ActiveInstance { get; private set; }
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    public override void Update(float dt)
    {
        base.Update(dt);
        if (IsEditorOpen) hud.Maintain(xui);
    }
    public bool IsEditorOpen { get; private set; }
    public override void Init()
    {
        base.Init();
        RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();
        GetChildById("editorBack").OnPress += (sender, button) =>
        {
            var manager = xui.playerUI.windowManager;
            manager.Close(manager.IsWindowOpen("cosmetics") ? "cosmetics" : "assemble");
        };
    }

    public override void OnOpen()
    {
        IsEditorOpen = true;
        ActiveInstance = this;
        base.OnOpen();
        Update(0);
        Log.Out("[REBIRTH ItemEditor] opened editor; returnCaptured=" + RebirthItemWindowLifecycle.HasReturn(xui));
    }
    public override void OnClose()
    {
        IsEditorOpen = false;
        hud.Restore();
        if (ActiveInstance == this) ActiveInstance = null;
        base.OnClose();
    }
}
