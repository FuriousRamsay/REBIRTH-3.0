using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Two passive original-body observations. Never suppresses/reorders native death/unload.
internal static class RebirthPoiInheritedActorOutcomeHooks
{
    private const string Owner="rebirth.purge.inherited-actor-outcomes.v1";
    private static readonly Harmony Patcher=new Harmony(Owner);
    private static MethodInfo[] methods;
    internal static bool IsReady
    {
        get
        {
            // Native completion and actor identity are checked by the outcome observer.
            // An unrelated observer installed by another mod is not evidence of failure.
            try { return methods != null && methods.Length == 2 && methods.All(m =>
                { var info = Harmony.GetPatchInfo(m); return info != null && info.Owners.Contains(Owner); }); }
            catch { return false; }
        }
    }
    internal static void Install()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        if(IsReady)return;
        try
        {
            var selected=new[]{AccessTools.Method(typeof(EntityAlive),"SetDead",Type.EmptyTypes),AccessTools.Method(typeof(World),"unloadEntity",new[]{typeof(Entity),typeof(EnumRemoveEntityReason)})};
            if(selected.Any(m=>m==null||m.ReturnType!=typeof(void)))throw new MissingMethodException("Original native inherited actor outcomes unavailable.");
            for(int i=0;i<selected.Length;i++)
            {
                var info=Harmony.GetPatchInfo(selected[i]);if(info!=null&&info.Owners.Contains(Owner))continue;
                Patcher.Patch(selected[i],new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiInheritedActorOutcomeHooks),i==0?"DeathBefore":"UnloadBefore")),new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiInheritedActorOutcomeHooks),i==0?"DeathAfter":"UnloadAfter")),null,new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiInheritedActorOutcomeHooks),"Failed")),null);
            }
            methods=selected;if(!IsReady)Log.Warning("[REBIRTH Purge] Inherited actor outcome hooks were not installed.");
        }
        catch(Exception error){methods=null;Log.Warning("[REBIRTH Purge] Inherited actor outcomes unavailable: "+error.Message);}
    }
    private static void DeathBefore(EntityAlive __instance,out RebirthPoiInheritedActorOutcomes.Death __state)
    {__state=null;try{if(IsReady)__state=RebirthPoiInheritedActorOutcomes.Instance.BeforeSetDead(__instance);}catch{}}
    private static void DeathAfter(bool __runOriginal,RebirthPoiInheritedActorOutcomes.Death __state)
    {try{if(IsReady)RebirthPoiInheritedActorOutcomes.Instance.AfterSetDead(__state,__runOriginal);}catch{}}
    private static void UnloadBefore(World __instance,Entity __0,EnumRemoveEntityReason __1,out RebirthPoiInheritedActorOutcomes.Death __state)
    {__state=null;try{if(IsReady)__state=RebirthPoiInheritedActorOutcomes.Instance.BeforeUnload(__instance,__0 as EntityAlive,__1);}catch{}}
    private static void UnloadAfter(bool __runOriginal,EnumRemoveEntityReason __1,RebirthPoiInheritedActorOutcomes.Death __state)
    {try{if(IsReady)RebirthPoiInheritedActorOutcomes.Instance.AfterUnload(__state,__runOriginal,__1);}catch{}}
    private static Exception Failed(Exception __exception){return __exception;}
}