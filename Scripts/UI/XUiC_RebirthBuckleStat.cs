using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthBuckleStat : XUiC_HUDStatBar
{
    private Texture2D gauge;
    private XUiV_Texture view;
    private XUiV_Sprite icon;
    private readonly Color32[] pixels=new Color32[128*96];
    private float next;
    private RebirthVitalHudKind previousKind = (RebirthVitalHudKind)(-1);
    private int previousCurrent=-1,previousCap=-1,revision=-1;
    private RebirthVitalHudKind Kind => statType==HUDStatTypes.Health?RebirthVitalHudKind.Health:
        statType==HUDStatTypes.Stamina?RebirthVitalHudKind.Stamina:
        statType==HUDStatTypes.Food?RebirthVitalHudKind.Food:RebirthVitalHudKind.Water;
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="bucklecurrent")
        {
            base.GetBindingValueInternal(ref value,"statcurrentwithmax");
            int slash=value.IndexOf('/');if(slash>=0)value=value.Substring(0,slash);
            return true;
        }
        return base.GetBindingValueInternal(ref value,name);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+0.05f;
        if(view?.UiTransform==null)
        {
            XUiV_Texture resolved=GetChildById("buckleGauge")?.ViewComponent as XUiV_Texture;
            if(!ReferenceEquals(view,resolved))
            {
                view=resolved;previousCurrent=previousCap=revision=-1;
                if(view!=null&&gauge!=null)view.Texture=gauge;
            }
        }
        if(icon?.UiTransform==null)icon=GetChildById("Icon")?.ViewComponent as XUiV_Sprite;
        if(view==null)return;
        string value="0",cap="0";
        base.GetBindingValueInternal(ref value,"statfill");base.GetBindingValueInternal(ref cap,"statmodifiedmax");
        float current,maximum;
        float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out current);
        float.TryParse(cap,NumberStyles.Float,CultureInfo.InvariantCulture,out maximum);
        maximum=Mathf.Clamp01(maximum);current=Mathf.Clamp(current,0,maximum);
        int c=Mathf.RoundToInt(current*1000),m=Mathf.RoundToInt(maximum*1000);
        if(gauge==null)
        {
            gauge=new Texture2D(128,96,TextureFormat.RGBA32,false){name="RebirthBuckle"+Kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            view.Texture=gauge;
        }
        if(c==previousCurrent&&m==previousCap&&revision==RebirthVitalHudColors.Revision&&previousKind==Kind)return;
        previousCurrent=c;previousCap=m;revision=RebirthVitalHudColors.Revision;previousKind=Kind;
        Color32 color=RebirthVitalHudColors.Get(Kind);if(icon!=null)icon.Color=color;
        bool right=Kind==RebirthVitalHudKind.Stamina||Kind==RebirthVitalHudKind.Water;
        bool bottom=Kind==RebirthVitalHudKind.Food||Kind==RebirthVitalHudKind.Water;
        RebirthBuckleGaugeGeometry.Render(pixels, current, maximum, color, right, bottom);
        gauge.SetPixels32(pixels);gauge.Apply(false,false);
    }
    public static Color32 Sample(float position,float current,float cap,Color32 active)
    {
        cap=Mathf.Clamp01(cap);current=Mathf.Clamp(current,0,cap);
        return position>=cap?new Color32(0,0,0,255):position>=current?new Color32(106,108,112,255):active;
    }
}
