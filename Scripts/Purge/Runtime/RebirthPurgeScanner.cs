using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Per-player projection of existing native evidence. It never creates rooms,
// supplies clear evidence, or scans the world's complete entity collection.
internal static class RebirthPurgeScanner
{
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
    private static View Build(World world,EntityPlayer player)
    {
        var result=new View{Player=player.entityId,World=RebirthPoiMapSync.LocalWorld,Session=RebirthPoiMapSync.LocalSession,Sequence=++sequence};
        var lifetime=RebirthPurgeSupplyService.LifetimeKills;
        string playerKey=RebirthPurgeKillContributor.Identify(player,world);
        if(lifetime!=null&&playerKey!=null){long kills;lifetime.TryGetValue(playerKey,out kills);result.LifetimeKills=kills;}
        RebirthPoiNativeManifest manifest;EntityAlive[] actors;
        if(!Eligible(player)||!RebirthPoiNativeEvidence.Instance.TryScannerView(world,player,out manifest,out actors))
        {roomHistory.Remove(player.entityId);return result;}
        var identity=manifest.Identity;
        RebirthPoiWorldStore store;RebirthPoiClearanceRecord record;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)||store.Published==null||!store.Published.TryGet(identity,out record))return result;
        result.PoiKey=identity.Key;result.Epoch=record.Epoch;
        result.Prefab=identity.Prefab;result.X=identity.X;result.Y=identity.Y;result.Z=identity.Z;
        result.Tier=manifest.Prefab.prefab.DifficultyTier;
        // 2.6 displays every authored room, independently of the combat-only
        // manifest used to prove clearance. Count current native rooms likewise.
        result.Rooms=manifest.Prefab.prefab.SleeperVolumeList.Count;
        int remaining=0, observedRooms=0;
        foreach(var room in manifest.Prefab.sleeperVolumes)
        {
            if(room==null)continue;
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
        if(!counters.TryGetValue(identity.Key,out prior)||prior.Epoch!=record.Epoch||prior.Areas!=remaining||prior.Entities!=result.Enemies||prior.Approximate!=result.Approximate)
        {
            if(counters.Count>=RebirthPoiClearanceLedger.MaximumRecords)counters.Clear();
            counters[identity.Key]=new Counter{Epoch=record.Epoch,Areas=remaining,Entities=result.Enemies,Approximate=result.Approximate};CounterRevision++;
        }
        RoomHistory history;
        if(!roomHistory.TryGetValue(player.entityId,out history)||history.PoiKey!=identity.Key||history.Epoch!=record.Epoch)
        {
            if(roomHistory.Count>512)roomHistory.Clear();
            history=new RoomHistory{PoiKey=identity.Key,Epoch=record.Epoch};roomHistory[player.entityId]=history;
        }
        history.Maximum=Math.Max(history.Maximum,remaining);
        int max=history.Maximum;
        foreach(var entity in actors)
            if(entity.IsSleeper && entity.Buffs.GetCustomVar("$sleeperActivated")==1)ActivatePending(world,manifest,entity);
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
    private static void ActivatePending(World world,RebirthPoiNativeManifest manifest,EntityAlive entity)
    {
        var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
        var entityPoint=entity.GetBlockPosition();
        var entityPoi=decorator?.GetPrefabFromWorldPosInside(entityPoint.x,entityPoint.z);
        if(entityPoi==null)return;
        var closest=world.GetClosestPlayer(entity.position,120f,false);
        if(!TargetInPoi(closest,entityPoi,manifest,decorator))
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
                if(!TargetInPoi(candidate,entityPoi,manifest,decorator))continue;
                nearest=distance;closest=candidate;
            }
        }
        if(Eligible(closest))
        {
            var point=closest.GetBlockPosition();
            var nearestPoi=decorator?.GetPrefabFromWorldPosInside(point.x,point.z);
            if(nearestPoi!=null && nearestPoi.boundingBoxPosition.Equals(entityPoi.boundingBoxPosition) &&
                entityPoi.boundingBoxPosition.Equals(manifest.Prefab.boundingBoxPosition))
            {
                entity.Buffs.SetCustomVar("$sleeperActivated",2,true);
                entity.ConditionalTriggerSleeperWakeUp();entity.SetAttackTarget(closest,60000);
                entity.sightRangeBase=200;entity.SetMaxViewAngle(360);
            }
        }
        // Keep the request pending when there is no eligible player in this POI.
        // A player may return or leave fly mode on the next scanner pulse.
    }
    private static bool TargetInPoi(EntityPlayer player,PrefabInstance entityPoi,RebirthPoiNativeManifest manifest,DynamicPrefabDecorator decorator)
    {
        if(!Eligible(player)||entityPoi==null||decorator==null||
            !entityPoi.boundingBoxPosition.Equals(manifest.Prefab.boundingBoxPosition))return false;
        var point=player.GetBlockPosition();
        var playerPoi=decorator.GetPrefabFromWorldPosInside(point.x,point.z);
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
            RebirthPoiMapSync.LocalWorld!=Guid.Empty&&(view.World!=RebirthPoiMapSync.LocalWorld||view.Session!=RebirthPoiMapSync.LocalSession))return;
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
        if(current==null||!Eligible(player)||current.Player!=player.entityId||string.IsNullOrEmpty(current.Prefab)||
            Time.realtimeSinceStartup-receivedAt>10)return false;
        RebirthPoiMapRecord record;
        var map=RebirthPoiMapSync.LocalMap;
        if(current.World!=RebirthPoiMapSync.LocalWorld||current.Session!=RebirthPoiMapSync.LocalSession||map==null||
            !map.TryGetValue(current.PoiKey,out record)||record.Epoch!=current.Epoch||record.State!=RebirthPoiClearanceState.Discovered)return false;
        var point=player.GetBlockPosition();
        var prefab=GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabFromWorldPosInside(point.x,point.z);
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
        if(current==null||player==null||current.Player!=player.entityId||current.World!=RebirthPoiMapSync.LocalWorld||
            current.Session!=RebirthPoiMapSync.LocalSession||Time.realtimeSinceStartup-receivedAt>10||current.LifetimeKills<0)return "—";
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
