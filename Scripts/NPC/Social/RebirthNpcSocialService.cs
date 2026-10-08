
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcSocialEventKind : byte
{
    Greeting, Trade, Assistance, Healing, CombatSupport, Theft, Assault, Murder,
    Trespass, PropertyDamage, PromiseKept, PromiseBroken, Conversation
}

public sealed class RebirthNpcRelationship
{
    public float Familiarity, Trust, Respect, Fear, Gratitude, Suspicion, Loyalty, Hostility;
    public uint Revision;
    public long LastChangedUtcTicks;
}

public sealed class RebirthNpcSocialMemory
{
    public Guid EventId;
    public RebirthNpcStableId Subject;
    public string Counterparty;
    public RebirthNpcSocialEventKind Kind;
    public float Importance;
    public float Valence;
    public float Confidence;
    public long CreatedUtcTicks;
    public long LastReinforcedUtcTicks;
    public long LastDecayUtcTicks;
}

public static class RebirthNpcSocialService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthNpcRelationship> Relationships =
        new Dictionary<string, RebirthNpcRelationship>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<RebirthNpcStableId, List<RebirthNpcSocialMemory>> Memories =
        new Dictionary<RebirthNpcStableId, List<RebirthNpcSocialMemory>>();
    private static readonly HashSet<Guid> AppliedEvents = new HashSet<Guid>();
    private static readonly Queue<Guid> AppliedEventOrder = new Queue<Guid>();
    private static long eventsApplied, replays, decayPasses, memoriesExpired, gossipTransfers;
    private static long nextDecay;
    private const int MaxMemoriesPerNpc = 256;
    private const int MaxAppliedEvents = 4096;

    private static string Key(RebirthNpcStableId npc, string counterparty)
    {
        return npc.ToString() + "|" + (counterparty ?? string.Empty).Trim().ToLowerInvariant();
    }

    public static bool RecordGameplayOutcome(string source, string actorId, RebirthNpcStableId npcId, string detail, long magnitude)
    {
        if (npcId.IsEmpty || string.IsNullOrWhiteSpace(actorId))
            return false;

        string normalized = (source ?? string.Empty).Trim().ToLowerInvariant();
        RebirthNpcSocialEventKind kind;
        switch (normalized)
        {
            case "trade": kind = RebirthNpcSocialEventKind.Trade; break;
            case "theft": kind = RebirthNpcSocialEventKind.Theft; break;
            case "propertydamage": kind = RebirthNpcSocialEventKind.PropertyDamage; break;
            case "healing": kind = RebirthNpcSocialEventKind.Healing; break;
            default: kind = RebirthNpcSocialEventKind.Assistance; break;
        }

        float importance = Math.Max(0.05f, Math.Min(1f, Math.Abs(magnitude) / 100f));
        return ApplyEvent(Guid.NewGuid(), npcId, actorId.Trim(), kind, importance, 1f);
    }

    public static bool ApplyEvent(Guid eventId, RebirthNpcStableId subject, string counterparty,
        RebirthNpcSocialEventKind kind, float importance, float confidence)
    { bool newlyApplied;return TryApplyEvent(eventId,subject,counterparty,kind,importance,confidence,out newlyApplied); }

    public static bool TryApplyEvent(Guid eventId, RebirthNpcStableId subject, string counterparty,
        RebirthNpcSocialEventKind kind, float importance, float confidence,out bool newlyApplied)
    {
        newlyApplied=false;if (eventId == Guid.Empty || subject.IsEmpty || string.IsNullOrEmpty(counterparty)) return false;
        importance = Math.Max(0f, Math.Min(1f, importance));
        confidence = Math.Max(0f, Math.Min(1f, confidence));
        lock (Sync)
        {
            if (!AppliedEvents.Add(eventId)) { Interlocked.Increment(ref replays); return true; }
            newlyApplied=true;
            AppliedEventOrder.Enqueue(eventId);
            while (AppliedEvents.Count > MaxAppliedEvents && AppliedEventOrder.Count > 0)
                AppliedEvents.Remove(AppliedEventOrder.Dequeue());
            RebirthNpcRelationship r;
            string key = Key(subject, counterparty);
            if (!Relationships.TryGetValue(key, out r)) Relationships[key] = r = new RebirthNpcRelationship();
            float v = Valence(kind) * importance * confidence;
            r.Familiarity = Clamp01(r.Familiarity + 0.03f * importance);
            if (v >= 0f)
            {
                r.Trust = Clamp01(r.Trust + v * 0.18f);
                r.Respect = Clamp01(r.Respect + v * 0.12f);
                r.Gratitude = Clamp01(r.Gratitude + v * 0.2f);
                r.Hostility = Clamp01(r.Hostility - v * 0.15f);
                r.Suspicion = Clamp01(r.Suspicion - v * 0.1f);
            }
            else
            {
                float h = -v;
                r.Hostility = Clamp01(r.Hostility + h * 0.25f);
                r.Suspicion = Clamp01(r.Suspicion + h * 0.2f);
                r.Fear = Clamp01(r.Fear + h * 0.12f);
                r.Trust = Clamp01(r.Trust - h * 0.22f);
                r.Loyalty = Clamp01(r.Loyalty - h * 0.1f);
            }
            r.Revision++;
            r.LastChangedUtcTicks = DateTime.UtcNow.Ticks;
            List<RebirthNpcSocialMemory> list;
            if (!Memories.TryGetValue(subject, out list)) Memories[subject] = list = new List<RebirthNpcSocialMemory>();
            list.Add(new RebirthNpcSocialMemory {
                EventId=eventId, Subject=subject, Counterparty=counterparty, Kind=kind,
                Importance=importance, Valence=v, Confidence=confidence,
                CreatedUtcTicks=DateTime.UtcNow.Ticks, LastReinforcedUtcTicks=DateTime.UtcNow.Ticks, LastDecayUtcTicks=DateTime.UtcNow.Ticks
            });
            Trim(list);
            Interlocked.Increment(ref eventsApplied);
            RebirthNpcReplicationPerformanceService.QueueRelationship(eventId, subject.ToString(), counterparty, (int)Math.Round(v*1000f), kind.ToString());
            PublishSignals(subject, r);
            return true;
        }
    }

    public static bool ShareMemory(RebirthNpcStableId source, RebirthNpcStableId recipient, Guid eventId, float fidelity)
    {
        fidelity = Clamp01(fidelity);
        lock (Sync)
        {
            List<RebirthNpcSocialMemory> sourceList;
            if (!Memories.TryGetValue(source, out sourceList)) return false;
            RebirthNpcSocialMemory found = sourceList.Find(delegate(RebirthNpcSocialMemory m) { return m.EventId == eventId; });
            if (found == null) return false;
            Guid derived = DeterministicGuid(eventId, recipient.ToString());
            bool ok = ApplyEvent(derived, recipient, found.Counterparty, found.Kind,
                found.Importance * 0.8f, found.Confidence * fidelity);
            if (ok) Interlocked.Increment(ref gossipTransfers);
            return ok;
        }
    }

    public static RebirthNpcRelationship GetRelationship(RebirthNpcStableId npc, string counterparty)
    {
        lock (Sync)
        {
            RebirthNpcRelationship r;
            return Relationships.TryGetValue(Key(npc,counterparty), out r) ? CloneRelationship(r) : null;
        }
    }

    public static void Tick()
    {
        if (ConnectionManager.Instance != null && !ConnectionManager.Instance.IsServer) return;
        long now = DateTime.UtcNow.Ticks;
        if (now < nextDecay) return;
        nextDecay = now + TimeSpan.FromMinutes(1).Ticks;
        lock (Sync)
        {
            foreach (List<RebirthNpcSocialMemory> list in Memories.Values)
            {
                for (int i=list.Count-1;i>=0;i--)
                {
                    RebirthNpcSocialMemory m=list[i];
                    if (m.Importance >= 0.9f) continue;
                    long from=m.LastDecayUtcTicks>0?m.LastDecayUtcTicks:m.LastReinforcedUtcTicks;if(from<=0)from=m.CreatedUtcTicks;
                    double days = new TimeSpan(Math.Max(0,now-from)).TotalDays;
                    m.Confidence = Clamp01(m.Confidence - (float)(days * 0.01));m.LastDecayUtcTicks=now;
                    if (m.Confidence <= 0.02f && m.Importance < 0.25f)
                    { list.RemoveAt(i); Interlocked.Increment(ref memoriesExpired); }
                }
            }
        }
        RebirthNpcReplicationPerformanceService.FlushRelationships(256);
        Interlocked.Increment(ref decayPasses);
    }

    private static void PublishSignals(RebirthNpcStableId npc, RebirthNpcRelationship r)
    {
        RebirthNpcDecisionEngine.SubmitSignal(npc, "social.hostility", r.Hostility);
        RebirthNpcDecisionEngine.SubmitSignal(npc, "social.trust", r.Trust);
        RebirthNpcDecisionEngine.SubmitSignal(npc, "social.fear", r.Fear);
    }

    private static float Valence(RebirthNpcSocialEventKind kind)
    {
        switch(kind)
        {
            case RebirthNpcSocialEventKind.Theft:
            case RebirthNpcSocialEventKind.Assault:
            case RebirthNpcSocialEventKind.Murder:
            case RebirthNpcSocialEventKind.PropertyDamage:
            case RebirthNpcSocialEventKind.PromiseBroken: return -1f;
            case RebirthNpcSocialEventKind.Trespass: return -0.4f;
            default: return 1f;
        }
    }
    private static float Clamp01(float v) { return v<0f?0f:(v>1f?1f:v); }
    private static void Trim(List<RebirthNpcSocialMemory> list)
    {
        if (list.Count <= MaxMemoriesPerNpc) return;
        list.Sort(delegate(RebirthNpcSocialMemory a, RebirthNpcSocialMemory b) {
            int c=a.Importance.CompareTo(b.Importance); return c!=0?c:a.CreatedUtcTicks.CompareTo(b.CreatedUtcTicks);
        });
        while(list.Count>MaxMemoriesPerNpc) list.RemoveAt(0);
    }
    private static Guid DeterministicGuid(Guid source, string salt)
    {
        byte[] a=source.ToByteArray(); byte[] b=Encoding.UTF8.GetBytes(salt??string.Empty);
        for(int i=0;i<b.Length;i++) a[i%16]=(byte)(a[i%16]^b[i]);
        return new Guid(a);
    }

    public static RebirthNpcSocialSnapshot GetSnapshot()
    {
        lock (Sync)
        {
            int memories = 0;
            foreach (List<RebirthNpcSocialMemory> list in Memories.Values) memories += list.Count;
            return new RebirthNpcSocialSnapshot
            {
                RelationshipCount = Relationships.Count, MemoryOwnerCount = Memories.Count,
                MemoryCount = memories, AppliedEventCount = AppliedEvents.Count,
                MaxMemoriesPerNpc = MaxMemoriesPerNpc, MaxAppliedEvents = MaxAppliedEvents
            };
        }
    }

    public static RebirthNpcRelationshipSnapshot[] GetRelationshipSnapshots()
    {
        lock (Sync)
        {
            List<RebirthNpcRelationshipSnapshot> result = new List<RebirthNpcRelationshipSnapshot>();
            foreach (KeyValuePair<string, RebirthNpcRelationship> pair in Relationships)
            {
                RebirthNpcRelationship r = pair.Value;
                result.Add(new RebirthNpcRelationshipSnapshot
                {
                    Key = pair.Key, Familiarity = r.Familiarity, Trust = r.Trust, Respect = r.Respect,
                    Fear = r.Fear, Gratitude = r.Gratitude, Suspicion = r.Suspicion, Loyalty = r.Loyalty,
                    Hostility = r.Hostility, Revision = r.Revision, LastChangedUtcTicks = r.LastChangedUtcTicks
                });
            }
            return result.ToArray();
        }
    }


    public static RebirthNpcSocialMemorySnapshot[] GetMemorySnapshots()
    {
        lock (Sync)
        {
            List<RebirthNpcSocialMemorySnapshot> result = new List<RebirthNpcSocialMemorySnapshot>();
            foreach (List<RebirthNpcSocialMemory> list in Memories.Values)
                for (int i=0;i<list.Count;i++) { RebirthNpcSocialMemory m=list[i]; result.Add(new RebirthNpcSocialMemorySnapshot{EventId=m.EventId,Subject=m.Subject,Counterparty=m.Counterparty,Kind=m.Kind,Importance=m.Importance,Valence=m.Valence,Confidence=m.Confidence,CreatedUtcTicks=m.CreatedUtcTicks,LastReinforcedUtcTicks=m.LastReinforcedUtcTicks,LastDecayUtcTicks=m.LastDecayUtcTicks}); }
            return result.ToArray();
        }
    }

    public static Guid[] GetAppliedEventSnapshot(){lock(Sync)return new List<Guid>(AppliedEventOrder).ToArray();}

    public static void Restore(RebirthNpcRelationshipSnapshot[] relationships, RebirthNpcSocialMemorySnapshot[] memories, Guid[] applied)
    {
        lock(Sync)
        {
            Relationships.Clear(); Memories.Clear(); AppliedEvents.Clear(); AppliedEventOrder.Clear();
            if(relationships!=null) for(int i=0;i<relationships.Length;i++){RebirthNpcRelationshipSnapshot x=relationships[i];if(x==null||string.IsNullOrWhiteSpace(x.Key))continue;Relationships[x.Key]=new RebirthNpcRelationship{Familiarity=x.Familiarity,Trust=x.Trust,Respect=x.Respect,Fear=x.Fear,Gratitude=x.Gratitude,Suspicion=x.Suspicion,Loyalty=x.Loyalty,Hostility=x.Hostility,Revision=x.Revision,LastChangedUtcTicks=x.LastChangedUtcTicks};}
            if(memories!=null) for(int i=0;i<memories.Length;i++){RebirthNpcSocialMemorySnapshot x=memories[i];if(x==null||x.EventId==Guid.Empty||x.Subject.IsEmpty)continue;List<RebirthNpcSocialMemory> list;if(!Memories.TryGetValue(x.Subject,out list))Memories[x.Subject]=list=new List<RebirthNpcSocialMemory>();list.Add(new RebirthNpcSocialMemory{EventId=x.EventId,Subject=x.Subject,Counterparty=x.Counterparty,Kind=x.Kind,Importance=x.Importance,Valence=x.Valence,Confidence=x.Confidence,CreatedUtcTicks=x.CreatedUtcTicks,LastReinforcedUtcTicks=x.LastReinforcedUtcTicks,LastDecayUtcTicks=x.LastDecayUtcTicks>0?x.LastDecayUtcTicks:x.LastReinforcedUtcTicks});Trim(list);}
            if(applied!=null) for(int i=Math.Max(0,applied.Length-MaxAppliedEvents);i<applied.Length;i++)if(AppliedEvents.Add(applied[i]))AppliedEventOrder.Enqueue(applied[i]);
            foreach(KeyValuePair<string,RebirthNpcRelationship> pair in Relationships){int split=pair.Key.IndexOf('|');RebirthNpcStableId id;if(split>0&&RebirthNpcStableId.TryParse(pair.Key.Substring(0,split),out id))PublishSignals(id,pair.Value);}
        }
    }

    private static RebirthNpcRelationship CloneRelationship(RebirthNpcRelationship r){return r==null?null:new RebirthNpcRelationship{Familiarity=r.Familiarity,Trust=r.Trust,Respect=r.Respect,Fear=r.Fear,Gratitude=r.Gratitude,Suspicion=r.Suspicion,Loyalty=r.Loyalty,Hostility=r.Hostility,Revision=r.Revision,LastChangedUtcTicks=r.LastChangedUtcTicks};}

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Relationships.Clear(); Memories.Clear(); AppliedEvents.Clear(); AppliedEventOrder.Clear(); nextDecay = 0L;
        }
    }

    public static string GetReport()
    {
        int rel,mem=0,npcs;
        lock(Sync){rel=Relationships.Count;npcs=Memories.Count;foreach(List<RebirthNpcSocialMemory> l in Memories.Values)mem+=l.Count;}
        return new StringBuilder("[REBIRTH NPC Social]\n").Append("relationships=").Append(rel)
            .Append(" memoryOwners=").Append(npcs).Append(" memories=").Append(mem)
            .Append(" events=").Append(eventsApplied).Append(" replays=").Append(replays)
            .Append(" decayPasses=").Append(decayPasses).Append(" expired=").Append(memoriesExpired)
            .Append(" gossipTransfers=").Append(gossipTransfers)
            .Append(" appliedEventWindow=").Append(AppliedEvents.Count).Append("/").Append(MaxAppliedEvents).ToString();
    }
}


public sealed class RebirthNpcSocialSnapshot
{
    public int RelationshipCount, MemoryOwnerCount, MemoryCount, AppliedEventCount, MaxMemoriesPerNpc, MaxAppliedEvents;
}

public sealed class RebirthNpcRelationshipSnapshot
{
    public string Key;
    public float Familiarity, Trust, Respect, Fear, Gratitude, Suspicion, Loyalty, Hostility;
    public uint Revision;
    public long LastChangedUtcTicks;
}


public sealed class RebirthNpcSocialMemorySnapshot
{
    public Guid EventId; public RebirthNpcStableId Subject; public string Counterparty; public RebirthNpcSocialEventKind Kind;
    public float Importance, Valence, Confidence; public long CreatedUtcTicks, LastReinforcedUtcTicks, LastDecayUtcTicks;

}
