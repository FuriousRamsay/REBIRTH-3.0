using System;

// Internal completion coordinator. Callers must validate timed lesson/evidence eligibility
// before reserving an outcome; this processor never treats an arbitrary request as a lesson.
internal static class RebirthTheoryStudySettlement
{
    internal static bool TrySettle(EntityPlayer student,out string reason)
    {
        reason="Study completion is waiting to be saved.";
        if(student==null||student.world==null||student.world.IsRemote()||student.IsDead()||
            !RebirthSkillAwardService.TryGetEligible(student,out var identity,out var record)||record?.Progression==null)return false;
        var world=student.world;string ownerKey=identity.StorageKey;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            var state=record.Progression;var outcome=state.PendingTheoryStudy;
            if(outcome==null||!RebirthTheoryStudyPersistence.MatchesOwner(outcome,record.Origin?.CreationId))return false;
            if(!state.SkillKnowledge.TryGetValue(outcome.SkillId,out var theory)||theory==null||
                float.IsNaN(theory.Value)||float.IsInfinity(theory.Value)||theory.Value<0||theory.Value>100)return false;
            if(outcome.Mode=="solo"&&!state.SkillAwardReceipts.Contains(outcome.Receipt)&&theory.Value!=outcome.Before)return false;
            Func<bool,bool> current=cleared=>ReferenceEquals(student.world,world)&&!world.IsRemote()&&!student.IsDead()&&
                RebirthSkillAwardService.TryGetEligible(student,out var liveIdentity,out var liveRecord)&&ReferenceEquals(liveRecord,record)&&
                liveIdentity!=null&&liveIdentity.StorageKey==ownerKey&&ReferenceEquals(record.Progression,state)&&
                RebirthTheoryStudyPersistence.MatchesOwner(outcome,record.Origin?.CreationId)&&
                (cleared?state.PendingTheoryStudy==null:ReferenceEquals(state.PendingTheoryStudy,outcome));
            if(!current(false)||!RebirthTheorySoloService.ValidateOutcome(state.SoloTheory,outcome,state.SkillAwardReceipts.Contains(outcome.Receipt),false))return false;
            try
            {
                // Persist the completion intent before any Theory/history effect.
                if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-prepare")||
                    !RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,false)||!current(false))return false;
                if(!state.SkillAwardReceipts.Contains(outcome.Receipt))
                {
                    if(outcome.Mode=="solo")state.SoloTheory.Session.Consumed=true;
                    theory.Value=Math.Max(theory.Value,outcome.Target);
                    if(!state.TeachingHistory.TryGetValue(outcome.HistoryKey,out var history)||history==null)
                        state.TeachingHistory[outcome.HistoryKey]=history=new RebirthTeachingHistoryRuntimeState
                        {Key=outcome.HistoryKey,StudentStorageKey=identity.StorageKey,SkillId=outcome.SkillId};
                    history.CompletionCount=Math.Max(history.CompletionCount,outcome.HistoryCount);
                    history.LastCompletedUtcTicks=Math.Max(history.LastCompletedUtcTicks,outcome.CompletedTicks);
                    state.SkillAwardReceipts.Add(outcome.Receipt);
                    RebirthTeachingOutcomeStore.PruneAwardReceipts(state.SkillAwardReceipts,outcome.Receipt);
                    record.Touch("theory-study-apply:"+outcome.SkillId);
                }
                if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-apply")||
                    !RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,true)||!current(false))return false;
                var originalSolo=outcome.Mode=="solo"?state.SoloTheory.Clone():null;
                if(originalSolo!=null){var solo=state.SoloTheory;var session=solo.Session;solo.Evidence.RemoveAll(e=>e.Subject==session.Subject&&session.Ordinals.Contains(e.Ordinal));solo.LastSettlement[session.Subject]=record.Condition.ActivePlaySeconds;solo.LastSettledId=outcome.Id;solo.Session=null;}
                state.PendingTheoryStudy=null;record.Touch("theory-study-settle:"+outcome.SkillId);
                bool saved=false;
                try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"theory-study-settle");saved=current(true)&&RebirthWorldCharacterRepository.HasSavedTheoryStudy(identity,outcome,true,true)&&current(true);}
                finally{if(!saved&&state.PendingTheoryStudy==null){state.PendingTheoryStudy=outcome;if(originalSolo!=null)state.SoloTheory=originalSolo;record.Touch("theory-study-settle-retry");}}
                if(!saved||!current(true))return false;
                RebirthSkillAwardService.QueueOwnerPublication(student);
                                RebirthTeachingService.NotifyStudy(student,true,string.Format(Localization.Get("xuiRebirthStudyCompleted"),
                    RebirthTeachingService.FriendlySkill(outcome.SkillId),(outcome.Target-outcome.Before).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)));
                reason=string.Empty;return true;
            }
            catch{return false;}
        }
    }
}