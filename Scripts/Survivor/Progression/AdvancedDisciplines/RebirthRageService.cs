using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

/// <summary>PC010 Berserker/Rage authority. Rage is an Advanced LBD Skill layered over the ten real melee-family Skills.
/// Rage itself is the 2.6 Berserker rage (RebirthBerserkerRage / RebirthRageBuffs): Page Down, Page Up and Rage Capsules,
/// Blood Moon Rage and Death Rage, all carried as native buffs. This service keeps the 3.0 parts: the Berserker
/// initiation trial, the Rage Skill award while enraged, Rage Knowledge and the status/audit output.</summary>
public static class RebirthRageService
{
    public static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer player)
    {
        string kind;int tier;bool active=RebirthBerserkerRage.TryGetActive(player,out kind,out tier);
        RageCounts counts=null;if(player!=null)Counts.TryGetValue(player,out counts);
        return new Newtonsoft.Json.Linq.JObject{["tracked"]=active,["kind"]=active?kind:"none",["tier"]=tier,["skillTier"]=player==null?0:RebirthBerserkerRage.Tier(player),
            ["actor"]=player?.entityId??-1,["activations"]=counts?.Activations??0,["nativeActive"]=player?.Buffs?.GetCustomVar(ActiveCVar)??0f,["nativeTier"]=player?.Buffs?.GetCustomVar(TierCVar)??0f};
    }
    public const string ActiveCVar="$rbRageActive", TierCVar="$rbRageTier";
    private static bool installed;
    private sealed class RageCounts{internal long Activations;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,RageCounts> Counts=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,RageCounts>();
    public static int TrialHitsRequired=25, BreadthRequired=6, DepthRequired=3;
    public static float BreadthLevel=20f, DepthLevel=35f;
    // Kept for the owner-state codec and acceptance output: 2.6 Defensive Rage lasts 60 s and costs this much Energy.
    public const float DurationSeconds=60f, ActivationEnergy=RebirthBerserkerRage.DefensiveEnergy;
    public const bool BloodRequiresBloodMoon=true;
    // The 3.0 stamina/speed rage deltas are replaced by the 2.6 rage buffs, which carry their own passive effects.
    public static float GetStaminaDelta(EntityPlayer p)=>0f;
    public static float GetSpeedDelta(EntityPlayer p)=>0f;
    private static float nextMarkerTick;

    public static string Install()
    {
        if(installed)return "[REBIRTH Rage] already installed";
        LoadTunables();
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
        installed=true; return "[REBIRTH Rage] installed";
    }

    /// <summary>True while any 2.6 rage runs; tier is defensive, offensive, blood, other or death.</summary>
    public static bool IsActive(EntityPlayer player,out string tier)
    {
        tier=string.Empty;if(player==null||player.IsDead())return false;
        int t;return RebirthBerserkerRage.TryGetActive(player,out tier,out t);
    }

    internal static void NoteActivation(EntityPlayer player){if(player!=null)Counts.GetOrCreateValue(player).Activations++;}

    // The owner projection carried the old 12-second rage; buffs now replicate natively, so nothing is projected.
    public static void CaptureOwnerProjection(EntityPlayer player,out int tier,out float remaining){tier=0;remaining=0f;}
    public static void ApplyOwnerProjection(EntityPlayer player,string creation,int tier,float remaining){}

    public static bool HasBerserker(EntityPlayer p)
    { RebirthWorldCharacterRecord r;return p!=null&&RebirthWorldCharacterService.TryGet(p,out r)&&r!=null&&r.IsComplete&&r.Progression!=null&&r.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBerserker); }

    public static bool TryGetMeleeMastery(EntityPlayer p,out int breadth,out int depth,out float total)
    {
        breadth=depth=0;total=0f;if(p==null)return false;string[] ids=RebirthWeaponFamilySkillService.GetMeleeSkillIds();
        for(int i=0;i<ids.Length;i++){float v;if(!RebirthServiceCraftSkillService.TryGetPracticalSkillValue(p,ids[i],out v))v=0f;v=Math.Max(0f,v);total+=v;if(v>=BreadthLevel)breadth++;if(v>=DepthLevel)depth++;}return true;
    }

    public static void OnMeaningfulMeleeDamage(EntityPlayer player,EntityAlive target,float damage,string meleeSkillId,ItemValue attackingItem)
    {
        if(player==null||target==null||damage<=0f||!RebirthWeaponFamilySkillService.IsMeleeSkill(meleeSkillId)||player.world==null||player.world.IsRemote())return;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return;
        if(!record.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBerserker))
        {
            int breadth,depth;float total;TryGetMeleeMastery(player,out breadth,out depth,out total);
            if(breadth>=BreadthRequired&&depth>=DepthRequired){record.Progression.BerserkerInitiationMeleeHits=Math.Min(TrialHitsRequired,record.Progression.BerserkerInitiationMeleeHits+1);if(record.Progression.BerserkerInitiationMeleeHits>=TrialHitsRequired)Acquire(player,identity,record);else{record.Touch("berserker-trial-hit");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-trial-hit");}}
            return;
        }
        // 2.6: Berserker levels came from all melee damage dealt as a Berserker, not only while enraged. Rage is the 3.0
        // Berserker level, so every meaningful melee hit trains it at the normal melee combat rate (provisional pacing).
        AwardRage(player,target,damage,attackingItem);
    }

    private static void Acquire(EntityPlayer player,RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord record)
    {
        string grantReason; if(!RebirthAdvancedDisciplineRegistry.CanAcquireFromRecord(RebirthSurvivorIds.DisciplineBerserker,record,RebirthSurvivorIds.TrialBerserkerInitiation,out grantReason)){record.Progression.BerserkerInitiationMeleeHits=Math.Min(record.Progression.BerserkerInitiationMeleeHits,Math.Max(0,TrialHitsRequired-1));record.Touch("berserker-grant-deferred");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-grant-deferred");return;}
        record.Progression.CompletedTrialIds.Add(RebirthSurvivorIds.TrialBerserkerInitiation);record.Progression.AcquiredDisciplineIds.Add(RebirthSurvivorIds.DisciplineBerserker);record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeRageBasic);
        RebirthSkillRuntimeState rage;if(record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillRage,out rage)&&rage!=null)rage.Value=Math.Max(1f,rage.Value);
        record.Touch("berserker-acquired");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-acquired");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"berserker-acquired");
    }

    /// <summary>Debug start (rbsurvivor rage activate): defensive|offensive|other|blood|death, skipping capsules and cooldowns.</summary>
    public static bool TryActivate(EntityPlayer player,string requestedTier,out string reason)
    {
        reason=string.Empty;if(player==null||player.world==null||player.world.IsRemote()){reason="server authority required";return false;}if(player.IsDead()){reason="Rage requires a living Survivor.";return false;}
        string q=(requestedTier??"defensive").Trim().ToLowerInvariant();int tier=RebirthBerserkerRage.Tier(player);string buff;
        if(q=="defensive"||q=="basic"||q=="controlled"){buff=RebirthRageBuffs.Defensive;RebirthBerserkerRage.Start(player,buff,RebirthRageBuffs.DefPrefix,tier,RebirthRageBuffs.DefensiveStats(tier),0f);}
        else if(q=="offensive"){buff=RebirthRageBuffs.Offensive;RebirthBerserkerRage.Start(player,buff,RebirthRageBuffs.OffPrefix,tier,RebirthRageBuffs.OffensiveStats(tier),RebirthRageBuffs.OffensiveSeconds(tier));}
        else if(q=="blood"){buff=RebirthRageBuffs.Blood;RebirthBerserkerRage.Start(player,buff,RebirthRageBuffs.BloodPrefix,tier,RebirthRageBuffs.BloodStats(tier),0f);}
        else if(q=="other"){buff=RebirthRageBuffs.Other;player.Buffs.AddBuff(buff,player.entityId);}
        else if(q=="death"){buff=RebirthRageBuffs.Death;player.Buffs.AddBuff(buff,player.entityId);}
        else{reason="Unknown Rage: "+q;return false;}
        NoteActivation(player);reason=buff+" started (rank "+tier+").";return true;
    }

    private static void AwardRage(EntityPlayer p,EntityAlive target,float damage,ItemValue attackingItem)
    {
        if(p==null||target==null||attackingItem==null||damage<=0f)return;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(p,out identity,out record)||record==null||record.Progression==null)return;
        RebirthSkillRuntimeState rage;
        if(!record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillRage,out rage)||rage==null)return;
        RebirthWeaponSustainedDpsService.Profile profile;
        if(!RebirthWeaponSustainedDpsService.TryGetCombatProfile(p,attackingItem,out profile)||profile==null||!profile.Valid||!profile.Melee||profile.SustainedDps<=0f)return;
        float amount=RebirthWeaponSustainedDpsService.CalculateCombatProgress(Math.Max(0f,damage),profile.SustainedDps,rage.Value+rage.Progress);
        if(amount<=0f)return;
        RebirthWeaponSustainedDpsService.QueueCombatProgress(p,RebirthSurvivorIds.SkillRage,amount);
        RefreshKnowledge(p);
    }

    public static void RefreshKnowledge(EntityPlayer p)
    {
        RebirthStablePlayerIdentity id;RebirthWorldCharacterRecord r;if(!RebirthSkillAwardService.TryGetEligible(p,out id,out r))return;float v=0f;RebirthServiceCraftSkillService.TryGetPracticalSkillValue(p,RebirthSurvivorIds.SkillRage,out v);bool c=false;
        if(v>=1f)c|=r.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeRageBasic); // Advanced Rage techniques are learned from Chunk-J literature.
        if(c){r.Touch("rage-knowledge");RebirthWorldCharacterRepository.SaveIfDirty(id,"rage-knowledge");RebirthSurvivorNetworkService.SendOwnerState(p,0L,true,"rage-knowledge");}
    }

    private static void LoadTunables()
    {
        XDocument doc=XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath);XElement e=doc.Root?.Element("tunables")?.Element("rage");if(e==null)throw new InvalidOperationException("advanced_disciplines.xml missing Rage tunables");
        BreadthLevel=F(e,"breadth_level",20f);BreadthRequired=Math.Max(1,I(e,"breadth_families",6));DepthLevel=F(e,"depth_level",35f);DepthRequired=Math.Max(1,I(e,"depth_families",3));TrialHitsRequired=Math.Max(1,I(e,"trial_melee_hits",25));
    }
    private static string A(XElement e,string n){return ((string)e.Attribute(n)??string.Empty).Trim();}
    private static int I(XElement e,string n,int d){int v;return int.TryParse(A(e,n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}
    private static float F(XElement e,string n,float d){float v;return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:d;}

    private static void SetMarkers(EntityPlayer p)
    {
        if(p==null||p.Buffs==null)return;string kind;int tier;bool on=RebirthBerserkerRage.TryGetActive(p,out kind,out tier);
        float active=on?1f:0f,t=on?tier:0f;if(p.Buffs.GetCustomVar(ActiveCVar)!=active)p.Buffs.SetCustomVar(ActiveCVar,active);if(p.Buffs.GetCustomVar(TierCVar)!=t)p.Buffs.SetCustomVar(TierCVar,t);
    }
    private static void OnUpdate(ref ModEvents.SGameUpdateData d)
    {
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(world==null||world.IsRemote()||Time.time<nextMarkerTick)return;nextMarkerTick=Time.time+0.5f;
        if(world.Players?.list!=null)foreach(var player in world.Players.list)SetMarkers(player);
    }

    public static string BuildStatus(EntityPlayer p)
    {
        StringBuilder b=new StringBuilder();int br,de;float total;TryGetMeleeMastery(p,out br,out de,out total);string kind;int tier;bool active=RebirthBerserkerRage.TryGetActive(p,out kind,out tier);RebirthWorldCharacterRecord r;RebirthWorldCharacterService.TryGet(p,out r);
        float skill=RebirthBerserkerRage.RageSkill(p);
        b.AppendLine("[REBIRTH Rage]");
        b.AppendLine("berserker="+HasBerserker(p)+" breadth="+br+"/"+BreadthRequired+" depth="+de+"/"+DepthRequired+" meleeTotal="+total.ToString("0.##"));
        b.AppendLine("trialHits="+(r!=null?r.Progression.BerserkerInitiationMeleeHits:0)+"/"+TrialHitsRequired+" rageSkill="+skill.ToString("0.##",CultureInfo.InvariantCulture)+" rank="+RebirthBerserkerRage.Tier(p)+"/"+RebirthRageBuffs.MaxTier);
        b.AppendLine("active="+active+" kind="+(active?kind:"none")+" activeRank="+tier+" bloodMoon="+RebirthBerserkerRage.IsBloodMoon());
        b.AppendLine("offensiveUnlocked="+(skill>=RebirthBerserkerRage.OffensiveSkill&&RebirthBerserkerRage.HasKnowledge(p,RebirthSurvivorIds.KnowledgeRageOffensive))
            +" bloodUnlocked="+(skill>=RebirthBerserkerRage.BloodSkill&&RebirthBerserkerRage.HasKnowledge(p,RebirthSurvivorIds.KnowledgeRageBlood))
            +" capsules="+RebirthBerserkerRage.CountItem(p,RebirthBerserkerRage.CapsuleItem)+" otherCapsules="+RebirthBerserkerRage.CountItem(p,RebirthBerserkerRage.CapsuleOtherItem)
            +" buffsRegistered="+RebirthRageBuffs.Registered);
        return b.ToString();
    }
    public static string RunVectors(){return "Rage vectors: meleeFamilies="+RebirthWeaponFamilySkillService.GetMeleeSkillIds().Length+" breadth="+BreadthRequired+" depth="+DepthRequired+" trialHits="+TrialHitsRequired+" ranks="+RebirthRageBuffs.MaxTier+" rank(1)="+RebirthBerserkerRage.TierForSkill(1)+" rank(50)="+RebirthBerserkerRage.TierForSkill(50)+" rank(100)="+RebirthBerserkerRage.TierForSkill(100)+" buffs="+RebirthRageBuffs.Registered;}
}
