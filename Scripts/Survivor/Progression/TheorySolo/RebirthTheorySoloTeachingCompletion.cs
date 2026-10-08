using System;

// Only the original durable completed player-to-player lesson can reach this producer.
internal static class RebirthTheorySoloTeachingCompletion
{
    internal static bool TryApply(RebirthTeachingDurableOutcome outcome,EntityPlayer instructor,RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord record)
    {
        if(outcome==null||outcome.OriginalOrdinal<1||!outcome.StudentApplied||!outcome.InstructorApplied||!RebirthTeachingOutcomeStore.TryGetOriginal(outcome,out var original)||!original.StudentApplied||!original.InstructorApplied)return false;
        var world=instructor?.world;
        var progression=record?.Progression;
        Func<bool> current=()=>world!=null&&!world.IsRemote()&&ReferenceEquals(world,GameManager.Instance?.World)&&instructor!=null&&!instructor.IsDead()&&ReferenceEquals(world.GetEntity(instructor.entityId),instructor)&&RebirthWorldCharacterRepository.IsServerAuthority&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&ReferenceEquals(record.Progression,progression)&&record.Origin?.CreationId==outcome.InstructorCreationId&&identity?.StorageKey==outcome.InstructorStorageKey&&RebirthSkillAwardService.TryGetEligible(instructor,out var owner,out var actual)&&owner.StorageKey==identity.StorageKey&&ReferenceEquals(actual,record);
        if(!current())return false;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            if(!current())return false;
            if(progression.SoloTheory==null)progression.SoloTheory=new RebirthTheorySoloState{CreationId=outcome.InstructorCreationId};
            var solo=progression.SoloTheory;if(solo.CreationId!=outcome.InstructorCreationId)return false;
            if(solo.TeachingOriginal==null)solo.TeachingOriginal=new RebirthTheorySoloTeachingRetirement();
            var retired=solo.TeachingOriginal;
            if(!retired.IsAcknowledged(outcome.OriginalOrdinal))
            {
                var next=retired.Clone();
                if(!next.TryAcknowledge(outcome.OriginalOrdinal,RebirthTeachingOutcomeStore.PendingOriginalOrdinals(identity.StorageKey,outcome.InstructorCreationId),out var already)||already)return false;
                solo.TeachingOriginal=next;
                // The saved continuous student Theory target is authored lesson relevance, never practical XP.
                solo.RecordAcknowledged(RebirthSurvivorIds.SkillTeaching,"social_transfer","teaching:"+outcome.OriginalOrdinal+":"+outcome.OutcomeId,outcome.StudentKnowledgeTarget,record.Condition.ActivePlaySeconds);
                record.Touch("solo-theory-original-teaching");
            }
            // Save uncertainty retains this exact retirement/evidence attempt; a retry cannot append evidence again.
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"solo-theory-original-teaching");}
            catch{return false;}
            if(!current()||!ReferenceEquals(progression.SoloTheory,solo)||!RebirthWorldCharacterRepository.HasSavedSoloTeachingOriginal(identity,solo)||!current())return false;
            if(!RebirthTeachingOutcomeStore.AcknowledgeSolo(outcome.OutcomeId))return false;
            outcome.SoloEvidenceApplied=true;return true;
        }
    }
}