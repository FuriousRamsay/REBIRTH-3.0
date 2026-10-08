using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Platform;

// Keep native per-controller notifications, but avoid copying a delegate array for
// every widget registered or removed during a UI reload. Controllers are weakly held.
internal static class RebirthInputStyleSubscriptions
{
    private sealed class Hub
    {
        internal readonly LinkedList<WeakReference<XUiController>> Controllers = new LinkedList<WeakReference<XUiController>>();
        internal Hub(PlayerInputManager input) { input.OnLastInputStyleChanged += Changed; }
        private void Changed(PlayerInputManager.InputStyle style)
        {
            var node = Controllers.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.TryGetTarget(out var controller)) controller.OnLastInputStyleChanged(style);
                else Controllers.Remove(node);
                node = next;
            }
        }
    }
    private sealed class Subscription
    {
        internal Hub Hub;
        internal LinkedListNode<WeakReference<XUiController>> Node;
    }
    private static readonly ConditionalWeakTable<PlayerInputManager, Hub> Hubs = new ConditionalWeakTable<PlayerInputManager, Hub>();
    private static readonly ConditionalWeakTable<XUiController, Subscription> Subscriptions = new ConditionalWeakTable<XUiController, Subscription>();
    private static void Subscribe(XUiController controller,PlayerInputManager input)
    {
        if(Subscriptions.TryGetValue(controller,out var existing)&&existing.Node!=null)return;
        var hub=Hubs.GetValue(input,value=>new Hub(value));
        var subscription=Subscriptions.GetValue(controller,value=>new Subscription());
        subscription.Hub=hub;
        subscription.Node=hub.Controllers.AddLast(new WeakReference<XUiController>(controller));
    }
    internal static bool Register(XUiController controller)
    {
        if(controller.registeredForInputStyleChanges)return false;
        var input=PlatformManager.NativePlatform?.Input;
        if(input==null)return true;
        Subscribe(controller,input);
        controller.registeredForInputStyleChanges=true;
        return false;
    }
    // Native Init paths can subscribe directly instead of calling registerForInputStyleChanges.
    // Intercept only the native controller callback; all other listeners keep their native event.
    internal static bool AddNative(PlayerInputManager input,Action<PlayerInputManager.InputStyle> listener)
    {
        if(!(listener?.Target is XUiController controller)||listener.Method.Name!=nameof(XUiController.OnLastInputStyleChanged))return true;
        Subscribe(controller,input);
        return false;
    }
    internal static bool RemoveNative(Action<PlayerInputManager.InputStyle> listener)
    {
        if(!(listener?.Target is XUiController controller)||listener.Method.Name!=nameof(XUiController.OnLastInputStyleChanged)
            ||!Subscriptions.TryGetValue(controller,out _))return true;
        Remove(controller);
        return false;
    }    internal static void Remove(XUiController controller)
    {
        if (!Subscriptions.TryGetValue(controller, out var subscription)) return;
        if (subscription.Node != null) subscription.Hub.Controllers.Remove(subscription.Node);
        Subscriptions.Remove(controller);
        controller.registeredForInputStyleChanges = false;
    }
    internal static void Install()
    {
        var harmony = new Harmony("rebirth.ui.inputstylesubscriptions");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(EventAddPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(EventRemovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RegisterPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(CleanupPatch));
    }
    [HarmonyPatch(typeof(PlayerInputManager), "add_OnLastInputStyleChanged")]
    private static class EventAddPatch
    {
        private static bool Prefix(PlayerInputManager __instance,Action<PlayerInputManager.InputStyle> value)=>AddNative(__instance,value);
    }
    [HarmonyPatch(typeof(PlayerInputManager), "remove_OnLastInputStyleChanged")]
    private static class EventRemovePatch
    {
        private static bool Prefix(Action<PlayerInputManager.InputStyle> value)=>RemoveNative(value);
    }    [HarmonyPatch(typeof(XUiController), "registerForInputStyleChanges")]
    private static class RegisterPatch { private static bool Prefix(XUiController __instance) => Register(__instance); }
    [HarmonyPatch(typeof(XUiController), "Cleanup")]
    private static class CleanupPatch { private static void Prefix(XUiController __instance) => Remove(__instance); }
}