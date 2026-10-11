using System;
using HarmonyLib;
using System.Runtime.CompilerServices;

/// <summary>Restore a compartment's caller after the entire native Escape close pass.
/// Reopening inside OnClose is too early: that same pass can close the parent again.</summary>
[HarmonyPatch(typeof(GUIWindowManager),nameof(GUIWindowManager.CloseAllOpenModalWindows),
    new Type[]{typeof(GUIWindow),typeof(bool)})]
internal static class RebirthBackpackSectionEscapeReturnPatch
{
    private sealed class Caller { internal string Parent; internal long Revision; internal Func<bool> Return; }
    private static long nextRevision;
    private static readonly ConditionalWeakTable<GUIWindow,Caller> Callers=new ConditionalWeakTable<GUIWindow,Caller>();
    private static readonly System.Reflection.FieldInfo Modal=AccessTools.Field(typeof(GUIWindowManager),"modalWindow");
    internal static void Open(GUIWindowManager manager,string section,string fallback)
    {
        var child=manager.GetWindow(section);
        var active=Modal?.GetValue(manager) as GUIWindow;
        string parent=active!=null&&active.isShowing?active.Id:fallback;
        if(string.IsNullOrEmpty(parent)||parent==section||!manager.TryGetWindow(parent,out _))parent=string.Empty;
                var caller=Callers.GetValue(child,_=>new Caller());
        caller.Parent=parent;caller.Revision=++nextRevision;caller.Return=null;
        var ui=(child as XUiWindowGroup)?.Controller?.xui;
        if(ui!=null && RebirthContextNavigationService.IsContextVisible(ui))
        {
            var session=RebirthContextNavigationService.Session;
            if(RebirthContextNavigationService.SuspendForExternalRoute(ui))
                caller.Return=()=>ReferenceEquals(session,RebirthContextNavigationService.Session)&&RebirthContextNavigationService.ReturnToContext(ui);
        }
        else if(active is XUiWindowGroup stationGroup && stationGroup.Controller is XUiC_WorkstationWindowGroup stationOwner)
        {
            var tile=stationOwner.WorkstationData?.TileEntity;
            var player=ui?.playerUI?.entityPlayer;var world=player?.world;
            if(tile!=null&&world!=null)
                caller.Return=()=>
                {
                    if(!ReferenceEquals(world,player.world)||!ReferenceEquals(world,GameManager.Instance.World)||
                       !ReferenceEquals(player,ui.playerUI.entityPlayer)||player.IsDead()||
                       !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile)||
                       (player.position-tile.ToWorldPos().ToVector3()).sqrMagnitude>64)return false;
                    var position=tile.ToWorldPos();var block=world.GetBlock(position);
                    return block.Block.OnBlockActivated(world,position,block,player);
                };
        }
        manager.Open(section,true);
        child.openWindowOnEsc=parent;
    }
    internal static void CloseAndReturn(GUIWindowManager manager)
    {
        manager.CloseAllOpenModalWindows(null,true);
    }
    private sealed class ReturnState { internal GUIWindow Child; internal string Parent; internal long Revision; internal XUi Ui; internal object World; internal Func<bool> Return; }
    private static void Prefix(GUIWindowManager __instance,bool _fromEsc,out ReturnState __state)
    {
        __state=null;
        if(!_fromEsc)return;
        foreach(string id in new[]{"rebirthBackpackLibrary","rebirthBackpackSellStash"})
        {
            if(!__instance.TryGetWindow(id,out var child)||!child.isShowing||!child.isModal)continue;
            string parent=Callers.TryGetValue(child,out var caller)?caller.Parent:child.openWindowOnEsc;
            if(string.IsNullOrEmpty(parent)||parent==id||!__instance.TryGetWindow(parent,out _))return;
            var ui=(child as XUiWindowGroup)?.Controller?.xui;
            __state=new ReturnState{Child=child,Parent=parent,Revision=caller?.Revision??0,Ui=ui,World=ui?.playerUI?.entityPlayer?.world,Return=caller?.Return};
            child.openWindowOnEsc=string.Empty;
            return;
        }
    }
    private static void Postfix(GUIWindowManager __instance,ReturnState __state)
    {
        if(__state==null)return;
        __state.Child.openWindowOnEsc=__state.Parent;
        GameManager.Instance.StartCoroutine(ReturnLater(__instance,__state));
    }
    private static System.Collections.IEnumerator ReturnLater(GUIWindowManager manager,ReturnState state)
    {
        var ui=state.Ui;
        yield return RebirthWindowReturn.AfterCancelGesture(ui);
        if(ui?.playerUI?.windowManager!=manager||ui.playerUI.entityPlayer==null||ui.playerUI.entityPlayer.IsDead()||
            !ReferenceEquals(state.World,ui.playerUI.entityPlayer.world)||!ReferenceEquals(state.World,GameManager.Instance.World))yield break;
        if(state.Revision!=0&&(!Callers.TryGetValue(state.Child,out var current)||current.Revision!=state.Revision))yield break;
        // A different route opened while the key was being released; do not replace it.
        if(manager.IsModalWindowOpen()||manager.windowsToOpen.Exists(w=>w.isModal))yield break;
        if(state.Return!=null){state.Return();yield break;}
        if(!manager.IsWindowOpen(state.Parent))manager.Open(state.Parent,true);
    }
}
