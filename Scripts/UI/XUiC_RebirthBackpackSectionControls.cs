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
        var filter=GetChildById("sectionFilter");
        if(filter!=null)filter.OnPress+=Filter;
    }
    public override void OnOpen(){base.OnOpen();SetActive(GetChildById("sectionLock"),false);SetActive(GetChildById("sectionFilter"),false);}
    private static void SetActive(XUiController control,bool active)
    {
        if(control?.ViewComponent is XUiV_Button button)
        {
            button.DefaultSpriteColor=Color.white;
            button.SelectedSpriteColor=new Color32(181,140,255,255);
            button.Selected=active;
        }
    }
    private bool Ready(int button)
    {
        if(button!=0&&button!=-1||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||xui.DragAndDropWindow?.IsEmpty()!=true)return false;
        var player=xui.playerUI?.entityPlayer;
        if(player==null||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player))return false;
        backpack=backpack??windowGroup.Controller.GetChildByType<XUiC_RebirthBackpackSectionBackpack>();
        return backpack?.ViewComponent?.UiTransform?.gameObject.activeInHierarchy==true;
    }
    private void Toggle(XUiController sender,int button)
    {
        if(!Ready(button))return;backpack.ToggleLocks();
        SetActive(sender,backpack.LockMode);
    }
    private void Sort(XUiController sender,int button){if(Ready(button))backpack.SortItems();}
    private void Filter(XUiController sender,int button)
    {
        if(!Ready(button))return;
        backpack.ToggleModuleFilter();
        SetActive(sender,backpack.ModuleFilter);
    }
}
