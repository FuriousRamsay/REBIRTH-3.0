using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

/// <summary>PC010 Berserker/Rage authority. Rage is an Advanced LBD Skill layered over the ten real melee-family Skills.</summary>
public static class RebirthRageService
{
    public const string ActiveCVar="$rbRageActive", TierCVar="$rbRageTier";
    private sealed class ActiveState { public int PlayerId; public string CreationId; public float Until; public string Tier; }
    private static readonly Dictionary<int,ActiveState> Active=new Dictionary<int,ActiveState>();
    private static int[] updateIds=new int[4];
    private static bool installed;
    public static int TrialHitsRequired=25, BreadthRequired=6, DepthRequired=3;
    public static float BreadthLevel=20f, DepthLevel=35f, ActivationEnergy=15f, OffensiveEnergyExtra=5f, BloodEnergyExtra=10f, DurationSeconds=12f;
    public static float BasicStaminaDelta=-.10f, ControlledStaminaDelta=-.15f, OffensiveStaminaDelta=-.10f, BloodStaminaDelta=-.18f;
    public static float BasicSpeedDelta=.10f, ControlledSpeedDelta=.10f, OffensiveSpeedDelta=.15f, BloodSpeedDelta=.20f;
    public static bool BloodRequiresBloodMoon=true;

    public static string Install()
    {
        if(installed)return "[REBIRTH Rage] already installed";
        LoadTunables();
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnStart));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnStop));
        installed=true; return "[REBIRTH Rage] installed";
    }

    public static bool IsActive(EntityPlayer player,out string tier)
    {
        tier=string.Empty;if(player==null||player.IsDead())return false;
        if(player.world!=null&&player.world.IsRemote())
        {
            if(player.Buffs==null||player.Buffs.GetCustomVar(ActiveCVar)<=0f)return false;
            int marker=Mathf.RoundToInt(player.Buffs.GetCustomVar(TierCVar)); tier=marker==2?"controlled":marker==3?"offensive":marker==4?"blood":"basic"; return true;
        }
        ActiveState s;lock(Active)if(!Active.TryGetValue(player.entityId,out s)||s==null||Time.realtimeSinceStartup>=s.Until||!MatchesCharacter(player,s))return false;tier=s.Tier??"basic";return true;
    }

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
        string activeTier;bool wasActive=IsActive(player,out activeTier);
        if(!record.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBerserker))
        {
            int breadth,depth;float total;TryGetMeleeMastery(player,out breadth,out depth,out total);
            if(breadth>=BreadthRequired&&depth>=DepthRequired){record.Progression.BerserkerInitiationMeleeHits=Math.Min(TrialHitsRequired,record.Progression.BerserkerInitiationMeleeHits+1);if(record.Progression.BerserkerInitiationMeleeHits>=TrialHitsRequired)Acquire(player,identity,record);else{record.Touch("berserker-trial-hit");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-trial-hit");}}
            return;
        }
        if(wasActive)AwardRage(player,target,damage,activeTier,attackingItem);
        else TryActivate(player,"basic",out _); // triggering hit starts Rage but never earns retroactive Rage XP.
    }

    private static void Acquire(EntityPlayer player,RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord record)
    {
        string grantReason; if(!RebirthAdvancedDisciplineRegistry.CanAcquireFromRecord(RebirthSurvivorIds.DisciplineBerserker,record,RebirthSurvivorIds.TrialBerserkerInitiation,out grantReason)){record.Progression.BerserkerInitiationMeleeHits=Math.Min(record.Progression.BerserkerInitiationMeleeHits,Math.Max(0,TrialHitsRequired-1));record.Touch("berserker-grant-deferred");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-grant-deferred");return;}
        record.Progression.CompletedTrialIds.Add(RebirthSurvivorIds.TrialBerserkerInitiation);record.Progression.AcquiredDisciplineIds.Add(RebirthSurvivorIds.DisciplineBerserker);record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeRageBasic);
        RebirthSkillRuntimeState rage;if(record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillRage,out rage)&&rage!=null)rage.Value=Math.Max(1f,rage.Value);
        record.Touch("berserker-acquired");RebirthWorldCharacterRepository.SaveIfDirty(identity,"berserker-acquired");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"berserker-acquired");
    }

    public static bool TryActivate(EntityPlayer player,string requestedTier,out string reason)
    {
        reason=string.Empty;if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason="Rage is available only when Character Progression is Rebirth.";return false;}if(player==null||player.world==null||player.world.IsRemote()){reason="server authority required";return false;}if(player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)){reason="Rage requires a living, completed Survivor.";return false;}if(!HasBerserker(player)){reason="Berserker initiation is not complete.";return false;}
        RebirthWorldCharacterRecord owner;
        if(!RebirthWorldCharacterService.TryGet(player,out owner)||owner==null||!owner.IsComplete
            ||!RebirthSurvivorRequestScope.Matches(owner.Origin?.CreationId,owner.Origin?.CreationId))
        {reason="Rage character identity unavailable.";return false;}
        string tier;if(!TryResolveTier(player,requestedTier,out tier,out reason))return false;RebirthMetabolismState m=RebirthMetabolismStateRepository.GetOrCreate(player);if(m==null){reason="Energy state unavailable.";return false;}float cost=ActivationEnergy+(tier=="offensive"?OffensiveEnergyExtra:tier=="blood"?BloodEnergyExtra:0f);if(m.Energy+0.001f<cost){reason="Not enough Energy for "+tier+" Rage; requires "+cost.ToString("0.##",CultureInfo.InvariantCulture)+" Energy.";return false;}m.Energy=Math.Max(0f,m.Energy-cost);m.Touch();RebirthMetabolismStateRepository.SaveIfDirty("rage-activation");
        ActiveState s=new ActiveState{PlayerId=player.entityId,CreationId=owner.Origin.CreationId,Until=Time.realtimeSinceStartup+DurationSeconds,Tier=tier};lock(Active)Active[player.entityId]=s;SetMarkers(player,s);reason=tier+" Rage active.";return true;
    }

    private static float GetRageSkill(EntityPlayer p) { float v; return RebirthServiceCraftSkillService.TryGetPracticalSkillValue(p,RebirthSurvivorIds.SkillRage,out v)?v:0f; }

    private static bool TryResolveTier(EntityPlayer p,string requested,out string tier,out string reason)
    {
        tier=string.Empty;reason=string.Empty;RebirthWorldCharacterRecord r;if(!RebirthWorldCharacterService.TryGet(p,out r)||r==null||r.Progression==null){reason="Rage progression state unavailable.";return false;}string q=(requested??"basic").Trim().ToLowerInvariant();float v=GetRageSkill(p);
        if(q=="basic"){if(v<1f||!r.Progression.KnowledgeIds.Contains(RebirthSurvivorIds.KnowledgeRageBasic)){reason="Basic Rage Knowledge and Rage Skill 1 are required.";return false;}tier="basic";return true;}
        if(q=="controlled"){if(v<25f||!r.Progression.KnowledgeIds.Contains(RebirthSurvivorIds.KnowledgeRageControlled)){reason="Controlled Rage Knowledge and Rage Skill 25 are required.";return false;}tier="controlled";return true;}
        if(q=="offensive"){if(v<50f||!r.Progression.KnowledgeIds.Contains(RebirthSurvivorIds.KnowledgeRageOffensive)){reason="Offensive Rage Knowledge and Rage Skill 50 are required.";return false;}tier="offensive";return true;}
        if(q=="blood"){if(v<80f||!r.Progression.KnowledgeIds.Contains(RebirthSurvivorIds.KnowledgeRageBlood)){reason="Blood Rage Knowledge and Rage Skill 80 are required.";return false;}if(BloodRequiresBloodMoon&&!SkyManager.IsBloodMoonVisible()){reason="Blood Rage can only be activated during an active Blood Moon.";return false;}tier="blood";return true;}
        reason="Unknown Rage tier: "+q;return false;
    }

    private static void AwardRage(EntityPlayer p,EntityAlive target,float damage,string tier,ItemValue attackingItem)
    {
        if(p==null||target==null||attackingItem==null||damage<=0f)return;
        ActiveState active;lock(Active)Active.TryGetValue(p.entityId,out active);if(active==null)return;
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
        if(v>=1f)c|=r.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeRageBasic); // Advanced Rage techniques are learned from Chunk-J literature, then gated by Skill in ResolveTier.
        if(c){r.Touch("rage-knowledge");RebirthWorldCharacterRepository.SaveIfDirty(id,"rage-knowledge");RebirthSurvivorNetworkService.SendOwnerState(p,0L,true,"rage-knowledge");}
    }

    public static float GetStaminaDelta(EntityPlayer p){string t;if(!IsActive(p,out t))return 0f;return t=="controlled"?ControlledStaminaDelta:t=="offensive"?OffensiveStaminaDelta:t=="blood"?BloodStaminaDelta:BasicStaminaDelta;}
    public static float GetSpeedDelta(EntityPlayer p){string t;if(!IsActive(p,out t))return 0f;return t=="controlled"?ControlledSpeedDelta:t=="offensive"?OffensiveSpeedDelta:t=="blood"?BloodSpeedDelta:BasicSpeedDelta;}
    private static void LoadTunables()
    {
        XDocument doc=XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath);XElement e=doc.Root?.Element("tunables")?.Element("rage");if(e==null)throw new InvalidOperationException("advanced_disciplines.xml missing Rage tunables");
        BreadthLevel=F(e,"breadth_level",20f);BreadthRequired=Math.Max(1,I(e,"breadth_families",6));DepthLevel=F(e,"depth_level",35f);DepthRequired=Math.Max(1,I(e,"depth_families",3));TrialHitsRequired=Math.Max(1,I(e,"trial_melee_hits",25));
        ActivationEnergy=Math.Max(0f,F(e,"activation_energy",15f));OffensiveEnergyExtra=Math.Max(0f,F(e,"offensive_energy_extra",5f));BloodEnergyExtra=Math.Max(0f,F(e,"blood_energy_extra",10f));DurationSeconds=Math.Max(1f,F(e,"duration_seconds",12f));
        BasicStaminaDelta=F(e,"basic_stamina_delta",-.10f);ControlledStaminaDelta=F(e,"controlled_stamina_delta",-.15f);OffensiveStaminaDelta=F(e,"offensive_stamina_delta",-.10f);BloodStaminaDelta=F(e,"blood_stamina_delta",-.18f);
        BasicSpeedDelta=F(e,"basic_speed_delta",.10f);ControlledSpeedDelta=F(e,"controlled_speed_delta",.10f);OffensiveSpeedDelta=F(e,"offensive_speed_delta",.15f);BloodSpeedDelta=F(e,"blood_speed_delta",.20f);BloodRequiresBloodMoon=B(e,"blood_requires_blood_moon",true);
    }
    private static string A(XElement e,string n){return ((string)e.Attribute(n)??string.Empty).Trim();}
    private static int I(XElement e,string n,int d){int v;return int.TryParse(A(e,n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}
    private static float F(XElement e,string n,float d){float v;return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:d;}
    private static bool B(XElement e,string n,bool d){bool v;return bool.TryParse(A(e,n),out v)?v:d;}

    private static void SetMarkers(EntityPlayer p,ActiveState s){if(p==null||p.Buffs==null)return;p.Buffs.SetCustomVar(ActiveCVar,s!=null?1f:0f);p.Buffs.SetCustomVar(TierCVar,s==null?0f:s.Tier=="controlled"?2f:s.Tier=="offensive"?3f:s.Tier=="blood"?4f:1f);}
    private static bool MatchesCharacter(EntityPlayer player,ActiveState state)
    {
        if(player==null||state==null||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return false;
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete
            &&RebirthSurvivorRequestScope.Matches(state.CreationId,record.Origin?.CreationId);
    }
    private static void OnUpdate(ref ModEvents.SGameUpdateData d)
    {
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(world==null||world.IsRemote())return;
        int count;
        lock(Active)
        {
            count=Active.Count;if(count==0)return;
            if(updateIds.Length<count)Array.Resize(ref updateIds,Math.Max(count,updateIds.Length*2));
            Active.Keys.CopyTo(updateIds,0);
        }
        float now=Time.realtimeSinceStartup;
        for(int i=0;i<count;i++)
        {
            ActiveState state;lock(Active)if(!Active.TryGetValue(updateIds[i],out state))continue;
            var player=world.GetEntity(updateIds[i]) as EntityPlayer;
            if(state!=null&&now<state.Until&&MatchesCharacter(player,state))continue;
            if(player!=null)SetMarkers(player,null);
            lock(Active)Active.Remove(updateIds[i]);
        }
    }
    private static void OnStart(ref ModEvents.SGameStartingData d){lock(Active)Active.Clear();}
    private static void OnStop(ref ModEvents.SWorldShuttingDownData d){lock(Active)Active.Clear();}

    public static string BuildStatus(EntityPlayer p){StringBuilder b=new StringBuilder();int br,de;float total;TryGetMeleeMastery(p,out br,out de,out total);string tier;bool active=IsActive(p,out tier);RebirthWorldCharacterRecord r;RebirthWorldCharacterService.TryGet(p,out r);b.AppendLine("[REBIRTH Rage PC010]");b.AppendLine("berserker="+HasBerserker(p)+" breadth="+br+"/"+BreadthRequired+" depth="+de+"/"+DepthRequired+" meleeTotal="+total.ToString("0.##"));b.AppendLine("trialHits="+(r!=null?r.Progression.BerserkerInitiationMeleeHits:0)+"/"+TrialHitsRequired+" active="+active+" tier="+(active?tier:"none"));b.AppendLine("bloodMoonVisible="+SkyManager.IsBloodMoonVisible()+" bloodRequiresBloodMoon="+BloodRequiresBloodMoon+" activationEnergy="+ActivationEnergy.ToString("0.##",CultureInfo.InvariantCulture)+" duration="+DurationSeconds.ToString("0.##",CultureInfo.InvariantCulture));return b.ToString();}
    public static string RunVectors(){return "Rage vectors: meleeFamilies="+RebirthWeaponFamilySkillService.GetMeleeSkillIds().Length+" breadth="+BreadthRequired+" depth="+DepthRequired+" trialHits="+TrialHitsRequired+" energyCost="+ActivationEnergy.ToString("0.##",CultureInfo.InvariantCulture)+" bloodMoonOnly="+BloodRequiresBloodMoon+" dataDriven=true firstHitRetroactiveXp=false";}
}
