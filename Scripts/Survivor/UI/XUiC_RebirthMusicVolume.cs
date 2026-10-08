using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthMusicVolume : XUiController
{
    private XUiController track;
    private XUiV_Sprite fill;
    private XUiV_Label valueLabel,hints;
    private bool dragging;
    private int percent;
    private float refresh;
    public override void Init()
    {
        if(track!=null){track.OnDrag-=Drag;track.OnPress-=Click;}
        dragging=false;
        base.Init();
        track=GetChildById("musicVolumeTrack");
        fill=GetChildById("musicVolumeFill")?.ViewComponent as XUiV_Sprite;
        valueLabel=GetChildById("musicVolumeValue")?.ViewComponent as XUiV_Label;
        hints=GetChildById("musicBindingHints")?.ViewComponent as XUiV_Label;
        if(track!=null){track.OnDrag+=Drag;track.OnPress+=Click;}
    }
    public override void OnOpen(){base.OnOpen();dragging=false;RefreshLevel();RefreshHints();}
    public override void OnClose(){dragging=false;RefreshLevel();base.OnClose();}
    public override void Cleanup()
    {
        if(track!=null){track.OnDrag-=Drag;track.OnPress-=Click;}
        dragging=false;base.Cleanup();
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(!IsOpen)return;
        refresh+=dt;if(refresh<0.25f)return;refresh=0;
        if(!dragging)RefreshLevel();
        RefreshHints();
    }
    private bool ReadPointer()
    {
        if(!IsOpen||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return false;
        var t=track?.ViewComponent?.UiTransform;var camera=UICamera.currentCamera;
        if(t==null||camera==null)return false;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new Plane(t.forward,t.position).Raycast(ray,out distance))return false;
        Vector2 point=t.InverseTransformPoint(ray.GetPoint(distance));
        percent=Mathf.RoundToInt(Mathf.Clamp01(point.x/300f)*100f);RenderLevel();return true;
    }
    private void Click(XUiController sender,int button)
    {
        if(dragging||(button!=0&&button!=-1))return;
        if(ReadPointer())Commit();
    }
    private void Drag(XUiController sender,EDragType type,Vector2 delta)
    {
        if(type==EDragType.DragStart)dragging=true;
        if(!dragging)return;
        if(!ReadPointer()){dragging=false;RefreshLevel();return;}
        if(type==EDragType.DragEnd){dragging=false;Commit();}
    }
    private void Commit()
    {
        string message;
        if(!RebirthLegacyMusicPlaybackService.SetVolume(percent/100f,out message))
            RebirthSurvivorSupportUiFeedback.Receive(false,message);
        RefreshLevel();
    }
    private void RefreshLevel(){percent=Mathf.RoundToInt(RebirthLegacyMusicPlaybackService.Volume*100f);RenderLevel();}
    private void RenderLevel()
    {
        if(fill!=null)fill.Fill=percent/100f;
        if(valueLabel!=null)valueLabel.Text=percent+"%";
    }
    private static string Binding(InControl.PlayerAction action)
    {
        try{var text=action?.GetBindingString(false);return string.IsNullOrEmpty(text)?Localization.Get("xuiRebirthMusicUnbound"):text;}
        catch{return Localization.Get("xuiRebirthMusicUnbound");}
    }
    private void RefreshHints()
    {
        if(hints==null)return;
        var a=RebirthNativeControls.Actions;
        string text=string.Format(Localization.Get("xuiRebirthMusicBindingHints"),Binding(a?.MusicPlayPause),
            Binding(a?.MusicNext),Binding(a?.MusicVolumeDown),Binding(a?.MusicVolumeUp));
        if(hints.Text!=text)hints.Text=text;
    }
}