using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthMusicHud : XUiController
{
    private XUiV_Label label;
    private XUiController card;
    private float elapsed;
    private bool lifecycleClosed;
    private string lastSong,lastStatus,cachedText=string.Empty;
    public override void Init(){Hide();base.Init();label=GetChildById("musicHudTitle")?.ViewComponent as XUiV_Label;card=GetChildById("musicHudCard");Hide();lifecycleClosed=false;elapsed=0.25f;}
    public override void OnOpen(){base.OnOpen();lifecycleClosed=false;elapsed=0.25f;}
    public override void OnClose(){lifecycleClosed=true;Hide();base.OnClose();}
    public override void Cleanup(){lifecycleClosed=true;Hide();base.Cleanup();}
    private void Hide()
    {
        if(card?.ViewComponent!=null)card.ViewComponent.IsVisible=false;
        RebirthHudTrackingLayoutMetrics.SetMusicCardVisible(false);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(lifecycleClosed){Hide();return;}
        bool suppressed=RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress||
            xui?.playerUI?.windowManager==null||xui.playerUI.windowManager.IsModalWindowOpen();
        if(suppressed){Hide();elapsed=0.25f;return;}
        elapsed+=dt;if(elapsed<0.25f)return;elapsed=0;
        string song=RebirthLegacyMusicPlaybackService.NowPlaying;
        if(label==null){Hide();return;}
        bool visible=!string.IsNullOrEmpty(song);
        if(card?.ViewComponent!=null)card.ViewComponent.IsVisible=visible;
        RebirthHudTrackingLayoutMetrics.SetMusicCardVisible(visible);
        string status=visible?Localization.Get(RebirthLegacyMusicPlaybackService.IsPaused?"xuiRebirthMusicPaused":"xuiRebirthMusicPlaying"):string.Empty;
        if(song!=lastSong||status!=lastStatus)
        {
            lastSong=song;lastStatus=status;
            cachedText=visible?song+" ("+status+")":string.Empty;
        }
        if(label.Text!=cachedText)label.Text=cachedText;
    }
}