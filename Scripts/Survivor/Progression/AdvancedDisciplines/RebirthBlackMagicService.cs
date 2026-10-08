using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthBlackMagicTierRule
{
    public string TierId;
    public float SkillRequired;
    public string KnowledgeId;
    public float BaseDurationSeconds;
}

/// <summary>
/// PC008 / Advanced Disciplines Chunk F server authority for Witch Doctor initiation and temporary
/// Black Magic domination. Temporary control is deliberately non-persistent; Chunk G owns
/// conditioning/binding and permanent undead identity.
/// </summary>
public static class RebirthBlackMagicService
{
    private sealed class DominationState
    {
        public int TargetEntityId;
        public int OwnerEntityId;
        public byte OriginalFactionId;
        public byte OriginalFactionRank;
        public string TierId;
        public int CapacityCost;
        public float StartedRealtime;
        public float ExpiresRealtime;
        public string EntityClassId;
    }

    private static readonly object Gate=new object();
    private static readonly Dictionary<int,DominationState> Active=new Dictionary<int,DominationState>();
    private static readonly Dictionary<long,float> LastSuccessfulTargetControl=new Dictionary<long,float>();
    private static readonly HashSet<long> InitiationTargetsSeen=new HashSet<long>();
    private static readonly Dictionary<string,RebirthBlackMagicTierRule> TierRules=new Dictionary<string,RebirthBlackMagicTierRule>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,float> KnowledgeMilestones=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);

    private static bool ready,eventsInstalled;
    private static float initiationScythes=20f,initiationTactical=20f,maxDistance=6f,capacityStep=20f,maxDurationBonus=30f;
    private static int initiationAttunements=3,baseCapacity=2,maxCapacity=8;
    private static float dominationAward=.50f,targetRepeatSeconds=180f;
    public static float DominationReferenceGain { get { Ensure(); return dominationAward; } }
    public static float BindingReferenceGain { get { return .60f; } }
    private static float nextCleanup;
    private static long dominationAttempts,dominationSuccesses,manualReleases,expiryReleases,disconnectReleases,combatAwards,deniedCapacity,deniedTier,deniedRepeat,trialAttunements;
    private static long controlledDamageEvents,combatTargetAssignments;

    public static bool IsReady { get { return ready; } }

    public static string Install(Harmony harmony)
    {
        string report=LoadDefinitions();ClearRuntime(false);
        string boundUndead=RebirthBoundUndeadService.Install(harmony);
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthBlackMagicActivationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthBlackMagicTargetSafetyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthBlackMagicDisconnectPatch));
        if(!eventsInstalled)
        {
            ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
            ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
            ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
            ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
            eventsInstalled=true;
        }
        return report+" "+boundUndead+" patches=3";
    }

    public static string LoadDefinitions()
    {
        TierRules.Clear();KnowledgeMilestones.Clear();ready=false;
        string path=RebirthAdvancedDisciplineRegistry.SourcePath;if(string.IsNullOrEmpty(path)||!File.Exists(path))throw new FileNotFoundException("Advanced Disciplines configuration is unavailable for Black Magic.",path);
        XDocument doc=XDocument.Load(path);XElement root=doc.Root,wd=root==null?null:root.Element("witch_doctor");if(wd==null)throw new InvalidDataException("advanced_disciplines.xml is missing witch_doctor runtime definitions");
        initiationScythes=F(wd,"initiation_scythes_skill",20f);initiationTactical=F(wd,"initiation_tactical_rifles_skill",20f);initiationAttunements=Math.Max(1,I(wd,"initiation_attunements",3));maxDistance=Math.Max(2f,F(wd,"maximum_interaction_distance",6f));
        baseCapacity=Math.Max(1,I(wd,"base_domination_capacity",2));capacityStep=Math.Max(1f,F(wd,"capacity_skill_step",20f));maxCapacity=Math.Max(baseCapacity,I(wd,"max_domination_capacity",8));maxDurationBonus=Math.Max(0f,F(wd,"duration_skill_bonus_at_100",30f));
        dominationAward=Math.Max(0f,F(wd,"domination_skill_award",.5f));targetRepeatSeconds=Math.Max(10f,F(wd,"target_repeat_seconds",180f));
        foreach(XElement e in wd.Elements("tier"))
        {
            string id=A(e,"id").ToLowerInvariant();if(id.Length==0)continue;if(TierRules.ContainsKey(id))throw new InvalidDataException("Duplicate Black Magic tier: "+id);
            TierRules[id]=new RebirthBlackMagicTierRule{TierId=id,SkillRequired=F(e,"skill",0f),KnowledgeId=A(e,"knowledge"),BaseDurationSeconds=Math.Max(5f,F(e,"base_duration_seconds",30f))};
        }
        foreach(XElement e in wd.Descendants("knowledge_milestone")){string id=A(e,"id");if(id.Length>0)KnowledgeMilestones[id]=F(e,"skill",0f);}
        List<string> errors=ValidateAuthoring();if(errors.Count>0)throw new InvalidDataException("Black Magic authoring invalid: "+string.Join(" | ",errors.ToArray()));ready=true;
        return "blackMagic tiers="+TierRules.Count+" initiation="+initiationScythes.ToString("0",CultureInfo.InvariantCulture)+"/"+initiationTactical.ToString("0",CultureInfo.InvariantCulture)+" attunements="+initiationAttunements+" capacity="+baseCapacity+".."+maxCapacity;
    }

    public static bool HasWitchDoctor(EntityPlayer player)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||player==null)return false;
        RebirthWorldCharacterRecord r;if(player.world!=null&&!player.world.IsRemote()&&RebirthWorldCharacterService.TryGet(player,out r)&&r!=null&&r.Progression!=null)return r.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineWitchDoctor);
        return RebirthSurvivorClientState.HasProjectedDiscipline(player,RebirthSurvivorIds.DisciplineWitchDoctor);
    }

    public static float GetBlackMagicSkill(EntityPlayer player)
    {
        float value;return player!=null&&RebirthServiceCraftSkillService.TryGetSkillValue(player,RebirthSurvivorIds.SkillBlackMagic,out value)?value:0f;
    }

    public static float GetBlackMagicPracticalSkill(EntityPlayer player)
    {
        float value;return player!=null&&RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,RebirthSurvivorIds.SkillBlackMagic,out value)?value:0f;
    }

    public static int GetDominationCapacity(EntityPlayer player)
    {
        if(!HasWitchDoctor(player))return 0;float skill=Math.Max(1f,GetBlackMagicSkill(player));int cap=baseCapacity+(int)Math.Floor(Math.Max(0f,skill-1f)/capacityStep);
        if(RebirthKnowledgeService.HasKnowledge(player,RebirthSurvivorIds.KnowledgeBlackMagicMindControlII))cap++;
        if(RebirthKnowledgeService.HasKnowledge(player,RebirthSurvivorIds.KnowledgeBlackMagicMindControlIII))cap++;
        return Math.Max(0,Math.Min(maxCapacity,cap));
    }

    public static bool TryGetCapacityUsage(EntityPlayer player,out int used,out int capacity,out int activeCount)
    {
        used=0;capacity=GetDominationCapacity(player);activeCount=0;if(player==null)return false;
        lock(Gate)foreach(DominationState s in Active.Values)if(s!=null&&s.OwnerEntityId==player.entityId){used+=Math.Max(1,s.CapacityCost);activeCount++;}
        return true;
    }

    public static bool CanPresentInteraction(EntityAlive target,EntityPlayer player,out bool initiation,out string reason)
    {
        Ensure();initiation=false;if(target==null||player==null||target.IsDead()){reason="target unavailable";return false;}RebirthNpcStableId boundStable;if(RebirthBoundUndeadService.IsBoundEntity(target.entityId,out boundStable)){reason="target is permanently bound";return false;}if((target.position-player.position).sqrMagnitude>maxDistance*maxDistance){reason="move closer";return false;}
        if(target.world!=null&&target.world.IsRemote())
        {
            int projectedOwner=target.Buffs!=null?Mathf.RoundToInt(target.Buffs.GetCustomVar("$RB_BlackMagicOwner")):0;
            if(projectedOwner>0){if(projectedOwner==player.entityId){reason="controlled by you";return true;}reason="target is already dominated by another controller";return false;}
        }
        else
        {
            DominationState active;lock(Gate)Active.TryGetValue(target.entityId,out active);if(active!=null){if(active.OwnerEntityId==player.entityId){reason="controlled by you";return true;}reason="target is already dominated by another controller";return false;}
        }
        RebirthBlackMagicTargetClassification c;if(!RebirthBlackMagicTargetClassifier.TryClassifyTemporaryDomination(target,out c,out reason))return false;
        if(HasWitchDoctor(player))return true;
        initiation=true;if(c.IsZombieAnimal||!string.Equals(c.TierId,"normal",StringComparison.OrdinalIgnoreCase)){reason="Witch Doctor initiation requires a standard Normal zombie";return false;}
        float scythe,tactical;if(!RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,RebirthSurvivorIds.SkillScythes,out scythe)||!RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,RebirthSurvivorIds.SkillTacticalRifles,out tactical)){reason="skill state unavailable";return false;}
        if(scythe+0.001f<initiationScythes||tactical+0.001f<initiationTactical){reason="Scythes "+scythe.ToString("0.##",CultureInfo.InvariantCulture)+"/"+initiationScythes.ToString("0",CultureInfo.InvariantCulture)+", Tactical Rifles "+tactical.ToString("0.##",CultureInfo.InvariantCulture)+"/"+initiationTactical.ToString("0",CultureInfo.InvariantCulture);return false;}
        return true;
    }

    public static bool TryInteract(World world,EntityPlayer player,int targetEntityId,out string reason)
    {
        reason=string.Empty;if(world==null||world.IsRemote()||player==null||!RebirthWorldCharacterRepository.IsServerAuthority){reason="server authority required";return false;}Ensure();EntityAlive target=world.GetEntity(targetEntityId) as EntityAlive;bool initiation;string presentationReason;
        if(!CanPresentInteraction(target,player,out initiation,out presentationReason)){reason=presentationReason;return false;}
        DominationState existing;lock(Gate)Active.TryGetValue(targetEntityId,out existing);
        if(existing!=null)
        {
            if(existing.OwnerEntityId!=player.entityId){reason="target is already dominated by another controller";return false;}
            ReleaseInternal(world,existing,"manual",true);reason="Temporary domination released.";return true;
        }
        if(initiation)return TryAdvanceInitiation(player,target,out reason);
        return TryDominate(world,player,target,out reason);
    }

    private static bool TryAdvanceInitiation(EntityPlayer player,EntityAlive target,out string reason)
    {
        reason=string.Empty;RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record)){reason="Rebirth character unavailable";return false;}
        if(record.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineWitchDoctor)){reason="Witch Doctor already acquired";return false;}
        long key=PairKey(player.entityId,target.entityId);lock(Gate){if(InitiationTargetsSeen.Contains(key)){reason="this zombie has already been used for the current initiation trial";return false;}}
        record.Progression.WitchDoctorInitiationAttunements=Math.Min(initiationAttunements,Math.Max(0,record.Progression.WitchDoctorInitiationAttunements)+1);trialAttunements++;
        bool completed=record.Progression.WitchDoctorInitiationAttunements>=initiationAttunements;
        if(completed)
        {
            string grantReason;if(!RebirthAdvancedDisciplineRegistry.CanAcquireFromRecord(RebirthSurvivorIds.DisciplineWitchDoctor,record,RebirthSurvivorIds.TrialWitchDoctorInitiation,out grantReason)){record.Progression.WitchDoctorInitiationAttunements=Math.Max(0,initiationAttunements-1);reason=grantReason;return false;}
            lock(Gate)InitiationTargetsSeen.Add(key);
            record.Progression.CompletedTrialIds.Add(RebirthSurvivorIds.TrialWitchDoctorInitiation);record.Progression.AcquiredDisciplineIds.Add(RebirthSurvivorIds.DisciplineWitchDoctor);record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeBlackMagicMindControlI);
            RebirthSkillRuntimeState skill;if(record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillBlackMagic,out skill)&&skill!=null){skill.Value=Math.Max(1f,skill.Value);skill.Progress=Math.Max(0f,Math.Min(.999999f,skill.Progress));}
            record.Touch("witch-doctor-acquired");RebirthWorldCharacterRepository.SaveIfDirty(identity,"witch-doctor-acquired");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"witch-doctor-acquired");reason="Witch Doctor discipline acquired. Mind Control I learned; Black Magic activated.";return true;
        }
        lock(Gate)InitiationTargetsSeen.Add(key);
        record.Touch("witch-doctor-attunement");RebirthWorldCharacterRepository.SaveIfDirty(identity,"witch-doctor-attunement");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"witch-doctor-attunement");reason="Occult attunement "+record.Progression.WitchDoctorInitiationAttunements+"/"+initiationAttunements+" completed.";return true;
    }

    public static bool TrySpecialPantherDominate(World world,EntityPlayer player,EntityAlive target,out string reason)
    {
        reason=string.Empty;if(world==null||world.IsRemote()||player==null||target==null||!RebirthWorldCharacterRepository.IsServerAuthority){reason="server authority required";return false;}Ensure();if(!HasWitchDoctor(player)){reason="Witch Doctor discipline required";return false;}DominationState existing;lock(Gate)Active.TryGetValue(target.entityId,out existing);if(existing!=null){reason=existing.OwnerEntityId==player.entityId?"target already dominated by owner":"target already dominated";return false;}return TryDominate(world,player,target,out reason);
    }

    private static bool TryDominate(World world,EntityPlayer player,EntityAlive target,out string reason)
    {
        dominationAttempts++;RebirthBlackMagicTargetClassification c;if(!RebirthBlackMagicTargetClassifier.TryClassifyTemporaryDomination(target,out c,out reason))return false;
        RebirthBlackMagicTierRule rule;if(!TierRules.TryGetValue(c.TierId,out rule)){reason="no authored domination rule for tier "+c.TierId;return false;}
        float practicalSkill=GetBlackMagicPracticalSkill(player);if(practicalSkill+0.001f<rule.SkillRequired){deniedTier++;reason="Black Magic "+practicalSkill.ToString("0.##",CultureInfo.InvariantCulture)+" / "+rule.SkillRequired.ToString("0",CultureInfo.InvariantCulture)+" for "+c.TierId;return false;}
        if(!string.IsNullOrEmpty(rule.KnowledgeId)&&!RebirthKnowledgeService.HasKnowledge(player,rule.KnowledgeId)){deniedTier++;reason=RebirthKnowledgeService.GetDisplayName(rule.KnowledgeId)+" required for "+c.TierId+" domination";return false;}
        float now=Time.realtimeSinceStartup;long repeatKey=PairKey(player.entityId,target.entityId);float last;lock(Gate)if(LastSuccessfulTargetControl.TryGetValue(repeatKey,out last)&&now-last<targetRepeatSeconds){deniedRepeat++;reason="target is temporarily resistant to repeated domination for "+Math.Ceiling(targetRepeatSeconds-(now-last)).ToString("0",CultureInfo.InvariantCulture)+"s";return false;}
        int used,capacity,count;TryGetCapacityUsage(player,out used,out capacity,out count);if(used+c.DominationCapacityCost>capacity){deniedCapacity++;reason="Domination Capacity "+used+" + "+c.DominationCapacityCost+" / "+capacity;return false;}
        float outcomeSkill=GetBlackMagicSkill(player);float duration=rule.BaseDurationSeconds+maxDurationBonus*Math.Max(0f,Math.Min(100f,outcomeSkill))/100f;
        DominationState state=new DominationState{TargetEntityId=target.entityId,OwnerEntityId=player.entityId,OriginalFactionId=target.factionId,OriginalFactionRank=target.factionRank,TierId=c.TierId,CapacityCost=c.DominationCapacityCost,StartedRealtime=now,ExpiresRealtime=now+duration,EntityClassId=c.EntityClassId};
        lock(Gate){if(Active.ContainsKey(target.entityId)){reason="target control state changed; retry";return false;}Active[target.entityId]=state;LastSuccessfulTargetControl[repeatKey]=now;}
        target.factionId=player.factionId;target.factionRank=player.factionRank;target.SetAttackTarget(null,0);target.SetRevengeTarget(null);target.Buffs.SetCustomVar("$MC_Owner",player.entityId);target.Buffs.SetCustomVar("$RB_BlackMagicOwner",player.entityId);target.Buffs.SetCustomVar("$RB_BlackMagicCapacity",c.DominationCapacityCost);
        dominationSuccesses++;if(dominationAward>0f)AwardDiscreteAction(player,dominationAward,"blackmagic:dominate:"+target.entityId,targetRepeatSeconds,"successful domination");EnsureKnowledgeMilestones(player);
        reason="Dominated "+c.TierId+" target for "+duration.ToString("0",CultureInfo.InvariantCulture)+"s. Domination Capacity "+(used+c.DominationCapacityCost)+"/"+capacity+".";return true;
    }

    public static void OnDominatedCombatDamage(EntityAlive victim,DamageResponse response,int attackerId,float actualHealthLoss)
    {
        if(victim==null||victim.world==null||victim.world.IsRemote()||actualHealthLoss<=0f||attackerId<=0||response.Source==null)return;
        DominationState state;lock(Gate)if(!Active.TryGetValue(attackerId,out state)||state==null)return;
        controlledDamageEvents++;
        EntityPlayer owner=victim.world.GetEntity(state.OwnerEntityId) as EntityPlayer;if(owner==null||owner.IsDead())return;
        EntityAlive attacker=victim.world.GetEntity(attackerId) as EntityAlive;
        if(attacker==null||victim.entityId==owner.entityId||ShouldBlockControlledAttack(attacker,victim))return;
        ItemValue attackingItem=response.Source.AttackingItem;
        float sustainedDps;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;RebirthSkillRuntimeState skillState;
        if(attackingItem!=null&&RebirthSkillAwardService.TryGetEligible(owner,out identity,out record)&&
            record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillBlackMagic,out skillState)&&skillState!=null&&
            RebirthWeaponSustainedDpsService.TryGetControlledMeleeDps(attacker,attackingItem,out sustainedDps))
        {
            float amount=RebirthWeaponSustainedDpsService.CalculateCombatProgress(actualHealthLoss,sustainedDps,skillState.Value+skillState.Progress);
            if(amount>0f){RebirthWeaponSustainedDpsService.QueueCombatProgress(owner,RebirthSurvivorIds.SkillBlackMagic,amount);combatAwards++;EnsureKnowledgeMilestones(owner);}
        }
        if(attacker!=null)RebirthBoundUndeadService.OnDominatedCombatDamage(attacker,victim,response);
    }

    public static bool AwardBindingCompletion(EntityPlayer player,string stableKey)
    {
        return AwardDiscreteAction(player,.60f,"blackmagic:binding:"+(stableKey??string.Empty),0f,"successful permanent binding");
    }

    private static bool AwardDiscreteAction(EntityPlayer player,float raw,string source,float minimumInterval,string description)
    {
        if(player==null||raw<=0f)return false;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence{SkillId=RebirthSurvivorIds.SkillBlackMagic,SourceKey=source??string.Empty,
            ReferenceDescription=description??string.Empty,AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,
            CreditedWork=1f,DiscreteRawAward=raw,MinimumInterval=Math.Max(0f,minimumInterval)};
        RebirthSkillTrainingComputation computation;float skillAward,attributeAward;
        return RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out computation,out skillAward,out attributeAward);
    }

    private static void EnsureKnowledgeMilestones(EntityPlayer player)
    {
        if(player==null||player.world==null||player.world.IsRemote())return;RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return;float skill=GetBlackMagicPracticalSkill(player);bool changed=false;
        foreach(KeyValuePair<string,float> m in KnowledgeMilestones)if(skill+0.001f>=m.Value&&!record.Progression.KnowledgeIds.Contains(m.Key)&&ShouldAutoGrantMilestone(m.Key)){record.Progression.KnowledgeIds.Add(m.Key);changed=true;}
        if(changed){record.Touch("black-magic-knowledge-milestone");RebirthWorldCharacterRepository.SaveIfDirty(identity,"black-magic-knowledge-milestone");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"black-magic-knowledge-milestone");}
    }


    private static bool ShouldAutoGrantMilestone(string knowledgeId)
    {
        // Chunk J separates practical mastery from study. Initiation grants Mind Control I; later
        // Black Magic techniques are learned from authored literature and then require the Skill gate.
        return string.Equals(knowledgeId,RebirthSurvivorIds.KnowledgeBlackMagicMindControlI,StringComparison.OrdinalIgnoreCase);
    }

    public static void RefreshKnowledgeMilestones(EntityPlayer player)
    { Ensure(); EnsureKnowledgeMilestones(player); }

    public static bool IsControlled(int targetEntityId,out int ownerEntityId)
    {
        ownerEntityId=0;lock(Gate){DominationState s;if(!Active.TryGetValue(targetEntityId,out s)||s==null)return false;ownerEntityId=s.OwnerEntityId;return true;}
    }

    public static bool TryGetControlInfo(int targetEntityId,out int ownerEntityId,out string tierId,out int capacityCost,out float startedRealtime)
    {
        ownerEntityId=0;tierId=string.Empty;capacityCost=0;startedRealtime=0f;lock(Gate){DominationState state;if(!Active.TryGetValue(targetEntityId,out state)||state==null)return false;ownerEntityId=state.OwnerEntityId;tierId=state.TierId??string.Empty;capacityCost=state.CapacityCost;startedRealtime=state.StartedRealtime;return true;}
    }

    public static bool TryTransferToPermanentBinding(World world,int targetEntityId,int ownerEntityId,out string tierId,out string reason)
    {
        tierId=string.Empty;reason=string.Empty;if(world==null||world.IsRemote()){reason="server authority required";return false;}
        EntityAlive target=world.GetEntity(targetEntityId) as EntityAlive;if(target==null||target.IsDead()){reason="target became unavailable during binding";return false;}
        DominationState state;lock(Gate){if(!Active.TryGetValue(targetEntityId,out state)||state==null){reason="temporary domination is no longer active";return false;}if(state.OwnerEntityId!=ownerEntityId){reason="temporary domination belongs to another controller";return false;}if(target.IsDead()){reason="target became unavailable during binding";return false;}Active.Remove(targetEntityId);}
        tierId=state.TierId??string.Empty;target.SetAttackTarget(null,0);target.SetRevengeTarget(null);target.Buffs.SetCustomVar("$MC_Owner",0f);target.Buffs.SetCustomVar("$RB_BlackMagicOwner",0f);target.Buffs.SetCustomVar("$RB_BlackMagicCapacity",0f);RebirthBoundUndeadService.OnTemporaryControlEnded(targetEntityId,ownerEntityId);return true;
    }

    public static bool ShouldBlockControlledAttack(EntityAlive controller,EntityAlive proposedTarget)
    {
        if(controller==null||proposedTarget==null)return false;DominationState state;lock(Gate)Active.TryGetValue(controller.entityId,out state);
        if(state!=null){if(proposedTarget.entityId==state.OwnerEntityId)return true;int otherOwner;if(IsControlled(proposedTarget.entityId,out otherOwner)&&otherOwner==state.OwnerEntityId)return true;EntityPlayer owner=controller.world!=null?controller.world.GetEntity(state.OwnerEntityId) as EntityPlayer:null;if(owner!=null&&proposedTarget is EntityPlayer&&proposedTarget.factionId==owner.factionId)return true;}
        return RebirthBoundUndeadService.ShouldBlockAttack(controller,proposedTarget);
    }

    public static void OnTargetUnloading(EntityAlive target)
    {
        if(target==null||target.world==null||target.world.IsRemote())return;DominationState state;lock(Gate)Active.TryGetValue(target.entityId,out state);if(state!=null)ReleaseInternal(target.world,state,"unload",false);RebirthBoundUndeadService.OnEntityUnloading(target);
    }

    public static void ClearStaleMarkersOnAdded(EntityAlive target)
    {
        if(target==null||target.world==null||target.world.IsRemote())return;DominationState state;lock(Gate)Active.TryGetValue(target.entityId,out state);if(state==null&&target.Buffs.GetCustomVar("$RB_BlackMagicOwner")>0f){target.Buffs.SetCustomVar("$RB_BlackMagicOwner",0f);target.Buffs.SetCustomVar("$RB_BlackMagicCapacity",0f);target.Buffs.SetCustomVar("$MC_Owner",0f);}RebirthBoundUndeadService.OnEntityAdded(target);
    }

    public static void ReleaseOwner(EntityPlayer player,string reason)
    { if(player==null||player.world==null)return;ReleaseOwner(player.world,player.entityId,reason); }
    public static void ReleaseOwner(World world,int ownerEntityId,string reason)
    {
        if(world==null||ownerEntityId<=0)return;List<DominationState> release=new List<DominationState>();lock(Gate)foreach(DominationState s in Active.Values)if(s!=null&&s.OwnerEntityId==ownerEntityId)release.Add(s);for(int i=0;i<release.Count;i++)ReleaseInternal(world,release[i],reason,false);
    }

    private static void ReleaseInternal(World world,DominationState state,string reason,bool manual)
    {
        if(state==null)return;bool removed;lock(Gate)removed=Active.Remove(state.TargetEntityId);if(!removed)return;EntityAlive target=world!=null?world.GetEntity(state.TargetEntityId) as EntityAlive:null;if(target!=null&&!target.IsDead()){target.factionId=state.OriginalFactionId;target.factionRank=state.OriginalFactionRank;target.SetAttackTarget(null,0);target.SetRevengeTarget(null);target.Buffs.SetCustomVar("$MC_Owner",0f);target.Buffs.SetCustomVar("$RB_BlackMagicOwner",0f);target.Buffs.SetCustomVar("$RB_BlackMagicCapacity",0f);}RebirthBoundUndeadService.OnTemporaryControlEnded(state.TargetEntityId,state.OwnerEntityId);if(manual)manualReleases++;else if(string.Equals(reason,"expired",StringComparison.OrdinalIgnoreCase))expiryReleases++;else if(string.Equals(reason,"disconnect",StringComparison.OrdinalIgnoreCase))disconnectReleases++;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data){ClearRuntime(false);}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime(true);}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime(true);}
    public static bool IsValidDominatedEnemy(EntityAlive controlled, EntityAlive enemy)
    {
        int ownerId;
        if(controlled==null||enemy==null||enemy==controlled||!(enemy is EntityEnemy)||enemy.IsDead()||enemy.IsSleeping||
            !IsControlled(controlled.entityId,out ownerId)||ShouldBlockControlledAttack(controlled,enemy))return false;
        // Never recruit another controller's undead into friendly combat.
        int otherOwner;RebirthNpcStableId boundId;
        if(IsControlled(enemy.entityId,out otherOwner)||RebirthBoundUndeadService.IsBoundEntity(enemy.entityId,out boundId))return false;
        if(enemy.factionId==controlled.factionId)return false;
        return true;
    }

    private static float nextCombatTargetScan;
    private static void RefreshDominatedTargets(World world)
    {
        List<DominationState> states;lock(Gate)states=new List<DominationState>(Active.Values);
        foreach(DominationState state in states)
        {
            EntityAlive controlled=world.GetEntity(state.TargetEntityId) as EntityAlive;
            if(controlled==null||controlled.IsDead())continue;
            EntityAlive current=controlled.GetAttackTarget();
            if(IsValidDominatedEnemy(controlled,current)&&(current.position-controlled.position).sqrMagnitude<=400f)continue;
            EntityAlive nearest=null;float best=16f*16f;
            foreach(Entity entity in world.Entities.list)
            {
                EntityAlive candidate=entity as EntityAlive;
                if(!IsValidDominatedEnemy(controlled,candidate))continue;
                float distance=(candidate.position-controlled.position).sqrMagnitude;
                if(distance<best&&controlled.CanSee(candidate)){nearest=candidate;best=distance;}
            }
            controlled.SetAttackTarget(nearest,nearest!=null?200:0);
            if(nearest!=null)combatTargetAssignments++;
        }
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null||!c.IsServer||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;float now=Time.realtimeSinceStartup;if(now<nextCleanup)return;nextCleanup=now+.25f;World world=GameManager.Instance!=null?GameManager.Instance.World:null;if(world==null)return;
        List<DominationState> release=new List<DominationState>();lock(Gate)foreach(DominationState s in Active.Values)if(s!=null){EntityAlive target=world.GetEntity(s.TargetEntityId) as EntityAlive;EntityPlayer owner=world.GetEntity(s.OwnerEntityId) as EntityPlayer;if(target==null||target.IsDead()||owner==null||owner.IsDead()||now>=s.ExpiresRealtime)release.Add(s);}
        for(int i=0;i<release.Count;i++){DominationState s=release[i];EntityPlayer owner=world.GetEntity(s.OwnerEntityId) as EntityPlayer;string why=now>=s.ExpiresRealtime?"expired":(owner==null?"disconnect":"invalid");ReleaseInternal(world,s,why,false);}
        PruneRepeat(now);
        if(now>=nextCombatTargetScan){nextCombatTargetScan=now+1f;RefreshDominatedTargets(world);}
    }

    private static void ClearRuntime(bool restore)
    {
        World world=restore&&GameManager.Instance!=null?GameManager.Instance.World:null;List<DominationState> states;lock(Gate){states=new List<DominationState>(Active.Values);}if(restore&&world!=null)for(int i=0;i<states.Count;i++)ReleaseInternal(world,states[i],"shutdown",false);
        lock(Gate){Active.Clear();LastSuccessfulTargetControl.Clear();InitiationTargetsSeen.Clear();}nextCleanup=0f;
    }

    private static void PruneRepeat(float now)
    { lock(Gate){if(LastSuccessfulTargetControl.Count<256)return;List<long> remove=new List<long>();foreach(KeyValuePair<long,float> p in LastSuccessfulTargetControl)if(now-p.Value>targetRepeatSeconds*2f)remove.Add(p.Key);for(int i=0;i<remove.Count;i++)LastSuccessfulTargetControl.Remove(remove[i]);} }

    public static string BuildStatus(EntityPlayer player,int targetEntityId)
    {
        Ensure();StringBuilder b=new StringBuilder();b.Append("[REBIRTH BlackMagic] ready=").Append(ready).Append(" witchDoctor=").Append(HasWitchDoctor(player)).Append(" skill=").Append(GetBlackMagicSkill(player).ToString("0.##",CultureInfo.InvariantCulture));int used,capacity,count;TryGetCapacityUsage(player,out used,out capacity,out count);b.Append(" capacity=").Append(used).Append('/').Append(capacity).Append(" active=").Append(count);
        RebirthWorldCharacterRecord r;if(player!=null&&player.world!=null&&!player.world.IsRemote()&&RebirthWorldCharacterService.TryGet(player,out r)&&r!=null)b.Append(" attunements=").Append(r.Progression.WitchDoctorInitiationAttunements).Append('/').Append(initiationAttunements);else b.Append(" attunements=<server-persistent>");
        b.Append(" knowledge[I/II/III]=").Append(RebirthKnowledgeService.HasKnowledge(player,RebirthSurvivorIds.KnowledgeBlackMagicMindControlI)).Append('/').Append(RebirthKnowledgeService.HasKnowledge(player,RebirthSurvivorIds.KnowledgeBlackMagicMindControlII)).Append('/').Append(RebirthKnowledgeService.HasKnowledge(player,RebirthSurvivorIds.KnowledgeBlackMagicMindControlIII));
        if(targetEntityId>0&&GameManager.Instance!=null&&GameManager.Instance.World!=null){EntityAlive t=GameManager.Instance.World.GetEntity(targetEntityId) as EntityAlive;b.Append("\n  ").Append(BuildExplain(player,t));}
        b.Append("\n  stats attempts=").Append(dominationAttempts).Append(" success=").Append(dominationSuccesses).Append(" releaseManual=").Append(manualReleases).Append(" releaseExpiry=").Append(expiryReleases).Append(" releaseDisconnect=").Append(disconnectReleases).Append(" combatAwards=").Append(combatAwards).Append(" denyCapacity=").Append(deniedCapacity).Append(" denyTier=").Append(deniedTier).Append(" denyRepeat=").Append(deniedRepeat);
        b.Append("\n  controlledDamageEvents=").Append(controlledDamageEvents).Append(" targetAssignments=").Append(combatTargetAssignments);
        if(player!=null&&player.world!=null)
        {
            List<DominationState> active;lock(Gate)active=new List<DominationState>(Active.Values);
            foreach(DominationState s in active)
            {
                if(s.OwnerEntityId!=player.entityId)continue;
                EntityAlive actor=player.world.GetEntity(s.TargetEntityId) as EntityAlive;
                if(actor==null)continue;
                EntityAlive enemy=actor.GetAttackTarget();
                b.Append("\n  controlled=").Append(actor.entityId).Append(" health=").Append(actor.Health)
                    .Append(" target=").Append(enemy!=null?enemy.entityId:0).Append(" remainingSeconds=").Append(Math.Max(0f,s.ExpiresRealtime-Time.realtimeSinceStartup).ToString("0.0",CultureInfo.InvariantCulture));
            }
        }
        return b.ToString();
    }

    public static string BuildExplain(EntityPlayer player,EntityAlive target)
    {
        if(target==null)return "target unavailable";RebirthBlackMagicTargetClassification c;string reason;bool eligible=RebirthBlackMagicTargetClassifier.TryClassifyTemporaryDomination(target,out c,out reason);StringBuilder b=new StringBuilder();b.Append("entity=").Append(target.entityId).Append(" class=").Append(target.EntityClass==null?"<none>":target.EntityClass.entityClassName).Append(" eligible=").Append(eligible).Append(" reason=").Append(reason);if(c==null)return b.ToString();
        RebirthBlackMagicTierRule rule;TierRules.TryGetValue(c.TierId,out rule);float skill=GetBlackMagicPracticalSkill(player);b.Append(" tier=").Append(c.TierId).Append(" cost=").Append(c.DominationCapacityCost).Append(" skill=").Append(skill.ToString("0.##",CultureInfo.InvariantCulture));if(rule!=null)b.Append(" requiredSkill=").Append(rule.SkillRequired.ToString("0.##",CultureInfo.InvariantCulture)).Append(" knowledge=").Append(rule.KnowledgeId).Append(" hasKnowledge=").Append(RebirthKnowledgeService.HasKnowledge(player,rule.KnowledgeId));int used,cap,count;TryGetCapacityUsage(player,out used,out cap,out count);b.Append(" capacity=").Append(used).Append('+').Append(c.DominationCapacityCost).Append('/').Append(cap);int owner;b.Append(" controlled=").Append(IsControlled(target.entityId,out owner)).Append(" controller=").Append(owner);return b.ToString();
    }

    public static string RunVectors()
    {
        Ensure();List<string> e=ValidateAuthoring();int w;string[] tiers={"normal","feral","radiated","charged","infernal"};for(int i=0;i<tiers.Length;i++){if(!TierRules.ContainsKey(tiers[i]))e.Add("missing runtime tier "+tiers[i]);if(!RebirthAdvancedDisciplineRegistry.TryGetZombieTierWeight(tiers[i],out w)||w!=i+1)e.Add("unexpected tier weight "+tiers[i]);}
        if(TierRules.ContainsKey("normal")&&TierRules["normal"].SkillRequired>1.001f)e.Add("Normal domination must activate at Black Magic 1");if(TierRules.ContainsKey("infernal")&&TierRules["infernal"].SkillRequired<80f)e.Add("Infernal domination unlocks too early");
        return "[REBIRTH BlackMagic] chunkFVectors="+(e.Count==0?"PASS":"FAIL")+" failures="+e.Count+(e.Count==0?string.Empty:" "+string.Join(" | ",e.ToArray()));
    }

    private static List<string> ValidateAuthoring()
    {
        List<string> e=new List<string>();string[] order={"normal","feral","radiated","charged","infernal"};float last=-1f;for(int i=0;i<order.Length;i++){RebirthBlackMagicTierRule r;if(!TierRules.TryGetValue(order[i],out r)){e.Add("missing tier "+order[i]);continue;}if(r.SkillRequired<last)e.Add("tier Skill order is not monotonic at "+order[i]);last=r.SkillRequired;if(string.IsNullOrEmpty(r.KnowledgeId))e.Add("tier Knowledge missing: "+order[i]);}
        RebirthSkillDefinition skill;if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(RebirthSurvivorIds.SkillBlackMagic,out skill)||skill==null||!skill.Advanced)e.Add("skill.black_magic missing or not Advanced");
        if(baseCapacity<1||maxCapacity<baseCapacity)e.Add("invalid domination capacity range");return e;
    }

    private static long PairKey(int a,int b){unchecked{return ((long)a<<32)^(uint)b;}}
    private static void Ensure(){if(!ready)LoadDefinitions();}
    private static string A(XElement e,string n){return e==null?string.Empty:((string)e.Attribute(n)??string.Empty).Trim();}
    private static int I(XElement e,string n,int d){int v;return int.TryParse(A(e,n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}
    private static float F(XElement e,string n,float d){float v;return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:d;}
}

[HarmonyPatch]
public static class RebirthBlackMagicActivationPatch
{
    [HarmonyPostfix][HarmonyPatch(typeof(EntityAlive),"InitLocalActivationCommands")]
    private static void ActivationCommands(Entity __instance,Action<EntityActivationCommand> __0)
    {
        EntityAlive target=__instance as EntityAlive;if(target==null||__0==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;EntityPlayerLocal player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer() as EntityPlayerLocal:null;if(player==null)return;
        int ownerId;if(RebirthBlackMagicService.IsControlled(target.entityId,out ownerId)&&ownerId==player.entityId)
        {
            __0(new EntityActivationCommand("rebirthBlackMagicControl","hand",null,"rebirthBlackMagicRelease"));
            string conditionReason;if(RebirthBoundUndeadService.CanPresentConditioning(target,player,out conditionReason))__0(new EntityActivationCommand("rebirthBlackMagicCondition","hand",null,"rebirthBlackMagicCondition"));
            string bindReason;if(RebirthBoundUndeadService.CanPresentBinding(target,player,out bindReason))__0(new EntityActivationCommand("rebirthBlackMagicBind","hand",null,"rebirthBlackMagicBind"));
            return;
        }
        bool initiation;string reason;if(!RebirthBlackMagicService.CanPresentInteraction(target,player,out initiation,out reason))return;string textKey=initiation?"rebirthWitchDoctorAttune":"rebirthBlackMagicDominate";__0(new EntityActivationCommand(initiation?"rebirthWitchDoctorAttune":"rebirthBlackMagicControl","hand",null,textKey));
    }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),"OnEntityActivated",new Type[]{typeof(EntityActivationCommand),typeof(EntityPlayerLocal)})]
    private static bool Activated(Entity __instance,EntityActivationCommand __0,EntityPlayerLocal __1)
    {
        EntityActivationCommand command = __0; EntityPlayerLocal focusingPlayer = __1;
        bool black=command.commandId=="rebirthWitchDoctorAttune"||command.commandId=="rebirthBlackMagicControl";bool condition=command.commandId=="rebirthBlackMagicCondition";bool bind=command.commandId=="rebirthBlackMagicBind";if(!black&&!condition&&!bind)return true;if(focusingPlayer==null)return false;PersistentPlayerData pp=GameManager.Instance!=null?GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(focusingPlayer.entityId):null;if(pp?.PrimaryId==null)return false;World world=GameManager.Instance!=null?GameManager.Instance.World:null;if(world==null)return false;
        if(black){if(!world.IsRemote()){string r;bool ok=RebirthBlackMagicService.TryInteract(world,focusingPlayer,__instance.entityId,out r);if(!string.IsNullOrEmpty(r))GameManager.ShowTooltipMP(focusingPlayer,r,ok?"ui_success":"ui_denied");}else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBlackMagicInteractionRequest>().Setup(focusingPlayer.entityId,pp.PrimaryId,__instance.entityId));return false;}
        RebirthBoundUndeadInteractionKind kind=condition?RebirthBoundUndeadInteractionKind.Condition:RebirthBoundUndeadInteractionKind.Bind;if(!world.IsRemote()){string r;bool ok=RebirthBoundUndeadService.TryInteract(world,focusingPlayer,__instance.entityId,kind,out r);if(!string.IsNullOrEmpty(r))GameManager.ShowTooltipMP(focusingPlayer,r,ok?"ui_success":"ui_denied");}else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBoundUndeadInteractionRequest>().Setup(focusingPlayer.entityId,pp.PrimaryId,__instance.entityId,kind));return false;
    }
}

[HarmonyPatch]
public static class RebirthBlackMagicTargetSafetyPatch
{
    private static readonly System.Reflection.FieldInfo ChaseTime = AccessTools.Field(typeof(EAIApproachAndAttackTarget), "chaseTimeMax");

    public static EntityFlags DamageGateFlags(Entity entity)
    {
        int owner;
        return RebirthBlackMagicService.IsControlled(entity.entityId,out owner)
            ? entity.entityFlags & ~EntityFlags.Zombie : entity.entityFlags;
    }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.DamageEntity),new Type[]{typeof(DamageSource),typeof(int),typeof(bool),typeof(float)})]
    private static bool ControlledDamageSafety(EntityAlive __instance,DamageSource _damageSource,ref int __result)
    {
        if(__instance==null||__instance.world==null||_damageSource==null)return true;
        EntityAlive attacker=__instance.world.GetEntity(_damageSource.getEntityId()) as EntityAlive;
        if(!RebirthBlackMagicService.ShouldBlockControlledAttack(attacker,__instance))return true;
        __result=-1;return false;
    }

    [HarmonyTranspiler][HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.DamageEntity),new Type[]{typeof(DamageSource),typeof(int),typeof(bool),typeof(float)})]
    private static IEnumerable<CodeInstruction> ControlledZombieDamage(IEnumerable<CodeInstruction> instructions)
    {
        var code=new List<CodeInstruction>(instructions);
        var flags=AccessTools.Field(typeof(Entity),"entityFlags");
        int count=0;
        foreach(var instruction in code)if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,flags))count++;
        // 3.2's single immunity expression reads victim and attacker flags. Do not modify a
        // different future method layout. No live entity flags, damage types or source IDs change.
        if(count!=2){Log.Error("[REBIRTH BlackMagic] Native zombie damage gate layout changed; controlled combat patch was not applied.");return code;}
        var read=AccessTools.Method(typeof(RebirthBlackMagicTargetSafetyPatch),nameof(DamageGateFlags));
        foreach(var instruction in code)
            if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,flags)){instruction.opcode=OpCodes.Call;instruction.operand=read;}
        return code;
    }

    [HarmonyPostfix][HarmonyPatch(typeof(EAIApproachAndAttackTarget),nameof(EAIApproachAndAttackTarget.CanExecute))]
    private static void ControlledCombat(EAIApproachAndAttackTarget __instance,ref bool __result)
    {
        if(__result||ChaseTime==null)return;
        EntityAlive actor=__instance.theEntity;
        if(actor==null||actor.world==null||actor.world.IsRemote()||actor.sleepingOrWakingUp||
            actor.bodyDamage.CurrentStun!=EnumEntityStunType.None||(actor.Jumping&&!actor.isSwimming))return;
        // Native CanExecute already resolved entityTarget, but ordinary zombies' class filters
        // exclude other zombies. Only temporarily dominated actors may extend that filter.
        if(!RebirthBlackMagicService.IsValidDominatedEnemy(actor,actor.GetAttackTarget()))return;
        ChaseTime.SetValue(__instance,0f);__result=true;
    }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),"SetAttackTarget",new Type[]{typeof(EntityAlive),typeof(int)})]
    private static bool AttackTarget(EntityAlive __instance,EntityAlive _attackTarget)
    { return !RebirthBlackMagicService.ShouldBlockControlledAttack(__instance,_attackTarget); }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),"SetRevengeTarget",new Type[]{typeof(EntityAlive)})]
    private static bool RevengeTarget(EntityAlive __instance,EntityAlive _other)
    { return !RebirthBlackMagicService.ShouldBlockControlledAttack(__instance,_other); }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.OnEntityUnload))]
    private static void Unload(EntityAlive __instance){RebirthBlackMagicService.OnTargetUnloading(__instance);}

    [HarmonyPostfix][HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.OnAddedToWorld))]
    private static void Added(EntityAlive __instance){RebirthBlackMagicService.ClearStaleMarkersOnAdded(__instance);}
}

[HarmonyPatch(typeof(GameManager),nameof(GameManager.PlayerDisconnected),new Type[]{typeof(ClientInfo)})]
public static class RebirthBlackMagicDisconnectPatch
{
    [HarmonyPrefix]public static void Prefix(ClientInfo _cInfo){if(_cInfo==null||GameManager.Instance==null||GameManager.Instance.World==null)return;RebirthBlackMagicService.ReleaseOwner(GameManager.Instance.World,_cInfo.entityId,"disconnect");}
}

[Preserve]
public sealed class NetPackageRebirthBlackMagicInteractionRequest:NetPackage
{
    private int playerEntityId,targetEntityId;private PlatformUserIdentifierAbs userId;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBlackMagicInteractionRequest Setup(int playerId,PlatformUserIdentifierAbs user,int targetId){playerEntityId=playerId;userId=user;targetEntityId=targetId;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;playerEntityId=b.ReadInt32();userId=PlatformUserIdentifierAbs.FromStream(b);targetEntityId=b.ReadInt32();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(playerEntityId);userId.ToStream(b);b.Write(targetEntityId);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||userId==null||!ValidEntityIdForSender(playerEntityId)||!ValidUserIdForSender(userId))return;EntityPlayer p=world.GetEntity(playerEntityId) as EntityPlayer;PersistentPlayerData pp=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);if(p==null||pp?.PrimaryId==null||!pp.PrimaryId.Equals(userId))return;string reason;bool ok=RebirthBlackMagicService.TryInteract(world,p,targetEntityId,out reason);if(!string.IsNullOrEmpty(reason))GameManager.ShowTooltipMP(p,reason,ok?"ui_success":"ui_denied");}
    public int GetLength()=>0;
}
