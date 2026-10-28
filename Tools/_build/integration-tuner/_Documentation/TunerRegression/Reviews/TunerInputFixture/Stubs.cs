using System;
using System.Collections.Generic;
namespace UnityEngine {
 public enum KeyCode {F8,F9,LeftControl,RightControl,LeftAlt,RightAlt,LeftShift,RightShift}
 public static class Time {public static int frameCount;}
 public static class Input {public static readonly HashSet<KeyCode> Held=new(),Down=new(); public static bool GetKey(KeyCode k)=>Held.Contains(k);public static bool GetKeyDown(KeyCode k)=>Down.Contains(k);}
}
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute{}}
namespace InControl { public sealed class BindingSource {public string Name; public BindingSource(string name){Name=name;}} public class PlayerActionSet {public readonly List<PlayerAction> Actions=new();} public sealed class PlayerAction{public string Name="native";public readonly List<BindingSource> Bindings=new();public bool Enabled=true; public bool WasPressed=>Enabled;}}
public static class Log{public static readonly List<string> Messages=new();public static void Warning(string x)=>Messages.Add(x);}
public static class Localization{public static string Get(string k)=>k;}
public static class GUIWindowConsole{public static bool Open; public static bool IsOpen()=>Open;}
public sealed class PlayerActionsGlobal:InControl.PlayerActionSet {public static PlayerActionsGlobal Instance=new();public InControl.PlayerAction ShowFPS=new(),Screenshot=new(),Console=new(),Other=new();public PlayerActionsGlobal(){Actions.AddRange(new[]{ShowFPS,Screenshot,Console,Other});ShowFPS.Name="ShowFPS";Screenshot.Name="Screenshot";ShowFPS.Bindings.Add(new("F8"));Screenshot.Bindings.Add(new("F9"));}}
public sealed class PlayerInput:InControl.PlayerActionSet {public InControl.PlayerActionSet GUIActions=new(),VehicleActions=new(),PermanentActions=new();}
public static class RebirthNativeControls{public static InControl.PlayerActionSet Actions=new();}
public class EntityVehicle{}
public class EntityPlayerLocal {public bool Dead; public bool IsDead()=>Dead;}
public sealed class World {public EntityPlayerLocal Player;public EntityPlayerLocal GetPrimaryPlayer()=>Player;}
public sealed class GameManager {public static GameManager Instance=new();public World World=new();}
public sealed class RebirthVehicleTuningSession {public static bool Dm=true;public bool Available=true;public bool Valid=>Available&&Dm;}
public sealed class LocalPlayerUI {public EntityPlayerLocal entityPlayer=new();public GUIWindowManager windowManager=new();public PlayerInput playerInput=new();}
public class XUiController {
 public XUiController Child; public Action Open,Close;
 public T GetChildByType<T>() where T:class=>Child as T;
 public virtual void OnOpen()=>Open?.Invoke();public virtual void OnClose()=>Close?.Invoke();
}
public sealed class XUiC_RebirthVehicleTuning:XUiController {public RebirthVehicleTuningSession Bound;public void Bind(RebirthVehicleTuningSession s)=>Bound=s;}
public class GUIWindow {
 public string Id;public bool bActionSetEnabled,alwaysUsesMouseCursor,isShowing,isModal,Pending;
 public GUIWindowManager Manager;public XUiController Controller=new();
 public virtual bool HasActionSet()=>true;
 public virtual void OnOpen()=>Controller.OnOpen();public virtual void OnClose()=>Controller.OnClose();
}
public sealed class XUiWindowGroup:GUIWindow {
 public enum EHasActionSetFor{Both,None}
 public readonly EHasActionSetFor hasActionSetFor;
 // Exact native default before first OnOpen is true; readonly source flag differs.
 private bool hasActionSetThisOpen=true;
 public XUiWindowGroup(string id,EHasActionSetFor flag){Id=id;hasActionSetFor=flag;}
 public override void OnOpen(){Controller.OnOpen();hasActionSetThisOpen=hasActionSetFor==EHasActionSetFor.Both;}
 public override bool HasActionSet()=>hasActionSetThisOpen;
}
public sealed class GUIWindowManager {
 public readonly Dictionary<string,GUIWindow> Windows=new();public readonly List<string> Events=new();
 public int CloseFailureBudget,PopFailureBudget;public bool InputActive;
 public bool TryGetWindow(string id,out GUIWindow w)=>Windows.TryGetValue(id,out w);
 public bool IsWindowOpen(GUIWindow w)=>w.isShowing||w.Pending;
 public bool IsWindowOpen(string id)=>Windows.TryGetValue(id,out var w)&&IsWindowOpen(w);
 public GUIWindow GetModalWindow(){foreach(var w in Windows.Values)if(IsWindowOpen(w)&&w.isModal)return w;return null;}
 public bool IsInputActive()=>InputActive;
 public void FlushOpen(){foreach(var w in Windows.Values)if(w.Pending){w.Pending=false;w.isShowing=true;}}
 public void Open(GUIWindow w,bool modal,bool notEsc){
  if(IsWindowOpen(w)){Events.Add("alreadyopen:"+w.Id);return;}
  if(modal)foreach(var other in Windows.Values)if(other!=w&&other.isModal&&IsWindowOpen(other))Close(other);
  w.Manager=this;w.isModal=modal;w.Pending=true;Events.Add("open:"+w.Id);w.OnOpen();
  if(w.HasActionSet()&&!w.bActionSetEnabled&&IsWindowOpen(w)){w.bActionSetEnabled=true;Events.Add("push:"+w.Id);}
 }
 public void Close(string id){if(Windows.TryGetValue(id,out var w))Close(w);}
 public void Close(GUIWindow w,bool fromEsc=false){
  Events.Add("close:"+w.Id);if(CloseFailureBudget>0){CloseFailureBudget--;throw new Exception("Injected native Close pre-hide failure");}
  if(w.isShowing){w.isShowing=false;Events.Add("onclose:"+w.Id);w.OnClose();}
  else if(w.Pending){w.Pending=false;Events.Add("onclose:"+w.Id);w.OnClose();}
  if(w.bActionSetEnabled){if(PopFailureBudget>0){PopFailureBudget--;throw new Exception("Injected action Pop failure");}w.bActionSetEnabled=false;Events.Add("pop:"+w.Id);}
 }
}
public static class ModEvents {
 public struct SGameUpdateData{} public struct SWorldShuttingDownData{}public struct SGameShutdownData{}
 public delegate void Handler<T>(ref T d);
 public sealed class Event<T>{public Handler<T> Handlers;public void RegisterHandler(Handler<T> h)=>Handlers+=h;public void Fire(){T d=default;Handlers?.Invoke(ref d);}}
 public static Event<SGameUpdateData> GameUpdate=new();public static Event<SWorldShuttingDownData> WorldShuttingDown=new();public static Event<SGameShutdownData> GameShutdown=new();
}
