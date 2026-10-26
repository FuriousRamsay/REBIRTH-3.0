using UnityEngine;
public sealed partial class XUiC_RebirthCompanions
{
    private bool EquipmentBusy=>(GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment)?.Busy==true;
    private bool EquipmentRequestPending=>(GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment)?.RequestPending==true;
    private bool NativeHumanInventory()
    {
        var selected=Selected();
        return currentTab==Tab.Inventory&&selected!=null&&selected.Kind==RebirthCompanionTargetKind.Npc&&
            !selected.IsDog&&!selected.IsBoundUndead;
    }
    private void RenderEquipmentTab()
    {
        bool equipment=currentTab==Tab.Equipment;
        var panel=GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment;
        // Enter the native cargo view before its first reply. Gating on
        // NativeStorageFor here deadlocks the initial inventory snapshot.
        bool storage=NativeHumanInventory();
        equipment|=storage;
        panel?.Present(Selected(),equipment,storage);
        SetVisible(panelInventory,currentTab==Tab.Inventory&&!storage);
        SetVisible(GetChildById("companionSelectedCard"),!equipment);
        SetVisible(GetChildById("companionCommandCard"),!equipment&&currentTab==Tab.Overview);
        SetVisible(GetChildById("companionFeedback"),!equipment);
        // The companion shell has one authored size across all tabs. Its roster and
        // body geometry are owned by the shared window layout, not this tab.
    }
}



