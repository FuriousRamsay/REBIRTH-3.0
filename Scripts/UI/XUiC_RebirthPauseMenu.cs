using System;
using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthPauseMenu : XUiC_InGameMenuWindow
{
    private static readonly string[] FrameIds={"rebirthPauseBackdrop","rebirthPauseFrame"};
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    public override void Init()
    {
        base.Init();
        GetChildById("btnRebirthResume").GetChildByType<XUiC_SimpleButton>().OnPressed+=(s,b)=>xui.playerUI.windowManager.Close((GUIWindow)WindowGroup);
        var helpButton=GetChildById("btnRebirthPlayerHelp")?.GetChildByType<XUiC_SimpleButton>();
        if(helpButton!=null)helpButton.OnPressed+=OpenPlayerHelp;
    }
    private void OpenPlayerHelp(XUiController sender,int mouseButton)
    {
        if(mouseButton!=0&&mouseButton!=-1)return;
        var help=xui.FindWindowGroupByName("rebirthPlayerHelp")?.GetChildByType<XUiC_RebirthPlayerHelp>();
        if(help==null){Log.Error("[REBIRTH Player Help] Window controller is unavailable.");return;}
        xui.playerUI.windowManager.Close((GUIWindow)WindowGroup);
        xui.playerUI.windowManager.Open((GUIWindow)help.WindowGroup,true);
    }
    public override void OnOpen(){base.OnOpen();Layout();hud.Maintain(xui);}
    public override void Update(float dt){base.Update(dt);Layout();hud.Maintain(xui);}
    public override void OnClose(){hud.Restore();base.OnClose();}
    private void Layout()
    {
        var buttons=GetChildById("buttons");int count=0;foreach(var button in buttons.Children)if(button.ViewComponent.IsVisible)count++;
        int height=Math.Max(430,150+count*56);
        var screen=xui.GetXUiScreenSize();float scale=Math.Min(1f,Math.Min((screen.x-48)/860f,(screen.y-48)/(float)height));
        ViewComponent.Position=new Vector2i(-Mathf.RoundToInt(430*scale),Mathf.RoundToInt(height*scale/2));
        ViewComponent.TryUpdatePosition();ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
        ViewComponent.Size=new Vector2i(860,height);
        foreach(var id in FrameIds){var v=GetChildById(id).ViewComponent;v.Size=new Vector2i(860,height);}
        GetChildById("rebirthPauseRail").ViewComponent.Size=new Vector2i(3,height-48);
        var note=GetChildById("rebirthPauseHint").ViewComponent;note.Position=new Vector2i(28,-height+50);
    }
}
