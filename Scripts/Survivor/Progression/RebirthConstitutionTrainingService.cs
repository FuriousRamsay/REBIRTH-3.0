using System;

#nullable disable

public static class RebirthConstitutionTrainingService
{
    public sealed class ExposureEvidence
    {
        public EntityPlayer Player;
        public string ItemId=string.Empty;
        public RebirthConstitutionExposureDefinition Definition;
        public bool EffectWasActive;
    }

    public static bool OnHealthRestored(EntityAlive patient,float actualHealthRestored)
    {
        EntityPlayer player=patient as EntityPlayer;
        if(player==null||actualHealthRestored<=0f||player.IsDead())return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return false;
        float maxHealth=player.Stats!=null?player.Stats.Health.Max:0f;
        if(maxHealth<=0f||float.IsNaN(maxHealth)||float.IsInfinity(maxHealth))return false;
        RebirthConstitutionTrainingRuntimeState state=EnsureState(record);
        double now=Math.Max(0d,record.Condition!=null?record.Condition.ActivePlaySeconds:0d);
        ResetWindowIfNeeded(state,now);
        float fraction=Math.Max(0f,Math.Min(actualHealthRestored,maxHealth))/maxHealth;
        float weighted=WeightedHealingIncrement(state.HealingFractionInWindow,fraction);
        state.HealingFractionInWindow=Math.Min(1.000001f,state.HealingFractionInWindow+fraction);
        float raw=weighted*RebirthProgressionRuntimeConfig.ConstitutionHealingConversion;
        return ApplyDirect(record,identity,state,now,raw,"constitution-direct-healing");
    }

    public static ExposureEvidence BeginExposure(EntityPlayer player,string itemId)
    {
        if(player==null||string.IsNullOrEmpty(itemId))return null;
        RebirthConstitutionExposureDefinition def;
        if(!RebirthProgressionRuntimeConfig.TryGetConstitutionExposure(itemId,out def)||def==null)return null;
        return new ExposureEvidence{Player=player,ItemId=itemId,Definition=def,EffectWasActive=HasBuff(player,def.EffectBuff)};
    }

    public static bool CompleteExposure(ExposureEvidence evidence)
    {
        if(evidence==null||evidence.Player==null||evidence.Definition==null||evidence.EffectWasActive)return false;
        if(!HasBuff(evidence.Player,evidence.Definition.EffectBuff))return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(evidence.Player,out identity,out record))return false;
        RebirthConstitutionTrainingRuntimeState state=EnsureState(record);
        double now=Math.Max(0d,record.Condition!=null?record.Condition.ActivePlaySeconds:0d);
        ResetWindowIfNeeded(state,now);
        double readyAt;
        if(state.ExposureFamilyReadyAtActiveSeconds.TryGetValue(evidence.Definition.FamilyId,out readyAt)&&now<readyAt)return false;
        bool awarded=ApplyDirect(record,identity,state,now,evidence.Definition.RawAward,"constitution-exposure:"+evidence.Definition.FamilyId);
        if(awarded)
        {
            state.ExposureFamilyReadyAtActiveSeconds[evidence.Definition.FamilyId]=now+RebirthProgressionRuntimeConfig.ConstitutionExposureCooldownActiveSeconds;
            record.Touch("constitution-exposure-cooldown:"+evidence.Definition.FamilyId);
            RebirthWorldCharacterRepository.SaveIfDirty(identity,"constitution-exposure-cooldown:"+evidence.Definition.FamilyId);
        }
        return awarded;
    }

    private static bool ApplyDirect(RebirthWorldCharacterRecord record,RebirthStablePlayerIdentity identity,RebirthConstitutionTrainingRuntimeState state,double now,float proposedRaw,string reason)
    {
        if(proposedRaw<=0f)return false;
        ResetWindowIfNeeded(state,now);
        float remaining=Math.Max(0f,RebirthProgressionRuntimeConfig.ConstitutionDirectWindowRawCap-state.RawDirectAwardInWindow);
        float raw=Math.Min(proposedRaw,remaining); if(raw<=0f)return false;
        float applied; if(!RebirthAttributeProgressionService.ApplyTraining(record,"constitution",raw,out applied))return false;
        state.RawDirectAwardInWindow=Math.Min(RebirthProgressionRuntimeConfig.ConstitutionDirectWindowRawCap,state.RawDirectAwardInWindow+raw);
        record.Touch(reason);
        return RebirthWorldCharacterRepository.SaveIfDirty(identity,reason);
    }

    private static RebirthConstitutionTrainingRuntimeState EnsureState(RebirthWorldCharacterRecord record)
    {
        if(record.Progression.ConstitutionTraining==null)record.Progression.ConstitutionTraining=new RebirthConstitutionTrainingRuntimeState();
        return record.Progression.ConstitutionTraining;
    }

    private static void ResetWindowIfNeeded(RebirthConstitutionTrainingRuntimeState state,double now)
    {
        if(state.DirectWindowStartedActiveSeconds<=0d||now<state.DirectWindowStartedActiveSeconds||now-state.DirectWindowStartedActiveSeconds>=RebirthProgressionRuntimeConfig.ConstitutionDirectWindowActiveSeconds)
        {
            state.DirectWindowStartedActiveSeconds=now; state.HealingFractionInWindow=0f; state.RawDirectAwardInWindow=0f;
        }
    }

    internal static float WeightedHealingIncrement(float already,float added)
    {
        float start=Math.Max(0f,already),end=Math.Max(start,start+Math.Max(0f,added)),weighted=0f;
        weighted+=Overlap(start,end,0f,.25f)*1f;
        weighted+=Overlap(start,end,.25f,.50f)*.50f;
        weighted+=Overlap(start,end,.50f,1f)*.20f;
        return weighted;
    }
    private static float Overlap(float a,float b,float lo,float hi){return Math.Max(0f,Math.Min(b,hi)-Math.Max(a,lo));}
    private static bool HasBuff(EntityAlive entity,string id){return entity!=null&&entity.Buffs!=null&&!string.IsNullOrEmpty(id)&&entity.Buffs.HasBuff(id);}
}
