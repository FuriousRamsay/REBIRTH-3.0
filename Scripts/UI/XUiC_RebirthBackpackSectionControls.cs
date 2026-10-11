using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthBackpackSectionControls:XUiController
{
    private XUiC_RebirthBackpackSectionBackpack backpack;
    public override void Init()
    {
        base.Init();
        GetChildById("sectionLock").OnPress+=Toggle;
        GetChildById("sectionSort").OnPress+=Sort;
    }
    public override void OnOpen(){base.OnOpen();(GetChildById("sectionLock")?.ViewComponent as XUiV_Sprite)?.SetColorImmediately(new Color32(255,255,255,255));}
    private bool Ready(int button)
    {
        if(button!=0&&button!=-1||!windowGroup.isShowing||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||xui.DragAndDropWindow?.IsEmpty()!=true)return false;
        var player=xui.playerUI?.entityPlayer;
        if(player==null||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player))return false;
        backpack=backpack??windowGroup.Controller.GetChildByType<XUiC_RebirthBackpackSectionBackpack>();
        return backpack!=null;
    }
    private void Toggle(XUiController sender,int button)
    {
        if(!Ready(button))return;backpack.ToggleLocks();
        (sender.ViewComponent as XUiV_Sprite)?.SetColorImmediately(backpack.LockMode?new Color32(220,195,120,255):new Color32(255,255,255,255));
    }
    private void Sort(XUiController sender,int button){if(Ready(button))backpack.SortItems();}
}
