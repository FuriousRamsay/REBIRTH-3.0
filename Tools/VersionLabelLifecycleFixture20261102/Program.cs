using System;
using System.Collections.Generic;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static Transform Window(NGUIWindowManager manager){var window=new Transform();manager.windowMap[EnumNGUIWindow.Version]=window;return window;}
    static int Main()
    {
        try
        {
            GameManager.Instance=new GameManager{World=new World()};LocalPlayerUI.Current=new LocalPlayerUI();Time.unscaledTime=10;
            var manager=new NGUIWindowManager();Resources.Managers.Add(manager);var first=Window(manager);
            var view=new XUiC_RebirthVersionLabel();view.Init();view.OnOpen();
            Check(Resources.Scans==1&&first.Label.text.Contains("REBIRTH")&&first.Label.supportEncoding&&first.Label.fontSize==18,"first open discovers and formats actual native label");
            for(int i=0;i<200;i++)view.OnOpen();
            Check(Resources.Scans==1,"cached HUD reopen repeated global discovery");
            LocalPlayerUI.Current.windowManager.Modal=true;view.Update(0);
            Check(!first.Label.enabled&&Resources.Scans==1,"modal suppression updates visibility without scanning");
            LocalPlayerUI.Current.windowManager.Modal=false;RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress=true;view.Update(0);
            Check(!first.Label.enabled&&Resources.Scans==1,"crafting suppression retained without scanning");
            RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress=false;view.OnOpen();
            Check(first.Label.enabled&&Resources.Scans==1,"HUD reopen restores visibility from cached label");
            var replacement=Window(manager);view.Update(0);
            Check(Resources.Scans==2&&replacement.Label.text.Contains("REBIRTH"),"native window-map replacement rediscovers label");
            GameManager.Instance.World=new World();view.OnOpen();
            Check(Resources.Scans==3,"new native world invalidates discovery");
            LocalPlayerUI.Current=new LocalPlayerUI();view.OnOpen();
            Check(Resources.Scans==4,"new primary player UI invalidates discovery");
            var secondManager=new NGUIWindowManager();Resources.Managers.Add(secondManager);var second=Window(secondManager);
            var secondView=new XUiC_RebirthVersionLabel();secondView.Init();secondView.OnOpen();
            Check(Resources.Scans==5&&second.Label.text.Contains("REBIRTH"),"new controller Init admits additional manager");
            manager.windowMap.Clear();secondManager.windowMap.Clear();view.Update(0);
            int scans=Resources.Scans;
            for(int i=0;i<200;i++)view.OnOpen();
            Check(Resources.Scans==scans,"missing native views retry cadence survives repeated opens");
            var restored=Window(manager);Time.unscaledTime+=2;view.Update(0);
            Check(Resources.Scans==scans+1&&restored.Label.text.Contains("REBIRTH"),"missing view is discovered on existing retry cadence");
            int beforeMenu=Resources.Scans;GameManager.Instance.World=null;view.OnOpen();
            Check(Resources.Scans==beforeMenu+1,"world unload/menu transition invalidates prior owners");
            Console.WriteLine("RESULT "+checks+" PASS: actual whole production class; Unity/UI discovery and native lifecycle boundaries doubled. Native performance acceptance separate.");return 0;
        }
        catch(Exception error){Console.WriteLine("FAIL "+error.Message);return 1;}
    }
}
public class XUiController{public virtual void Init(){}public virtual void OnOpen(){}public virtual void Update(float dt){}}
public class World{}
public class GameManager{public static GameManager Instance;public World World;}
public class LocalPlayerUI{public static LocalPlayerUI Current;public FakeWindowManager windowManager=new();public static LocalPlayerUI GetUIForPrimaryPlayer()=>Current;}
public class FakeWindowManager{public bool Modal;public bool IsModalWindowOpen()=>Modal;}
public static class RebirthPersonalCraftingHudSuppressionInstaller{public static bool ShouldSuppress;}
public enum EnumNGUIWindow{Version}
public class NGUIWindowManager{public Dictionary<EnumNGUIWindow,Transform> windowMap=new();}
public class UILabel{public enum Overflow{ResizeFreely}public bool enabled,supportEncoding;public int fontSize;public string text="native";public Overflow overflowMethod;}
public class ModInfo{public string VersionString="fixture";}
public static class ModManager{public static ModInfo GetMod(string name)=>new();}
public static class Constants{public static VersionInfo cVersionInformation=new();}
public class VersionInfo{public int Major=3,Minor=40,Build=9;}
namespace UnityEngine
{
    public class GameObject{}
    public class Transform{public GameObject gameObject=new();public UILabel Label=new();public T GetComponent<T>() where T:class=>Label as T;}
    public static class Time{public static float unscaledTime;}
    public static class Resources{public static int Scans;public static List<NGUIWindowManager> Managers=new();public static T[] FindObjectsOfTypeAll<T>() where T:class{Scans++;return Managers.ConvertAll(x=>x as T).ToArray();}}
}
namespace UnityEngine.Scripting{public class PreserveAttribute:Attribute{}}
