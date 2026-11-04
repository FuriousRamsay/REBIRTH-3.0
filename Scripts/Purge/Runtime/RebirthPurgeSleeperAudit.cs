using System;
using System.Collections.Generic;
using UnityEngine;

// Read-only snapshots on command boundaries or a bounded stalled-HUD event.
// Never ticks, activates, restores, kills or clears a room/actor.
internal static class RebirthPurgeSleeperAudit
{
    internal static void Capture(EntityPlayer player,string reason)
    {
        try{CaptureCore(player,reason);}
        catch(Exception error){Log.Warning("[PurgeAudit] snapshot failed reason="+reason+" error="+error.GetType().Name+": "+error.Message);}
    }
    private static string DescribeModel(EntityAlive actor)
    {
        if(actor==null)return "model=unloaded";
        var model=actor.emodel!=null?actor.emodel.GetModelTransform():null;
        if(model==null)return "model=missing";
        var renderers=model.GetComponentsInChildren<Renderer>(true);
        int active=0,visible=0;
        foreach(var renderer in renderers)
        {
            if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
            active++;
            // Unity reports visibility to any camera; this is diagnostic state,
            // not proof the player can see this actor through occlusion.
            if(renderer.isVisible)visible++;
        }
        return "modelActive="+model.gameObject.activeInHierarchy+" renderers="+renderers.Length+
            " activeRenderers="+active+" rendererReportedVisible="+visible+
            " physicsRoot="+(actor.PhysicsTransform!=null);
    }
    private static void CaptureCore(EntityPlayer player,string reason)
    {
        var world=player?.world as World;
        if(world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var prefab=RebirthPurgeScannerPoi.Resolve(world,player.position);
        if(prefab?.prefab==null){Log.Out("[PurgeAudit] reason="+reason+" player="+player.entityId+" poi=none");return;}
        int cleared=0,uncleared=0,registered=0,spawned=0,spawning=0,estimate=0,hudEstimate=0,excluded=0,nativeCount=0,live=0,dead=0,missing=0,pending=0,details=0,actorDetails=0;
        var ids=new HashSet<int>();
        foreach(var room in prefab.sleeperVolumes)
        {
            if(room==null||!ReferenceEquals(room.prefabInstance,prefab))continue;
            registered++;
            if(room.wasCleared){cleared++;continue;}
            uncleared++;if(room.isSpawned)spawned++;if(room.isSpawning)spawning++;
            estimate+=Math.Max(1,room.numSpawned);nativeCount+=room.GetAliveCount();pending+=room.pendingSpawnMap.Count;
            int ignored=RebirthPurgeSleeperPolicy.ExcludedNativeMembers(room);excluded+=ignored;
            if(!RebirthPurgeSleeperPolicy.CombatSettled(room))hudEstimate+=Math.Max(1,room.numSpawned-ignored);
            int roomLive=0,roomDead=0,roomMissing=0;
            foreach(var pair in room.respawnMap)
            {
                if(!ids.Add(pair.Key))continue;
                var actor=world.GetEntity(pair.Key) as EntityAlive;
                if(actor==null){roomMissing++;missing++;}
                else if(actor.IsAlive()){roomLive++;live++;}
                else{roomDead++;dead++;}
                if(actorDetails++<64)
                    Log.Out("[PurgeAudit.Actor] id="+pair.Key+" expectedClass="+pair.Value.className+
                        " room="+room.BoxMin+" loaded="+(actor!=null)+" alive="+(actor?.IsAlive()??false)+
                        " sleeper="+(actor?.IsSleeper??false)+" sleeping="+(actor?.IsSleeping??false)+" passive="+(actor?.IsSleeperPassive??false)+
                        " attackTarget="+(actor?.GetAttackTarget()?.entityId??-1)+" targetTicks="+(actor?.attackTargetTime??0)+
                        " distanceToPlayer="+(actor==null?"unknown":Vector3.Distance(actor.position,player.position).ToString("F1",System.Globalization.CultureInfo.InvariantCulture))+" required="+RebirthPoiCombatantPolicy.IsRequired(pair.Value.className)+" pending="+room.pendingSpawnMap.Contains(pair.Key)+
                        " name="+(actor?.EntityName??"unloaded")+" position="+(actor?.position.ToString()??"unknown")+" "+DescribeModel(actor));
            }
            if(details++<24)
                Log.Out("[PurgeAudit.Room] box="+room.BoxMin+".."+room.BoxMax+" cleared="+room.wasCleared+
                    " spawned="+room.isSpawned+" spawning="+room.isSpawning+" nativePlannedPlusRespawn="+room.GetAliveCount()+
                    " numSpawned="+room.numSpawned+" respawn="+room.respawnMap.Count+" pending="+room.pendingSpawnMap.Count+
                    " restoreQueue="+(room.respawnList?.Count??0)+" liveLoaded="+roomLive+" deadLoaded="+roomDead+
                    " missingLoaded="+roomMissing+" spawnPoints="+room.spawnPointList.Count+
                    " trigger="+room.triggerState+" script="+(room.minScript!=null)+" scriptRunning="+(room.minScript?.IsRunning()??false));
        }
        int authored=prefab.prefab.SleeperVolumeList?.Count??0;
        string state="unpublished";RebirthPoiWorldStore store;RebirthPoiIdentity identity;
        var origin=prefab.boundingBoxPosition;var size=prefab.boundingBoxSize;
        RebirthPoiNativeManifest.RoomCertificate certificate;
        if(RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,prefab,out certificate)&&certificate.BindIdentity())
        {
            identity=certificate.Identity;RebirthPoiClearanceRecord record;
            if(RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)&&store.Published!=null&&store.Published.TryGet(identity,out record))
                state=record.State+" epoch="+record.Epoch+" revision="+record.Revision;
        }
        // One explicit POI-bounds query exposes live sleepers omitted from room registries.
        var bounds=new Bounds(new Vector3(origin.x+size.x*.5f,origin.y+size.y*.5f,origin.z+size.z*.5f),new Vector3(size.x,size.y,size.z));
        int unregisteredLive=0;
        var nearby=world.GetEntitiesInBounds(null,bounds);
        if(nearby!=null)foreach(var entity in nearby)
        {
            var actor=entity as EntityAlive;
            if(actor==null||!actor.IsAlive()||!actor.IsSleeper||ids.Contains(actor.entityId))continue;
            unregisteredLive++;
            if(unregisteredLive<=16)Log.Out("[PurgeAudit.Unregistered] id="+actor.entityId+" name="+actor.EntityName+" position="+actor.position);
        }
        Log.Out("[PurgeAudit] reason="+reason+" player="+player.entityId+" position="+player.position+
            " poi="+prefab.prefab.PrefabName+" origin="+origin+" state="+state+" authored="+authored+
            " registered="+registered+" unresolvedRooms="+Math.Max(0,authored-registered)+" cleared="+cleared+" uncleared="+uncleared+
            " spawned="+spawned+" spawning="+spawning+" nativeEstimate="+estimate+" hudEstimate="+(hudEstimate+Math.Max(0,authored-registered))+" excludedNonCombatants="+excluded+
            " nativePlannedPlusRespawn="+nativeCount+" actualLiveLoaded="+live+" actualDeadLoaded="+dead+
            " missingLoaded="+missing+" pending="+pending+" unregisteredLive="+unregisteredLive+
            " completion="+RebirthPoiNativeEvidence.Instance.CompletionStatus(world,player)+
            " roomDetailTruncated="+(details>24)+" actorDetailTruncated="+(actorDetails>64));
    }
}
