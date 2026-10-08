using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class RebirthTheorySoloService
{
    private sealed class Binding { internal EntityPlayer Player;internal World World;internal RebirthWorldCharacterRecord Record;internal RebirthWorldProgressionState Progression;internal string Creation,Storage,Session;internal float Last,Health;internal Vector3 Position; }
    private static readonly Dictionary<int,Binding> Active=new Dictionary<int,Binding>();
    private static bool installed;
    internal static void Install(){if(installed)return;ModEvents.GameUpdate.RegisterHandler(OnUpdate);installed=true;}
    internal static bool IsRunning(EntityPlayer player,string id)=>player!=null&&Active.TryGetValue(player.entityId,out var b)&&ReferenceEquals(b.Player,player)&&ReferenceEquals(b.World,player.world)&&b.Session==id;
    internal static bool IsRunning(EntityPlayer player)=>player!=null&&Active.TryGetValue(player.entityId,out var b)&&ReferenceEquals(b.Player,player)&&ReferenceEquals(b.World,player.world)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(b.Record)&&ReferenceEquals(b.Record.Progression,b.Progression)&&b.Record.Origin.CreationId==b.Creation&&b.Progression.SoloTheory?.Session?.Id==b.Session;
    internal static bool HasLiteratureStudy(EntityPlayer player)=>RebirthLiteratureStudySessionService.TryGetUiState(player,out _,out _,out _,out _)||RebirthAudiobookListeningSessionService.TryGetUiState(player,out _,out _,out _,out _);
    internal static void PauseForOtherStudy(EntityPlayer player){if(IsRunning(player))Active.Remove(player.entityId);}
    internal static bool TryBegin(EntityPlayer player,string subject,out string reason)
    {
        reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryNeedWork","Complete more relevant work before studying this skill.");
        if(player==null||player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthWorldCharacterRepository.IsServerAuthority||player.IsDead()||RebirthBackpackLibraryReservation.BlocksResourceUse(player)||!RebirthTheorySoloRegistry.TryGet(subject,out var rule)||!RebirthSkillAwardService.TryGetEligible(player,out var identity,out var record)||record?.Progression==null||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
        if(HasLiteratureStudy(player)||RebirthTheorySpecialistLessonService.IsActive(player)){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryFinishCurrent","Finish your current study session first.");return false;}
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var p=record.Progression;var solo=p.SoloTheory;
            if(!string.IsNullOrEmpty(solo?.CancelPendingId)){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelWaiting","Cancellation is waiting to be saved.");return false;}
            if(solo==null||solo.CreationId!=record.Origin.CreationId||!string.IsNullOrEmpty(solo.CancelPendingId)||p.PendingTheoryStudy!=null||!p.SkillKnowledge.TryGetValue(subject,out var theory)||theory==null)return false;
            if(solo.Session!=null&&solo.Session.Subject==subject&&!Eligible(solo,solo.Session,theory.Value,rule))return false;
            if(solo.Session!=null&&solo.Session.Subject!=subject){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryFinishCurrent","Finish your current study session first.");return false;}
            if(solo.Session==null)
            {
                if(solo.LastSettlement.TryGetValue(subject,out var completed)&&record.Condition.ActivePlaySeconds-completed<rule.Cooldown){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryPracticeFirst","Put what you learned into practice before studying again.");return false;}
                var evidence=solo.Evidence.Where(e=>e.Subject==subject&&rule.Relevant(theory.Value,e.Difficulty)).OrderByDescending(e=>e.Ordinal).Take(rule.MinimumOutcomes).ToArray();
                if(evidence.Length!=rule.MinimumOutcomes)return false;
                var session=new RebirthTheorySoloSession{Id=Guid.NewGuid().ToString("N"),Subject=subject,Duration=rule.Duration,StartedActive=record.Condition.ActivePlaySeconds};session.Ordinals.AddRange(evidence.Select(e=>e.Ordinal));solo.Session=session;record.Touch("solo-theory-reserve");
            }
            RebirthWorldCharacterRepository.SaveIfDirty(identity,"solo-theory-reserve");
            if(!RebirthWorldCharacterRepository.HasSavedSoloSession(identity,solo)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||!ReferenceEquals(record.Progression,p)||!ReferenceEquals(p.SoloTheory,solo)||!ReferenceEquals(player.world,GameManager.Instance?.World)) {reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryWaitingSave","Study is waiting to be saved.");return false;}
            Active[player.entityId]=new Binding{Player=player,World=player.world,Record=record,Progression=p,Creation=record.Origin.CreationId,Storage=identity.StorageKey,Session=solo.Session.Id,Last=Time.realtimeSinceStartup,Health=player.Stats.Health.Value,Position=player.position};
            reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryStarted","Study started. Stay still and safe while you reflect on your work.");return true;
        }
    }
    internal static bool TryCancel(EntityPlayer player,string subject,string sessionId,out string reason)
    {
        reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelUnavailable","This study can no longer be cancelled.");
        if(player==null||player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthWorldCharacterRepository.IsServerAuthority||player.IsDead()||!RebirthSkillAwardService.TryGetEligible(player,out var identity,out var record)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var world=player.world;var p=record.Progression;var solo=p.SoloTheory;var session=solo?.Session;var pending=p.PendingTheoryStudy;
            if(solo?.CreationId!=record.Origin.CreationId)return false;
            if(!string.IsNullOrEmpty(solo.CancelPendingId))
            {if(solo.CancelPendingId!=sessionId||solo.CancelPendingSubject!=subject||session!=null||pending!=null)return false;}
            else
            {
                if(session==null||session.Id!=sessionId||session.Subject!=subject||session.Consumed||pending!=null&&(pending.Mode!="solo"||pending.Id!=sessionId||p.SkillAwardReceipts.Contains(pending.Receipt)))return false;
                Active.Remove(player.entityId);solo.Session=null;solo.LastCancelledId=sessionId;solo.CancelPendingId=sessionId;solo.CancelPendingSubject=subject;p.PendingTheoryStudy=null;record.Touch("solo-theory-cancel-intent");
            }
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"solo-theory-cancel-prepare");if(!RebirthWorldCharacterRepository.HasSavedSoloCancellationHold(identity,solo)){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelWaiting","Cancellation is waiting to be saved.");return false;}}
            catch{reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelWaiting","Cancellation is waiting to be saved.");return false;}
            // Once cancellation is requested, uncertainty is a hold, never permission to resume.
            solo.CancelPendingId=null;solo.CancelPendingSubject=null;record.Touch("solo-theory-cancel-terminal");
            Func<bool> current=()=>ReferenceEquals(player.world,world)&&!world.IsRemote()&&!player.IsDead()&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&ReferenceEquals(record.Progression,p)&&ReferenceEquals(p.SoloTheory,solo)&&record.Origin.CreationId==solo.CreationId&&solo.Session==null&&solo.LastCancelledId==sessionId&&p.PendingTheoryStudy==null;
            bool saved=false;
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"solo-theory-cancel");saved=current()&&RebirthWorldCharacterRepository.HasSavedSoloCancellation(identity,solo.CreationId,sessionId)&&current();}
            catch{}
            finally{if(!saved&&current()){solo.CancelPendingId=sessionId;solo.CancelPendingSubject=subject;record.Touch("solo-theory-cancel-hold");}}
            if(!saved){reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelWaiting","Cancellation is waiting to be saved.");return false;}
            RebirthSkillAwardService.QueueOwnerPublication(player);reason=RebirthSurvivorUiText.L("xuiRebirthSoloTheoryCancelled","Study cancelled. Your completed work remains available.");return true;
        }
    }
    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;float now=Time.realtimeSinceStartup;
        foreach(var pair in Active.ToArray())
        {
            var b=pair.Value;float delta=now-b.Last;b.Last=now;
            if(world==null||world.IsRemote()||!ReferenceEquals(world,b.World)||b.Player.IsDead()||!ReferenceEquals(world.GetEntity(pair.Key),b.Player)||!RebirthSkillAwardService.TryGetEligible(b.Player,out var identity,out var record)||!ReferenceEquals(record,b.Record)||!ReferenceEquals(record.Progression,b.Progression)||identity.StorageKey!=b.Storage||record.Origin.CreationId!=b.Creation||record.Progression.SoloTheory?.Session?.Id!=b.Session){Active.Remove(pair.Key);continue;}
            if(HasLiteratureStudy(b.Player)||RebirthTheorySpecialistLessonService.IsActive(b.Player)){Active.Remove(pair.Key);continue;}
            float health=b.Player.Stats.Health.Value;bool interrupted=health+.01f<b.Health||(b.Player.position-b.Position).sqrMagnitude>.0025f;b.Health=health;b.Position=b.Player.position;
            if(interrupted){Active.Remove(pair.Key);RebirthTeachingService.NotifyStudy(b.Player,false,RebirthSurvivorUiText.L("xuiRebirthSoloTheoryPaused","Study paused. Select Reflect again when you are safe and still."));continue;}
            if(GameManager.Instance.IsPaused()||delta<=0||delta>1||RebirthBackpackLibraryReservation.BlocksResourceUse(b.Player))continue;
            lock(RebirthSkillKnowledgeService.SyncRoot)
            {
                var p=record.Progression;var solo=p.SoloTheory;var s=solo.Session;
                if(p.PendingTheoryStudy!=null){if(p.PendingTheoryStudy.Id==s.Id)RebirthTheoryStudySettlement.TrySettle(b.Player,out _);continue;}
                if(!RebirthTheorySoloRegistry.TryGet(s.Subject,out var rule)||!p.SkillKnowledge.TryGetValue(s.Subject,out var theory)||theory==null||!Eligible(solo,s,theory.Value,rule)){Active.Remove(pair.Key);continue;}
                s.Elapsed=Math.Min(s.Duration,s.Elapsed+delta);record.Touch("solo-theory-progress");
                if(s.Elapsed<s.Duration)continue;
                p.TeachingHistory.TryGetValue("solo|reflection|"+s.Subject,out var history);int count=history==null?1:history.CompletionCount==int.MaxValue?0:history.CompletionCount+1;
                if(!RebirthTheoryStudyOutcome.TryCreate(Guid.ParseExact(s.Id,"N"),b.Creation,s.Subject,"solo","reflection",theory.Value,Math.Min(100,theory.Value+rule.Gain),count,DateTime.UtcNow.Ticks,s.Elapsed,out var outcome)){Active.Remove(pair.Key);continue;}
                p.PendingTheoryStudy=outcome;record.Touch("solo-theory-complete");
                if(RebirthTheoryStudySettlement.TrySettle(b.Player,out _))Active.Remove(pair.Key);
            }
        }
    }
    private static bool Eligible(RebirthTheorySoloState state,RebirthTheorySoloSession session,float theory,RebirthTheorySoloRule rule)
    {
        if(state==null||session==null||session.Subject!=rule.Subject||session.Duration!=rule.Duration||session.Ordinals.Count!=rule.MinimumOutcomes||session.Ordinals.Distinct().Count()!=session.Ordinals.Count)return false;
        return session.Ordinals.All(o=>state.Evidence.Any(e=>e.Subject==session.Subject&&e.Ordinal==o&&e.Family==rule.Family&&rule.Relevant(theory,e.Difficulty)));
    }
    internal static bool ValidateOutcome(RebirthTheorySoloState state,RebirthTheoryStudyOutcome outcome,bool applied,bool settled)
    {
        if(outcome.Mode!="solo")return true;
        if(state==null||state.LastCancelledId==outcome.Id||state.CreationId!=outcome.CreationId||outcome.SourceId!="reflection")return false;
        if(settled)return state.Session==null&&state.LastSettledId==outcome.Id;
        var s=state.Session;
        return RebirthTheorySoloRegistry.TryGet(outcome.SkillId,out var rule)&&Eligible(state,s,outcome.Before,rule)&&outcome.Target==Math.Min(100,outcome.Before+rule.Gain)&&s!=null&&s.Id==outcome.Id&&s.Subject==outcome.SkillId&&s.Elapsed>=s.Duration&&s.Duration==outcome.LessonSeconds&&(!applied||s.Consumed)&&s.Ordinals.All(o=>state.Evidence.Any(e=>e.Subject==s.Subject&&e.Ordinal==o));
    }
}
