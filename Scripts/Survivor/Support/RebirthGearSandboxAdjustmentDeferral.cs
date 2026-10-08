using System;
using System.Diagnostics;
using HarmonyLib;

// Native sandbox metadata changes bypass inventory setters. Preserve the exact
// held original; replay the native adjustment only for the same current scope.
internal static class RebirthGearSandboxAdjustmentDeferral
{
    private static EntityPlayerLocal owner;
    private static World world;
    private static object session;
    private static string creation, savedWorld;
    private static double nextAttempt;
    internal static void Reset(){owner=null;world=null;session=null;creation=null;savedWorld=null;nextAttempt=0;}
    internal static bool AllowNative(EntityPlayerLocal player)
    {
        if(player==null||!RebirthGearOwnerReservation.IsHeld(player))return true;
        // Refuse metadata mutation while held, even if teardown prevents replay.
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var connections=manager?.connectionToServer;
            if(ThreadManager.IsMainThread()&&player.world!=null&&player.world.IsRemote()&&
                ReferenceEquals(GameManager.Instance?.World,player.world)&&
                ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&manager!=null&&!manager.IsServer&&
                connections!=null&&connections.Length>0&&connections[0]!=null&&!connections[0].IsDisconnected()&&
                (RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out var current)||
                    RebirthGearOwnerReservation.TryGetHeldOriginalCreation(player,out current)))
            {
                owner=player;world=player.world;session=connections[0];creation=current;
                savedWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);
            }
        }
        catch { }
        return false;
    }
    internal static void NativeCompleted(EntityPlayerLocal player,bool ranOriginal)
    {
        if(ranOriginal&&ReferenceEquals(owner,player)&&!RebirthGearOwnerReservation.IsHeld(player))Reset();
    }
    internal static void Tick()
    {
        if(owner==null||!ThreadManager.IsMainThread())return;
        try
        {
            var player=owner;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var connections=manager?.connectionToServer;
            if(world==null||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(player.world,world)||
                !world.IsRemote()||!ReferenceEquals(world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(world.GetEntity(player.entityId),player)||manager==null||manager.IsServer||
                connections==null||connections.Length==0||!ReferenceEquals(connections[0],session)||
                connections[0].IsDisconnected()||GamePrefs.GetString(EnumGamePrefs.GameGuidClient)!=savedWorld)

            {Reset();return;} // Never replay another player/session/character's adjustment.
            // A cold join can hold the saved original before character projection
            // is published. Absence is pending; a published mismatch cancels it.
            var projected=RebirthSurvivorClientState.GetProjectedCreationId(player);
            if(string.IsNullOrEmpty(projected))return;
            if(!RebirthSurvivorRequestScope.Matches(creation,projected)){Reset();return;}
            if(!player.IsSpawned()||player.IsDead()||RebirthGearOwnerReservation.IsHeld(player))return;
            double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
            if(now<nextAttempt)return;
            nextAttempt=now+0.75;
            player.AdjustItemsForSandboxOptions(); // Original-run postfix confirms completion.
        }
        catch { } // Original deferred adjustment remains pending on native failure.
    }
}
[HarmonyPatch(typeof(EntityPlayerLocal),nameof(EntityPlayerLocal.AdjustItemsForSandboxOptions))]
internal static class RebirthGearSandboxAdjustmentPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityPlayerLocal __instance)=>RebirthGearSandboxAdjustmentDeferral.AllowNative(__instance);
    [HarmonyPostfix]
    private static void Postfix(EntityPlayerLocal __instance,bool __runOriginal)=>RebirthGearSandboxAdjustmentDeferral.NativeCompleted(__instance,__runOriginal);
}