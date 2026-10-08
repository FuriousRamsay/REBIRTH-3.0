using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcWorkProgressionSink : IRebirthNpcWorkProgressionEventSink
{
    public int Priority { get { return 100; } }
    public void Handle(RebirthNpcProgressionEvent e)
    {
        if(e==null || e.EventId==Guid.Empty || e.NpcId.IsEmpty) return;
        string track="profession.general"; long xp=0;
        switch(e.Kind){
            case RebirthNpcProgressionEventKind.WorkCompleted: xp=100; break;
            case RebirthNpcProgressionEventKind.ResourceProduced: xp=Math.Max(1,Math.Min(250,e.Quantity)); break;
            case RebirthNpcProgressionEventKind.ResourceDelivered: xp=Math.Max(1,Math.Min(150,e.Quantity)); break;
            case RebirthNpcProgressionEventKind.RepairCompleted: xp=75; break;
            case RebirthNpcProgressionEventKind.CraftingCompleted: xp=75; break;
            case RebirthNpcProgressionEventKind.FarmingCompleted: xp=75; break;
            case RebirthNpcProgressionEventKind.CapabilityUsed: xp=25; break;
            case RebirthNpcProgressionEventKind.SettlementContribution: xp=25; break;
            default: return;
        }
        RebirthNpcProgressionService.Award(new RebirthNpcProgressionAwardRequest{AwardId=e.EventId,NpcId=e.NpcId,ProgressionId=track,RequestedXp=xp,SourceType="work."+e.Kind,SourceId=e.OutcomeId.ToString("N"),CreatedUtcTicks=e.CreatedUtcTicks});
    }
}

public static class RebirthNpcProgressionService
{
    private const int MaxReplayIds=8192; private static readonly object Sync=new object();
    private static readonly Dictionary<string,RebirthNpcProgressionDefinition> Definitions=new Dictionary<string,RebirthNpcProgressionDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcProgressionRecord> Records=new Dictionary<RebirthNpcStableId,RebirthNpcProgressionRecord>();
    private static readonly HashSet<Guid> ReplayIds=new HashSet<Guid>(); private static readonly Queue<Guid> ReplayOrder=new Queue<Guid>();
    private static readonly RebirthNpcWorkProgressionSink WorkSink=new RebirthNpcWorkProgressionSink();
    private static bool initialized; private static long accepted,rejected,duplicates,multiLevel,atMaximum;
    public static void EnsureInitialized(){lock(Sync){if(initialized)return;RegisterDefinitionNoLock(new RebirthNpcProgressionDefinition("profession.general",RebirthNpcProgressionTrackKind.Profession));RebirthNpcWorkOutcomeService.RegisterSink(WorkSink);initialized=true;}RebirthNpcProgressionPersistenceStore.EnsureLoaded();}
    public static void RegisterDefinition(RebirthNpcProgressionDefinition d){if(d==null)throw new ArgumentNullException(nameof(d));lock(Sync)RegisterDefinitionNoLock(d);}
    private static void RegisterDefinitionNoLock(RebirthNpcProgressionDefinition d){Definitions[d.Id]=d;}
    public static RebirthNpcProgressionAwardResult Award(RebirthNpcProgressionAwardRequest q)
    {
        EnsureInitialized();
        if(!IsAuthoritative()){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"not-authority","Progression mutation requires authoritative server execution.",q==null?0:q.RequestedXp);}
        if(q==null || q.AwardId==Guid.Empty || q.NpcId.IsEmpty || string.IsNullOrWhiteSpace(q.ProgressionId)){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"invalid-request","Award id, NPC id and progression id are required.",q==null?0:q.RequestedXp);}
        if(q.RequestedXp<=0){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"invalid-xp","XP must be positive.",q.RequestedXp);}
        if(string.IsNullOrWhiteSpace(q.SourceType)||string.IsNullOrWhiteSpace(q.SourceId)){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"invalid-source","A validated source type and source id are required.",q.RequestedXp);}
        lock(Sync)
        {
            RebirthNpcProgressionDefinition d;if(!Definitions.TryGetValue(q.ProgressionId,out d)){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"unknown-definition","Unknown progression definition.",q.RequestedXp);}
            if(q.RequestedXp>d.MaximumAward && !q.IsAdministrative){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"award-bound","Award exceeds the definition maximum.",q.RequestedXp);}
            if(ReplayIds.Contains(q.AwardId)){Interlocked.Increment(ref duplicates);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Duplicate,"duplicate-award","Award id has already been committed.",q.RequestedXp);}
            RebirthNpcProgressionRecord r;if(!Records.TryGetValue(q.NpcId,out r)){r=new RebirthNpcProgressionRecord{NpcId=q.NpcId};Records.Add(q.NpcId,r);}
            RebirthNpcProgressionEntryRecord e;if(!r.Entries.TryGetValue(d.Id,out e)){e=new RebirthNpcProgressionEntryRecord{ProgressionId=d.Id,CachedLevel=1,AppliedCurveVersion=d.CurveVersion};r.Entries.Add(d.Id,e);}
            long max=d.XpFloor(d.MaximumLevel), remaining=Math.Max(0,max-e.CumulativeXp); long grant=Math.Min(q.RequestedXp,remaining);
            if(grant<=0){Interlocked.Increment(ref atMaximum);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.AtMaximum,"maximum-level","Progression entry is already at maximum level.",q.RequestedXp);}
            long oldXp=e.CumulativeXp;int oldLevel=d.ResolveLevel(oldXp);long newXp;try{newXp=checked(oldXp+grant);}catch(OverflowException){Interlocked.Increment(ref rejected);return RebirthNpcProgressionAwardResult.Reject(RebirthNpcProgressionAwardStatus.Rejected,"overflow","Award would overflow cumulative XP.",q.RequestedXp);}
            int newLevel=d.ResolveLevel(newXp);e.CumulativeXp=newXp;e.CachedLevel=newLevel;e.AppliedCurveVersion=d.CurveVersion;e.EntryRevision++;e.LastAwardId=q.AwardId;e.LastAwardUtcTicks=q.CreatedUtcTicks>0?q.CreatedUtcTicks:DateTime.UtcNow.Ticks;r.ComponentRevision++;
            ReplayIds.Add(q.AwardId);ReplayOrder.Enqueue(q.AwardId);while(ReplayOrder.Count>MaxReplayIds)ReplayIds.Remove(ReplayOrder.Dequeue());
            RebirthNpcProgressionProjectionRevisionService.Bump(q.NpcId);
            RebirthNpcProgressionPersistenceStore.MarkDirty();Interlocked.Increment(ref accepted);if(newLevel-oldLevel>1)Interlocked.Increment(ref multiLevel);
            RebirthNpcProgressionAwardResult result=new RebirthNpcProgressionAwardResult{Status=RebirthNpcProgressionAwardStatus.Accepted,Code="accepted",Message="Award committed.",RequestedXp=q.RequestedXp,AcceptedXp=grant,OldXp=oldXp,NewXp=newXp,OldLevel=oldLevel,NewLevel=newLevel,CrossedLevels=newLevel-oldLevel,ComponentRevision=r.ComponentRevision,EntryRevision=e.EntryRevision};
            RebirthNpcProgressionTelemetry.Award(result,q.ProgressionId);
            RebirthNpcProgressionReplicationService.PublishDelta(q.NpcId);
            return result;
        }
    }
    public static bool TryGetView(RebirthNpcStableId npcId,string id,out RebirthNpcProgressionView v){EnsureInitialized();lock(Sync){RebirthNpcProgressionRecord r;RebirthNpcProgressionEntryRecord e;RebirthNpcProgressionDefinition d;if(!Records.TryGetValue(npcId,out r)||!r.Entries.TryGetValue(id,out e)||!Definitions.TryGetValue(id,out d)){v=null;return false;}int level=d.ResolveLevel(e.CumulativeXp);long floor=d.XpFloor(level),next=level>=d.MaximumLevel?floor:d.XpFloor(level+1);v=new RebirthNpcProgressionView{NpcId=npcId,ProgressionId=id,CumulativeXp=e.CumulativeXp,Level=level,MaximumLevel=d.MaximumLevel,CurrentFloor=floor,NextFloor=next,XpToNext=level>=d.MaximumLevel?0:next-e.CumulativeXp,ProgressNumerator=level>=d.MaximumLevel?1:e.CumulativeXp-floor,ProgressDenominator=level>=d.MaximumLevel?1:next-floor,ComponentRevision=r.ComponentRevision,EntryRevision=e.EntryRevision};return true;}}
    public static RebirthNpcProgressionRecord[] ExportRecords(){EnsureInitialized();lock(Sync){var a=new RebirthNpcProgressionRecord[Records.Count];int i=0;foreach(var p in Records)a[i++]=p.Value.Clone();return a;}}
    public static bool TryExportRecord(RebirthNpcStableId id,out RebirthNpcProgressionRecord record){EnsureInitialized();lock(Sync){RebirthNpcProgressionRecord r;if(Records.TryGetValue(id,out r)){record=r.Clone();return true;}record=null;return false;}}
    public static Guid[] ExportReplayIds(){EnsureInitialized();lock(Sync)return ReplayOrder.ToArray();}
    public static void ImportRecords(IEnumerable<RebirthNpcProgressionRecord> records,bool replace){ImportRecords(records,null,replace);}
    public static void ImportRecords(IEnumerable<RebirthNpcProgressionRecord> records,IEnumerable<Guid> replayIds,bool replace){lock(Sync){if(replace){Records.Clear();ReplayIds.Clear();ReplayOrder.Clear();}if(records!=null)foreach(var source in records){if(source==null||source.NpcId.IsEmpty)continue;var r=source.Clone();foreach(var e in r.Entries.Values){RebirthNpcProgressionDefinition d;if(!Definitions.TryGetValue(e.ProgressionId,out d))continue;e.CumulativeXp=Math.Max(0,Math.Min(e.CumulativeXp,d.XpFloor(d.MaximumLevel)));e.CachedLevel=d.ResolveLevel(e.CumulativeXp);e.AppliedCurveVersion=d.CurveVersion;}Records[r.NpcId]=r;}if(replayIds!=null)foreach(Guid id in replayIds){if(id==Guid.Empty||!ReplayIds.Add(id))continue;ReplayOrder.Enqueue(id);while(ReplayOrder.Count>MaxReplayIds)ReplayIds.Remove(ReplayOrder.Dequeue());}}}
    public static bool RemoveRecordForRepair(RebirthNpcStableId id){lock(Sync){return Records.Remove(id);}}
    public static void ResetForWorldChange(){lock(Sync){Records.Clear();ReplayIds.Clear();ReplayOrder.Clear();}RebirthNpcProgressionProjectionRevisionService.Reset();RebirthNpcProgressionPersistenceStore.Reset(false);}
    public static string GetReport(){EnsureInitialized();lock(Sync){var b=new StringBuilder("[REBIRTH NPC Progression] definitions=").Append(Definitions.Count).Append(" npcs=").Append(Records.Count).Append(" accepted=").Append(Interlocked.Read(ref accepted)).Append(" rejected=").Append(Interlocked.Read(ref rejected)).Append(" duplicates=").Append(Interlocked.Read(ref duplicates)).Append(" multiLevel=").Append(Interlocked.Read(ref multiLevel)).Append(" atMaximum=").Append(Interlocked.Read(ref atMaximum));foreach(var r in Records.Values)foreach(var e in r.Entries.Values)b.AppendLine().Append("  ").Append(r.NpcId).Append(' ').Append(e.ProgressionId).Append(" xp=").Append(e.CumulativeXp).Append(" level=").Append(e.CachedLevel).Append(" revision=").Append(r.ComponentRevision).Append(':').Append(e.EntryRevision);return b.ToString();}}
    private static bool IsAuthoritative(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c==null||c.IsServer;}
}
