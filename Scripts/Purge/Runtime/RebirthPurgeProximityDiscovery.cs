using System;
using System.Collections.Generic;
using UnityEngine;

// Actual visits publish marker discovery independently of live sleeper registration.
// This never spawns, activates, observes deaths, or grants clearance/rewards.
internal static class RebirthPurgeProximityDiscovery
{
    private static World owner;
    private static float next;
    private static readonly Dictionary<int,PrefabInstance> visited = new Dictionary<int,PrefabInstance>();
    internal static void Reset(){owner=null;next=0;visited.Clear();}
    internal static void Pulse()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        if(!ReferenceEquals(owner,world)){owner=world;next=0;visited.Clear();}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.5f;
        RebirthPoiWorldStore store;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)||store.HasPending)return;
        var players=world.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        if(players==null)return;
        foreach(var tracked in players)
        {
            var player=tracked.Player;
            if(player==null||player.IsDead()||!player.IsSpawned()||player.IsSpectator||player.IsFlyMode.Value||player.AttachedToEntity!=null||
                !ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player))continue;
            var prefab=RebirthPurgeScannerPoi.Resolve(world,player.position);
            if(prefab==null){visited.Remove(player.entityId);continue;}
            if(visited.TryGetValue(player.entityId,out var previous)&&ReferenceEquals(previous,prefab))continue;
            // Authored combat eligibility only: no live room, sleeper, LOS or sight requirement.
            var definitions=prefab.prefab?.SleeperVolumeList;
            bool combat=false;
            if(definitions!=null)for(int i=0;i<definitions.Count;i++)
            {var definition=definitions[i];if(definition.spawnCountMax>0||!string.IsNullOrWhiteSpace(definition.minScript)){combat=true;break;}}
            if(!combat)continue;
            if(!RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,prefab,out var authored)||!authored.BindIdentity())continue;
            var snapshot=store.Published;
            if(snapshot==null||!snapshot.Binding.IsCurrent)continue;
            RebirthPoiClearanceRecord record;
            if(snapshot.TryGet(authored.Identity,out record)&&record.State==RebirthPoiClearanceState.ResetPending)continue;
            if(record==null||record.ResetOnly)
            {
                var result=store.TryDiscover(snapshot,authored.Identity);
                if(result!=RebirthPoiStoreResult.Published&&result!=RebirthPoiStoreResult.Duplicate)return;
                snapshot=store.Published;
                if(snapshot==null||!snapshot.TryGet(authored.Identity,out record)||record.ResetOnly)return;
            }
            var history=RebirthPurgePoiHistory.Get(record);
            if(history==null||history.DiscoveredAt==0)
                RebirthPurgeDiscoveryNotification.Send(authored.Identity,player);
            visited[player.entityId]=prefab;
            if(store.HasPending)return;
        }
    }
}
