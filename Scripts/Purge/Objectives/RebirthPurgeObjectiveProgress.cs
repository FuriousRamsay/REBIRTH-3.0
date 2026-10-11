using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Derived current-world truth; never a reward receipt or personal quest state.
internal sealed class RebirthPurgeObjectiveProgress
{
    internal static readonly RebirthPurgeObjectiveProgress Instance=new RebirthPurgeObjectiveProgress();
    internal sealed class BiomeProgress
    {
        internal readonly string Biome;
        internal readonly int Eligible,Discovered,Cleared;
        internal readonly IReadOnlyDictionary<int,int> EligibleByTier,ClearedByTier;
        internal BiomeProgress(string biome,Counter counter)
        {
            Biome=biome;Eligible=counter.Eligible;Discovered=counter.Discovered;Cleared=counter.Cleared;
            EligibleByTier=new ReadOnlyDictionary<int,int>(counter.Tiers);
            ClearedByTier=new ReadOnlyDictionary<int,int>(counter.ClearedTiers);
        }
        internal int RemainingFor(int percent)
        {
            if(percent<1||percent>100)throw new ArgumentOutOfRangeException(nameof(percent));
            return Math.Max(0,(int)(((long)Eligible*percent+99)/100)-Cleared);
        }
    }
    internal sealed class Counter
    {
        internal int Eligible,Discovered,Cleared;
        internal readonly Dictionary<int,int> Tiers=new Dictionary<int,int>(),ClearedTiers=new Dictionary<int,int>();
        internal static void Increment(Dictionary<int,int> counts,int tier) { int n;counts.TryGetValue(tier,out n);counts[tier]=n+1; }
    }
    private readonly object changesGate=new object();
    private readonly Queue<RebirthPoiIdentity> changes=new Queue<RebirthPoiIdentity>();
    private readonly HashSet<string> queued=new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string,byte> states=new Dictionary<string,byte>(StringComparer.Ordinal);
    private IEnumerator<RebirthPurgePoiCensus.Entry> pending;
    private RebirthPoiWorldStore owner;
    private RebirthPoiWorldSnapshot snapshot,observed;
    private IReadOnlyDictionary<string,RebirthPurgePoiCensus.Entry> eligible;
    private bool notificationGap;
    private long censusGeneration=-1;
    private Dictionary<string,Counter> counters;
    internal IReadOnlyDictionary<string,BiomeProgress> Published { get; private set; }
    internal Guid WorldId { get; private set; }
    internal long Revision { get; private set; }
    internal int TargetPercentage => RebirthPurgeObjectivePolicy.TargetPercentage;
    internal long CensusGeneration => Math.Max(0,censusGeneration);
    internal void Reset()
    {
        if(owner!=null)owner.PublicationChanged-=Changed;
        owner=null;
        if(pending!=null)pending.Dispose();pending=null;snapshot=null;counters=null;eligible=null;
        lock(changesGate){observed=null;notificationGap=false;changes.Clear();queued.Clear();}
        states.Clear();Published=null;WorldId=Guid.Empty;Revision=0;censusGeneration=-1;
    }
    private void Changed(RebirthPoiWorldSnapshot original,RebirthPoiWorldSnapshot next,IReadOnlyList<RebirthPoiIdentity> identities)
    {
        lock(changesGate)
        {
            if(!ReferenceEquals(observed,original)||identities==null||next==null||snapshot==null||
               !ReferenceEquals(next.Binding,snapshot.Binding)){notificationGap=true;return;}
            observed=next;
            foreach(var identity in identities)
            {
                RebirthPurgePoiCensus.Entry entry;
                if(identity!=null&&eligible.TryGetValue(identity.Key,out entry)&&entry.Identity.Biome==identity.Biome&&queued.Add(identity.Key))
                    changes.Enqueue(entry.Identity);
            }
        }
    }
    private static byte State(RebirthPoiWorldSnapshot source,RebirthPoiIdentity identity)
    {
        RebirthPoiClearanceRecord record;
        if(!source.TryGet(identity,out record)||record.ResetOnly)return 0;
        return record.State==RebirthPoiClearanceState.Cleared?(byte)2:(byte)1;
    }
    private static void AdjustTier(Dictionary<int,int> tiers,int tier,int delta)
    {
        int before;tiers.TryGetValue(tier,out before);int next=before+delta;
        if(next==0)tiers.Remove(tier);else tiers[tier]=next;
    }
    private void Apply(RebirthPurgePoiCensus.Entry entry,byte state)
    {
        byte before;states.TryGetValue(entry.Identity.Key,out before);
        if(before==state)return;
        var counter=counters[entry.Identity.Biome];
        counter.Discovered+=(state>0?1:0)-(before>0?1:0);
        int cleared=(state==2?1:0)-(before==2?1:0);counter.Cleared+=cleared;
        if(cleared!=0)AdjustTier(counter.ClearedTiers,entry.Tier,cleared);
        if(state==0)states.Remove(entry.Identity.Key);else states[entry.Identity.Key]=state;
    }
    internal void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        var census=RebirthPurgePoiCensus.Instance;RebirthPoiWorldStore store;
        if(!census.Ready||census.Published==null||!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)){Reset();return;}
        var current=store.Published;
        if(current==null||!current.Binding.IsCurrent){Reset();return;}
        bool gap;lock(changesGate)gap=notificationGap||owner!=null&&!ReferenceEquals(observed,current);
        if(owner==null||!ReferenceEquals(owner,store)||!ReferenceEquals(snapshot?.Binding,current.Binding)||censusGeneration!=census.Generation||gap)
        {
            Reset();owner=store;snapshot=current;observed=current;censusGeneration=census.Generation;eligible=census.Published;
            counters=new Dictionary<string,Counter>(StringComparer.Ordinal);
            pending=eligible.Values.GetEnumerator();owner.PublicationChanged+=Changed;
        }
        // Complete the original census slice even when newer publications arrive.
        // Exact changed identities then catch up without restarting the whole world.
        long deadline=System.Diagnostics.Stopwatch.GetTimestamp()+(long)(System.Diagnostics.Stopwatch.Frequency*.004);
        int budget=64;
        while(pending!=null&&budget-- >0&&System.Diagnostics.Stopwatch.GetTimestamp()<deadline)
        {
            if(!pending.MoveNext()){pending.Dispose();pending=null;break;}
            var entry=pending.Current;Counter counter;
            if(!counters.TryGetValue(entry.Identity.Biome,out counter)){counter=new Counter();counters.Add(entry.Identity.Biome,counter);}
            counter.Eligible++;Counter.Increment(counter.Tiers,entry.Tier);
            Apply(entry,State(snapshot,entry.Identity));
        }
        if(pending!=null)return;
        budget=64;
        while(budget-- >0&&System.Diagnostics.Stopwatch.GetTimestamp()<deadline)
        {
            RebirthPoiIdentity identity;
            lock(changesGate){if(changes.Count==0)break;identity=changes.Dequeue();queued.Remove(identity.Key);}
            RebirthPurgePoiCensus.Entry entry;
            if(eligible.TryGetValue(identity.Key,out entry))Apply(entry,State(current,identity));
        }
        bool ready;lock(changesGate)ready=!notificationGap&&changes.Count==0&&ReferenceEquals(observed,current);
        if(!ready||!ReferenceEquals(store.Published,current))return;
        if(Published!=null&&Revision==current.Revision&&WorldId==current.WorldId)return;
        var result=new Dictionary<string,BiomeProgress>(StringComparer.Ordinal);
        foreach(var pair in counters)
        {
            // A detached publication must not share the next mutable counters.
            var copy=new Counter{Eligible=pair.Value.Eligible,Discovered=pair.Value.Discovered,Cleared=pair.Value.Cleared};
            foreach(var tier in pair.Value.Tiers)copy.Tiers.Add(tier.Key,tier.Value);
            foreach(var tier in pair.Value.ClearedTiers)copy.ClearedTiers.Add(tier.Key,tier.Value);
            result.Add(pair.Key,new BiomeProgress(pair.Key,copy));
        }
        Published=new ReadOnlyDictionary<string,BiomeProgress>(result);WorldId=current.WorldId;Revision=current.Revision;
    }
}