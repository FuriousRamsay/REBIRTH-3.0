using UnityEngine;
using UnityEngine.Scripting;

// Keep XUiV_Button for the native vehicle controller's casts and Enabled
// assignments. Render its frame with the crafting action sprite implementation.
[Preserve]
public sealed class XUiC_RebirthVehicleActionButton : XUiController
{
    private XUiV_Sprite frame;
    private bool over;
    public override void Init()
    {
        base.Init();
        frame=Parent.GetChildById(ViewComponent.ID+"ActionFrame")?.ViewComponent as XUiV_Sprite;
        OnHover+=(sender,hover)=>{over=hover;Refresh();};
        Refresh();
    }
    public override void Update(float dt){base.Update(dt);Refresh();}
    public override void OnOpen(){over=false;base.OnOpen();Refresh();}
    private void Refresh()
    {
        if(frame==null)return;
        bool highlight=over && ViewComponent.Enabled;
        string sprite=highlight?"ui_game_select_row":"menu_empty2px";
        Color color=highlight?Color.white:new Color32(240,240,244,255);
        if(frame.SpriteName!=sprite)frame.SpriteName=sprite;
        if(frame.Color!=color)frame.Color=color;
    }
}
