/// <summary>One menu-ownership check for the native drag cursor across REBIRTH inventory windows.</summary>
public static class RebirthWindowInventoryScope
{
    private static bool Showing(XUi ui,XUiController owner)
        => owner!=null&&ReferenceEquals(owner.xui,ui)&&owner.WindowGroup?.isShowing==true;

    public static bool OwnsCursor(XUi ui)
    {
        if(ui==null)return false;
        if(RebirthContextNavigationService.IsContextVisible(ui))return true;
        if(Showing(ui,ui.Trader?.TraderWindowGroup as XUiC_RebirthTraderWorkspace))return true;
        if(Showing(ui,XUiC_RebirthQuestTurnInWorkspace.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthCreativeWorkspace.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthCookingWorkspace.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthStationWorkspace.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthSurvivorCharacter.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthPersonalCrafting.ActiveInstance))return true;
        if(Showing(ui,XUiC_RebirthItemEditorHeader.ActiveInstance))return true;
        var manager=ui.playerUI?.windowManager;
        return manager!=null&&(manager.IsWindowOpen("rebirthBackpackLibrary")||manager.IsWindowOpen("rebirthBackpackSellStash"));
    }
}