
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcNeedKind : byte { Hunger, Thirst, Fatigue, Warmth, Safety, Morale, Social, Medical }
public enum RebirthNpcWorkState : byte { Available, Claimed, Running, Completed, Failed, Cancelled }
public enum RebirthNpcResourceReservationState : byte { Reserved, Consumed }

public sealed class RebirthNpcNeedState
{
    public readonly Dictionary<RebirthNpcNeedKind,float> Values =
        new Dictionary<RebirthNpcNeedKind,float>();
    public long LastUpdateUtcTicks;
}

public sealed class RebirthNpcProfessionRecord
{
    public string ProfessionId = "unassigned";
    public readonly Dictionary<string,float> Skills =
        new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public string WorkplaceId = string.Empty;
    public string SettlementId = string.Empty;
    public uint Revision;
}

public sealed class RebirthNpcSettlementPersistentState
{
    public RebirthNpcNeedPersistentRecord[] Needs;
    public RebirthNpcProfessionPersistentRecord[] Professions;
    public RebirthNpcWorkOrder[] WorkOrders;
    public RebirthNpcSettlementResourcePersistentRecord[] Resources;
    public RebirthNpcResourceReservationPersistentRecord[] ResourceReservations;
}

public sealed class RebirthNpcSettlementResourcePersistentRecord
{
    public string SettlementId;
    public string ResourceKey;
    public long Quantity;
    public uint Revision;
}

public sealed class RebirthNpcResourceReservationPersistentRecord
{
    public Guid WorkId;
    public string SettlementId;
    public string ResourceKey;
    public int Quantity;
    public RebirthNpcResourceReservationState State;
    public long CreatedUtcTicks;
}

public sealed class RebirthNpcNeedPersistentRecord
{
    public RebirthNpcStableId NpcId;
    public long LastUpdateUtcTicks;
    public Dictionary<RebirthNpcNeedKind,float> Values;
}

public sealed class RebirthNpcProfessionPersistentRecord
{
    public RebirthNpcStableId NpcId;
    public string ProfessionId;
    public string WorkplaceId;
    public string SettlementId;
    public uint Revision;
    public Dictionary<string,float> Skills;
}

public sealed class RebirthNpcWorkOrder
{
    public Guid WorkId;
    public string SettlementId;
    public string WorkType;
    public string RequiredProfession;
    public int Priority;
    public RebirthNpcWorkState State;
    public RebirthNpcStableId ClaimedBy;
    public long ClaimExpiresUtcTicks;
    public long CreatedUtcTicks;
    public string ResourceKey;
    public int ResourceQuantity;
    public Vector3 TargetPosition;
    public int DurationSeconds;
    public long ExecutionStartedUtcTicks;
    public long ExecutionElapsedTicks;
    public long LastExecutionHeartbeatUtcTicks;
    public int ExecutionAttemptCount;
    public string LastExecutionDetail;
}

public static class RebirthNpcSettlementSimulation
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcNeedState> Needs =
        new Dictionary<RebirthNpcStableId,RebirthNpcNeedState>();
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcProfessionRecord> Professions =
        new Dictionary<RebirthNpcStableId,RebirthNpcProfessionRecord>();
    private static readonly Dictionary<Guid,RebirthNpcWorkOrder> Work =
        new Dictionary<Guid,RebirthNpcWorkOrder>();
    private static readonly Dictionary<string,RebirthNpcSettlementResourcePersistentRecord> Resources =
        new Dictionary<string,RebirthNpcSettlementResourcePersistentRecord>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Guid,RebirthNpcResourceReservationPersistentRecord> ResourceReservations =
        new Dictionary<Guid,RebirthNpcResourceReservationPersistentRecord>();
    private static long needTicks, workClaims, completed, expiredClaims, assignmentFailures, budgetDeferrals;
    private static long resourceDeposits, resourceWithdrawals, resourceReservations, resourceConsumes, resourceReleases, resourceShortages;
    private static long nextNeedsTick, nextAssignmentTick;
    private const int WorkBudget = 32;
    private const int WorkDiscoveryBudget = 128;
    private const int ClaimExpiryBudget = 64;
    private const int MaxTerminalWorkOrders = 512;
    private static readonly SortedDictionary<int, Queue<Guid>> AvailableByPriority =
        new SortedDictionary<int, Queue<Guid>>(Comparer<int>.Create(delegate(int left, int right) { return right.CompareTo(left); }));
    private static readonly HashSet<Guid> AvailableQueued = new HashSet<Guid>();
    private static readonly Dictionary<RebirthNpcStableId, Guid> ClaimedWorkByNpc =
        new Dictionary<RebirthNpcStableId, Guid>();
    private static readonly HashSet<RebirthNpcStableId> BusyNpcs = new HashSet<RebirthNpcStableId>();
    private static readonly SortedDictionary<long, Queue<Guid>> ClaimExpiryByDue =
        new SortedDictionary<long, Queue<Guid>>();
    private static readonly Queue<Guid> TerminalWorkOrderHistory = new Queue<Guid>();


    public static bool DepositResource(string settlementId, string resourceKey, long quantity)
    {
        settlementId=Normalize(settlementId,string.Empty); resourceKey=Normalize(resourceKey,string.Empty);
        if(settlementId.Length==0||resourceKey.Length==0||quantity<=0)return false;
        lock(Sync)
        {
            string key=ResourceKey(settlementId,resourceKey); RebirthNpcSettlementResourcePersistentRecord r;
            if(!Resources.TryGetValue(key,out r)) Resources[key]=r=new RebirthNpcSettlementResourcePersistentRecord{SettlementId=settlementId,ResourceKey=resourceKey};
            if(long.MaxValue-r.Quantity<quantity)return false; r.Quantity+=quantity; r.Revision++;
        }
        Interlocked.Increment(ref resourceDeposits); RebirthNpcSettlementPersistenceStore.MarkDirty(); return true;
    }

    public static bool TryWithdrawResource(string settlementId, string resourceKey, long quantity)
    {
        return TryWithdrawResourceCore(settlementId,resourceKey,quantity,Guid.Empty);
    }

    public static bool TryWithdrawResourceWithExternalReservation(string settlementId,string resourceKey,
        long quantity,Guid reservationTransactionId)
    {
        if(reservationTransactionId==Guid.Empty)return false;
        return TryWithdrawResourceCore(settlementId,resourceKey,quantity,reservationTransactionId);
    }

    private static bool TryWithdrawResourceCore(string settlementId,string resourceKey,long quantity,
        Guid reservationTransactionId)
    {
        settlementId=Normalize(settlementId,string.Empty);resourceKey=Normalize(resourceKey,string.Empty);
        if(settlementId.Length==0||resourceKey.Length==0||quantity<=0)return false;
        // Query endpoint reservations before taking the settlement simulation lock. A commit
        // excludes only its own token; unrelated raw withdrawals leave every reservation intact.
        int externalReserved=RebirthNpcSettlementInventoryEndpointRegistry.GetReservedDebitQuantity(
            settlementId,resourceKey,reservationTransactionId);
        lock(Sync)
        {
            RebirthNpcSettlementResourcePersistentRecord r;
            long available=AvailableQuantityLocked(settlementId,resourceKey)-externalReserved;
            if(!Resources.TryGetValue(ResourceKey(settlementId,resourceKey),out r)||available<quantity)return false;
            r.Quantity-=quantity;r.Revision++;
        }
        Interlocked.Increment(ref resourceWithdrawals);RebirthNpcSettlementPersistenceStore.MarkDirty();return true;
    }

    public static long GetAvailableResource(string settlementId,string resourceKey)
    {
        settlementId=Normalize(settlementId,string.Empty);resourceKey=Normalize(resourceKey,string.Empty);
        int externalReserved=RebirthNpcSettlementInventoryEndpointRegistry.GetReservedDebitQuantity(
            settlementId,resourceKey,Guid.Empty);
        lock(Sync)return Math.Max(0,AvailableQuantityLocked(settlementId,resourceKey)-externalReserved);
    }

    private static bool TryReserveResourcesLocked(RebirthNpcWorkOrder work)
    {
        if(work.ResourceQuantity<=0||string.IsNullOrEmpty(work.ResourceKey))return true;
        RebirthNpcResourceReservationPersistentRecord existing;
        if(ResourceReservations.TryGetValue(work.WorkId,out existing))return existing.State==RebirthNpcResourceReservationState.Reserved||existing.State==RebirthNpcResourceReservationState.Consumed;
        if(AvailableQuantityLocked(work.SettlementId,work.ResourceKey)<work.ResourceQuantity){Interlocked.Increment(ref resourceShortages);return false;}
        ResourceReservations.Add(work.WorkId,new RebirthNpcResourceReservationPersistentRecord{WorkId=work.WorkId,SettlementId=work.SettlementId,ResourceKey=work.ResourceKey,Quantity=work.ResourceQuantity,State=RebirthNpcResourceReservationState.Reserved,CreatedUtcTicks=DateTime.UtcNow.Ticks});
        Interlocked.Increment(ref resourceReservations); return true;
    }

    private static bool TryConsumeResourcesLocked(RebirthNpcWorkOrder work)
    {
        if(work.ResourceQuantity<=0||string.IsNullOrEmpty(work.ResourceKey))return true;
        RebirthNpcResourceReservationPersistentRecord reservation;
        if(!ResourceReservations.TryGetValue(work.WorkId,out reservation))return false;
        if(reservation.State==RebirthNpcResourceReservationState.Consumed)return true;
        RebirthNpcSettlementResourcePersistentRecord stock;
        if(!Resources.TryGetValue(ResourceKey(reservation.SettlementId,reservation.ResourceKey),out stock)||stock.Quantity<reservation.Quantity)return false;
        stock.Quantity-=reservation.Quantity; stock.Revision++; reservation.State=RebirthNpcResourceReservationState.Consumed;
        Interlocked.Increment(ref resourceConsumes); return true;
    }

    private static void ReleaseReservationLocked(Guid workId)
    {
        RebirthNpcResourceReservationPersistentRecord reservation;
        if(ResourceReservations.TryGetValue(workId,out reservation)&&reservation.State==RebirthNpcResourceReservationState.Reserved)
        {ResourceReservations.Remove(workId);Interlocked.Increment(ref resourceReleases);}
    }

    private static long AvailableQuantityLocked(string settlementId,string resourceKey)
    {
        RebirthNpcSettlementResourcePersistentRecord stock; long quantity=Resources.TryGetValue(ResourceKey(settlementId,resourceKey),out stock)?stock.Quantity:0;
        foreach(RebirthNpcResourceReservationPersistentRecord r in ResourceReservations.Values)
            if(r.State==RebirthNpcResourceReservationState.Reserved&&string.Equals(r.SettlementId,settlementId,StringComparison.OrdinalIgnoreCase)&&string.Equals(r.ResourceKey,resourceKey,StringComparison.OrdinalIgnoreCase))quantity-=r.Quantity;
        return Math.Max(0,quantity);
    }

    private static string ResourceKey(string settlementId,string resourceKey)=>settlementId+"\u001f"+resourceKey;

    public static void AssignProfession(RebirthNpcStableId npcId, string professionId,
        string settlementId, string workplaceId)
    {
        if (npcId.IsEmpty) return;
        lock(Sync)
        {
            RebirthNpcProfessionRecord r;
            if(!Professions.TryGetValue(npcId,out r)) Professions[npcId]=r=new RebirthNpcProfessionRecord();
            r.ProfessionId=Normalize(professionId,"unassigned");
            r.SettlementId=Normalize(settlementId,string.Empty);
            r.WorkplaceId=Normalize(workplaceId,string.Empty);
            r.Revision++;
        }
        EnsureNeeds(npcId);
        RebirthNpcSettlementPersistenceStore.MarkDirty();
    }

    public static Guid EnqueueWork(string settlementId, string workType, string requiredProfession,
        int priority, string resourceKey, int resourceQuantity)
    {
        return EnqueueWork(settlementId, workType, requiredProfession, priority, resourceKey, resourceQuantity, Vector3.zero, 5);
    }

    public static Guid EnqueueWork(string settlementId, string workType, string requiredProfession,
        int priority, string resourceKey, int resourceQuantity, Vector3 targetPosition, int durationSeconds)
    {
        RebirthNpcWorkOrder w=new RebirthNpcWorkOrder {
            WorkId=Guid.NewGuid(), SettlementId=Normalize(settlementId,string.Empty),
            WorkType=Normalize(workType,"generic"), RequiredProfession=Normalize(requiredProfession,string.Empty),
            Priority=priority, State=RebirthNpcWorkState.Available, CreatedUtcTicks=DateTime.UtcNow.Ticks,
            ResourceKey=Normalize(resourceKey,string.Empty), ResourceQuantity=Math.Max(0,resourceQuantity),
            TargetPosition=targetPosition, DurationSeconds=Math.Max(1,durationSeconds)
        };
        lock(Sync)
        {
            Work[w.WorkId]=w;
            EnqueueAvailableLocked(w);
        }
        RebirthNpcSettlementPersistenceStore.MarkDirty();
        return w.WorkId;
    }


    public static bool TryGetClaimedWork(RebirthNpcStableId npcId, out RebirthNpcWorkOrder work)
    {
        lock(Sync)
        {
            Guid workId;
            if(ClaimedWorkByNpc.TryGetValue(npcId,out workId) && Work.TryGetValue(workId,out work) &&
                (work.State==RebirthNpcWorkState.Claimed || work.State==RebirthNpcWorkState.Running) &&
                work.ClaimedBy.Equals(npcId)) return true;
        }
        work=null; return false;
    }

    public static bool TryMarkRunning(Guid workId, RebirthNpcStableId npcId)
    {
        lock(Sync)
        {
            RebirthNpcWorkOrder w;
            if(!Work.TryGetValue(workId,out w) || (w.State!=RebirthNpcWorkState.Claimed && w.State!=RebirthNpcWorkState.Running) || !w.ClaimedBy.Equals(npcId)) return false;
            if(!TryConsumeResourcesLocked(w))return false;
            long now=DateTime.UtcNow.Ticks;
            w.State=RebirthNpcWorkState.Running;
            if(w.ExecutionStartedUtcTicks<=0)w.ExecutionStartedUtcTicks=now;
            w.LastExecutionHeartbeatUtcTicks=now;
            w.ExecutionAttemptCount=Math.Max(1,w.ExecutionAttemptCount+1);
            w.LastExecutionDetail="Execution started.";
            w.ClaimExpiresUtcTicks=now+TimeSpan.FromMinutes(5).Ticks;
            RebirthNpcSettlementPersistenceStore.MarkDirty();
            return true;
        }
    }


    public static bool TryHeartbeatWork(Guid workId, RebirthNpcStableId npcId, long elapsedTicks, string detail)
    {
        lock(Sync)
        {
            RebirthNpcWorkOrder w;
            if(!Work.TryGetValue(workId,out w)||w.State!=RebirthNpcWorkState.Running||!w.ClaimedBy.Equals(npcId))return false;
            long now=DateTime.UtcNow.Ticks;
            w.ExecutionElapsedTicks=Math.Max(w.ExecutionElapsedTicks,Math.Max(0,elapsedTicks));
            w.LastExecutionHeartbeatUtcTicks=now;
            w.ClaimExpiresUtcTicks=now+TimeSpan.FromMinutes(5).Ticks;
            if(!string.IsNullOrEmpty(detail))w.LastExecutionDetail=detail;
        }
        RebirthNpcSettlementPersistenceStore.MarkDirty();
        return true;
    }

    public static bool TryGetExecutionRemaining(Guid workId, RebirthNpcStableId npcId, out long remainingTicks)
    {
        lock(Sync)
        {
            RebirthNpcWorkOrder w;
            if(!Work.TryGetValue(workId,out w)||!w.ClaimedBy.Equals(npcId)){remainingTicks=0;return false;}
            int effectiveSeconds=RebirthNpcProfessionWorkExecutor.ApplyDurationSeconds(npcId,w.RequiredProfession.Length>0?w.RequiredProfession:w.WorkType,w.DurationSeconds);
            long total=TimeSpan.FromSeconds(Math.Max(1,effectiveSeconds)).Ticks;
            remainingTicks=Math.Max(0,total-Math.Max(0,w.ExecutionElapsedTicks));
            return true;
        }
    }

    public static bool SatisfyNeed(RebirthNpcStableId npcId, RebirthNpcNeedKind kind, float amount)
    {
        if(npcId.IsEmpty || amount<=0f) return false;
        RebirthNpcNeedState n=EnsureNeeds(npcId);
        lock(Sync) n.Values[kind]=Clamp01(Get(n,kind)-amount);
        RebirthNpcSettlementPersistenceStore.MarkDirty();
        return true;
    }

    public static float GetNeed(RebirthNpcStableId npcId, RebirthNpcNeedKind kind)
    {
        lock(Sync)
        {
            RebirthNpcNeedState n; return Needs.TryGetValue(npcId,out n)?Get(n,kind):0f;
        }
    }

    public static bool CompleteWork(Guid workId, RebirthNpcStableId npcId, bool success)
    {
        lock(Sync)
        {
            RebirthNpcWorkOrder w;
            if(!Work.TryGetValue(workId,out w) || !w.ClaimedBy.Equals(npcId)) return false;
            if(success && w.State!=RebirthNpcWorkState.Running) return false;
            if(!success && w.State!=RebirthNpcWorkState.Claimed && w.State!=RebirthNpcWorkState.Running) return false;
            w.State=success?RebirthNpcWorkState.Completed:RebirthNpcWorkState.Failed;
            w.ClaimExpiresUtcTicks=0;
            w.LastExecutionHeartbeatUtcTicks=DateTime.UtcNow.Ticks;
            w.LastExecutionDetail=success?"Execution completed.":"Execution failed.";
            BusyNpcs.Remove(npcId);
            Guid claimedId;
            if(ClaimedWorkByNpc.TryGetValue(npcId,out claimedId) && claimedId==workId)
                ClaimedWorkByNpc.Remove(npcId);
            if(!success)ReleaseReservationLocked(workId);
            ResourceReservations.Remove(workId);
            TerminalWorkOrderHistory.Enqueue(workId);
            TrimTerminalWorkHistoryLocked();
            if(success)
            {
                Interlocked.Increment(ref completed);
                RebirthNpcProfession profession;
                if(RebirthNpcProfessionDefinitions.TryResolve(w.RequiredProfession.Length>0?w.RequiredProfession:w.WorkType,out profession))
                {
                    Guid progressionId=DeriveProfessionOutcomeId(w.WorkId,profession);
                    PublishProfessionCompletion(progressionId,npcId,profession,w);
                }
            }
            RebirthNpcSettlementPersistenceStore.MarkDirty();
            return true;
        }
    }

    public static void SetNeed(RebirthNpcStableId npcId, RebirthNpcNeedKind kind, float value)
    {
        RebirthNpcNeedState n=EnsureNeeds(npcId);
        lock(Sync) n.Values[kind]=Clamp01(value);
        RebirthNpcSettlementPersistenceStore.MarkDirty();
    }

    public static void Tick()
    {
        if (ConnectionManager.Instance != null && !ConnectionManager.Instance.IsServer) return;
        long now=DateTime.UtcNow.Ticks;
        if (now < nextNeedsTick && now < nextAssignmentTick) return;
        RebirthNpcSettlementPersistenceStore.EnsureLoaded();
        if(now>=nextNeedsTick){nextNeedsTick=now+TimeSpan.FromSeconds(10).Ticks;TickNeeds(now);}
        if(now>=nextAssignmentTick){nextAssignmentTick=now+TimeSpan.FromSeconds(2).Ticks;TickAssignments(now);}
    }

    private static void TickNeeds(long now)
    {
        lock(Sync)
        {
            foreach(KeyValuePair<RebirthNpcStableId,RebirthNpcNeedState> pair in Needs)
            {
                RebirthNpcNeedState n=pair.Value;
                double minutes=n.LastUpdateUtcTicks==0?0.166:new TimeSpan(now-n.LastUpdateUtcTicks).TotalMinutes;
                n.LastUpdateUtcTicks=now;
                Increase(n,RebirthNpcNeedKind.Hunger,(float)(minutes*0.0025));
                Increase(n,RebirthNpcNeedKind.Thirst,(float)(minutes*0.004));
                Increase(n,RebirthNpcNeedKind.Fatigue,(float)(minutes*0.002));
                RebirthNpcDecisionEngine.SubmitSignal(pair.Key,"need.hunger",Get(n,RebirthNpcNeedKind.Hunger));
                RebirthNpcDecisionEngine.SubmitSignal(pair.Key,"need.thirst",Get(n,RebirthNpcNeedKind.Thirst));
                RebirthNpcDecisionEngine.SubmitSignal(pair.Key,"need.fatigue",Get(n,RebirthNpcNeedKind.Fatigue));
            }
        }
        RebirthNpcSettlementPersistenceStore.MarkDirty();
        Interlocked.Increment(ref needTicks);
    }

    private static void TickAssignments(long now)
    {
        lock(Sync)
        {
            ProcessExpiredClaimsLocked(now, ClaimExpiryBudget);
            int discovered=0;
            int claimed=0;
            bool visitedAny=true;
            while(visitedAny && discovered<WorkDiscoveryBudget && claimed<WorkBudget)
            {
                visitedAny=false;
                foreach(KeyValuePair<int,Queue<Guid>> priority in AvailableByPriority)
                {
                    if(discovered>=WorkDiscoveryBudget || claimed>=WorkBudget) break;
                    Queue<Guid> queue=priority.Value;
                    if(queue.Count==0)continue;
                    Guid workId=queue.Dequeue();
                    AvailableQueued.Remove(workId);
                    discovered++;
                    visitedAny=true;
                    RebirthNpcWorkOrder w;
                    if(!Work.TryGetValue(workId,out w) || w.State!=RebirthNpcWorkState.Available)continue;

                    RebirthNpcStableId best=default(RebirthNpcStableId);
                    float bestSkill=-1f;
                    foreach(KeyValuePair<RebirthNpcStableId,RebirthNpcProfessionRecord> candidate in Professions)
                    {
                        RebirthNpcProfessionRecord profession=candidate.Value;
                        if(!string.Equals(profession.SettlementId,w.SettlementId,StringComparison.OrdinalIgnoreCase))continue;
                        if(w.RequiredProfession.Length>0 && !string.Equals(profession.ProfessionId,w.RequiredProfession,StringComparison.OrdinalIgnoreCase))continue;
                        if(BusyNpcs.Contains(candidate.Key))continue;
                        float skill=RebirthNpcProfessionProgressionService.CapabilityScore(candidate.Key,
                            w.RequiredProfession.Length>0?w.RequiredProfession:w.WorkType);
                        if(skill>bestSkill || (skill==bestSkill && !best.IsEmpty && CompareStableId(candidate.Key,best)<0))
                        {
                            best=candidate.Key;
                            bestSkill=skill;
                        }
                    }
                    if(best.IsEmpty || !TryReserveResourcesLocked(w))
                    {
                        Interlocked.Increment(ref assignmentFailures);
                        EnqueueAvailableLocked(w);
                        continue;
                    }
                    w.State=RebirthNpcWorkState.Claimed;
                    w.ClaimedBy=best;
                    w.ClaimExpiresUtcTicks=now+TimeSpan.FromMinutes(2).Ticks;
                    BusyNpcs.Add(best);
                    ClaimedWorkByNpc[best]=w.WorkId;
                    ScheduleClaimExpiryLocked(w);
                    claimed++;
                    Interlocked.Increment(ref workClaims);
                    RebirthNpcDecisionEngine.SubmitSignal(best,"work.available",1f);
                }
            }
            if(discovered>=WorkDiscoveryBudget && HasAvailableWorkLocked())
                Interlocked.Increment(ref budgetDeferrals);
        }
        RebirthNpcSettlementPersistenceStore.MarkDirty();
    }


    private static Guid DeriveProfessionOutcomeId(Guid workId,RebirthNpcProfession profession)
    {
        byte[] b=workId.ToByteArray();b[15]=(byte)(b[15]^(byte)(0xA0+(int)profession));return new Guid(b);
    }

    private static void PublishProfessionCompletion(Guid id,RebirthNpcStableId npc,RebirthNpcProfession profession,RebirthNpcWorkOrder w)
    {
        string target=string.IsNullOrWhiteSpace(w.ResourceKey)?w.WorkId.ToString("N"):w.ResourceKey;int q=Math.Max(1,w.ResourceQuantity);int difficulty=Math.Max(1,Math.Min(20,w.Priority));
        switch(profession)
        {
            case RebirthNpcProfession.Farming:RebirthNpcProfessionOutcomePublisher.Farming(id,npc,target,q,difficulty);break;
            case RebirthNpcProfession.Looting:RebirthNpcProfessionOutcomePublisher.Looting(id,npc,target,q,difficulty);break;
            case RebirthNpcProfession.Gathering:RebirthNpcProfessionOutcomePublisher.Gathering(id,npc,target,q,difficulty);break;
            case RebirthNpcProfession.Mining:RebirthNpcProfessionOutcomePublisher.Mining(id,npc,target,q,difficulty);break;
            case RebirthNpcProfession.Salvaging:RebirthNpcProfessionOutcomePublisher.Salvaging(id,npc,target,q,difficulty);break;
            case RebirthNpcProfession.Cooking:RebirthNpcProfessionOutcomePublisher.Cooking(id,npc,target,q,difficulty,q);break;
            case RebirthNpcProfession.BaseRepairs:RebirthNpcProfessionOutcomePublisher.BaseRepairs(id,npc,target,Math.Max(1,q),difficulty,q);break;
            case RebirthNpcProfession.Medicine:RebirthNpcProfessionOutcomePublisher.Medicine(id,npc,target,q,difficulty,q);break;
        }
    }

    private static bool IsBusy(RebirthNpcStableId id)
    {
        return BusyNpcs.Contains(id);
    }
    private static void EnqueueAvailableLocked(RebirthNpcWorkOrder work)
    {
        if(work==null || work.State!=RebirthNpcWorkState.Available || AvailableQueued.Contains(work.WorkId))return;
        Queue<Guid> queue;
        if(!AvailableByPriority.TryGetValue(work.Priority,out queue))
            AvailableByPriority[work.Priority]=queue=new Queue<Guid>();
        queue.Enqueue(work.WorkId);
        AvailableQueued.Add(work.WorkId);
    }

    private static bool HasAvailableWorkLocked()
    {
        return AvailableQueued.Count>0;
    }

    private static void ScheduleClaimExpiryLocked(RebirthNpcWorkOrder work)
    {
        if(work==null || work.ClaimExpiresUtcTicks<=0)return;
        Queue<Guid> queue;
        if(!ClaimExpiryByDue.TryGetValue(work.ClaimExpiresUtcTicks,out queue))
            ClaimExpiryByDue[work.ClaimExpiresUtcTicks]=queue=new Queue<Guid>();
        queue.Enqueue(work.WorkId);
    }

    private static void ProcessExpiredClaimsLocked(long now,int budget)
    {
        int processed=0;
        while(processed<budget && ClaimExpiryByDue.Count>0)
        {
            long due=0; Queue<Guid> queue=null;
            foreach(KeyValuePair<long,Queue<Guid>> pair in ClaimExpiryByDue)
            { due=pair.Key; queue=pair.Value; break; }
            if(queue==null || due>now)return;
            ClaimExpiryByDue.Remove(due);
            while(queue.Count>0 && processed<budget)
            {
                Guid workId=queue.Dequeue(); processed++;
                RebirthNpcWorkOrder work;
                if(!Work.TryGetValue(workId,out work) || work.ClaimExpiresUtcTicks!=due)continue;
                if(work.State!=RebirthNpcWorkState.Claimed)continue;
                RebirthNpcStableId npc=work.ClaimedBy;
                ReleaseReservationLocked(work.WorkId);
                work.State=RebirthNpcWorkState.Available;
                work.ClaimedBy=default(RebirthNpcStableId);
                work.ClaimExpiresUtcTicks=0;
                BusyNpcs.Remove(npc);
                Guid current;
                if(ClaimedWorkByNpc.TryGetValue(npc,out current) && current==work.WorkId)
                    ClaimedWorkByNpc.Remove(npc);
                EnqueueAvailableLocked(work);
                Interlocked.Increment(ref expiredClaims);
            }
            if(queue.Count>0)
            {
                Queue<Guid> remaining;
                if(!ClaimExpiryByDue.TryGetValue(due,out remaining))
                    ClaimExpiryByDue[due]=remaining=new Queue<Guid>();
                while(queue.Count>0)remaining.Enqueue(queue.Dequeue());
                return;
            }
        }
    }

    private static void TrimTerminalWorkHistoryLocked()
    {
        while(TerminalWorkOrderHistory.Count>MaxTerminalWorkOrders)
        {
            Guid old=TerminalWorkOrderHistory.Dequeue();
            RebirthNpcWorkOrder work;
            if(Work.TryGetValue(old,out work) &&
                (work.State==RebirthNpcWorkState.Completed || work.State==RebirthNpcWorkState.Failed || work.State==RebirthNpcWorkState.Cancelled))
                Work.Remove(old);
        }
    }

    private static int CompareStableId(RebirthNpcStableId left,RebirthNpcStableId right)
    {
        int high=left.High.CompareTo(right.High);
        return high!=0?high:left.Low.CompareTo(right.Low);
    }

    private static RebirthNpcNeedState EnsureNeeds(RebirthNpcStableId id)
    {
        lock(Sync)
        {
            RebirthNpcNeedState n;
            if(!Needs.TryGetValue(id,out n))
            {
                Needs[id]=n=new RebirthNpcNeedState{LastUpdateUtcTicks=DateTime.UtcNow.Ticks};
                foreach(RebirthNpcNeedKind k in Enum.GetValues(typeof(RebirthNpcNeedKind))) n.Values[k]=0f;
            }
            return n;
        }
    }

    public static RebirthNpcSettlementPersistentState CapturePersistentState()
    {
        lock(Sync)
        {
            List<RebirthNpcNeedPersistentRecord> needs=new List<RebirthNpcNeedPersistentRecord>();
            foreach(KeyValuePair<RebirthNpcStableId,RebirthNpcNeedState> pair in Needs)
                needs.Add(new RebirthNpcNeedPersistentRecord{NpcId=pair.Key,LastUpdateUtcTicks=pair.Value.LastUpdateUtcTicks,
                    Values=new Dictionary<RebirthNpcNeedKind,float>(pair.Value.Values)});
            List<RebirthNpcProfessionPersistentRecord> professions=new List<RebirthNpcProfessionPersistentRecord>();
            foreach(KeyValuePair<RebirthNpcStableId,RebirthNpcProfessionRecord> pair in Professions)
                professions.Add(new RebirthNpcProfessionPersistentRecord{NpcId=pair.Key,ProfessionId=pair.Value.ProfessionId,
                    WorkplaceId=pair.Value.WorkplaceId,SettlementId=pair.Value.SettlementId,Revision=pair.Value.Revision,
                    Skills=new Dictionary<string,float>(pair.Value.Skills,StringComparer.OrdinalIgnoreCase)});
            List<RebirthNpcWorkOrder> work=new List<RebirthNpcWorkOrder>();
            foreach(RebirthNpcWorkOrder w in Work.Values) work.Add(CloneWork(w));
            List<RebirthNpcSettlementResourcePersistentRecord> resources=new List<RebirthNpcSettlementResourcePersistentRecord>();
            foreach(RebirthNpcSettlementResourcePersistentRecord r in Resources.Values)resources.Add(new RebirthNpcSettlementResourcePersistentRecord{SettlementId=r.SettlementId,ResourceKey=r.ResourceKey,Quantity=r.Quantity,Revision=r.Revision});
            List<RebirthNpcResourceReservationPersistentRecord> reservations=new List<RebirthNpcResourceReservationPersistentRecord>();
            foreach(RebirthNpcResourceReservationPersistentRecord r in ResourceReservations.Values)reservations.Add(new RebirthNpcResourceReservationPersistentRecord{WorkId=r.WorkId,SettlementId=r.SettlementId,ResourceKey=r.ResourceKey,Quantity=r.Quantity,State=r.State,CreatedUtcTicks=r.CreatedUtcTicks});
            return new RebirthNpcSettlementPersistentState{Needs=needs.ToArray(),Professions=professions.ToArray(),WorkOrders=work.ToArray(),Resources=resources.ToArray(),ResourceReservations=reservations.ToArray()};
        }
    }

    public static bool ValidatePersistentState(RebirthNpcSettlementPersistentState state, out string error)
    {
        error=string.Empty;
        if(state==null){error="State is null.";return false;}
        var needs=new HashSet<RebirthNpcStableId>();
        var professions=new HashSet<RebirthNpcStableId>();
        var resources=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reservations=new Dictionary<Guid,RebirthNpcResourceReservationPersistentRecord>();
        var workIds=new HashSet<Guid>();
        var busy=new HashSet<RebirthNpcStableId>();
        if(state.Needs!=null)foreach(var r in state.Needs)
            if(r==null||r.NpcId.IsEmpty||!needs.Add(r.NpcId)){error="Invalid or duplicate need record.";return false;}
        if(state.Professions!=null)foreach(var r in state.Professions)
            if(r==null||r.NpcId.IsEmpty||!professions.Add(r.NpcId)){error="Invalid or duplicate profession record.";return false;}
        if(state.Resources!=null)foreach(var r in state.Resources)
            if(r==null||string.IsNullOrWhiteSpace(r.SettlementId)||string.IsNullOrWhiteSpace(r.ResourceKey)||r.Quantity<0
                ||!resources.Add(ResourceKey(r.SettlementId,r.ResourceKey))){error="Invalid or duplicate resource record.";return false;}
        if(state.ResourceReservations!=null)foreach(var r in state.ResourceReservations)
        {
            if(r==null||r.WorkId==Guid.Empty||string.IsNullOrWhiteSpace(r.SettlementId)||string.IsNullOrWhiteSpace(r.ResourceKey)
                ||r.Quantity<=0||!Enum.IsDefined(typeof(RebirthNpcResourceReservationState),r.State)||reservations.ContainsKey(r.WorkId)){error="Invalid resource reservation.";return false;}
            reservations.Add(r.WorkId,r);
        }
        long now=DateTime.UtcNow.Ticks;
        if(state.WorkOrders!=null)foreach(var w in state.WorkOrders)
        {
            if(w==null||w.WorkId==Guid.Empty||!Enum.IsDefined(typeof(RebirthNpcWorkState),w.State)||!workIds.Add(w.WorkId)){error="Invalid or duplicate work order.";return false;}
            // Mirror restart claim recovery without mutating input records or migrating skills.
            bool active=w.State==RebirthNpcWorkState.Running
                ||(w.State==RebirthNpcWorkState.Claimed&&w.ClaimExpiresUtcTicks>now);
            if(active)active=!w.ClaimedBy.IsEmpty&&busy.Add(w.ClaimedBy);
            RebirthNpcResourceReservationPersistentRecord reservation;
            if(active&&reservations.TryGetValue(w.WorkId,out reservation)
                &&(!string.Equals(w.SettlementId,reservation.SettlementId,StringComparison.OrdinalIgnoreCase)
                ||!string.Equals(w.ResourceKey,reservation.ResourceKey,StringComparison.OrdinalIgnoreCase)
                ||w.ResourceQuantity!=reservation.Quantity))
            {error="Resource reservation does not match its work order.";return false;}
        }
        return true;
    }

    public static bool TryRestorePersistentState(RebirthNpcSettlementPersistentState state, out string error)
    {
        if(!ValidatePersistentState(state,out error))return false;
        lock(Sync)
        {
            Needs.Clear(); Professions.Clear(); Work.Clear(); Resources.Clear(); ResourceReservations.Clear();
            AvailableByPriority.Clear(); AvailableQueued.Clear(); ClaimedWorkByNpc.Clear(); BusyNpcs.Clear();
            ClaimExpiryByDue.Clear(); TerminalWorkOrderHistory.Clear();
            if(state.Needs!=null) foreach(RebirthNpcNeedPersistentRecord r in state.Needs)
            {
                if(r==null||r.NpcId.IsEmpty||Needs.ContainsKey(r.NpcId)){error="Invalid or duplicate need record.";return false;}
                RebirthNpcNeedState n=new RebirthNpcNeedState{LastUpdateUtcTicks=r.LastUpdateUtcTicks};
                foreach(RebirthNpcNeedKind k in Enum.GetValues(typeof(RebirthNpcNeedKind)))
                { float v=0f; if(r.Values!=null)r.Values.TryGetValue(k,out v); n.Values[k]=Clamp01(v); }
                Needs.Add(r.NpcId,n);
            }
            if(state.Professions!=null) foreach(RebirthNpcProfessionPersistentRecord r in state.Professions)
            {
                if(r==null||r.NpcId.IsEmpty||Professions.ContainsKey(r.NpcId)){error="Invalid or duplicate profession record.";return false;}
                RebirthNpcProfessionRecord p=new RebirthNpcProfessionRecord{ProfessionId=Normalize(r.ProfessionId,"unassigned"),
                    WorkplaceId=Normalize(r.WorkplaceId,string.Empty),SettlementId=Normalize(r.SettlementId,string.Empty),Revision=r.Revision};
                if(r.Skills!=null)foreach(KeyValuePair<string,float> skill in r.Skills)
                    if(!string.IsNullOrEmpty(skill.Key)&&skill.Value>=0f)p.Skills[skill.Key]=skill.Value;
                Professions.Add(r.NpcId,p);
                RebirthNpcProfessionProgressionService.MigrateLegacySkills(r.NpcId,p.Skills);
            }
            if(state.Resources!=null) foreach(RebirthNpcSettlementResourcePersistentRecord r in state.Resources)
            {
                if(r==null||string.IsNullOrWhiteSpace(r.SettlementId)||string.IsNullOrWhiteSpace(r.ResourceKey)||r.Quantity<0){error="Invalid resource record.";return false;}
                string key=ResourceKey(r.SettlementId,r.ResourceKey);if(Resources.ContainsKey(key)){error="Duplicate resource record.";return false;}
                Resources.Add(key,new RebirthNpcSettlementResourcePersistentRecord{SettlementId=r.SettlementId,ResourceKey=r.ResourceKey,Quantity=r.Quantity,Revision=r.Revision});
            }
            if(state.ResourceReservations!=null) foreach(RebirthNpcResourceReservationPersistentRecord r in state.ResourceReservations)
            {
                if(r==null||r.WorkId==Guid.Empty||string.IsNullOrWhiteSpace(r.SettlementId)||string.IsNullOrWhiteSpace(r.ResourceKey)||r.Quantity<=0||ResourceReservations.ContainsKey(r.WorkId)){error="Invalid resource reservation.";return false;}
                ResourceReservations.Add(r.WorkId,new RebirthNpcResourceReservationPersistentRecord{WorkId=r.WorkId,SettlementId=r.SettlementId,ResourceKey=r.ResourceKey,Quantity=r.Quantity,State=r.State,CreatedUtcTicks=r.CreatedUtcTicks});
            }
            long now=DateTime.UtcNow.Ticks;
            if(state.WorkOrders!=null) foreach(RebirthNpcWorkOrder source in state.WorkOrders)
            {
                if(source==null||source.WorkId==Guid.Empty||Work.ContainsKey(source.WorkId)){error="Invalid or duplicate work order.";return false;}
                RebirthNpcWorkOrder w=CloneWork(source);
                if(w.State==RebirthNpcWorkState.Running){w.State=RebirthNpcWorkState.Claimed;w.ClaimExpiresUtcTicks=now+TimeSpan.FromMinutes(2).Ticks;w.LastExecutionDetail="Recovered after restart; awaiting executor reattachment.";}
                else if(w.State==RebirthNpcWorkState.Claimed&&w.ClaimExpiresUtcTicks<=now){w.State=RebirthNpcWorkState.Available;w.ClaimedBy=default(RebirthNpcStableId);w.ClaimExpiresUtcTicks=0;}
                if(w.State==RebirthNpcWorkState.Claimed)
                {
                    if(w.ClaimedBy.IsEmpty || BusyNpcs.Contains(w.ClaimedBy))
                    {
                        w.State=RebirthNpcWorkState.Available;
                        w.ClaimedBy=default(RebirthNpcStableId);
                        w.ClaimExpiresUtcTicks=0;
                    }
                    else
                    {
                        BusyNpcs.Add(w.ClaimedBy);
                        ClaimedWorkByNpc[w.ClaimedBy]=w.WorkId;
                        ScheduleClaimExpiryLocked(w);
                    }
                }
                Work.Add(w.WorkId,w);
                if(w.State==RebirthNpcWorkState.Available)EnqueueAvailableLocked(w);
                else if(w.State==RebirthNpcWorkState.Completed || w.State==RebirthNpcWorkState.Failed || w.State==RebirthNpcWorkState.Cancelled)
                    TerminalWorkOrderHistory.Enqueue(w.WorkId);
            }
            List<Guid> removeReservations=new List<Guid>();
            foreach(KeyValuePair<Guid,RebirthNpcResourceReservationPersistentRecord> pair in ResourceReservations)
            {
                RebirthNpcWorkOrder w;if(!Work.TryGetValue(pair.Key,out w)||w.State==RebirthNpcWorkState.Completed||w.State==RebirthNpcWorkState.Failed||w.State==RebirthNpcWorkState.Cancelled||w.State==RebirthNpcWorkState.Available)removeReservations.Add(pair.Key);
                else if(!string.Equals(w.SettlementId,pair.Value.SettlementId,StringComparison.OrdinalIgnoreCase)||!string.Equals(w.ResourceKey,pair.Value.ResourceKey,StringComparison.OrdinalIgnoreCase)||w.ResourceQuantity!=pair.Value.Quantity){error="Resource reservation does not match its work order.";return false;}
            }
            for(int i=0;i<removeReservations.Count;i++)ResourceReservations.Remove(removeReservations[i]);
            foreach(RebirthNpcWorkOrder w in Work.Values)
            {
                if(w.State!=RebirthNpcWorkState.Claimed||w.ResourceQuantity<=0||string.IsNullOrEmpty(w.ResourceKey)||ResourceReservations.ContainsKey(w.WorkId))continue;
                if(!TryReserveResourcesLocked(w))
                {
                    BusyNpcs.Remove(w.ClaimedBy);
                    ClaimedWorkByNpc.Remove(w.ClaimedBy);
                    w.State=RebirthNpcWorkState.Available;w.ClaimedBy=default(RebirthNpcStableId);w.ClaimExpiresUtcTicks=0;
                    EnqueueAvailableLocked(w);
                }
            }
            TrimTerminalWorkHistoryLocked();
        }
        return true;
    }

    public static void ClearPersistentRuntimeState()
    {
        lock(Sync)
        {
            Needs.Clear();Professions.Clear();Work.Clear();Resources.Clear();ResourceReservations.Clear();
            AvailableByPriority.Clear();AvailableQueued.Clear();ClaimedWorkByNpc.Clear();BusyNpcs.Clear();
            ClaimExpiryByDue.Clear();TerminalWorkOrderHistory.Clear();nextNeedsTick=0;nextAssignmentTick=0;
        }
    }

    private static RebirthNpcWorkOrder CloneWork(RebirthNpcWorkOrder w)
    {
        return new RebirthNpcWorkOrder{WorkId=w.WorkId,SettlementId=w.SettlementId,WorkType=w.WorkType,
            RequiredProfession=w.RequiredProfession,Priority=w.Priority,State=w.State,ClaimedBy=w.ClaimedBy,
            ClaimExpiresUtcTicks=w.ClaimExpiresUtcTicks,CreatedUtcTicks=w.CreatedUtcTicks,ResourceKey=w.ResourceKey,
            ResourceQuantity=w.ResourceQuantity,TargetPosition=w.TargetPosition,DurationSeconds=w.DurationSeconds,
            ExecutionStartedUtcTicks=w.ExecutionStartedUtcTicks,ExecutionElapsedTicks=w.ExecutionElapsedTicks,
            LastExecutionHeartbeatUtcTicks=w.LastExecutionHeartbeatUtcTicks,ExecutionAttemptCount=w.ExecutionAttemptCount,
            LastExecutionDetail=w.LastExecutionDetail};
    }

    private static void Increase(RebirthNpcNeedState n,RebirthNpcNeedKind k,float v){n.Values[k]=Clamp01(Get(n,k)+v);}
    private static float Get(RebirthNpcNeedState n,RebirthNpcNeedKind k){float v;return n.Values.TryGetValue(k,out v)?v:0f;}
    private static float Clamp01(float v){return v<0?0:(v>1?1:v);}
    private static string Normalize(string v,string fallback){v=(v??string.Empty).Trim();return v.Length==0?fallback:v;}

    public static string GetReport()
    {
        int needs,prof,work,resources,reservations,available=0,claimed=0;
        lock(Sync){needs=Needs.Count;prof=Professions.Count;work=Work.Count;resources=Resources.Count;reservations=ResourceReservations.Count;foreach(RebirthNpcWorkOrder w in Work.Values){if(w.State==RebirthNpcWorkState.Available)available++;if(w.State==RebirthNpcWorkState.Claimed)claimed++;}}
        return new StringBuilder("[REBIRTH NPC Settlement Simulation]\n").Append("needStates=").Append(needs)
            .Append(" professions=").Append(prof).Append(" workOrders=").Append(work)
            .Append(" available=").Append(available).Append(" claimed=").Append(claimed)
            .Append(" resources=").Append(resources).Append(" reservations=").Append(reservations)
            .Append(" needTicks=").Append(needTicks).Append(" claims=").Append(workClaims)
            .Append(" completed=").Append(completed).Append(" expiredClaims=").Append(expiredClaims)
            .Append(" assignmentFailures=").Append(assignmentFailures).Append(" budgetDeferrals=").Append(budgetDeferrals)
            .Append(" deposits=").Append(resourceDeposits).Append(" withdrawals=").Append(resourceWithdrawals)
            .Append(" resourceReservations=").Append(resourceReservations).Append(" consumes=").Append(resourceConsumes)
            .Append(" releases=").Append(resourceReleases).Append(" shortages=").Append(resourceShortages).ToString();
    }
}
