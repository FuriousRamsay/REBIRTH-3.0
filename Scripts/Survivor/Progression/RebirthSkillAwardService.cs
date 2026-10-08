using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthSkillAwardService
{
    private static readonly object Gate=new object();
    private static readonly Dictionary<string,WindowAmount> WindowAwards=new Dictionary<string,WindowAmount>(StringComparer.Ordinal);
    private static readonly Dictionary<int,EntityPlayer> PendingOwnerPublications=new Dictionary<int,EntityPlayer>();
    private static float nextOwnerPublicationAttempt;
    private struct WindowAmount { public float Started; public float Amount; }

    public static bool TryAwardTrainingEvidence(EntityPlayer player,RebirthSkillTrainingEvidence evidence,out RebirthSkillTrainingComputation computation,out float appliedSkillProgress,out float appliedAttribute)
    {
        computation=null; appliedSkillProgress=0f; appliedAttribute=0f;
        if(!RebirthSkillTrainingProfileRegistry.LiveAwardsFlag)return false;
        return TryAwardMigratedTrainingEvidence(player,evidence,out computation,out appliedSkillProgress,out appliedAttribute);
    }

    /// <summary>
    /// Route-explicit live award entry for a Skill family that has already been migrated to the
    /// unified evidence model. The global live_awards switch remains false until all routes are
    /// migrated, so Phase-specific systems must opt in here only after proving authoritative
    /// evidence and a LOCKED training profile.
    /// </summary>
    public static bool TryAwardMigratedTrainingEvidence(EntityPlayer player,RebirthSkillTrainingEvidence evidence,out RebirthSkillTrainingComputation computation,out float appliedSkillProgress,out float appliedAttribute)
    {
        computation=null; appliedSkillProgress=0f; appliedAttribute=0f;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(evidence==null||!TryGetEligible(player,out identity,out record))return false;
        RebirthSkillRuntimeState state; if(!record.Progression.Skills.TryGetValue(evidence.SkillId??string.Empty,out state)||state==null)return false;
        float practical=state.Value+state.Progress;
        if(!RebirthSkillTrainingCalculator.TryCalculate(evidence,practical,out computation)||computation==null)return false;
        return TryAwardInternal(player,evidence.SkillId,computation.RawBeforeLearningModifiers,evidence.SourceKey,evidence.MinimumInterval,evidence.DurableReceiptId,out appliedSkillProgress,out appliedAttribute);
    }

    /// <summary>Read-only unified evidence preview. It uses the same calculator and Skill projection path as the live award API.</summary>
    public static bool TryPreviewTrainingEvidence(EntityPlayer player,RebirthSkillTrainingEvidence evidence,out RebirthSkillTrainingComputation computation)
    {
        return RebirthSkillTrainingProjectionService.TryPreview(player,evidence,out computation);
    }

    public static bool TryAward(EntityPlayer player,string skillId,float rawProgress,string sourceKey)
    {
        float ignoredSkill,ignoredAttribute;
        return TryAward(player,skillId,rawProgress,sourceKey,0f,out ignoredSkill,out ignoredAttribute);
    }

    public static bool TryAward(EntityPlayer player,string skillId,float rawProgress,string sourceKey,float minimumInterval,
        out float appliedSkillProgress,out float appliedAttribute)
    {
        return TryAwardInternal(player,skillId,rawProgress,sourceKey,minimumInterval,string.Empty,out appliedSkillProgress,out appliedAttribute);
    }

    public static bool TryAwardDurable(EntityPlayer player,string skillId,float rawProgress,string sourceKey,float minimumInterval,string durableReceiptId,
        out float appliedSkillProgress,out float appliedAttribute)
    {
        appliedSkillProgress=0f; appliedAttribute=0f;
        if(string.IsNullOrEmpty(durableReceiptId))return false;
        return TryAwardInternal(player,skillId,rawProgress,sourceKey,minimumInterval,durableReceiptId,out appliedSkillProgress,out appliedAttribute);
    }

    private static bool TryAwardInternal(EntityPlayer player,string skillId,float rawProgress,string sourceKey,float minimumInterval,string durableReceiptId,
        out float appliedSkillProgress,out float appliedAttribute,
        RebirthCraftTrainingRules.Model? trainingModel=null,int trainingCycles=1,float preparationMultiplier=1f)
    {
        appliedSkillProgress=0f; appliedAttribute=0f;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!TryGetEligible(player,out identity,out record))return false;
        if(!string.IsNullOrEmpty(durableReceiptId) && record.Progression.SkillAwardReceipts.Contains(durableReceiptId))
        {
            // The reward and receipt live in the same player snapshot. If a previous write failed
            // but the in-memory record remains dirty, retry that exact snapshot before acking.
            if(record.Dirty && !RebirthWorldCharacterRepository.SaveIfDirty(identity,"skill-award-receipt-retry:"+skillId))return false;
            lock(Gate) PendingOwnerPublications[player.entityId]=player;
            return true;
        }
        if(!IsFinite(rawProgress) || rawProgress<0f || string.IsNullOrEmpty(skillId) ||
            rawProgress==0f && string.IsNullOrEmpty(durableReceiptId))return false;
        RebirthSkillDefinition def; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out def)||def==null)return false;
        RebirthSkillRuntimeState state; if(!record.Progression.Skills.TryGetValue(skillId,out state)||state==null)return false;
        if(state.Value>=def.Max || rawProgress==0f)
        {
            if(string.IsNullOrEmpty(durableReceiptId))return false;
            record.Progression.SkillAwardReceipts.Add(durableReceiptId);PruneDurableReceipts(record.Progression.SkillAwardReceipts,record.Progression.PendingTheoryStudy?.Receipt);
            record.Touch("skill-award-max-receipt:"+skillId);
            return RebirthWorldCharacterRepository.SaveIfDirty(identity,"skill-award-max-receipt:"+skillId);
        }

        string cooldownKey=skillId+"|"+(sourceKey??string.Empty);
        double nowActive=record.Condition!=null?Math.Max(0d,record.Condition.ActivePlaySeconds):0d;
        lock(Gate)
        {
            RebirthSkillAntiRepeatRuntimeState antiRepeat=record.Progression.SkillAntiRepeat ?? (record.Progression.SkillAntiRepeat=new RebirthSkillAntiRepeatRuntimeState());
            double readyAt;
            if(minimumInterval>0f && antiRepeat.AwardReadyAtActiveSeconds.TryGetValue(cooldownKey,out readyAt) && readyAt>nowActive+0.000001d)return false;
            float beforeValue=state.Value, beforeProgress=state.Progress;
            float nextValue, nextProgress;
            bool projected=trainingModel.HasValue
                ? TryProjectCraft(player,record,skillId,def,state.Value,state.Progress,trainingModel.Value,trainingCycles,preparationMultiplier,out nextValue,out nextProgress)
                : TryProject(player,record,skillId,def,state.Value,state.Progress,rawProgress,out nextValue,out nextProgress);
            if(!projected)return false;
            state.Value = nextValue;
            state.Progress = nextProgress;
            appliedSkillProgress=(state.Value-beforeValue)+(state.Progress-beforeProgress);
            if(!IsFinite(appliedSkillProgress))return false;
            if(appliedSkillProgress<=0f)
            {
                if(string.IsNullOrEmpty(durableReceiptId))return false;
                record.Progression.SkillAwardReceipts.Add(durableReceiptId);PruneDurableReceipts(record.Progression.SkillAwardReceipts,record.Progression.PendingTheoryStudy?.Receipt);
                record.Touch("skill-award-zero-receipt:"+skillId);
                return RebirthWorldCharacterRepository.SaveIfDirty(identity,"skill-award-zero-receipt:"+skillId);
            }
            // Repeat suppression is consumed only by an accepted mutation and is persisted on
            // the character's active-play clock. Restart/reconnect therefore cannot reset it,
            // while time spent offline does not consume the window.
            if(minimumInterval>0f)
            {
                antiRepeat.AwardReadyAtActiveSeconds[cooldownKey]=nowActive+minimumInterval;
                PruneSkillAwardCooldowns(antiRepeat.AwardReadyAtActiveSeconds,nowActive);
            }
            if(!string.IsNullOrEmpty(durableReceiptId))
            {
                record.Progression.SkillAwardReceipts.Add(durableReceiptId);
                PruneDurableReceipts(record.Progression.SkillAwardReceipts,record.Progression.PendingTheoryStudy?.Receipt);
            }

            string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(skillId);
            if(attributeId.Length>0)
                RebirthAttributeProgressionService.ApplyTraining(record,attributeId,appliedSkillProgress*RebirthProgressionRuntimeConfig.SkillProgressToAttribute,out appliedAttribute);
            record.Touch("skill-award:"+skillId);
        }
        // Durability remains synchronous at the accepted logical event boundary. Owner-state
        // projection is safe to coalesce to one latest-revision body per game-update frame.
        bool persisted=RebirthWorldCharacterRepository.SaveIfDirty(identity,"skill-award:"+skillId);
        if(!string.IsNullOrEmpty(durableReceiptId) && !persisted)return false;
        lock(Gate) PendingOwnerPublications[player.entityId]=player;
        RebirthStatisticsService.RecordSkillProgress(player,skillId,appliedSkillProgress);
        return true;
    }

    /// <summary>Read-only preview; it does not consume repeat suppression, write skill state or grant XP.</summary>
    public static bool TryPreview(EntityPlayer player, string skillId, float rawProgress,
        out float currentValue, out float expectedGain)
    {
        currentValue = expectedGain = 0f;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        RebirthSkillDefinition definition; RebirthSkillRuntimeState state;
        if (!TryGetEligible(player, out identity, out record) || string.IsNullOrEmpty(skillId) ||
            !IsFinite(rawProgress) || rawProgress < 0f ||
            !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out definition) || definition == null ||
            !record.Progression.Skills.TryGetValue(skillId, out state) || state == null) return false;
        currentValue = state.Value + state.Progress;
        if (state.Value >= definition.Max || rawProgress == 0f) return true;
        float nextValue, nextProgress;
        if (!TryProject(player, record, skillId, definition, state.Value, state.Progress,
            rawProgress, out nextValue, out nextProgress)) return false;
        expectedGain = Math.Max(0f, (nextValue - state.Value) + (nextProgress - state.Progress));
        return true;
    }

    private static bool TryProject(EntityPlayer player, RebirthWorldCharacterRecord record, string skillId,
        RebirthSkillDefinition definition, float beforeValue, float beforeProgress, float rawProgress,
        out float nextValue, out float nextProgress)
    {
        nextValue = beforeValue; nextProgress = beforeProgress;
        float support = RebirthTraitSupportService.GetSkillGainMultiplier(record, beforeValue);
        float trait = RebirthTraitGameplayModifierService.GetSkillGainMultiplier(player, record, skillId, beforeValue);
        float lesson = RebirthTeachingService.GetSubjectLearningMultiplier(player, skillId);
        if (!IsFinite(support) || !IsFinite(trait) || !IsFinite(lesson)) return false;
        float total = Math.Max(0f, beforeProgress) + rawProgress * support * trait * lesson;
        if (!IsFinite(total)) return false;
        while (total >= 1f && nextValue < definition.Max) { nextValue += 1f; total -= 1f; }
        nextValue = Math.Max(definition.Min, Math.Min(definition.Max, nextValue));
        nextProgress = nextValue >= definition.Max ? 0f : Math.Max(0f, Math.Min(0.999999f, total));
        return true;
    }

    public static bool TryAwardCraft(EntityPlayer player,string skillId,RebirthCraftTrainingRules.Model model,int cycles,
        float preparationMultiplier,string source,string receipt="")
    {
        if(!model.Valid || cycles<1 || cycles>9999 || !IsFinite(preparationMultiplier) || preparationMultiplier<0f || preparationMultiplier>10f)return false;
        float gained,attribute;
        return TryAwardInternal(player,skillId,model.Raw,source,0f,receipt,out gained,out attribute,model,cycles,preparationMultiplier);
    }
    public static bool TryPreviewCraft(EntityPlayer player,string skillId,RebirthCraftTrainingRules.Model model,int cycles,
        float preparationMultiplier,out float current,out float gain)
    {
        current=gain=0f;
        if(!model.Valid||cycles<1||cycles>9999||!IsFinite(preparationMultiplier)||preparationMultiplier<0f||preparationMultiplier>10f)return false;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
        RebirthSkillDefinition definition;RebirthSkillRuntimeState state;
        if(!TryGetEligible(player,out identity,out record)||
            !RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out definition)||definition==null||
            !record.Progression.Skills.TryGetValue(skillId,out state)||state==null)return false;
        current=state.Value+state.Progress;
        float nextValue,nextProgress;
        if(!TryProjectCraft(player,record,skillId,definition,state.Value,state.Progress,model,cycles,preparationMultiplier,out nextValue,out nextProgress))return false;
        gain=Math.Max(0f,(nextValue-state.Value)+(nextProgress-state.Progress));return true;
    }
    private static bool TryProjectCraft(EntityPlayer player,RebirthWorldCharacterRecord record,string skillId,
        RebirthSkillDefinition definition,float value,float progress,RebirthCraftTrainingRules.Model model,int cycles,float preparationMultiplier,
        out float nextValue,out float nextProgress)
    {
        nextValue=value;nextProgress=progress;
        float cachedLevel=float.NaN,multiplier=0f;
        for(int i=0;i<cycles && nextValue<definition.Max;i++)
        {
            if(nextValue!=cachedLevel)
            {
                cachedLevel=nextValue;
                multiplier=RebirthTraitSupportService.GetSkillGainMultiplier(record,nextValue)*
                    RebirthTraitGameplayModifierService.GetSkillGainMultiplier(player,record,skillId,nextValue)*
                    RebirthTeachingService.GetSubjectLearningMultiplier(player,skillId);
                if(!IsFinite(multiplier)||multiplier<0f)return false;
            }
            float raw=RebirthCraftTrainingRules.Practice(model,nextValue+nextProgress)*preparationMultiplier;
            if(!IsFinite(raw)||raw<0f)return false;
            float total=Math.Max(0f,nextProgress)+raw*multiplier;
            if(!IsFinite(total))return false;
            if(total==nextProgress)break;
            while(total>=1f && nextValue<definition.Max){nextValue+=1f;total-=1f;}
            nextValue=Math.Max(definition.Min,Math.Min(definition.Max,nextValue));
            nextProgress=nextValue>=definition.Max?0f:Math.Max(0f,Math.Min(0.999999f,total));
        }
        return true;
    }

    public static float ClampCombatAward(string skillId,RebirthStablePlayerIdentity identity,float proposed)
    {
        if(!IsFinite(proposed) || proposed<=0f)return 0f;
        // Chunk-7 weapon families are already normalized to sustained DPS and are batched across
        // the short combat window, so the legacy shotgun pellet cap must not distort them.
        if(RebirthWeaponFamilySkillService.IsChunk7Skill(skillId))return proposed;
        if(!string.Equals(skillId,"skill.shotguns",StringComparison.OrdinalIgnoreCase) || identity==null)return proposed;
        string key=identity.StorageKey+"|shotgun-window"; float now=Time.realtimeSinceStartup;
        lock(Gate)
        {
            WindowAmount w;
            if(!WindowAwards.TryGetValue(key,out w) || now-w.Started>=RebirthProgressionRuntimeConfig.ShotgunWindowSeconds) w=new WindowAmount{Started=now,Amount=0f};
            float allowed=Math.Max(0f,RebirthProgressionRuntimeConfig.ShotgunWindowMax-w.Amount);
            return Math.Min(Math.Max(0f,proposed),allowed);
        }
    }

    public static bool TryAwardCombat(EntityPlayer player,string skillId,float proposed,string sourceKey,float minimumInterval,
        out float appliedSkillProgress,out float appliedAttribute)
    {
        appliedSkillProgress=0f;appliedAttribute=0f;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
        if(!TryGetEligible(player,out identity,out record))return false;
        float accepted=ClampCombatAward(skillId,identity,proposed);
        if(accepted<=0f)return false;
        if(!TryAward(player,skillId,accepted,sourceKey,minimumInterval,out appliedSkillProgress,out appliedAttribute))return false;
        if(!RebirthWeaponFamilySkillService.IsChunk7Skill(skillId) && string.Equals(skillId,"skill.shotguns",StringComparison.OrdinalIgnoreCase))
        {
            string key=identity.StorageKey+"|shotgun-window";float now=Time.realtimeSinceStartup;
            lock(Gate)
            {
                WindowAmount w;if(!WindowAwards.TryGetValue(key,out w)||now-w.Started>=RebirthProgressionRuntimeConfig.ShotgunWindowSeconds)w=new WindowAmount{Started=now,Amount=0f};
                w.Amount=Math.Min(RebirthProgressionRuntimeConfig.ShotgunWindowMax,w.Amount+accepted);WindowAwards[key]=w;
            }
        }
        return true;
    }

    private static void PruneSkillAwardCooldowns(Dictionary<string,double> cooldowns,double nowActive)
    {
        if(cooldowns==null)return;
        if(cooldowns.Count>384)
        {
            List<string> expired=new List<string>();
            foreach(KeyValuePair<string,double> pair in cooldowns)
                if(pair.Value<=nowActive)expired.Add(pair.Key);
            for(int i=0;i<expired.Count;i++)cooldowns.Remove(expired[i]);
        }
        while(cooldowns.Count>512)
        {
            string oldestKey=null;double oldest=double.MaxValue;
            foreach(KeyValuePair<string,double> pair in cooldowns)
                if(pair.Value<oldest){oldest=pair.Value;oldestKey=pair.Key;}
            if(string.IsNullOrEmpty(oldestKey))break;
            cooldowns.Remove(oldestKey);
        }
    }

    private static void PruneDurableReceipts(HashSet<string> receipts,string protectedReceipt)
    {
        RebirthTeachingOutcomeStore.PruneAwardReceipts(receipts,protectedReceipt);
    }

    private static bool IsFinite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}

    public static void QueueOwnerPublication(EntityPlayer player)
    {
        if (player == null || player.world == null || player.world.IsRemote()) return;
        lock (Gate) PendingOwnerPublications[player.entityId] = player;
    }
    public static void FlushOwnerPublications()
    {
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(world==null||world.IsRemote()||Time.realtimeSinceStartup<nextOwnerPublicationAttempt)return;
        KeyValuePair<int,EntityPlayer>[] pending;
        lock(Gate)
        {
            if(PendingOwnerPublications.Count==0)return;
            nextOwnerPublicationAttempt=Time.realtimeSinceStartup+1f;
            pending=new List<KeyValuePair<int,EntityPlayer>>(PendingOwnerPublications).ToArray();PendingOwnerPublications.Clear();
        }
        for(int i=0;i<pending.Length;i++)
        {
            EntityPlayer player=world.GetEntity(pending[i].Key) as EntityPlayer;
            if(player==null||!ReferenceEquals(player,pending[i].Value)||!ReferenceEquals(player.world,world))continue;
            RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null||
                !RebirthWorldCharacterRepository.TryGet(identity,out record)||record==null)continue;
            if(record.Dirty && !RebirthWorldCharacterRepository.SaveIfDirty(identity,"owner-publication-retry"))
            {
                if(ReferenceEquals(GameManager.Instance.World,world))
                    lock(Gate) PendingOwnerPublications[player.entityId]=player;
                continue;
            }
            if(!RebirthSurvivorNetworkService.SendOwnerState(player,record.Revision,true,"skill-award-coalesced"))
                if(ReferenceEquals(GameManager.Instance.World,world))
                    lock(Gate) PendingOwnerPublications[player.entityId]=player;
        }
    }

    public static void ClearRuntimeAntiRepeat()
    { lock(Gate){WindowAwards.Clear();PendingOwnerPublications.Clear();nextOwnerPublicationAttempt=0f;} }

    internal static bool TryGetEligible(EntityPlayer player,out RebirthStablePlayerIdentity identity,out RebirthWorldCharacterRecord record)
    {
        identity=null;record=null;
        if(player==null || player.world==null || player.world.IsRemote())return false;
        if(!RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        if(RebirthCharacterCreationHoldService.IsHeld(player))return false;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null)return false;
        return RebirthWorldCharacterRepository.TryGet(identity,out record) && record!=null && record.IsComplete && record.Progression!=null;
    }
}
