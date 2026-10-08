using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Character-owned evidence, separate from practical XP. Producers must already have
// acknowledged an original completed event in the same durable character snapshot.
public sealed class RebirthTheorySoloState
{
    public string CreationId;
    public readonly Dictionary<string,long> Streams=new Dictionary<string,long>(StringComparer.Ordinal);
    public readonly Dictionary<string,double> LastEvidence=new Dictionary<string,double>(StringComparer.Ordinal);
    public readonly Dictionary<string,double> LastSettlement=new Dictionary<string,double>(StringComparer.Ordinal);
    public readonly List<RebirthTheorySoloEvidence> Evidence=new List<RebirthTheorySoloEvidence>();
    public RebirthTheorySoloSession Session;
    public RebirthTheorySoloOriginalTaskLedger OriginalTasks;
    public RebirthTheorySoloTeachingRetirement TeachingOriginal;
    public RebirthTheorySoloLockpickLedger LockpickOriginal;
    public RebirthTheorySoloCombatLedger CombatOriginal;
    public string LastSettledId,LastCancelledId,CancelPendingId,CancelPendingSubject;
    public RebirthTheorySoloState Clone()
    {
        var copy=new RebirthTheorySoloState{CreationId=CreationId,LastSettledId=LastSettledId,LastCancelledId=LastCancelledId,CancelPendingId=CancelPendingId,CancelPendingSubject=CancelPendingSubject,Session=Session?.Clone(),OriginalTasks=OriginalTasks?.Clone(),TeachingOriginal=TeachingOriginal?.Clone(),LockpickOriginal=LockpickOriginal?.Clone(),CombatOriginal=CombatOriginal?.Clone()};
        foreach(var p in Streams)copy.Streams.Add(p.Key,p.Value);
        foreach(var p in LastEvidence)copy.LastEvidence.Add(p.Key,p.Value);
        foreach(var p in LastSettlement)copy.LastSettlement.Add(p.Key,p.Value);
        copy.Evidence.AddRange(Evidence.Select(e=>e.Clone()));return copy;
    }
    internal bool RecordAcknowledged(string subject,string family,string originalEvent,float difficulty,double activeSeconds)
    {
        if(!RebirthTheorySoloRegistry.TryGet(subject,out var rule)||rule.Family!=family||!Text(originalEvent,256)||
            !Finite(activeSeconds)||activeSeconds<0||!Finite(difficulty)||difficulty<0||difficulty>100)return false;
        // Original-event replay prevention belongs to the producer's permanent acknowledgement.
        // Active evidence also rejects accidental duplicate calls before its bounded compaction.
        if(Evidence.Any(e=>e.Subject==subject&&e.OriginalEvent==originalEvent))return false;
        if(LastEvidence.TryGetValue(subject,out var previous)&&activeSeconds-previous<rule.EventSpacing)return false;
        Streams.TryGetValue(subject,out var ordinal);if(ordinal==long.MaxValue)return false;
        var removable=Evidence.Where(e=>e.Subject==subject&&!Reserved(e.Ordinal,e.Subject)).OrderBy(e=>e.Ordinal).ToList();
        while(removable.Count>=16){Evidence.Remove(removable[0]);removable.RemoveAt(0);}
        if(Evidence.Count>=768)return false;
        Streams[subject]=++ordinal;LastEvidence[subject]=activeSeconds;
        Evidence.Add(new RebirthTheorySoloEvidence{Subject=subject,Family=family,Ordinal=ordinal,OriginalEvent=originalEvent,Difficulty=difficulty,ActiveSeconds=activeSeconds});return true;
    }
    internal bool Reserved(long ordinal,string subject)=>Session!=null&&Session.Subject==subject&&Session.Ordinals.Contains(ordinal);
    internal static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    internal static bool Text(string value,int length)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=length&&!value.Any(char.IsControl);
}
public sealed class RebirthTheorySoloEvidence
{
    public string Subject,Family,OriginalEvent;
    public long Ordinal;
    public float Difficulty;
    public double ActiveSeconds;
    public RebirthTheorySoloEvidence Clone()=>(RebirthTheorySoloEvidence)MemberwiseClone();
}
public sealed class RebirthTheorySoloSession
{
    public string Id,Subject;
    public float Elapsed,Duration;
    public double StartedActive;
    public bool Consumed;
    public readonly List<long> Ordinals=new List<long>();
    public RebirthTheorySoloSession Clone(){var s=new RebirthTheorySoloSession{Id=Id,Subject=Subject,Elapsed=Elapsed,Duration=Duration,StartedActive=StartedActive,Consumed=Consumed};s.Ordinals.AddRange(Ordinals);return s;}
}
