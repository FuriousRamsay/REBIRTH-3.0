using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameEvent.SequenceActions;
using HarmonyLib;

// Own the scripted action before advancing any native reset. Action reuse or a
// replacement world invalidates the original lease, including delayed completion.
internal static class RebirthPoiScriptResetOwner
{
    private static readonly Dictionary<ActionPOIReset,object> leases=new Dictionary<ActionPOIReset,object>();
    private static double Now => (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal static void Reset(){leases.Clear();}
    internal static void Invalidate(ActionPOIReset action){leases.Remove(action);}
    internal static IEnumerator Capture(ActionPOIReset action)
    {
        var token=new object();leases[action]=token;
        var world=GameManager.Instance?.World;var owner=action.Owner;
        var poi=owner?.POIInstance;var requester=owner?.Requester;
        var targets=poi==null?null:GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabsIntersecting(poi);
        var originals=targets?.ToArray();
        Func<bool> current=()=>leases.TryGetValue(action,out var active)&&ReferenceEquals(active,token)&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(action.Owner,owner)&&ReferenceEquals(owner?.POIInstance,poi)&&ReferenceEquals(owner?.Requester,requester)&&action._state==ActionPOIReset.State.Wait&&targets!=null&&originals!=null&&targets.SequenceEqual(originals);
        int player=GameManager.Instance.IsEditMode()||GameUtils.IsPlaytesting()?-1:requester?.entityId??-1;
        return Run(action,token,world,targets,player,current);
    }
    private static IEnumerator Run(ActionPOIReset action,object token,World world,List<PrefabInstance> targets,int player,Func<bool> current)
    {
        return RebirthPoiOwnedWorldReset.Run(world,targets,QuestEventManager.manualResetTag,player,null,RebirthPoiResetCaller.Event,current,success=>
        {
            if(current())action._retVal=success?BaseAction.ActionCompleteStates.Complete:BaseAction.ActionCompleteStates.InCompleteRefund;
            if(leases.TryGetValue(action,out var original)&&ReferenceEquals(original,token))leases.Remove(action);
        });
    }
}
[HarmonyPatch(typeof(ActionPOIReset),nameof(ActionPOIReset.onPerformAction))]
internal static class RebirthPoiScriptResetHook
{
    private static bool Prefix(ActionPOIReset __instance,ref IEnumerator __result)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled)return true;
        __result=RebirthPoiScriptResetOwner.Capture(__instance);return false;
    }
}
[HarmonyPatch(typeof(ActionPOIReset),nameof(ActionPOIReset.OnReset))]
internal static class RebirthPoiScriptResetReuseHook
{private static void Prefix(ActionPOIReset __instance){RebirthPoiScriptResetOwner.Invalidate(__instance);}}