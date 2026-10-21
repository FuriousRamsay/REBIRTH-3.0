using UnityEngine;
public sealed partial class XUiC_RebirthCompanions
{
    private bool EquipmentBusy=>(GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment)?.Busy==true;
    private bool EquipmentRequestPending=>(GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment)?.RequestPending==true;
    private void RenderEquipmentTab()
    {
        bool equipment=currentTab==Tab.Equipment;
        var panel=GetChildById("panelEquipment") as XUiC_RebirthHumanEquipment;
        bool storage=currentTab==Tab.Inventory&&panel?.NativeStorageFor(Selected())==true;
        equipment|=storage;
        panel?.Present(Selected(),equipment,storage);
        SetVisible(GetChildById("companionSelectedCard"),!equipment);
        SetVisible(GetChildById("companionCommandCard"),!equipment);
        SetVisible(GetChildById("companionFeedback"),!equipment);
        // Existing tabs keep their established layout; only Equipment expands the shell.
        var size=equipment?new Vector2i(1856,940):new Vector2i(1450,700);
        if(ViewComponent.Size!=size){ViewComponent.Size=size;ViewComponent.Position=equipment?new Vector2i(-928,470):new Vector2i(-725,350);ViewComponent.TryUpdatePosition();}
        var roster=GetChildById("companionRosterCard")?.ViewComponent;if(roster!=null)roster.Size=equipment?new Vector2i(340,850):new Vector2i(340,565);
        var body=GetChildById("body")?.ViewComponent;if(body!=null)body.Size=equipment?new Vector2i(1856,891):new Vector2i(1450,651);
    }
}



