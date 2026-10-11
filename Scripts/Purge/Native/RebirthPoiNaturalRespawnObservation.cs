using System;
using HarmonyLib;
// Context covers only the original native natural respawn entry. Script/quest/
// region calls to Reset cannot masquerade as this positive observation.
internal static class RebirthPoiNaturalRespawnObservation
{
    internal sealed class Scope
    {internal SleeperVolume Volume;internal World World;internal EntityPlayer Player;internal Scope Previous;}
    [ThreadStatic] private static Scope current;
    internal static void Before(SleeperVolume volume,World world,EntityPlayer player,out Scope scope)
    {
        scope=null;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled||RebirthSandboxOptionManager.Current.IsPurge||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||player==null||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||volume.isSpawned||world.worldTime<volume.respawnTime)return;
        scope=new Scope{Volume=volume,World=world,Player=player,Previous=current};current=scope;
    }
    internal static void ResetCompleted(SleeperVolume volume,bool originalRan)
    {
        var scope=current;
        if(scope!=null&&ReferenceEquals(scope.Volume,volume)&&ReferenceEquals(GameManager.Instance?.World,scope.World)&&ReferenceEquals(scope.World.GetEntity(scope.Player.entityId),scope.Player)&&RebirthPoiNativeResetPostconditions.Volume(volume,originalRan))RebirthPoiNativeEvidence.Instance.NativeNaturalReset(scope.World,volume);
    }
    internal static Exception Finished(Scope scope,Exception error){if(scope!=null&&ReferenceEquals(current,scope))current=scope.Previous;return error;}
}
[HarmonyPatch(typeof(SleeperVolume),nameof(SleeperVolume.UpdatePlayerTouched))]
internal static class RebirthPoiNaturalRespawnEntryHook
{
    private static void Prefix(SleeperVolume __instance,World __0,EntityPlayer __1,out RebirthPoiNaturalRespawnObservation.Scope __state){RebirthPoiNaturalRespawnObservation.Before(__instance,__0,__1,out __state);}
    private static Exception Finalizer(RebirthPoiNaturalRespawnObservation.Scope __state,Exception __exception)=>RebirthPoiNaturalRespawnObservation.Finished(__state,__exception);
}
[HarmonyPatch(typeof(SleeperVolume),nameof(SleeperVolume.Reset))]
internal static class RebirthPoiNaturalRespawnResetHook
{private static void Postfix(SleeperVolume __instance,bool __runOriginal){RebirthPoiNaturalRespawnObservation.ResetCompleted(__instance,__runOriginal);}}