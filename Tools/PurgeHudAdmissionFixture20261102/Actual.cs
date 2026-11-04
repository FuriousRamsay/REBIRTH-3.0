partial class XUiC_RebirthPurgeInformation {
    internal static bool Open(XUi ui,RebirthJournalEntry guide,string image,bool manual=false)
    {
        var controller=ui?.GetChildByType<XUiC_RebirthPurgeInformation>();
        if(controller==null || controller.windowGroup==null || ui.playerUI.windowManager.IsFullHUDDisabled())return false;
        controller.entry=guide;controller.sprite=image;controller.manualReading=manual;
        // Own the explicit Cancel press below: native auto-Escape closes on press,
        // before a release handler can mark this as a deliberate dismissal.
        ui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,true,true);
        // OnOpen acknowledges the admitted request; isShowing may still remain
        // false until the manager's next update.
        return true;
    }
}