using System;
using HarmonyLib;

// Qualified native local JoinMultiplayer spawn boundary: ToPlayer and grids are
// hydrated, Spawned is true, and original spawn observers/local AfterRespawn have
// not run yet. No original method suppression or inventory application here.
[HarmonyPatch(typeof(GameManager),nameof(GameManager.PlayerSpawnedInWorld),
    new Type[]{typeof(ClientInfo),typeof(RespawnType),typeof(Vector3i),typeof(int)})]
internal static class RebirthGearColdSpawnAdmissionPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    internal static void Prefix(GameManager __instance,ClientInfo _cInfo,RespawnType _respawnReason,int _entityId)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||_cInfo!=null||_respawnReason!=RespawnType.JoinMultiplayer||
                !ReferenceEquals(__instance,GameManager.Instance))return;
            var world=__instance?.World;var player=world?.GetPrimaryPlayer();
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var connections=manager?.connectionToServer;
            if(world==null||!world.IsRemote()||player==null||player.entityId!=_entityId||
                manager==null||manager.IsServer||connections==null||connections.Length==0||connections[0]==null||
                connections[0].IsDisconnected()||RebirthGearOwnerReservation.IsHeld(player))return;
            RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connections[0]);
        }
        catch { } // Missing/uncertain native evidence never invents an original plan.
    }
}