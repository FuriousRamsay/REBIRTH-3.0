using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcFactionStanding
{
    public string FactionId { get; internal set; }
    public string Counterparty { get; internal set; }
    public float Standing { get; internal set; }
    public uint Revision { get; internal set; }
    public long LastChangedUtcTicks { get; internal set; }
}

/// <summary>Bounded server-authoritative faction projection derived from social events.</summary>
public static partial class RebirthNpcFactionGameplayService
{
    private const int MaxStandings = 2048;
    private static readonly object Sync = new object();
    private static Dictionary<string, RebirthNpcFactionStanding> Standings = new Dictionary<string, RebirthNpcFactionStanding>(StringComparer.OrdinalIgnoreCase);
    private static LinkedList<string> Order = new LinkedList<string>();
    private static Dictionary<string, LinkedListNode<string>> OrderNodes = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
    private static object recoveryEpoch=new object();internal static object RecoverySync=>Sync;internal static object RecoveryEpoch=>System.Threading.Volatile.Read(ref recoveryEpoch);
    private static long applied, rejected, evicted;

    public static bool Apply(string factionId, string counterparty, RebirthNpcSocialEventKind kind, float importance)
    {
        if (string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(counterparty)) { Interlocked.Increment(ref rejected); return false; }
        float delta = GetDelta(kind) * Math.Max(0f, Math.Min(1f, importance));
        string key = factionId.Trim().ToLowerInvariant() + "|" + counterparty.Trim().ToLowerInvariant();
        lock (Sync)
        {
            recoveryEpoch=new object();RebirthNpcSocialPersistenceStore.ObserveOwnedMutation();RebirthNpcFactionStanding standing;
            if (!Standings.TryGetValue(key, out standing))
            {
                standing = new RebirthNpcFactionStanding { FactionId = factionId.Trim(), Counterparty = counterparty.Trim() };
                Standings[key] = standing; Touch(key);
            }
            standing.Standing = Math.Max(-1f, Math.Min(1f, standing.Standing + delta));
            standing.Revision++; standing.LastChangedUtcTicks = DateTime.UtcNow.Ticks; Touch(key);
            while (Standings.Count > MaxStandings && Order.Count > 0) { string oldest=Order.First.Value; Order.RemoveFirst(); OrderNodes.Remove(oldest); if (Standings.Remove(oldest)) Interlocked.Increment(ref evicted); }
        }
        Interlocked.Increment(ref applied); return true;
    }

    public static RebirthNpcFactionStanding[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcFactionStanding[] result = new RebirthNpcFactionStanding[Standings.Count]; int i = 0;
            foreach (RebirthNpcFactionStanding value in Standings.Values)
                result[i++] = new RebirthNpcFactionStanding { FactionId=value.FactionId, Counterparty=value.Counterparty, Standing=value.Standing, Revision=value.Revision, LastChangedUtcTicks=value.LastChangedUtcTicks };
            return result;
        }
    }

    internal static bool TryPrepareRecovery(RebirthNpcFactionStanding[] rows,out RebirthNpcPreparedRecovery candidate,out string error)
    {
        candidate=null;error=string.Empty;if(rows==null||rows.Length>MaxStandings){error="faction recovery exceeds supported bound";return false;}
        object expected;lock(Sync)expected=recoveryEpoch;
        var next=new Dictionary<string,RebirthNpcFactionStanding>(StringComparer.OrdinalIgnoreCase);var order=new LinkedList<string>();var nodes=new Dictionary<string,LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
        var sorted=(RebirthNpcFactionStanding[])rows.Clone();Array.Sort(sorted,(a,b)=>(a?.LastChangedUtcTicks??0).CompareTo(b?.LastChangedUtcTicks??0));
        foreach(var row in sorted)
        {
            if(row==null||string.IsNullOrWhiteSpace(row.FactionId)||string.IsNullOrWhiteSpace(row.Counterparty)||float.IsNaN(row.Standing)||float.IsInfinity(row.Standing)){error="invalid faction recovery row";return false;}
            string key=row.FactionId.Trim().ToLowerInvariant()+"|"+row.Counterparty.Trim().ToLowerInvariant();if(next.ContainsKey(key)){error="duplicate faction recovery row";return false;}
            next.Add(key,new RebirthNpcFactionStanding{FactionId=row.FactionId,Counterparty=row.Counterparty,Standing=row.Standing,Revision=row.Revision,LastChangedUtcTicks=row.LastChangedUtcTicks});nodes.Add(key,order.AddLast(key));
        }
        Func<bool> current=()=>{lock(Sync)return ReferenceEquals(expected,recoveryEpoch);};candidate=new RebirthNpcPreparedRecovery("RebirthNpcSocialState.xml",current,()=>{lock(Sync){if(!current())return false;Standings=next;Order=order;OrderNodes=nodes;recoveryEpoch=new object();RebirthNpcSocialPersistenceStore.ObserveOwnedMutation();return true;}});return true;
    }    public static void Restore(RebirthNpcFactionStanding[] values){lock(Sync){recoveryEpoch=new object();RebirthNpcSocialPersistenceStore.ObserveOwnedMutation();Standings.Clear();Order.Clear();OrderNodes.Clear();if(values==null)return;Array.Sort(values,delegate(RebirthNpcFactionStanding a,RebirthNpcFactionStanding b){return a.LastChangedUtcTicks.CompareTo(b.LastChangedUtcTicks);});for(int i=0;i<values.Length;i++){RebirthNpcFactionStanding v=values[i];if(v==null||string.IsNullOrWhiteSpace(v.FactionId)||string.IsNullOrWhiteSpace(v.Counterparty))continue;string key=v.FactionId.Trim().ToLowerInvariant()+"|"+v.Counterparty.Trim().ToLowerInvariant();Standings[key]=new RebirthNpcFactionStanding{FactionId=v.FactionId,Counterparty=v.Counterparty,Standing=v.Standing,Revision=v.Revision,LastChangedUtcTicks=v.LastChangedUtcTicks};Touch(key);}while(Standings.Count>MaxStandings&&Order.Count>0){string oldest=Order.First.Value;Order.RemoveFirst();OrderNodes.Remove(oldest);Standings.Remove(oldest);}}}
    public static void ResetForWorldChange() { lock (Sync) { recoveryEpoch=new object();RebirthNpcSocialPersistenceStore.ObserveOwnedMutation();Standings.Clear(); Order.Clear(); OrderNodes.Clear(); } }
    public static string GetReport() { lock (Sync) return "[REBIRTH NPC Faction] standings="+Standings.Count+"/"+MaxStandings+" applied="+Interlocked.Read(ref applied)+" rejected="+Interlocked.Read(ref rejected)+" evicted="+Interlocked.Read(ref evicted); }
    private static void Touch(string key){LinkedListNode<string> node;if(OrderNodes.TryGetValue(key,out node))Order.Remove(node);node=Order.AddLast(key);OrderNodes[key]=node;}
    private static float GetDelta(RebirthNpcSocialEventKind kind)
    {
        switch (kind)
        {
            case RebirthNpcSocialEventKind.Murder: return -0.35f;
            case RebirthNpcSocialEventKind.Assault: return -0.18f;
            case RebirthNpcSocialEventKind.Theft: return -0.10f;
            case RebirthNpcSocialEventKind.PropertyDamage: return -0.08f;
            case RebirthNpcSocialEventKind.PromiseBroken: return -0.12f;
            case RebirthNpcSocialEventKind.Healing: return 0.12f;
            case RebirthNpcSocialEventKind.CombatSupport: return 0.10f;
            case RebirthNpcSocialEventKind.Assistance: return 0.08f;
            case RebirthNpcSocialEventKind.PromiseKept: return 0.10f;
            case RebirthNpcSocialEventKind.Trade: return 0.03f;
            default: return 0.01f;
        }
    }
}

public static class RebirthNpcSocialGameplayGateway
{
    public static bool Publish(Guid eventId, RebirthNpcStableId npcId, string actorId, string factionId, RebirthNpcSocialEventKind kind, float importance, float confidence)
    {
        bool newlyApplied;bool relationship = RebirthNpcSocialService.TryApplyEvent(eventId,npcId,actorId,kind,importance,confidence,out newlyApplied);
        if(!relationship)return false;if(!newlyApplied)return true;
        bool faction = string.IsNullOrWhiteSpace(factionId) || RebirthNpcFactionGameplayService.Apply(factionId, actorId, kind, importance * confidence);
        return faction;
    }

    public static void PublishExternalOutcome(string source, string actorId, RebirthNpcStableId npcId, string detail, long magnitude)
    {
        RebirthNpcSocialService.RecordGameplayOutcome(source, actorId, npcId, detail, magnitude);
    }
}
