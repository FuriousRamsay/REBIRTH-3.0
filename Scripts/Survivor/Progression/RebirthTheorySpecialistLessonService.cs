using System;
using System.Collections.Generic;
using UnityEngine;

// Timed authoritative specialist workflow. UI initiation remains a separate integration.
internal static class RebirthTheorySpecialistLessonService
{
    private sealed class Session
    {
        public EntityPlayer Student;
        public World World;
        public string Actor,SessionKey,Creation,Storage,Subject,Source;
        public int Instructor;
        public float Requested,Elapsed,Last,NextFeedback=5f;
    }
    private static readonly Dictionary<int,Session> Sessions=new Dictionary<int,Session>();
    private static float nextTick,nextRecovery;
    private static World activeWorld;
    internal static bool IsActive(EntityPlayer student)=>student!=null&&Sessions.TryGetValue(student.entityId,out var session)&&ReferenceEquals(session.Student,student)&&ReferenceEquals(session.World,student.world);
    internal static bool TryBegin(RebirthNpcInteractionContext context,string subject,out string reason)
    {
        reason="The lesson is unavailable.";
        if(GameManager.Instance==null||GameManager.Instance.IsPaused()||context==null||string.IsNullOrEmpty(subject)||
            RebirthNpcInteractionSessionService.Authorize(context.SessionKey,context.ActorId,RebirthNpcInteractionAction.Dialogue,out var current)!=RebirthNpcInteractionResult.Allowed||
            !current.NpcId.Equals(context.NpcId)||!RebirthNpcRuntimeRegistry.TryGetEntityId(current.NpcId,out var instructor))return false;
        var student=RebirthNpcInteractionMutationHandlers.FindActorPlayer(current.ActorId);
        if(!RebirthSkillAwardService.TryGetEligible(student,out var identity,out var record)||record?.Progression==null||record.Progression.PendingTheoryStudy!=null)return false;
        if(!ReferenceEquals(student.world,GameManager.Instance.World))return false;
        if(RebirthTheorySoloService.IsRunning(student)||RebirthTheorySoloService.HasLiteratureStudy(student)){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryFinishCurrent","Finish your current study session first.");return false;}
        if(!ReferenceEquals(activeWorld,student.world)){Reset();activeWorld=student.world;}
        if(Sessions.TryGetValue(student.entityId,out var existing))
        {
            bool same=ReferenceEquals(existing.Student,student)&&existing.SessionKey==current.SessionKey&&existing.Subject==subject;
            reason=same?"The lesson is already in progress.":"Finish the current lesson first.";return same;
        }
        if(!RebirthTheorySpecialistResolver.TryResolve(student,instructor,subject,out var source,out _)||
            !RebirthTheoryProgressionService.TryPreviewNpcInstruction(student,instructor,subject,out var transfer,out _,out reason))return false;
        Sessions[student.entityId]=new Session{Student=student,World=student.world,Actor=current.ActorId,
            SessionKey=current.SessionKey,Creation=record.Origin.CreationId,Storage=identity.StorageKey,
            Subject=subject,Source=source,Instructor=instructor,Requested=transfer,Last=Time.realtimeSinceStartup};
        reason="Lesson started. Stay near the specialist for "+RebirthTeachingService.SessionDurationSeconds.ToString("0")+" seconds.";return true;
    }
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;float now=Time.realtimeSinceStartup;
        if(world==null||world.IsRemote()||!RebirthWorldCharacterRepository.IsServerAuthority){Reset();return;}
        if(!ReferenceEquals(activeWorld,world)){Reset();activeWorld=world;}
        if(now<nextTick)return;nextTick=now+0.25f;
        if(GameManager.Instance.IsPaused()){foreach(var session in Sessions.Values)session.Last=now;return;}
        foreach(var pair in new List<KeyValuePair<int,Session>>(Sessions))
        {
            var s=pair.Value;
            if(!ReferenceEquals(s.World,world)||!ReferenceEquals(world.GetEntity(pair.Key),s.Student)||
                !ReferenceEquals(RebirthNpcInteractionMutationHandlers.FindActorPlayer(s.Actor),s.Student)||
                RebirthNpcInteractionSessionService.Authorize(s.SessionKey,s.Actor,RebirthNpcInteractionAction.Dialogue,out var context)!=RebirthNpcInteractionResult.Allowed||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out var instructor)||instructor!=s.Instructor||
                !RebirthTheorySpecialistResolver.TryResolve(s.Student,instructor,s.Subject,out var source,out _)||source!=s.Source||
                !RebirthSkillAwardService.TryGetEligible(s.Student,out var identity,out var record)||record?.Progression==null||
                identity.StorageKey!=s.Storage||!RebirthSurvivorRequestScope.Matches(s.Creation,record.Origin?.CreationId)||
                record.Progression.PendingTheoryStudy!=null)
            {Cancel(pair.Key,s);continue;}
            float delta=now-s.Last;s.Last=now;
            // Never credit a loading/pause/stall gap or a backwards clock.
            if(delta>0f&&delta<=1f)s.Elapsed+=delta;
            if(s.Elapsed<RebirthTeachingService.SessionDurationSeconds)
            {
                if(s.Elapsed>=s.NextFeedback)
                {
                    s.NextFeedback+=5f;
                    RebirthTeachingService.NotifyStudy(s.Student,true,string.Format(Localization.Get("xuiRebirthStudyProgress"),
                        RebirthTeachingService.FriendlySkill(s.Subject),Math.Floor(s.Elapsed),RebirthTeachingService.SessionDurationSeconds));
                }
                continue;
            }
            lock(RebirthSkillKnowledgeService.SyncRoot)
            {
                if(!RebirthTheoryProgressionService.TryPreviewNpcInstruction(s.Student,s.Instructor,s.Subject,out var available,out _,out _)||
                    !record.Progression.SkillKnowledge.TryGetValue(s.Subject,out var theory)||theory==null)
                {Cancel(pair.Key,s);continue;}
                float transfer=Math.Min(s.Requested,available);
                record.Progression.TeachingHistory.TryGetValue("npc|"+s.Source+"|"+s.Subject,out var history);
                int count=history==null?1:history.CompletionCount==int.MaxValue?0:Math.Max(0,history.CompletionCount)+1;
                if(!RebirthTheoryStudyOutcome.TryCreate(Guid.NewGuid(),s.Creation,s.Subject,"npc",s.Source,theory.Value,
                    theory.Value+transfer,count,DateTime.UtcNow.Ticks,s.Elapsed,out var outcome))
                {Cancel(pair.Key,s);continue;}
                record.Progression.PendingTheoryStudy=outcome;record.Touch("specialist-lesson-complete");
                Sessions.Remove(pair.Key);
                if(!RebirthTheoryStudySettlement.TrySettle(s.Student,out _))
                    RebirthTeachingService.NotifyStudy(s.Student,false,Localization.Get("xuiRebirthStudyWaitingSave"));
            }
        }
    }
    // Reconcile completed durable intents for online players without creating new lessons.
    internal static void RetryPending()
    {
        var world=GameManager.Instance?.World;float now=Time.realtimeSinceStartup;
        if(world==null||world.IsRemote()||!RebirthWorldCharacterRepository.IsServerAuthority||now<nextRecovery)return;
        nextRecovery=now+1f;
        if(world.Players?.list==null)return;
        foreach(var player in world.Players.list)
            if(player!=null&&!player.IsDead()&&RebirthSkillAwardService.TryGetEligible(player,out _,out var record)&&record?.Progression?.PendingTheoryStudy!=null)
                RebirthTheoryStudySettlement.TrySettle(player,out _);
    }
    private static void Cancel(int key,Session session)
    {
        Sessions.Remove(key);
        if(ReferenceEquals(GameManager.Instance?.World,session.World)&&
            ReferenceEquals(session.World.GetEntity(key),session.Student)&&
            RebirthSkillAwardService.TryGetEligible(session.Student,out _,out var record)&&
            RebirthSurvivorRequestScope.Matches(session.Creation,record?.Origin?.CreationId))
            RebirthTeachingService.NotifyStudy(session.Student,false,Localization.Get("xuiRebirthStudyCancelled"));
    }
    internal static void Reset(){Sessions.Clear();nextTick=nextRecovery=0f;activeWorld=null;}
}