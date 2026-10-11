using System;
namespace UnityEngine {}
public class EntityPlayerLocal {public int Cruise;}
public class PlayerUI {public EntityPlayerLocal entityPlayer=new();}
public class Xui {public PlayerUI playerUI=new();}
public class XUiController {
 public Xui xui=new();public bool IsDirty;public int Refreshes,Updates;
 public virtual void OnOpen(){}public virtual void Update(float dt){Updates++;}
 public void RefreshBindings(){Refreshes++;IsDirty=false;}
 public virtual bool GetBindingValueInternal(ref string value,string name)=>false;
}
public static class Localization {public static string Suffix="";public static string Get(string key)=>key+Suffix;}
public static class RebirthNativeControls {public static int GetCruiseState(EntityPlayerLocal p)=>p?.Cruise??0;}
public static class RebirthOreSenseService {public static bool Active;public static string NearestType="";public static float NearestDistance;public static int CachedCount;}
class Program {
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
 static void Main(){
 var c=new RebirthCruiseControlHud();c.OnOpen();int n=c.Refreshes;
 for(int i=0;i<1000;i++)c.Update(.05f);Check(c.Refreshes==n&&c.Updates==1000,"idle cruise/base");
 c.xui.playerUI.entityPlayer.Cruise=1;c.Update(.05f);Check(c.Refreshes==++n,"normal");
 Localization.Suffix="localized";c.Update(.05f);Check(c.Refreshes==++n,"localization");
 c.xui.playerUI.entityPlayer=new EntityPlayerLocal{Cruise=2};c.Update(.05f);Check(c.Refreshes==++n,"replacement");
 c.xui.playerUI.entityPlayer=null;c.Update(.05f);Check(c.Refreshes==++n,"null");
 c.IsDirty=true;c.Update(.05f);Check(c.Refreshes==++n,"dirty");c.OnOpen();Check(c.Refreshes==++n,"reopen");
 var o=new XUiC_RebirthOreSenseHud();o.OnOpen();n=o.Refreshes;
 for(int i=0;i<1000;i++)o.Update(.1f);Check(o.Refreshes==n&&o.Updates==1000,"idle ore/base");
 RebirthOreSenseService.Active=true;o.Update(.1f);Check(o.Refreshes==++n,"activation");
 RebirthOreSenseService.CachedCount=2;o.Update(.1f);Check(o.Refreshes==++n,"scan count");
 RebirthOreSenseService.NearestType="Iron";RebirthOreSenseService.NearestDistance=2;o.Update(.1f);Check(o.Refreshes==++n,"nearest");
 o.Update(.1f);Check(o.Refreshes==n,"stable active");
 RebirthOreSenseService.Active=false;o.Update(.1f);Check(o.Refreshes==++n,"off");
 o.IsDirty=true;o.Update(.1f);Check(o.Refreshes==++n,"ore dirty");o.OnOpen();Check(o.Refreshes==++n,"ore reopen");
 Console.WriteLine("PASS: actual HUD classes with UI/service doubles;2000 idle samples suppressed explicit refresh; base cadence and state/localization/owner/dirty/reopen transitions retained. Native UI not tested.");
 }
}