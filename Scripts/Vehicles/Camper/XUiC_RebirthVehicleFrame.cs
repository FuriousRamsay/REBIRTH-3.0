using UnityEngine;
using UnityEngine.Scripting;

// Native vehicle assembly remains the action/inventory owner. This adapter
// supplies the same authored workspace and screen fitting as Modify.
[Preserve]
public sealed class XUiC_RebirthVehicleFrame : XUiC_VehicleFrameWindow
{
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    private bool opened;
    public override void Init()
    {
        base.Init();
        GetChildById("vehicleEditorClose").OnPress+=(sender,button)=>
        {if(opened&&(button==0||button==-1))xui.playerUI.windowManager.Close((GUIWindow)windowGroup);};
        GetChildByType<XUiC_RebirthCharacterOverviewList>()?.BindOwner(()=>opened&&windowGroup.isShowing);
    }
    public override void OnOpen(){RebirthVehicleServicePointerDrop.Reset(this);opened=true;base.OnOpen();}
    public override void Update(float dt)
    {
        RebirthVehicleServicePointerDrop.Tick(this);
        base.Update(dt);
        // Item Modify uses the authored Center window and native XUi scale.
        // Do not fit its logical coordinates a second time through screen bounds.
        if(opened)hud.Maintain(xui);
    }
    public override void OnClose(){RebirthVehicleServicePointerDrop.Reset(this);opened=false;hud.Restore();base.OnClose();}
    public override void Cleanup(){RebirthVehicleServicePointerDrop.Reset(this);hud.Restore();base.Cleanup();}
}

[Preserve]
public sealed class XUiC_RebirthVehiclePartSlot : XUiC_VehiclePartStack
{
    public override void Init(){base.Init();RebirthVehicleServicePointerDrop.Initialize(this);}
    private bool hovered;
    private XUiV_Sprite paletteBackground;
    private static readonly Color Tint = new Color32(35,35,43,245);
    public override void Update(float dt)
    {
        RebirthVehicleServicePointerDrop.Arm(this);
        base.Update(dt);
        paletteBackground = paletteBackground ?? GetChildById("background")?.ViewComponent as XUiV_Sprite;
        // Native selection texture becomes black when multiplied by the dark row tint.
        // Preserve native selection, using the editor's flat palette instead.
        var desired = hovered ? (Color)new Color32(76,55,100,255) : Tint;
        if (paletteBackground != null)
        {
            if (paletteBackground.SpriteName != "menu_empty") paletteBackground.SpriteName = "menu_empty";
            if (paletteBackground.Color != desired || paletteBackground.Sprite?.color != desired)
                paletteBackground.SetColorImmediately(desired);
        }
    }
    public override void OnHovered(bool over)
    {
        hovered=over;
        base.OnHovered(over);
        if(over)GetParentByType<XUiC_RebirthVehicleFrame>()?.GetChildByType<XUiC_RebirthEditorBackpack>()?.PreviewVehiclePart(ItemStack);
    }
}

[Preserve]
public sealed class XUiC_RebirthVehicleCosmeticSlot : XUiC_ItemCosmeticStack
{
    public override void Init(){base.Init();RebirthVehicleServicePointerDrop.Initialize(this);}
    private bool hovered;
    private XUiV_Sprite paletteBackground;
    private static readonly Color Tint = new Color32(35,35,43,245);
    public override void Update(float dt)
    {
        RebirthVehicleServicePointerDrop.Arm(this);
        base.Update(dt);
        paletteBackground = paletteBackground ?? GetChildById("background")?.ViewComponent as XUiV_Sprite;
        // Native selection texture becomes black when multiplied by the dark row tint.
        // Preserve native selection, using the editor's flat palette instead.
        var desired = hovered ? (Color)new Color32(76,55,100,255) : Tint;
        if (paletteBackground != null)
        {
            if (paletteBackground.SpriteName != "menu_empty") paletteBackground.SpriteName = "menu_empty";
            if (paletteBackground.Color != desired || paletteBackground.Sprite?.color != desired)
                paletteBackground.SetColorImmediately(desired);
        }
    }
    public override void OnHovered(bool over)
    {
        hovered=over;
        base.OnHovered(over);
        if(over)GetParentByType<XUiC_RebirthVehicleFrame>()?.GetChildByType<XUiC_RebirthEditorBackpack>()?.PreviewVehiclePart(ItemStack);
    }
}
