using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthJournalTypeDropdown : XUiController
{
    private static readonly string[] Types = { "Notes", "Places", "Plans" };
    private string text = "Notes";
    private int openedFrame = -1;
    public string Text { get => text; set { text = System.Array.IndexOf(Types,value) >= 0 ? value : "Notes"; RefreshCaption(); } }
    public override void Init()
    {
        base.Init();
        GetChildById("journalTypeToggle").OnPress += (s,b) => {
            if(b!=0&&b!=-1)return;
            var choices=GetChildById("journalTypeChoices")?.ViewComponent;
            if(choices==null)return;
            choices.IsVisible=!choices.IsVisible;
            if(choices.IsVisible)openedFrame=Time.frameCount;
        };
        for(int i=0;i<Types.Length;i++){int index=i;GetChildById("journalTypeChoice"+i).OnPress += (s,b) => { if(b!=0&&b!=-1)return;Text=Types[index];GetChildById("journalTypeChoices").ViewComponent.IsVisible=false; };}
    }
    public override void OnOpen(){base.OnOpen();GetChildById("journalTypeChoices").ViewComponent.IsVisible=false;openedFrame=-1;RefreshCaption();}
    public override void Update(float dt)
    {
        base.Update(dt);
        var choices=GetChildById("journalTypeChoices")?.ViewComponent;
        if(choices==null||!choices.IsVisible||Time.frameCount==openedFrame||!Input.GetMouseButtonDown(0))return;
        GameObject hovered=UICamera.hoveredObject;
        Transform root=ViewComponent?.UiTransform;
        if(hovered==null||root==null||!hovered.transform.IsChildOf(root))choices.IsVisible=false;
    }
    private void RefreshCaption(){(GetChildById("journalTypeCaption")?.ViewComponent as XUiV_Label)?.SetTextImmediately(text);}
}
