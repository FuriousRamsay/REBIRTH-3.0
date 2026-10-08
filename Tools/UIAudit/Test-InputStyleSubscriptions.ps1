$ErrorActionPreference='Stop'
$production=Get-Content (Join-Path $PSScriptRoot '../../Scripts/UI/RebirthInputStyleSubscriptions.cs') -Raw
$production=$production.Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Runtime.CompilerServices;','').Replace('using HarmonyLib;','').Replace('using Platform;','')
$stubs=@"
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Platform;
namespace HarmonyLib { public class Harmony {public Harmony(string id){}} [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string name){}} }
public static class RebirthHarmonyBootstrap {public static void PatchClassOnce(Harmony h,Type t){}}
namespace Platform {public class PlayerInputManager {public enum InputStyle{Keyboard,Controller}public int NativeAdds;private Action<InputStyle> handlers;public event Action<InputStyle> OnLastInputStyleChanged {add{NativeAdds++;handlers+=value;}remove{handlers-=value;}}public void Raise(InputStyle s){handlers?.Invoke(s);}}public class FakePlatform{public PlayerInputManager Input;}public static class PlatformManager{public static FakePlatform NativePlatform;}}
public class XUiController {public bool registeredForInputStyleChanges;public int Notifications;public void OnLastInputStyleChanged(PlayerInputManager.InputStyle style){Notifications++;}public void OtherCallback(PlayerInputManager.InputStyle style){Notifications+=100;}}
public static class InputStyleFixture {
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static WeakReference Detached(PlayerInputManager input){var c=new XUiController();RebirthInputStyleSubscriptions.AddNative(input,c.OnLastInputStyleChanged);return new WeakReference(c);}
 public static string Run(){var input=new PlayerInputManager();PlatformManager.NativePlatform=new FakePlatform{Input=input};var all=new List<XUiController>();
 for(int i=0;i<10000;i++){var c=new XUiController{registeredForInputStyleChanges=true};all.Add(c);if(RebirthInputStyleSubscriptions.AddNative(input,c.OnLastInputStyleChanged))throw new Exception("Native controller bypassed hub");RebirthInputStyleSubscriptions.AddNative(input,c.OnLastInputStyleChanged);}
 if(input.NativeAdds!=1)throw new Exception("More than one native hub listener");input.Raise(PlayerInputManager.InputStyle.Controller);foreach(var c in all)if(c.Notifications!=1)throw new Exception("Missing or duplicate delivery");
 for(int i=0;i<all.Count;i+=2){if(RebirthInputStyleSubscriptions.RemoveNative(all[i].OnLastInputStyleChanged))throw new Exception("Managed removal fell through");RebirthInputStyleSubscriptions.Remove(all[i]);}
 input.Raise(PlayerInputManager.InputStyle.Keyboard);for(int i=0;i<all.Count;i++)if(all[i].Notifications!=(i%2==0?1:2))throw new Exception("Removed controller still subscribed");
 var normal=new XUiController();if(RebirthInputStyleSubscriptions.Register(normal))throw new Exception("Register bypassed hub");RebirthInputStyleSubscriptions.Register(normal);input.Raise(PlayerInputManager.InputStyle.Controller);if(normal.Notifications!=1)throw new Exception("Register duplicated callback");
 if(!RebirthInputStyleSubscriptions.AddNative(input,normal.OtherCallback)||!RebirthInputStyleSubscriptions.RemoveNative(normal.OtherCallback))throw new Exception("Unrelated listener intercepted");
 RebirthInputStyleSubscriptions.Remove(normal);var detached=Detached(input);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();if(detached.IsAlive)throw new Exception("Subscription retained detached controller");input.Raise(PlayerInputManager.InputStyle.Keyboard);PlatformManager.NativePlatform=null;if(!RebirthInputStyleSubscriptions.Register(new XUiController()))throw new Exception("Unavailable platform fallback lost");return "10,000 direct subscriptions: single native listener, deduplication, notification delivery, removal, normal registration and unrelated-listener fallback and detached-controller collection passed";
 }
}
"@
Add-Type -TypeDefinition ($stubs+"`n"+$production)
[InputStyleFixture]::Run()