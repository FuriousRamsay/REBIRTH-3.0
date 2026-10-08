using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcCombatLifeState : byte { Healthy=0, Injured=1, Incapacitated=2, Rescued=3, Dead=4 }
public enum RebirthNpcInjurySeverity : byte { None=0, Minor=1, Moderate=2, Severe=3, Critical=4 }
public enum RebirthNpcTacticalOrderKind : byte { None=0, Engage=1, Defend=2, Retreat=3, Rescue=4, HoldFire=5 }

public sealed class RebirthNpcCombatStateRecord
{
    public RebirthNpcStableId NpcId;
    public RebirthNpcCombatLifeState LifeState;
    public RebirthNpcInjurySeverity InjurySeverity;
    public int InjuryPoints;
    public int BleedPoints;
    public long IncapacitatedUtcTicks;
    public long DeathFinalizedUtcTicks;
    public string SettlementId;
    public string LastCause;
    public uint Revision;
}

public sealed class RebirthNpcTacticalOrder
{
    public Guid OrderId;
    public RebirthNpcStableId NpcId;
    public RebirthNpcTacticalOrderKind Kind;
    public RebirthNpcStableId TargetNpcId;
    public Vector3 TargetPosition;
    public string IssuerKey;
    public long CreatedUtcTicks;
    public long ExpiresUtcTicks;
    public uint ExpectedRevision;
}

public sealed class RebirthNpcCombatMutationResult
{
    public bool Accepted;
    public bool StateChanged;
    public string Code;
    public string Detail;
    public uint Revision;
    public static RebirthNpcCombatMutationResult Reject(string code,string detail,uint revision){return new RebirthNpcCombatMutationResult{Accepted=false,StateChanged=false,Code=code,Detail=detail,Revision=revision};}
    public static RebirthNpcCombatMutationResult Accept(string code,string detail,uint revision,bool changed){return new RebirthNpcCombatMutationResult{Accepted=true,StateChanged=changed,Code=code,Detail=detail,Revision=revision};}
}

/// <summary>Server-authoritative WP16 combat, injury, incapacitation, rescue and death-finalization aggregate.</summary>
public static class RebirthNpcCombatEmergencyService
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcCombatStateRecord> States=new Dictionary<RebirthNpcStableId,RebirthNpcCombatStateRecord>();
    private static readonly Dictionary<Guid,RebirthNpcTacticalOrder> Orders=new Dictionary<Guid,RebirthNpcTacticalOrder>();
    private static readonly HashSet<string> FinalizedDeathTransactions=new HashSet<string>(StringComparer.Ordinal);
    private static long injuries,incapacitations,rescues,deaths,duplicateDeaths,ordersAccepted,ordersRejected,equipmentEffects,organizationConsequences;

    public static RebirthNpcCombatMutationResult ApplyDamage(RebirthNpcStableId npcId,int damage,string cause,string settlementId,uint expectedRevision)
    { return ApplyDamage(default(RebirthNpcStableId),npcId,damage,cause,settlementId,expectedRevision,RebirthNpcDamageChannel.Area); }

    public static RebirthNpcCombatMutationResult ApplyDamage(RebirthNpcStableId attackerId,RebirthNpcStableId npcId,int damage,string cause,string settlementId,uint expectedRevision,RebirthNpcDamageChannel channel)
    {
        string friendlyReason;
        if(!RebirthNpcFriendlyFirePolicy.CanDamage(attackerId,npcId,channel,out friendlyReason))
            return RebirthNpcCombatMutationResult.Reject(friendlyReason,"Damage denied by authoritative friendly-fire policy.",0);
        if(!IsServer())return RebirthNpcCombatMutationResult.Reject("not-authority","Combat mutation requires server authority.",0);
        if(npcId.IsEmpty||damage<=0)return RebirthNpcCombatMutationResult.Reject("invalid-request","NPC identity and positive damage are required.",0);
        lock(Sync)
        {
            RebirthNpcCombatStateRecord state=GetOrCreateNoLock(npcId,settlementId);
            if(state.LifeState==RebirthNpcCombatLifeState.Dead)return RebirthNpcCombatMutationResult.Reject("already-dead","Death is already finalized.",state.Revision);
            if(expectedRevision!=0&&expectedRevision!=state.Revision)return RebirthNpcCombatMutationResult.Reject("stale-revision","Combat state revision changed.",state.Revision);
            int progressed=RebirthNpcProgressionAiIntegration.EvaluateIncomingPhysicalDamage(npcId,damage,Guid.NewGuid());
            if(progressed<=0)return RebirthNpcCombatMutationResult.Accept("dodged","Dexterity progression avoided incoming physical damage.",state.Revision,false);
            int mitigated=ApplyEquipmentMitigationNoLock(npcId,progressed);
            state.InjuryPoints=Math.Min(1000,state.InjuryPoints+mitigated);
            state.BleedPoints=Math.Min(1000,state.BleedPoints+Math.Max(0,mitigated/4));
            state.InjurySeverity=Severity(state.InjuryPoints);
            state.LastCause=cause??string.Empty;
            if(state.InjurySeverity>=RebirthNpcInjurySeverity.Critical&&state.LifeState!=RebirthNpcCombatLifeState.Incapacitated)
            {state.LifeState=RebirthNpcCombatLifeState.Incapacitated;state.IncapacitatedUtcTicks=DateTime.UtcNow.Ticks;Interlocked.Increment(ref incapacitations);}
            else if(state.LifeState==RebirthNpcCombatLifeState.Healthy)state.LifeState=RebirthNpcCombatLifeState.Injured;
            state.Revision++;Interlocked.Increment(ref injuries);RebirthNpcCombatPersistenceStore.MarkDirty();
            return RebirthNpcCombatMutationResult.Accept("damage-applied","Authoritative injury state updated.",state.Revision,true);
        }
    }

    public static RebirthNpcCombatMutationResult SubmitTacticalOrder(RebirthNpcTacticalOrder order)
    {
        if(!IsServer()){Interlocked.Increment(ref ordersRejected);return RebirthNpcCombatMutationResult.Reject("not-authority","Tactical orders require server authority.",0);}
        if(order==null||order.NpcId.IsEmpty||order.Kind==RebirthNpcTacticalOrderKind.None||string.IsNullOrWhiteSpace(order.IssuerKey))
        {Interlocked.Increment(ref ordersRejected);return RebirthNpcCombatMutationResult.Reject("invalid-order","Order identity, kind and issuer are required.",0);}
        lock(Sync)
        {
            RebirthNpcCombatStateRecord state=GetOrCreateNoLock(order.NpcId,string.Empty);
            if(state.LifeState==RebirthNpcCombatLifeState.Dead||state.LifeState==RebirthNpcCombatLifeState.Incapacitated)
            {Interlocked.Increment(ref ordersRejected);return RebirthNpcCombatMutationResult.Reject("actor-unavailable","Dead or incapacitated NPCs cannot accept tactical orders.",state.Revision);}
            if(order.ExpectedRevision!=0&&order.ExpectedRevision!=state.Revision)
            {Interlocked.Increment(ref ordersRejected);return RebirthNpcCombatMutationResult.Reject("stale-revision","Combat state revision changed.",state.Revision);}
            if(order.OrderId==Guid.Empty)order.OrderId=Guid.NewGuid();
            order.CreatedUtcTicks=DateTime.UtcNow.Ticks;if(order.ExpiresUtcTicks<=order.CreatedUtcTicks)order.ExpiresUtcTicks=order.CreatedUtcTicks+TimeSpan.FromMinutes(2).Ticks;
            Orders[order.OrderId]=order;Interlocked.Increment(ref ordersAccepted);
            return RebirthNpcCombatMutationResult.Accept("order-accepted","Tactical order accepted.",state.Revision,true);
        }
    }

    public static RebirthNpcCombatMutationResult Rescue(RebirthNpcStableId patientId,RebirthNpcStableId rescuerId,string settlementId,uint expectedRevision)
    {
        if(!IsServer())return RebirthNpcCombatMutationResult.Reject("not-authority","Rescue requires server authority.",0);
        lock(Sync)
        {
            RebirthNpcCombatStateRecord state=GetOrCreateNoLock(patientId,settlementId);
            if(expectedRevision!=0&&expectedRevision!=state.Revision)return RebirthNpcCombatMutationResult.Reject("stale-revision","Combat state revision changed.",state.Revision);
            if(state.LifeState!=RebirthNpcCombatLifeState.Incapacitated)return RebirthNpcCombatMutationResult.Reject("not-incapacitated","Patient is not incapacitated.",state.Revision);
            if(rescuerId.IsEmpty||rescuerId==patientId)return RebirthNpcCombatMutationResult.Reject("invalid-rescuer","A distinct rescuer is required.",state.Revision);
            state.LifeState=RebirthNpcCombatLifeState.Rescued;state.InjuryPoints=Math.Min(state.InjuryPoints,700);state.BleedPoints=Math.Min(state.BleedPoints,250);state.InjurySeverity=Severity(state.InjuryPoints);state.Revision++;
            Interlocked.Increment(ref rescues);RecordOrganizationConsequenceNoLock(state.SettlementId,"rescue",patientId);RebirthNpcCombatPersistenceStore.MarkDirty();
            return RebirthNpcCombatMutationResult.Accept("rescued","Incapacitated NPC moved to rescued state.",state.Revision,true);
        }
    }

    public static RebirthNpcCombatMutationResult FinalizeDeath(RebirthNpcStableId npcId,string transactionId,string cause,string settlementId,uint expectedRevision)
    {
        if(!IsServer())return RebirthNpcCombatMutationResult.Reject("not-authority","Death finalization requires server authority.",0);
        if(string.IsNullOrWhiteSpace(transactionId))return RebirthNpcCombatMutationResult.Reject("missing-transaction","A stable death transaction id is required.",0);
        lock(Sync)
        {
            RebirthNpcCombatStateRecord state=GetOrCreateNoLock(npcId,settlementId);
            if(FinalizedDeathTransactions.Contains(transactionId)||state.LifeState==RebirthNpcCombatLifeState.Dead)
            {Interlocked.Increment(ref duplicateDeaths);return RebirthNpcCombatMutationResult.Accept("already-finalized","Death transaction was already finalized.",state.Revision,false);}
            if(expectedRevision!=0&&expectedRevision!=state.Revision)return RebirthNpcCombatMutationResult.Reject("stale-revision","Combat state revision changed.",state.Revision);
            FinalizedDeathTransactions.Add(transactionId);state.LifeState=RebirthNpcCombatLifeState.Dead;state.InjurySeverity=RebirthNpcInjurySeverity.Critical;state.DeathFinalizedUtcTicks=DateTime.UtcNow.Ticks;state.LastCause=cause??string.Empty;state.Revision++;
            CancelOrdersForNoLock(npcId);Interlocked.Increment(ref deaths);RecordOrganizationConsequenceNoLock(state.SettlementId,"death",npcId);RebirthNpcCombatPersistenceStore.MarkDirty();
            return RebirthNpcCombatMutationResult.Accept("death-finalized","Death finalized exactly once; inventory/reward disposition may proceed once.",state.Revision,true);
        }
    }

    public static bool TryGetState(RebirthNpcStableId npcId,out RebirthNpcCombatStateRecord state)
    {lock(Sync){RebirthNpcCombatStateRecord s;if(!States.TryGetValue(npcId,out s)){state=null;return false;}state=Clone(s);return true;}}
    public static RebirthNpcCombatStateRecord[] CaptureStates(){lock(Sync){RebirthNpcCombatStateRecord[] a=new RebirthNpcCombatStateRecord[States.Count];int i=0;foreach(RebirthNpcCombatStateRecord s in States.Values)a[i++]=Clone(s);Array.Sort(a,delegate(RebirthNpcCombatStateRecord x,RebirthNpcCombatStateRecord y){return string.CompareOrdinal(x.NpcId.ToString(),y.NpcId.ToString());});return a;}}
    public static string[] CaptureFinalizedTransactions(){lock(Sync){string[] a=new string[FinalizedDeathTransactions.Count];FinalizedDeathTransactions.CopyTo(a);Array.Sort(a,StringComparer.Ordinal);return a;}}
    public static void Restore(RebirthNpcCombatStateRecord[] states,string[] transactions){lock(Sync){States.Clear();Orders.Clear();FinalizedDeathTransactions.Clear();if(states!=null)foreach(RebirthNpcCombatStateRecord s in states)if(s!=null&&!s.NpcId.IsEmpty)States[s.NpcId]=Clone(s);if(transactions!=null)foreach(string t in transactions)if(!string.IsNullOrWhiteSpace(t))FinalizedDeathTransactions.Add(t);}}
    public static void ResetForWorldChange(){lock(Sync){States.Clear();Orders.Clear();FinalizedDeathTransactions.Clear();}}
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC WP16 Combat] states="+States.Count+" orders="+Orders.Count+" finalizedDeaths="+FinalizedDeathTransactions.Count+" injuries="+Interlocked.Read(ref injuries)+" incapacitations="+Interlocked.Read(ref incapacitations)+" rescues="+Interlocked.Read(ref rescues)+" deaths="+Interlocked.Read(ref deaths)+" duplicateDeaths="+Interlocked.Read(ref duplicateDeaths)+" ordersAccepted="+Interlocked.Read(ref ordersAccepted)+" ordersRejected="+Interlocked.Read(ref ordersRejected)+" equipmentEffects="+Interlocked.Read(ref equipmentEffects)+" organizationConsequences="+Interlocked.Read(ref organizationConsequences);}

    private static RebirthNpcCombatStateRecord GetOrCreateNoLock(RebirthNpcStableId id,string settlement){RebirthNpcCombatStateRecord s;if(!States.TryGetValue(id,out s)){s=new RebirthNpcCombatStateRecord{NpcId=id,LifeState=RebirthNpcCombatLifeState.Healthy,SettlementId=settlement??string.Empty,Revision=1};States.Add(id,s);}else if(string.IsNullOrEmpty(s.SettlementId)&&!string.IsNullOrEmpty(settlement))s.SettlementId=settlement;return s;}
    private static int ApplyEquipmentMitigationNoLock(RebirthNpcStableId id,int damage){Interlocked.Increment(ref equipmentEffects);return Math.Max(1,damage);}
    private static RebirthNpcInjurySeverity Severity(int points){if(points<=0)return RebirthNpcInjurySeverity.None;if(points<150)return RebirthNpcInjurySeverity.Minor;if(points<350)return RebirthNpcInjurySeverity.Moderate;if(points<650)return RebirthNpcInjurySeverity.Severe;return RebirthNpcInjurySeverity.Critical;}
    private static void CancelOrdersForNoLock(RebirthNpcStableId id){List<Guid> remove=new List<Guid>();foreach(KeyValuePair<Guid,RebirthNpcTacticalOrder> p in Orders)if(p.Value.NpcId==id)remove.Add(p.Key);foreach(Guid g in remove)Orders.Remove(g);}
    private static void RecordOrganizationConsequenceNoLock(string settlement,string kind,RebirthNpcStableId id){if(string.IsNullOrEmpty(settlement))return;Interlocked.Increment(ref organizationConsequences);{ if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC WP16] settlement="+settlement+" consequence="+kind+" npc="+id); }}
    private static RebirthNpcCombatStateRecord Clone(RebirthNpcCombatStateRecord s){return new RebirthNpcCombatStateRecord{NpcId=s.NpcId,LifeState=s.LifeState,InjurySeverity=s.InjurySeverity,InjuryPoints=s.InjuryPoints,BleedPoints=s.BleedPoints,IncapacitatedUtcTicks=s.IncapacitatedUtcTicks,DeathFinalizedUtcTicks=s.DeathFinalizedUtcTicks,SettlementId=s.SettlementId,LastCause=s.LastCause,Revision=s.Revision};}
    private static bool IsServer(){return SingletonMonoBehaviour<ConnectionManager>.Instance!=null&&SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;}
}
