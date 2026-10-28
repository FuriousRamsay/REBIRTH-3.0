using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Per-player projection of existing native evidence. It never creates rooms,
// supplies clear evidence, or scans the world's complete entity collection.
internal static class RebirthPurgeScanner
{
    private static readonly FastTags<TagGroup.Poi> RwgOnly=FastTags<TagGroup.Poi>.Parse("rwgonly");
    private static readonly FastTags<TagGroup.Poi> StreetTile=FastTags<TagGroup.Poi>.Parse("streettile");
    internal sealed class View
    {
        internal int Player, X, Y, Z, Tier, Rooms, Cleared, Enemies;
        internal string Prefab="";
        internal string PoiKey="";
        internal Guid World, Session;
        internal long Sequence, Epoch;
        internal long LifetimeKills=-1;
        internal bool Approximate;
        internal int[] Targets=Array.Empty<int>();
    }
    private sealed class Counter { internal long Epoch; internal int Areas,Entities; internal bool Approximate; }
    private static readonly Dictionary<string,Counter> counters=new Dictionary<string,Counter>(StringComparer.Ordinal);
    internal static long CounterRevision {get;private set;}
    internal static bool TryCounters(RebirthPoiClearanceRecord record,out int areas,out int entities,out bool approximate)
    {
        areas=entities=-1;approximate=true;Counter value;
        if(!ReferenceEquals(owner,GameManager.Instance?.World)||!counters.TryGetValue(record.Identity.Key,out value)||value.Epoch!=record.Epoch)return false;
        areas=value.Areas;entities=value.Entities;approximate=value.Approximate;return true;
    }
    private static World owner;
    private static int cursor;
    private static float next, receivedAt;
    private static long sequence;
    private static View current;
    private static readonly Dictionary<int,float> playerDue=new Dictionary<int,float>();
    private sealed class RoomHistory
    {
        internal string PoiKey;
        internal long Epoch;
        internal int Maximum;
    }
    private static readonly Dictionary<int,RoomHistory> roomHistory=new Dictionary<int,RoomHistory>();
    private static readonly Dictionary<int,NavObject> markers=new Dictionary<int,NavObject>();
    private static readonly Dictionary<int,EntityAlive> projectedActors=new Dictionary<int,EntityAlive>();
    private static readonly HashSet<int> visibleActors=new HashSet<int>();
    private static readonly List<int> removedMarkers=new List<int>();
    internal static bool Threshold(int tier,int total,int cleared)
    {return total>0&&(tier<4?total>2&&total-cleared<=1:total-cleared<=(tier==4?2:tier==5?3:4));}
    private static bool Eligible(EntityPlayer p)=>p!=null&&p.IsSpawned()&&p.IsAlive()&&!p.IsSpectator&&p.AttachedToEntity==null&&!p.IsFlyMode.Value;
    internal static void Reset()
    {
        ClearMarkers();projectedActors.Clear();counters.Clear();CounterRevision++;current=null;owner=null;cursor=0;next=0;receivedAt=0;sequence=0;playerDue.Clear();roomHistory.Clear();
    }
    private static void ClearMarkers()
    {
        if(NavObjectManager.HasInstance)foreach(var marker in markers.Values)NavObjectManager.Instance.UnRegisterNavObject(marker);
        markers.Clear();
    }
    internal static void Pulse()
    {
        var world=GameManager.Instance?.World;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null)
        {if(owner!=null)Reset();return;}
        if(!ReferenceEquals(owner,world)){Reset();owner=world;}
        float now=Time.realtimeSinceStartup;
        if(now<next)return;next=now+.1f;
        if(!world.IsRemote())
        {
            var tracked=world.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
            if(tracked!=null)for(int n=0,limit=Math.Min(4,tracked.Count);n<limit;n++)
            {
                if(cursor>=tracked.Count)cursor=0;
                var player=tracked[cursor++].Player;if(player==null)continue;
                float due;if(playerDue.TryGetValue(player.entityId,out due)&&now<due)continue;
                if(playerDue.Count>512)playerDue.Clear();
                playerDue[player.entityId]=now+.5f;
                var view=Build(world,player);
                if(player is EntityPlayerLocal)Receive(world,view);
                else
                {
                    var client=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId);
                    if(client!=null&&client.loginDone)client.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeScanner>().Setup(view));
                }
            }
        }
        Render(world);
    }
    private static bool TryNativeView(World world,EntityPlayer player,out PrefabInstance prefab,out RebirthPoiIdentity identity,out EntityAlive[] actors)
    {
        prefab=null;identity=null;actors=Array.Empty<EntityAlive>();
        // Match legacy scanner occupancy without borrowing the native name/info-area gate.
        prefab=RebirthPurgeScannerPoi.Resolve(world,player.position);
        var authored=prefab?.prefab;
        if(authored==null||authored.bTraderArea||authored.SleeperVolumeList==null||
            authored.SleeperVolumeList.Count<1||authored.SleeperVolumeList.Count>4096)return false;
        if(!RebirthPurgePoiEligibility.Allows(authored.PrefabName,authored.bTraderArea,authored.Tags.Test_AnySet(RwgOnly),authored.Tags.Test_AnySet(StreetTile)))return false;
        var origin=prefab.boundingBoxPosition;var size=prefab.boundingBoxSize;
        var biome=world.GetBiome(origin.x+size.x/2,origin.z+size.z/2);
        if(biome==null)
        {
            // Entry rooms can be loaded before a large POI's centre chunk.
            // Reuse the census's authored biome metadata without loading chunks.
            try
            {
                var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
                biome=provider?.GetBiomeProvider()?.GetBiomeAt(origin.x+size.x/2,origin.z+size.z/2);
            }
            catch { return false; }
        }
        if(biome==null)return false;
        try{identity=new RebirthPoiIdentity(authored.PrefabName,origin.x,origin.y,origin.z,(int)prefab.rotation,size.x,size.y,size.z,biome.m_sBiomeName);}
        catch(ArgumentException){return false;}
        var live=new List<EntityAlive>();var seen=new HashSet<int>();int roomBudget=4096,entryBudget=8192;
        foreach(var room in prefab.sleeperVolumes)
        {
            if(roomBudget--<=0)break;
            if(room==null||!ReferenceEquals(room.prefabInstance,prefab))continue;
            foreach(int id in room.respawnMap.Keys)
            {
                if(entryBudget--<=0){actors=live.ToArray();return true;}
                if(!seen.Add(id))continue;
                var entity=world.GetEntity(id) as EntityAlive;
                if(entity!=null&&entity.IsAlive()&&entity.IsSleeper)live.Add(entity);
                if(seen.Count>=8192){actors=live.ToArray();return true;}
            }
        }
        actors=live.ToArray();return true;
    }
    private static View Build(World world,EntityPlayer player)
    {
        var result=new View{Player=player.entityId,World=RebirthPoiMapSync.TransportWorld,Session=RebirthPoiMapSync.TransportSession,Sequence=++sequence};
        var lifetime=RebirthPurgeSupplyService.LifetimeKills;
        string playerKey=RebirthPurgeKillContributor.Identify(player,world);
        if(lifetime!=null&&playerKey!=null){long kills;lifetime.TryGetValue(playerKey,out kills);result.LifetimeKills=kills;}
        PrefabInstance prefab;RebirthPoiIdentity identity;EntityAlive[] actors;
        if(!Eligible(player)||!TryNativeView(world,player,out prefab,out identity,out actors))
        {roomHistory.Remove(player.entityId);return result;}
        // Room HUD is a native presentation, not a durable clearance proof.
        // A partially loaded POI need not yet have a complete evidence manifest
        // or a published discovery record (the 2.6 scanner had neither gate).
        RebirthPoiWorldStore store;RebirthPoiClearanceRecord record=null;
        if(RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)&&store.Published!=null)
            store.Published.TryGet(identity,out record);
        if(record!=null&&(record.ResetOnly||record.State!=RebirthPoiClearanceState.Discovered))return result;
        result.PoiKey=identity.Key;result.Epoch=record?.Epoch??0;
        result.Prefab=identity.Prefab;result.X=identity.X;result.Y=identity.Y;result.Z=identity.Z;
        result.Tier=prefab.prefab.DifficultyTier;
        // 2.6 displays every authored room, independently of the combat-only
        // manifest used to prove clearance. Count current native rooms likewise.
        result.Rooms=prefab.prefab.SleeperVolumeList.Count;
        int remaining=0, observedRooms=0,roomBudget=4096;
        foreach(var room in prefab.sleeperVolumes)
        {
            if(roomBudget--<=0||observedRooms>=result.Rooms)break;
            if(room==null||!ReferenceEquals(room.prefabInstance,prefab))continue;
            observedRooms++;
            if(room.wasCleared)result.Cleared++;
            else
            {
                remaining++;
                // The original counter uses current native population, with one
                // estimated enemy for an uncleared room that has not populated.
                result.Enemies=(int)Math.Min(1000000L,(long)result.Enemies+Math.Max(1,room.numSpawned));
                if(room.numSpawned<=0||room.isSpawning)result.Approximate=true;
            }
        }
        // Non-combat authored rooms need not be loaded for clearance evidence.
        // Keep those missing rooms uncleared in the presentation too; otherwise
        // partial loading can satisfy the scanner threshold prematurely.
        int unresolvedRooms=Math.Max(0,result.Rooms-observedRooms);
        if(unresolvedRooms>0)
        {
            remaining+=unresolvedRooms;
            result.Enemies=(int)Math.Min(1000000L,(long)result.Enemies+unresolvedRooms);
            result.Approximate=true;
        }
        Counter prior;
        if(record!=null&&(!counters.TryGetValue(identity.Key,out prior)||prior.Epoch!=record.Epoch||prior.Areas!=remaining||prior.Entities!=result.Enemies||prior.Approximate!=result.Approximate))
        {
            if(counters.Count>=RebirthPoiClearanceLedger.MaximumRecords)counters.Clear();
            counters[identity.Key]=new Counter{Epoch=record.Epoch,Areas=remaining,Entities=result.Enemies,Approximate=result.Approximate};CounterRevision++;
        }
        RoomHistory history;
        if(!roomHistory.TryGetValue(player.entityId,out history)||history.PoiKey!=identity.Key||history.Epoch!=result.Epoch)
        {
            if(roomHistory.Count>512)roomHistory.Clear();
            history=new RoomHistory{PoiKey=identity.Key,Epoch=result.Epoch};roomHistory[player.entityId]=history;
        }
        history.Maximum=Math.Max(history.Maximum,remaining);
        int max=history.Maximum;
        // Presentation may be available before evidence. Never use it to
        // override an existing reset/fault owner or mutate clearance state.
        if(record!=null&&(record.ResetOnly||record.State==RebirthPoiClearanceState.ResetPending)||
            RebirthPoiNativeEvidence.Instance.IsWithheld(identity))return result;
        foreach(var entity in actors)
            if(entity.IsSleeper && entity.Buffs.GetCustomVar("$sleeperActivated")==1)ActivatePending(world,prefab,entity);
        if(!Threshold(result.Tier,result.Rooms,result.Cleared))return result;
        var targets=new List<int>();
        // The projection supports the full 8192-actor observation bound, rather
        // than silently dropping markers in large authored POIs.
        foreach(var entity in actors)
        {
            if(!entity.IsSleeper)continue;
            bool admitted=false;
            if(max>0&&(max>1||remaining>0))
            {
                float state=entity.Buffs.GetCustomVar("$sleeperActivated");
                if(state==0){entity.Buffs.SetCustomVar("$sleeperActivated",1,true);continue;}
                else if(state==1)continue;
                else admitted=state==2;
                // The original max>1 branch withholds all other activation states.
                if(max>1&&!admitted)continue;
            }
            if(!admitted&&!entity.IsSleeperPassive&&player.QuestJournal?.ActiveQuest!=null)
            {
                Vector3 point=entity.position;point.y=point.z;
                admitted=QuestBoundsContain(player,point);
            }
            if(admitted&&(entity.position-player.position).sqrMagnitude<=2500f)targets.Add(entity.entityId);
        }
        result.Targets=targets.ToArray();return result;
    }
    private static void ActivatePending(World world,PrefabInstance prefab,EntityAlive entity)
    {
        var entityPoi=RebirthPurgeScannerPoi.Resolve(world,entity.position);
        if(entityPoi==null)return;
        var closest=world.GetClosestPlayer(entity.position,120f,false);
        if(!TargetInPoi(world,closest,entityPoi,prefab))
        {
            // A nearer player outside the building must not prevent an eligible
            // player inside this POI from receiving the remaining sleeper.
            closest=null;
            float nearest=120f*120f;
            var tracked=world.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
            if(tracked!=null)foreach(var entry in tracked)
            {
                var candidate=entry.Player;
                if(!Eligible(candidate))continue;
                float distance=(candidate.position-entity.position).sqrMagnitude;
                if(distance>nearest)continue;
                if(!TargetInPoi(world,candidate,entityPoi,prefab))continue;
                nearest=distance;closest=candidate;
            }
        }
        if(Eligible(closest))
        {
            var nearestPoi=RebirthPurgeScannerPoi.Resolve(world,closest.position);
            if(nearestPoi!=null && nearestPoi.boundingBoxPosition.Equals(entityPoi.boundingBoxPosition) &&
                entityPoi.boundingBoxPosition.Equals(prefab.boundingBoxPosition))
            {
                entity.Buffs.SetCustomVar("$sleeperActivated",2,true);
                entity.ConditionalTriggerSleeperWakeUp();entity.SetAttackTarget(closest,60000);
                entity.sightRangeBase=200;entity.SetMaxViewAngle(360);
            }
        }
        // Keep the request pending when there is no eligible player in this POI.
        // A player may return or leave fly mode on the next scanner pulse.
    }
    private static bool TargetInPoi(World world,EntityPlayer player,PrefabInstance entityPoi,PrefabInstance prefab)
    {
        if(!Eligible(player)||entityPoi==null||world==null||
            !entityPoi.boundingBoxPosition.Equals(prefab.boundingBoxPosition))return false;
        var playerPoi=RebirthPurgeScannerPoi.Resolve(world,player.position);
        return playerPoi!=null&&playerPoi.boundingBoxPosition.Equals(entityPoi.boundingBoxPosition);
    }
    private static bool QuestBoundsContain(EntityPlayer player,Vector3 point)
    {
        var quest=player.QuestJournal?.ActiveQuest;
        if(quest==null)return false;
        var local=player as EntityPlayerLocal;
        if(local!=null)return local.ZombieCompassBounds.Contains(point);
        // ObjectiveClearSleepers builds the local rectangle from these native
        // quest fields. Remote EntityPlayer has no ZombieCompassBounds member.
        Vector3 position=Vector3.zero,size=Vector3.zero;
        return quest.GetPositionData(out position,Quest.PositionDataTypes.POIPosition)&&
            quest.GetPositionData(out size,Quest.PositionDataTypes.POISize)&&
            new Rect(position.x,position.z,size.x,size.z).Contains(point);
    }
    internal static void Receive(World world,View view)
    {
        if(world==null||!ReferenceEquals(GameManager.Instance?.World,world)||view==null||
            world.GetPrimaryPlayer()?.entityId!=view.Player)return;
        if(!ReferenceEquals(owner,world)){Reset();owner=world;}
        if(view.World==Guid.Empty||view.Session==Guid.Empty||view.Sequence<1||
            RebirthPoiMapSync.TransportWorld!=Guid.Empty&&(view.World!=RebirthPoiMapSync.TransportWorld||view.Session!=RebirthPoiMapSync.TransportSession))return;
        if(current!=null&&current.World==view.World&&current.Session==view.Session&&view.Sequence<=current.Sequence)return;
        // Pin already replicated actors for this snapshot. A reused runtime ID
        // must not make an old projection follow a replacement entity.
        projectedActors.Clear();
        foreach(int id in view.Targets)
        {
            var entity=world.GetEntity(id) as EntityAlive;
            if(entity!=null)projectedActors[id]=entity;
        }
        current=view;receivedAt=Time.realtimeSinceStartup;
    }
    private static bool IsCurrent(World world,EntityPlayerLocal player)
    {
        if(world==null||!ReferenceEquals(owner,world)||!ReferenceEquals(GameManager.Instance?.World,world)||
            current==null||!Eligible(player)||current.Player!=player.entityId||string.IsNullOrEmpty(current.Prefab)||
            Time.realtimeSinceStartup-receivedAt>10)return false;
        RebirthPoiMapRecord record;
        var map=RebirthPoiMapSync.TransportMap;
        // Receive already validates the native world object and nonempty
        // scanner identity. Before the first map publication, its identity is
        // unknown; do not make map transfer completion a scanner HUD gate.
        if(RebirthPoiMapSync.TransportWorld!=Guid.Empty&&
            (current.World!=RebirthPoiMapSync.TransportWorld||current.Session!=RebirthPoiMapSync.TransportSession))return false;
        // A known successor epoch invalidates old projections; an absent map
        // entry or an older epoch must not hide a newer scanner snapshot while
        // the independently paged map projection catches up after a reset.
        if(map!=null&&map.TryGetValue(current.PoiKey,out record)&&(record.Epoch>current.Epoch||record.Epoch==current.Epoch&&record.State!=RebirthPoiClearanceState.Discovered))return false;
        var prefab=RebirthPurgeScannerPoi.Resolve(world,player.position);
        return prefab?.prefab!=null&&prefab.prefab.PrefabName==current.Prefab&&prefab.boundingBoxPosition.x==current.X&&
            prefab.boundingBoxPosition.y==current.Y&&prefab.boundingBoxPosition.z==current.Z;
    }
    // Explicit Bridge observation uses the existing projection without advancing it.
    internal static View Observe(EntityPlayerLocal player) => IsCurrent(GameManager.Instance?.World, player) ? current : null;
    internal static int ObservedMarkerCount => markers.Count;
    internal static string Hud(EntityPlayerLocal player)
    {
        if(!IsCurrent(GameManager.Instance?.World,player))return "";
        return string.Format(Localization.Get("xuiRebirthPurgeRoomProgress"),current.Cleared,current.Rooms,
            (current.Approximate?"~":"")+current.Enemies);
    }
    internal static string LifetimeText(EntityPlayerLocal player)
    {
        var world=GameManager.Instance?.World;
        if(world==null||!ReferenceEquals(owner,world)||current==null||player==null||current.Player!=player.entityId||
            RebirthPoiMapSync.TransportWorld!=Guid.Empty&&
            (current.World!=RebirthPoiMapSync.TransportWorld||current.Session!=RebirthPoiMapSync.TransportSession)||
            Time.realtimeSinceStartup-receivedAt>10||current.LifetimeKills<0)return "—";
        return current.LifetimeKills.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    private static void Render(World world)
    {
        var player=world.GetPrimaryPlayer();
        if(!IsCurrent(world,player)||!NavObjectManager.HasInstance){ClearMarkers();return;}
        visibleActors.Clear();
        foreach(int id in current.Targets)
        {
            var entity=world.GetEntity(id) as EntityAlive;
            EntityAlive projected;
            if(entity==null||!entity.IsAlive()||!entity.IsSleeper||
                projectedActors.TryGetValue(id,out projected)&&!ReferenceEquals(projected,entity)||
                (entity.position-player.position).sqrMagnitude>2500f)continue;
            projectedActors[id]=entity;
            visibleActors.Add(id);NavObject marker;
            if(!markers.TryGetValue(id,out marker))
            {
                marker=NavObjectManager.Instance.RegisterNavObject("rebirth_purge_scanner",entity.position,string.Empty,false,-1000000-id,player);
                if(marker==null)continue;markers.Add(id,marker);
            }
            marker.TrackedPosition=entity.position;marker.IsActive=true;marker.ForceDisabled=false;
            marker.hiddenOnCompass=false;marker.hiddenOnMap=false;
        }
        removedMarkers.Clear();
        foreach(int id in markers.Keys)if(!visibleActors.Contains(id))removedMarkers.Add(id);
        foreach(int id in removedMarkers)
        {NavObjectManager.Instance.UnRegisterNavObject(markers[id]);markers.Remove(id);}
    }
}
