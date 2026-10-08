using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthMessagePopup : XUiC_PopupToolTip
{
    private XUiV_Label text;
    private UILabel nativeLabel;
    private readonly XUiV_Texture[] panels = new XUiV_Texture[5];
    private string lastProcessedText;
    private int lastScreenWidth = -1;
    private int lastHeight = -1;
    private bool lastVisible;
    private bool refsReady;

    public override void Init()
    {
        base.Init();
        ResolveReferences();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!refsReady) ResolveReferences();
        if (text == null) return;

        bool visible = !string.IsNullOrEmpty(tooltipText) && textAlphaCurrent > .01f;
        if (!visible)
        {
            if (lastVisible) SetPanelsVisible(false);
            lastVisible = false;
            return;
        }

        string processed = nativeLabel != null ? (nativeLabel.processedText ?? string.Empty) : (tooltipText ?? string.Empty);
        int screenWidth = xui.GetXUiScreenSize().x;
        if (lastVisible && string.Equals(processed, lastProcessedText) && screenWidth == lastScreenWidth) return;

        int lines = 1;
        for (int i = 0; i < processed.Length; i++) if (processed[i] == '\n') { lines = 2; break; }
        int height = lines == 1 ? 40 : 66;
        GetGeometry(screenWidth,height,out var position,out var size);
        int width=size.x;
        text.Size = new Vector2i(width - 40, 52);
        ViewComponent.Position = position;
        ViewComponent.Size = new Vector2i(width, height);
        text.Position = new Vector2i(width / 2, -height / 2);
        SetPanel(panels[0], 0, 0, width, height, new Color32(24, 24, 29, 250), true);
        SetPanel(panels[1], 0, 0, width, 2, new Color32(201, 166, 239, 255), true);
        SetPanel(panels[2], 0, -height + 2, width, 2, new Color32(201, 166, 239, 255), true);
        SetPanel(panels[3], 0, 0, 2, height, new Color32(201, 166, 239, 255), true);
        SetPanel(panels[4], width - 2, 0, 2, height, new Color32(201, 166, 239, 255), true);

        lastProcessedText = processed;
        lastScreenWidth = screenWidth;
        lastHeight = height;
        lastVisible = true;
    }

    internal static void GetGeometry(int screenWidth,int height,out Vector2i position,out Vector2i size)
    {
        float scale=Mathf.Min(1f,Mathf.Max(1,screenWidth-120)/(float)XUiC_RebirthToolbeltLayout.DesignWidth);
        int width=Mathf.RoundToInt(XUiC_RebirthToolbeltLayout.DesignWidth*scale);
        position=new Vector2i(-Mathf.RoundToInt(width/2f),Mathf.RoundToInt(66*scale)+4+height);
        size=new Vector2i(width,height);
    }    private void ResolveReferences()
    {
        XUiController textController = GetChildById("lblText");
        text = textController != null ? textController.ViewComponent as XUiV_Label : null;
        nativeLabel = text != null && text.UiTransform != null ? text.UiTransform.GetComponent<UILabel>() : null;
        panels[0] = GetTexture("rebirthMessageBackground");
        panels[1] = GetTexture("messageTop");
        panels[2] = GetTexture("messageBottom");
        panels[3] = GetTexture("messageLeft");
        panels[4] = GetTexture("messageRight");
        refsReady = text != null;
    }

    private XUiV_Texture GetTexture(string id)
    {
        XUiController child = GetChildById(id);
        XUiV_Texture view = child != null ? child.ViewComponent as XUiV_Texture : null;
        if (view != null) view.AutoUnload = false;
        return view;
    }

    private void SetPanelsVisible(bool visible)
    {
        for (int i = 0; i < panels.Length; i++) if (panels[i] != null) panels[i].IsVisible = visible;
    }

    internal static void SetPanel(XUiV_Texture view, int x, int y, int width, int height, Color color, bool visible)
    {
        if (view == null) return;
        if (view.Texture != Texture2D.whiteTexture) view.Texture = Texture2D.whiteTexture;
        view.Color = color;
        view.Position = new Vector2i(x, y);
        view.Size = new Vector2i(width, height);
        view.IsVisible = visible;
    }
}
