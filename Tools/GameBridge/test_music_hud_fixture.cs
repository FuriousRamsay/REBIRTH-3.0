using System;
public class View {public bool IsVisible;}
public class XUiV_Label:View {public string Text;}
public class Manager {public bool Modal;public bool IsModalWindowOpen(){return Modal;}}
public class PlayerUI {public Manager windowManager=new Manager();}
public class Xui {public PlayerUI playerUI=new PlayerUI();}
public class XUiController {
 public static XUiV_Label Label=new XUiV_Label();public static View Card=new View();
 public View ViewComponent;public Xui xui=new Xui();
 public XUiController GetChildById(string id){return new XUiController{ViewComponent=id=="musicHudTitle"?(View)Label:Card};}
 public virtual void Init(){} public virtual void Update(float dt){} public virtual void OnOpen(){} public virtual void OnClose(){} public virtual void Cleanup(){}
}
public static class RebirthPersonalCraftingHudSuppressionInstaller {public static bool ShouldSuppress;}
public static class RebirthHudTrackingLayoutMetrics {public static bool Visible;public static void SetMusicCardVisible(bool value){Visible=value;}}
public static class RebirthLegacyMusicPlaybackService {public static string NowPlaying;public static bool IsPaused;}
public static class Localization {public static string Language="en";public static string Get(string key){return Language+key;}}
// METHODS
public static class Check {
 static void Assert(bool b){if(!b)throw new Exception("HUD assertion");}
 public static void Main(){
 var h=new XUiC_RebirthMusicHud();h.Init();h.Update(.25f);Assert(XUiController.Label.Text==""&&!XUiController.Card.IsVisible&&!RebirthHudTrackingLayoutMetrics.Visible);
 RebirthLegacyMusicPlaybackService.NowPlaying="Song";h.Update(.25f);string first=XUiController.Label.Text;
 Assert(first=="Song (enxuiRebirthMusicPlaying)"&&XUiController.Card.IsVisible&&RebirthHudTrackingLayoutMetrics.Visible);
 h.Update(.25f);Assert(object.ReferenceEquals(first,XUiController.Label.Text));
 RebirthLegacyMusicPlaybackService.IsPaused=true;h.Update(.25f);Assert(XUiController.Label.Text=="Song (enxuiRebirthMusicPaused)");
 Localization.Language="fr";h.Update(.25f);Assert(XUiController.Label.Text=="Song (frxuiRebirthMusicPaused)");
 XUiController.Label.Text="overwritten";h.Update(.25f);Assert(XUiController.Label.Text=="Song (frxuiRebirthMusicPaused)");
 RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress=true;h.Update(.01f);Assert(!XUiController.Card.IsVisible&&!RebirthHudTrackingLayoutMetrics.Visible);
 RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress=false;h.Update(.01f);Assert(XUiController.Card.IsVisible);
 h.xui.playerUI.windowManager.Modal=true;h.Update(.01f);Assert(!RebirthHudTrackingLayoutMetrics.Visible);
 h.xui.playerUI.windowManager.Modal=false;h.Update(.25f);h.OnClose();Assert(!RebirthHudTrackingLayoutMetrics.Visible);
 h.Update(.25f);Assert(!RebirthHudTrackingLayoutMetrics.Visible&&!XUiController.Card.IsVisible);h.OnOpen();h.Update(.01f);Assert(RebirthHudTrackingLayoutMetrics.Visible);h.Cleanup();Assert(!RebirthHudTrackingLayoutMetrics.Visible);
 h.Update(.25f);Assert(!RebirthHudTrackingLayoutMetrics.Visible);h.Init();Assert(!RebirthHudTrackingLayoutMetrics.Visible&&!XUiController.Card.IsVisible);h.OnOpen();
 RebirthLegacyMusicPlaybackService.NowPlaying="";h.Update(.25f);Assert(XUiController.Label.Text==""&&!XUiController.Card.IsVisible);
 Console.WriteLine("PASS actual music HUD: song(status), caching, localization, stop, immediate menu suppression, reserve and close/cleanup");
 }
}