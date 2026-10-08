using System;
using HarmonyLib;

/// <summary>Restore a compartment's caller after the entire native Escape close pass.
/// Reopening inside OnClose is too early: that same pass can close the parent again.</summary>
[HarmonyPatch(typeof(GUIWindowManager),nameof(GUIWindowManager.CloseAllOpenModalWindows),
    new Type[]{typeof(GUIWindow),typeof(bool)})]
internal static class RebirthBackpackSectionEscapeReturnPatch
{
    private sealed class ReturnState { internal GUIWindow Child; internal string Parent; }
    private static void Prefix(GUIWindowManager __instance,bool _fromEsc,out ReturnState __state)
    {
        __state=null;
        if(!_fromEsc)return;
        foreach(string id in new[]{"rebirthBackpackLibrary","rebirthBackpackSellStash"})
        {
            if(!__instance.TryGetWindow(id,out var child)||!child.isShowing||!child.isModal)continue;
            string parent=child.openWindowOnEsc;
            if(string.IsNullOrEmpty(parent)||parent==id||!__instance.TryGetWindow(parent,out _))return;
            __state=new ReturnState{Child=child,Parent=parent};
            child.openWindowOnEsc=string.Empty;
            return;
        }
    }
    private static void Postfix(GUIWindowManager __instance,ReturnState __state)
    {
        if(__state==null)return;
        __state.Child.openWindowOnEsc=__state.Parent;
        if(!__instance.IsWindowOpen(__state.Parent))__instance.Open(__state.Parent,true);
    }
}