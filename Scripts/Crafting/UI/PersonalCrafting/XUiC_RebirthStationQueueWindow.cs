using UnityEngine.Scripting;

// Legacy station modules still own their native fuel, output and queue transactions.
// Suppress world HUD overlays while their station UI is open, just as other Rebirth windows do.
[Preserve]
public sealed class XUiC_RebirthStationQueueWindow : XUiController
{
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    public override void OnOpen()
    {
        base.OnOpen();
        hud.Maintain(xui);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (windowGroup != null && windowGroup.isShowing) hud.Maintain(xui);
    }
    public override void OnClose()
    {
        hud.Restore();
        base.OnClose();
    }
}
