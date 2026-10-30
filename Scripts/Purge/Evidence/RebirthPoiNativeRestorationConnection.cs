using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// Exclusive native replacement ownership. No TTL or competing store writer.
internal static class RebirthPoiNativeRestorationConnection
{
    private sealed class Pending
    {internal RebirthPoiNativeRestorationWitness Witness;internal RebirthPoiActorObservation Observation;internal bool Failed;}
    private sealed class OriginalCall
    {internal RebirthPoiNativeRestorationWitness Witness;internal OriginalCall Previous;internal readonly List<EntityAlive> Callbacks=new List<EntityAlive>();}
    [ThreadStatic]private static OriginalCall active;
    private static readonly Dictionary<int,Pending> pending=new Dictionary<int,Pending>();
    private static readonly Queue<int> retry=new Queue<int>();
    private static readonly HashSet<SleeperVolume> blocked=new HashSet<SleeperVolume>();
    private static double nextPulse;
    private static bool exhausted;
    internal static void Reset(){pending.Clear();retry.Clear();blocked.Clear();nextPulse=0;active=null;exhausted=false;}
    private static bool Matches(Pending p,RebirthPoiIdentity identity)=>RebirthPoiNativeEvidence.Instance.Owns(p.Witness.Volume,identity);
    private static bool Blocked(RebirthPoiIdentity identity)=>blocked.Any(v=>RebirthPoiNativeEvidence.Instance.Owns(v,identity));
    internal static bool HasPending(RebirthPoiIdentity identity)=>exhausted||Blocked(identity)||pending.Values.Any(p=>Matches(p,identity));
    internal static bool HasUnresolved(RebirthPoiIdentity identity)=>exhausted||Blocked(identity)||pending.Values.Any(p=>Matches(p,identity)&&(p.Failed||p.Observation==null));
    internal static RebirthPoiNativeRestorationWitness BeforeUpdate(World world,SleeperVolume volume)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||world==null||world.IsRemote()||volume==null||volume.spawnDelay>1||volume.spawnsAvailable==null||
            volume.respawnList==null||volume.respawnList.Count==0)return null;
        int originalId=volume.respawnList[volume.respawnList.Count-1];
        if(world.GetEntity(originalId)!=null||volume.pendingSpawnMap.Contains(originalId)||!volume.respawnMap.TryGetValue(originalId,out var native))return null;
        var witness=RebirthPoiNativeEvidence.Instance.BeginRestoration(world,volume,originalId,native);
        active=new OriginalCall{Witness=witness,Previous=active};return witness;
    }
    internal static void AfterUpdate(World world,SleeperVolume volume,RebirthPoiNativeRestorationWitness witness,Exception failure)
    {
        OriginalCall call=null;
        if(active!=null&&ReferenceEquals(active.Witness,witness)){call=active;active=active.Previous;}
        if(witness==null)return;
        if(failure!=null||!witness.FinishOriginalUpdate())
        {
            if(failure==null&&witness.NativeDeferred&&call!=null&&call.Callbacks.Count==0)return;
            RebirthPoiNativeEvidence.Instance.Withhold(world,volume);
            if(blocked.Count<4096||blocked.Contains(volume))blocked.Add(volume);else exhausted=true;
            return;
        }
        int id=witness.SuccessorEntityId;
        if(pending.Count>=4096||pending.ContainsKey(id)){exhausted=true;RebirthPoiNativeEvidence.Instance.Withhold(world,volume);return;}
        pending.Add(id,new Pending{Witness=witness});retry.Enqueue(id);
        if(call!=null)foreach(var entity in call.Callbacks)if(entity.entityId==id)ActualSpawn(world,volume,entity);
    }
    internal static bool ActualSpawn(World world,SleeperVolume volume,EntityAlive entity)
    {
        if(entity==null)return false;
        if(blocked.Contains(volume))return true;
        if(!pending.TryGetValue(entity.entityId,out var p))
        {
            if(active!=null&&ReferenceEquals(active.Witness.World,world)&&ReferenceEquals(active.Witness.Volume,volume))
            {if(active.Callbacks.Count<4)active.Callbacks.Add(entity);else exhausted=true;return true;}
            return exhausted;
        }
        if(!ReferenceEquals(p.Witness.World,world)||!ReferenceEquals(p.Witness.Volume,volume))return true;
        if(p.Observation==null&&!p.Failed)
        {
            if(!p.Witness.TryCompleteActualSpawn(entity,out var observation)||!RebirthPoiNativeEvidence.Instance.NativeRestored(world,volume,entity,p.Witness.Actor,observation))
            {p.Failed=true;RebirthPoiNativeEvidence.Instance.Withhold(world,volume);return true;}
            p.Observation=observation;
        }
        return true;
    }
    internal static void Pulse(bool immediate=false)
    {
        double now=(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
        if(!immediate&&now<nextPulse)return;nextPulse=now+.5;
        int budget=Math.Min(4,retry.Count);
        while(budget-->0)
        {
            int id=retry.Dequeue();if(!pending.TryGetValue(id,out var p))continue;
            if(p.Observation!=null&&!p.Failed&&RebirthPoiNativeEvidence.Instance.IsAcknowledged(p.Witness.Volume,p.Observation))pending.Remove(id);
            else retry.Enqueue(id);
        }
    }
}
