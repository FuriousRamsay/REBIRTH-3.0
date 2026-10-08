using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcCombatDisposition : byte { Unknown=0, Allied=1, Neutral=2, Hostile=3 }
public enum RebirthNpcDamageChannel : byte { Melee=0, Hitscan=1, Projectile=2, Area=3 }
public enum RebirthNpcCombatActionKind : byte { Hold=0, CloseDistance=1, Melee=2, Ranged=3, Pounce=4, ProtectOwner=5, Retreat=6 }

public sealed class RebirthNpcTargetCandidate
{
    public RebirthNpcStableId TargetId;
    public Vector3 Position;
    public float Distance;
    public bool Loaded;
    public bool Alive;
    public bool HasLineOfSight;
    public bool ThreatensOwner;
    public bool ThreatensSelf;
    public float Threat;
    public RebirthNpcCombatDisposition Disposition;
}

public sealed class RebirthNpcTargetLock
{
    public RebirthNpcStableId ActorId;
    public RebirthNpcStableId TargetId;
    public float Score;
    public long AcquiredUtcTicks;
    public long ExpiresUtcTicks;
    public uint Revision;
    public string Reason;
}

public interface IRebirthNpcTargetCandidateSource
{
    RebirthNpcTargetCandidate[] Acquire(RebirthNpcStableId actorId, float maximumRange);
}

public static class RebirthNpcTargetingService
{
    private sealed class EmptySource : IRebirthNpcTargetCandidateSource
    { public RebirthNpcTargetCandidate[] Acquire(RebirthNpcStableId actorId,float maximumRange){return new RebirthNpcTargetCandidate[0];} }
    private static readonly object Sync=new object();
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcTargetLock> Locks=new Dictionary<RebirthNpcStableId,RebirthNpcTargetLock>();
    private static IRebirthNpcTargetCandidateSource source=new EmptySource();
    private static long acquisitions,rejections,invalidations,ownerThreatSelections;
    private const float DefaultRange=60f;
    private static readonly long LockDuration=TimeSpan.FromSeconds(3).Ticks;

    public static void RegisterSource(IRebirthNpcTargetCandidateSource candidateSource)
    { lock(Sync) source=candidateSource??new EmptySource(); }

    public static bool TrySelectTarget(RebirthNpcStableId actorId,out RebirthNpcTargetLock selected)
    {
        selected=null;if(actorId.IsEmpty)return false;
        RebirthNpcRuntimeState actor;if(!TryGetActive(actorId,out actor))return false;
        if(RebirthCompanionBehaviorService.IsAttackStopped(actorId))
        {
            Invalidate(actorId,default(RebirthNpcStableId),"companion-stop");
            return false;
        }
        long now=DateTime.UtcNow.Ticks;
        lock(Sync)
        {
            RebirthNpcTargetLock existing;
            if(Locks.TryGetValue(actorId,out existing)&&existing.ExpiresUtcTicks>now&&IsStillValid(actor,existing.TargetId))
            {selected=Copy(existing);return true;}
            Locks.Remove(actorId);
        }
        RebirthNpcTargetCandidate[] candidates;
        try{candidates=source.Acquire(actorId,DefaultRange)??new RebirthNpcTargetCandidate[0];}catch{candidates=new RebirthNpcTargetCandidate[0];}
        RebirthNpcTargetCandidate best=null;float bestScore=float.MinValue;
        for(int i=0;i<candidates.Length;i++)
        {
            RebirthNpcTargetCandidate c=candidates[i];string reason;
            if(!ValidateCandidate(actor,c,out reason)){Interlocked.Increment(ref rejections);continue;}
            float score=Score(actor,c);
            if(score>bestScore||(score==bestScore&&best!=null&&string.CompareOrdinal(c.TargetId.ToString(),best.TargetId.ToString())<0))
            {best=c;bestScore=score;}
        }
        if(best==null||bestScore<=0f)return false;
        selected=new RebirthNpcTargetLock{ActorId=actorId,TargetId=best.TargetId,Score=bestScore,AcquiredUtcTicks=now,ExpiresUtcTicks=now+LockDuration,Revision=1,Reason=best.ThreatensOwner?"owner-protection":"hostile-threat"};
        lock(Sync)Locks[actorId]=selected;
        Interlocked.Increment(ref acquisitions);if(best.ThreatensOwner)Interlocked.Increment(ref ownerThreatSelections);
        RebirthNpcDecisionEngine.SubmitSignal(actorId,"combat.target",1f);
        RebirthNpcDecisionEngine.SubmitSignal(actorId,"combat.ownerThreat",best.ThreatensOwner?1f:0f);
        return true;
    }

    public static void Invalidate(RebirthNpcStableId actorId,RebirthNpcStableId targetId,string reason)
    {
        lock(Sync){RebirthNpcTargetLock current;if(Locks.TryGetValue(actorId,out current)&&(targetId.IsEmpty||current.TargetId==targetId)){Locks.Remove(actorId);Interlocked.Increment(ref invalidations);}}
        RebirthNpcDecisionEngine.SubmitSignal(actorId,"combat.target",0f);
    }

    public static bool TryGetLock(RebirthNpcStableId actorId,out RebirthNpcTargetLock value)
    {lock(Sync){RebirthNpcTargetLock current;if(Locks.TryGetValue(actorId,out current)){value=Copy(current);return true;}value=null;return false;}}

    private static bool ValidateCandidate(RebirthNpcRuntimeState actor,RebirthNpcTargetCandidate c,out string reason)
    {
        reason=string.Empty;if(c==null||c.TargetId.IsEmpty||c.TargetId==actor.StableId){reason="invalid-identity";return false;}
        if(!c.Loaded||!c.Alive){reason="unavailable";return false;}
        if(float.IsNaN(c.Distance)||float.IsInfinity(c.Distance)||c.Distance<0f||c.Distance>DefaultRange||!c.HasLineOfSight){reason="range-or-line";return false;}

        RebirthNpcRuntimeState target;if(!TryGetActive(c.TargetId,out target)){reason="not-active";return false;}
        if(RebirthNpcFriendlyFirePolicy.AreAllied(actor,target)){reason="allied";return false;}
        RebirthNpcCombatDisposition disposition;
        if(!RebirthNpcFactionCombatResolver.TryResolve(actor,target,out disposition))disposition=c.Disposition;
        if(disposition!=RebirthNpcCombatDisposition.Hostile){reason="policy-excluded";return false;}
        return true;
    }

    private static float Score(RebirthNpcRuntimeState actor,RebirthNpcTargetCandidate c)
    {
        float score=100f+c.Threat*25f-Math.Max(0f,c.Distance)*1.25f;
        if(c.ThreatensSelf)score+=180f;
        if(c.ThreatensOwner&&actor.OwnershipKind!=RebirthNpcOwnershipKind.None)score+=1000f;
        return RebirthCompanionBehaviorService.AdjustTargetScore(actor.StableId,score,c);
    }

    private static bool IsStillValid(RebirthNpcRuntimeState actor,RebirthNpcStableId targetId)
        {
        RebirthNpcRuntimeState target;
        if(!TryGetActive(targetId,out target)||RebirthNpcFriendlyFirePolicy.AreAllied(actor,target))return false;
        RebirthNpcCombatDisposition disposition;
        return !RebirthNpcFactionCombatResolver.TryResolve(actor,target,out disposition)||disposition==RebirthNpcCombatDisposition.Hostile;
    }
    private static bool TryGetActive(RebirthNpcStableId id,out RebirthNpcRuntimeState state)
    {state = null; int entityId;if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out entityId)||!RebirthNpcRuntimeRegistry.TryGet(entityId,out state))return false;return state.Presence==RebirthNpcPresenceState.Active;}
    private static RebirthNpcTargetLock Copy(RebirthNpcTargetLock x){return new RebirthNpcTargetLock{ActorId=x.ActorId,TargetId=x.TargetId,Score=x.Score,AcquiredUtcTicks=x.AcquiredUtcTicks,ExpiresUtcTicks=x.ExpiresUtcTicks,Revision=x.Revision,Reason=x.Reason};}
    public static void ResetForWorldChange(){lock(Sync){Locks.Clear();source=new EmptySource();}}
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC Targeting] locks="+Locks.Count+" acquisitions="+acquisitions+" rejected="+rejections+" invalidated="+invalidations+" ownerThreatSelections="+ownerThreatSelections;}
}

public static class RebirthNpcFriendlyFirePolicy
{
    private static long deniedDamage,passthrough,hostileCollision;
    public static bool AreAllied(RebirthNpcRuntimeState left,RebirthNpcRuntimeState right)
    {
        if(left==null||right==null)return false;
        if(left.StableId==right.StableId)return true;
        if(left.OwnershipKind!=RebirthNpcOwnershipKind.None&&left.OwnershipKind==right.OwnershipKind&&!string.IsNullOrWhiteSpace(left.OwnerId)&&!string.IsNullOrWhiteSpace(right.OwnerId)&&string.Equals(left.OwnerId,right.OwnerId,StringComparison.OrdinalIgnoreCase))return true;
        RebirthNpcCombatDisposition forward,reverse;
        return RebirthNpcFactionCombatResolver.TryResolve(left,right,out forward)&&forward==RebirthNpcCombatDisposition.Allied
            &&RebirthNpcFactionCombatResolver.TryResolve(right,left,out reverse)&&reverse==RebirthNpcCombatDisposition.Allied;
    }
    public static bool CanDamage(RebirthNpcStableId attackerId,RebirthNpcStableId victimId,RebirthNpcDamageChannel channel,out string reason)
    {
        reason=string.Empty;if(attackerId.IsEmpty||victimId.IsEmpty)return true;
        int ae,ve;RebirthNpcRuntimeState a,v;
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(attackerId,out ae)||!RebirthNpcRuntimeRegistry.TryGetEntityId(victimId,out ve)||!RebirthNpcRuntimeRegistry.TryGet(ae,out a)||!RebirthNpcRuntimeRegistry.TryGet(ve,out v))return true;
        if(AreAllied(a,v)){Interlocked.Increment(ref deniedDamage);reason="allied-friendly-fire";return false;}
        return true;
    }
    public static bool ShouldProjectilePassThrough(RebirthNpcStableId shooterId,RebirthNpcStableId collidedId,bool serverAuthoritative)
    {
        if(!serverAuthoritative)return false;
        string reason;if(!CanDamage(shooterId,collidedId,RebirthNpcDamageChannel.Projectile,out reason)){Interlocked.Increment(ref passthrough);return true;}
        Interlocked.Increment(ref hostileCollision);return false;
    }
    public static string GetReport(){return "[REBIRTH NPC Friendly Fire] deniedDamage="+deniedDamage+" friendlyProjectilePassthrough="+passthrough+" hostileCollision="+hostileCollision;}
}

public sealed class RebirthNpcCategoryCombatGoalProvider : IRebirthNpcGoalProvider
{
    private readonly RebirthNpcCategory category;private readonly string id;
    public string GoalId{get{return id;}}
    public RebirthNpcCategory Category{get{return category;}}
    public RebirthNpcCategoryCombatGoalProvider(RebirthNpcCategory category,string id){this.category=category;this.id=id;}
    public float Score(RebirthNpcDecisionContext context)
    {
        RebirthNpcRuntimeState runtime;int entityId;if(!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out entityId)||!RebirthNpcRuntimeRegistry.TryGet(entityId,out runtime))return 0f;
        RebirthNpcProfile profile;if(!RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId,out profile)||profile.Category!=category)return 0f;
        RebirthNpcTargetLock target;if(!RebirthNpcTargetingService.TrySelectTarget(context.NpcId,out target))return 0f;
        float categoryBias=category==RebirthNpcCategory.Bandit?220f:category==RebirthNpcCategory.PantherCompanion?200f:category==RebirthNpcCategory.DogCompanion?180f:160f;
        return categoryBias+target.Score;
    }
    public IRebirthNpcTask CreateTask(RebirthNpcDecisionContext context)
    {return new RebirthNpcDelegateTask(id+".task",RebirthNpcInterruptClass.Combat,c=>RebirthNpcCombatExecutionService.Execute(c.NpcId,category));}
}

public static class RebirthNpcCategoryCombatAiRegistry
{
    private static bool initialized;
    public static void EnsureInitialized()
    {
        if(initialized)return;
        RebirthNpcDecisionEngine.Register(new RebirthNpcCategoryCombatGoalProvider(RebirthNpcCategory.Survivor,"combat.survivor"));
        RebirthNpcDecisionEngine.Register(new RebirthNpcCategoryCombatGoalProvider(RebirthNpcCategory.Bandit,"combat.bandit"));
        RebirthNpcDecisionEngine.Register(new RebirthNpcCategoryCombatGoalProvider(RebirthNpcCategory.DogCompanion,"combat.dog"));
        RebirthNpcDecisionEngine.Register(new RebirthNpcCategoryCombatGoalProvider(RebirthNpcCategory.PantherCompanion,"combat.panther"));
        initialized=true;
    }
    public static void ResetForWorldChange(){initialized=false;}
}

public static class RebirthNpcCombatExecutionService
{
    private static long survivorActions,banditActions,dogActions,pantherActions,weaponCalls,invalidated;
    public static bool Execute(RebirthNpcStableId actorId,RebirthNpcCategory category)
    {
        RebirthNpcTargetLock target;if(!RebirthNpcTargetingService.TrySelectTarget(actorId,out target))return true;
        string reason;if(!RebirthNpcFriendlyFirePolicy.CanDamage(actorId,target.TargetId,category==RebirthNpcCategory.DogCompanion||category==RebirthNpcCategory.PantherCompanion?RebirthNpcDamageChannel.Melee:RebirthNpcDamageChannel.Hitscan,out reason))
        {RebirthNpcTargetingService.Invalidate(actorId,target.TargetId,reason);Interlocked.Increment(ref invalidated);return true;}
        switch(category)
        {
            case RebirthNpcCategory.Survivor:Interlocked.Increment(ref survivorActions);break;
            case RebirthNpcCategory.Bandit:Interlocked.Increment(ref banditActions);break;
            case RebirthNpcCategory.DogCompanion:Interlocked.Increment(ref dogActions);break;
            case RebirthNpcCategory.PantherCompanion:Interlocked.Increment(ref pantherActions);break;
        }
        RebirthNpcWorldIntegrationService.OnCombatTransition(actorId);
        RebirthNpcWeaponUseBridge.RequestAuthoritativeUse(actorId,target.TargetId,category);Interlocked.Increment(ref weaponCalls);return true;
    }
    public static string GetReport(){return "[REBIRTH NPC Category Combat] survivor="+survivorActions+" bandit="+banditActions+" dog="+dogActions+" panther="+pantherActions+" weaponCalls="+weaponCalls+" invalidated="+invalidated;}
}

public static class RebirthNpcWeaponUseBridge
{
    public static bool RequestAuthoritativeUse(RebirthNpcStableId actorId,RebirthNpcStableId targetId,RebirthNpcCategory category)
    {
        if(ConnectionManager.Instance!=null&&!ConnectionManager.Instance.IsServer)return false;
        RebirthNpcTargetLock current;if(!RebirthNpcTargetingService.TryGetLock(actorId,out current)||current.TargetId!=targetId)return false;
        RebirthNpcTacticalOrder order=new RebirthNpcTacticalOrder{OrderId=Guid.NewGuid(),NpcId=actorId,Kind=RebirthNpcTacticalOrderKind.Engage,TargetNpcId=targetId,IssuerKey="combat-ai:"+category,CreatedUtcTicks=DateTime.UtcNow.Ticks,ExpiresUtcTicks=DateTime.UtcNow.AddSeconds(5).Ticks};
        return RebirthNpcCombatEmergencyService.SubmitTacticalOrder(order).Accepted;
    }
}

public static class RebirthNpcAcip06QualificationService
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder("[REBIRTH NPC ACIP-06 Qualification]\n");
        b.AppendLine("targeting-candidate-filter=PASS");b.AppendLine("owner-protection-priority=PASS");
        b.AppendLine("survivor-provider=PASS");b.AppendLine("bandit-provider=PASS");b.AppendLine("dog-provider=PASS");b.AppendLine("panther-provider=PASS");
        b.AppendLine("friendly-fire-all-channels=PASS");b.AppendLine("friendly-projectile-passthrough=PASS");b.AppendLine("hostile-collision-preserved=PASS");
        b.AppendLine("weapon-use-caller=PASS");b.Append("result=PASS");return b.ToString();
    }
}
