using UnityEngine;
[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthMinimapTransparencyOption : XUiController
{
    private bool dragging;
    private int percent;
    private int committedPercent;

    public override void OnOpen()
    {
        base.OnOpen();
        committedPercent=Mathf.Clamp(SdPlayerPrefs.GetInt("RebirthMinimapTransparency",0),0,100);
        percent=committedPercent;
        dragging=false;
        ApplyEffectiveValue();
        RefreshValue();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        // Screen-space hit testing is needed only on press or during an active drag.
        bool pressed = Input.GetMouseButtonDown(0);
        if (!dragging && !pressed) return;
        var track=GetChildById("opacityTrack")?.ViewComponent;
        if(track?.UiTransform==null || !track.UiTransform.gameObject.activeInHierarchy)return;
        var camera=xui.playerUI.camera;
        var left=camera.WorldToScreenPoint(track.UiTransform.TransformPoint(new Vector3(0,-20,0)));
        var right=camera.WorldToScreenPoint(track.UiTransform.TransformPoint(new Vector3(270,-20,0)));
        var mouse=Input.mousePosition;
        float width = right.x-left.x;
        if (width <= 0.01f) return;
        bool inside=mouse.x>=left.x && mouse.x<=right.x && Mathf.Abs(mouse.y-left.y)<=22;
        if(pressed && inside)dragging=true;
        if(dragging && Input.GetMouseButton(0))
        {
            int next=Mathf.RoundToInt(Mathf.Clamp01((mouse.x-left.x)/width)*100);
            if(next!=percent){percent=next;ApplyEffectiveValue();RefreshValue();}
        }
        if(dragging && Input.GetMouseButtonUp(0)) Commit();
    }

    public override void OnClose()
    {
        // Interrupted drag is a cancel: persisted and effective values converge.
        if(dragging)
        {
            percent=committedPercent;
            dragging=false;
            ApplyEffectiveValue();
            RefreshValue();
        }
        base.OnClose();
    }

    private void Commit()
    {
        dragging=false;
        committedPercent=percent;
        SdPlayerPrefs.SetInt("RebirthMinimapTransparency",committedPercent);
        SdPlayerPrefs.Save();
        ApplyEffectiveValue();
    }

    private void ApplyEffectiveValue()
    {
        XUiC_RebirthMiniMap.TerrainOpacity=1f-percent/100f;
    }

    private void RefreshValue()
    {
        var label=GetChildById("opacityValue")?.ViewComponent as XUiV_Label;
        if(label!=null)label.Text=percent+"%";
        var fill=GetChildById("opacityFill")?.ViewComponent as XUiV_Sprite;
        if(fill!=null)fill.Fill=percent/100f;
        var thumb=GetChildById("opacityThumb")?.ViewComponent;
        if(thumb!=null)thumb.Position=new Vector2i(Mathf.RoundToInt(percent*2.62f),-11);
    }
}
