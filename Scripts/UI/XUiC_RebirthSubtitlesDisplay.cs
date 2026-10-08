using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthSubtitlesDisplay : XUiC_SubtitlesDisplay
{
    private XUiV_Label speaker, subtitle;
    private XUiView panel;
    private readonly XUiV_Texture[] frame=new XUiV_Texture[5];
    public override void Init()
    {
        base.Init();
        speaker = GetChildById("lblSpeaker")?.ViewComponent as XUiV_Label;
        subtitle = GetChildById("lblSubtitle")?.ViewComponent as XUiV_Label;
        panel = GetChildById("bgPanel")?.ViewComponent;
        string[] ids={"rebirthSubtitleBackground","subtitleTop","subtitleBottom","subtitleLeft","subtitleRight"};
        for(int i=0;i<ids.Length;i++){frame[i]=GetChildById(ids[i])?.ViewComponent as XUiV_Texture;if(frame[i]!=null)frame[i].AutoUnload=false;}
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (speaker == null || subtitle == null || panel == null) return;
        int screenWidth = xui.GetXUiScreenSize().x;
        var label = subtitle.UiTransform != null ? subtitle.UiTransform.GetComponent<UILabel>() : null;
        int height = label != null && (label.processedText ?? "").IndexOf('\n') >= 0 ? 66 : 40;
        XUiC_RebirthMessagePopup.GetGeometry(screenWidth,height,out var position,out var size);
        int width=size.x;
        ViewComponent.Position=position;
        ViewComponent.TryUpdatePosition();
        ViewComponent.Size = new Vector2i(width, height);
        panel.Size = new Vector2i(width, height);
        panel.Position=Vector2i.zero;
        XUiC_RebirthMessagePopup.SetPanel(frame[0],0,0,width,height,new Color32(24,24,29,250),true);
        var border=new Color32(201,166,239,255);
        XUiC_RebirthMessagePopup.SetPanel(frame[1],0,0,width,2,border,true);
        XUiC_RebirthMessagePopup.SetPanel(frame[2],0,-height+2,width,2,border,true);
        XUiC_RebirthMessagePopup.SetPanel(frame[3],0,0,2,height,border,true);
        XUiC_RebirthMessagePopup.SetPanel(frame[4],width-2,0,2,height,border,true);
        speaker.FontSize = subtitle.FontSize = 22;
        speaker.Position = new Vector2i(20, -height / 2);
        speaker.Size = new Vector2i(160, height - 4);
        subtitle.Position = new Vector2i(180, -height / 2);
        subtitle.Size = new Vector2i(width - 200, height - 4);
    }
}