using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthMessagePopup : XUiC_PopupToolTip
{
    private static XUiC_RebirthMessagePopup current;
    private XUiV_Label text;
    private UILabel nativeLabel;
    private XUiV_Sprite legacyBackground;
    private XUiV_Sprite frame, accent;
    private string lastProcessedText;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private int lastHeight = -1;
    private bool lastVisible;
    private bool refsReady;

    public override void Init()
    {
        base.Init();
        ResolveReferences();
        current=this;
    }

    // Keep ordinary notices together. Only paginate a contributor list when the
    // actual native font/wrapping says it cannot fit the available screen height.
    internal static List<string> ClearNoticePages(string contributors,Func<string,string> format)
    {
        var result=new List<string>();
        var popup=current;
        if(popup!=null&&popup.nativeLabel==null)popup.ResolveReferences();
        if(popup==null||popup.nativeLabel==null||popup.xui==null)
        {result.Add(format(contributors.Replace("\n",", ")));return result;}
        var label=popup.nativeLabel;
        int oldWidth=label.width,oldLines=label.maxLineCount;
        try
        {
            var screen=popup.xui.GetXUiScreenSize();
            GetGeometry(screen.x,40,out var ignored,out var size);
            label.width=size.x-40;label.maxLineCount=0;
            int available=Mathf.Max(52,screen.y-174);
            string wrapped;
            string allNames=contributors.Replace("\n",", ");
            if(label.Wrap(format(allNames),out wrapped,available))
            {result.Add(format(allNames));return result;}
            string group=string.Empty;
            foreach(string name in contributors.Split(new[]{'\n'},StringSplitOptions.None))
            {
                string candidate=group.Length==0?name:group+", "+name;
                if(group.Length>0&&!label.Wrap(format(candidate),out wrapped,available))
                {result.Add(format(group));group=name;}
                else group=candidate;
            }
            if(group.Length>0)result.Add(format(group));
            return result;
        }
        finally{label.width=oldWidth;label.maxLineCount=oldLines;}
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

        int screenWidth = xui.GetXUiScreenSize().x;
        int screenHeight = xui.GetXUiScreenSize().y;
        if (lastVisible && string.Equals(tooltipText, lastProcessedText) && screenWidth == lastScreenWidth && screenHeight==lastScreenHeight) return;

        // Measure at the final width before sizing the background. A fixed
        // two-line label silently discarded long collaborative clear notices.
        GetGeometry(screenWidth,40,out var initialPosition,out var initialSize);
        int availableHeight=Mathf.Max(52,screenHeight-160);
        text.Overflow=UILabel.Overflow.ClampContent;
        text.Size=new Vector2i(initialSize.x-40,availableHeight);
        if(nativeLabel!=null)
        {
            nativeLabel.maxLineCount=0;
            nativeLabel.width=initialSize.x-40;
            nativeLabel.height=availableHeight;
        }
        string processed=nativeLabel!=null?(nativeLabel.processedText??string.Empty):(tooltipText??string.Empty);
        int lines=1;
        for(int i=0;i<processed.Length;i++)if(processed[i]=='\n')lines++;
        int measured=nativeLabel!=null?Mathf.CeilToInt(nativeLabel.printedSize.y):lines*26;
        int height=Mathf.Max(40,measured+14);
        GetGeometry(screenWidth,height,out var position,out var size);
        int width=size.x;
        text.Size = new Vector2i(width - 40, height-14);
        ViewComponent.Position = position;
        ViewComponent.Size = new Vector2i(width, height);
        text.Position = new Vector2i(width / 2, -height / 2);
        if(legacyBackground!=null)
        {
            legacyBackground.Position=new Vector2i(0,0);
            legacyBackground.Size=new Vector2i(width,height);
            legacyBackground.IsVisible=true;
        }
        if(frame!=null){frame.Size=new Vector2i(width,height);frame.IsVisible=true;}
        if(accent!=null){accent.Size=new Vector2i(width,3);accent.IsVisible=true;}

        lastProcessedText = tooltipText;
        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
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
        legacyBackground=GetChildById("rebirthLegacyMessageBackground")?.ViewComponent as XUiV_Sprite;
        frame=GetChildById("rebirthMessageFrame")?.ViewComponent as XUiV_Sprite;
        accent=GetChildById("rebirthMessageAccent")?.ViewComponent as XUiV_Sprite;
        // XUi may initialize the controller before creating the native label.
        // Keep resolving until measurement is possible instead of staying on
        // the fallback layout for the entire session.
        refsReady = text != null && nativeLabel != null;
    }

    private void SetPanelsVisible(bool visible)
    {
        if(legacyBackground!=null)legacyBackground.IsVisible=visible;
        if(frame!=null)frame.IsVisible=visible;
        if(accent!=null)accent.IsVisible=visible;
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
