using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthPoiEvidenceModApi : IModApi
{
    private static bool starting,update,worldShutdown,gameShutdown;
    public void InitMod(Mod mod)
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        RebirthPoiEvidenceHooks.Install();
        if(!starting){ModEvents.GameStarting.RegisterHandler(OnStarting);starting=true;}
        if(!update){ModEvents.GameUpdate.RegisterHandler(OnUpdate);update=true;}
        if(!worldShutdown){ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShutdown);worldShutdown=true;}
        if(!gameShutdown){ModEvents.GameShutdown.RegisterHandler(OnGameShutdown);gameShutdown=true;}
    }
    private static void OnStarting(ref ModEvents.SGameStartingData data){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiNativeEvidence.Instance.Reset();}
    private static void OnUpdate(ref ModEvents.SGameUpdateData data){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiNativeEvidence.Instance.Pulse();}
    private static void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiNativeEvidence.Instance.Reset();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiNativeEvidence.Instance.Reset();}
}
internal static class RebirthPoiEvidenceHooks
{
    private const string Owner="rebirth.purge.passive-evidence.v1";
    private static readonly Harmony Patcher=new Harmony(Owner);
    private static bool installed;
    internal static void Install()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        if(installed)return;
        Patch(typeof(World),"CheckSleeperVolumeTouching",new[]{typeof(EntityPlayer)},"Touch",null);
        Patch(typeof(SleeperVolume),"AddEnemyToWorld",new[]{typeof(World),typeof(EntityAlive),typeof(int),typeof(UnityEngine.Vector3),typeof(UnityEngine.Vector3),typeof(int)},null,"Spawned");
        Patch(typeof(SleeperVolume),"EntityDied",new[]{typeof(EntityAlive)},"Dying","Died");
        Patch(typeof(SleeperVolume),"UpdateSpawn",new[]{typeof(World)},null,"Updated");
        Patch(typeof(SleeperVolume),"DespawnAndReset",new[]{typeof(World)},null,"Reset");
        installed=true;
    }
    private static void Patch(Type type,string name,Type[] args,string prefix,string postfix)
    {
        var method=AccessTools.Method(type,name,args);
        if(method==null)throw new MissingMethodException(type.FullName,name);
        var info=Harmony.GetPatchInfo(method);
        if(info!=null && info.Owners.Contains(Owner))return;
        Patcher.Patch(method,prefix==null?null:new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiEvidenceHooks),prefix)),postfix==null?null:new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiEvidenceHooks),postfix)));
    }
    private static void Touch(World __instance,EntityPlayer __0){RebirthPoiNativeEvidence.Instance.PlayerTouch(__instance,__0);}
    private static void Spawned(SleeperVolume __instance,World __0,EntityAlive __1){RebirthPoiNativeEvidence.Instance.NativeSpawn(__0,__instance,__1);}
    private static void Dying(SleeperVolume __instance,EntityAlive __0,out RebirthPoiNativeEvidence.DeathWitness __state){__state=RebirthPoiNativeEvidence.Instance.BeforeDeath(__instance,__0);}
    private static void Died(RebirthPoiNativeEvidence.DeathWitness __state){RebirthPoiNativeEvidence.Instance.AfterDeath(__state);}
    private static void Updated(SleeperVolume __instance,World __0){RebirthPoiNativeEvidence.Instance.NativeDirty(__0,__instance);}
    private static void Reset(SleeperVolume __instance,World __0){RebirthPoiNativeEvidence.Instance.NativeReset(__0,__instance);}
}
