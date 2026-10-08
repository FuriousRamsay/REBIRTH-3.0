using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk 6 runtime owner for Mining, Logging, Salvage, Farming, Animal Processing and Tracking.
/// Resource yields reuse native HarvestCount tag channels. Animal tracking reuses native
/// Tracking/TrackDistance nav-object eligibility for perkAT01..perkAT05 animals only.
/// Progression awards remain server-authoritative.
/// </summary>
public static class RebirthResourceFieldSkillService
{
    public const string PassiveBuff = "RebirthSurvivorResourceFieldPassives";
    public const string ResourceHarvestCVar = "$rbSurvivorSkillResourceHarvest";
    public const string FarmingHarvestCVar = "$rbSurvivorSkillFarmingHarvest";
    public const string AnimalHarvestCVar = "$rbSurvivorSkillAnimalHarvest";
    public const string WholeAnimalHarvestCVar = "$rbBackgroundWholeAnimalHarvest";
    public const string TrackingTierCVar = "$rbSurvivorSkillTrackingTier";
    public const string TrackingDistanceCVar = "$rbSurvivorSkillTrackingDistance";
    public const string TrackingActiveCVar = "$rbSurvivorSkillTrackingActive";

    private sealed class TrackingState
    {
        public bool WasCrouched;
        public float CrouchStarted;
        public bool HasPosition;
        public Vector3 LastPosition;
        public float FollowDistance;
        public int LastTrackedEntityId = -1;
    }

    private static readonly Dictionary<int, TrackingState> TrackingByEntity = new Dictionary<int, TrackingState>();
    private static bool installed;
    private static float nextPassiveSync;
    private static float nextSample;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Resource/Field Skills] already installed";
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor Resource/Field Skills] installed";
    }

    public static float SignedEndpoint(float skillValue, float negativeAtMinus50, float positiveAt100)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v < 0f) return negativeAtMinus50 * (-v / 50f);
        if (v > 0f) return positiveAt100 * (v / 100f);
        return 0f;
    }

    public static int GetTrackingTier(float skillValue)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v <= 0f) return 1;
        if (v <= 25f) return 2;
        if (v <= 50f) return 3;
        if (v <= 75f) return 4;
        return 5;
    }

    public static float GetTrackingDistance(float skillValue)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v < 0f)
        {
            float t = -v / 50f;
            return Mathf.Lerp(RebirthProgressionRuntimeConfig.TrackingNeutralDistance, RebirthProgressionRuntimeConfig.TrackingMinDistance, t);
        }
        if (v > 0f)
        {
            float t = v / 100f;
            return Mathf.Lerp(RebirthProgressionRuntimeConfig.TrackingNeutralDistance, RebirthProgressionRuntimeConfig.TrackingMaxDistance, t);
        }
        return RebirthProgressionRuntimeConfig.TrackingNeutralDistance;
    }

    public static float GetTrackingAcquireSeconds(float skillValue)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v < 0f)
        {
            float t = -v / 50f;
            return Mathf.Lerp(RebirthProgressionRuntimeConfig.TrackingNeutralAcquireSeconds, RebirthProgressionRuntimeConfig.TrackingNegativeAcquireSeconds, t);
        }
        if (v > 0f)
        {
            float t = v / 100f;
            return Mathf.Lerp(RebirthProgressionRuntimeConfig.TrackingNeutralAcquireSeconds, RebirthProgressionRuntimeConfig.TrackingPositiveAcquireSeconds, t);
        }
        return RebirthProgressionRuntimeConfig.TrackingNeutralAcquireSeconds;
    }

    public static float ApplyHabitatDistanceKnowledge(float baseDistance, bool knowsHabitat)
    {
        if (!knowsHabitat) return Mathf.Max(1f, baseDistance);
        return Mathf.Max(1f, baseDistance * Mathf.Clamp(RebirthProgressionRuntimeConfig.TrackingHabitatDistanceMultiplier, 1f, 1.50f));
    }

    public static float ApplyHabitatAcquireKnowledge(float baseSeconds, bool knowsHabitat)
    {
        if (!knowsHabitat) return Mathf.Max(0.25f, baseSeconds);
        return Mathf.Max(0.25f, baseSeconds * Mathf.Clamp(RebirthProgressionRuntimeConfig.TrackingHabitatAcquireMultiplier, 0.50f, 1f));
    }

    public static void SyncNativePassiveEffects(EntityPlayer player)
    {
        if (player == null || player.Buffs == null) return;
        float mining, logging, salvage, farming, animalProcessing, tracking;
        bool hasCharacter = TryGetSixSkillValues(player, out mining, out logging, out salvage, out farming, out animalProcessing, out tracking);
        if (!hasCharacter)
        {
            SetAllZero(player);
            TrackingByEntity.Remove(player.entityId);
            if (player.Buffs.HasBuff(PassiveBuff)) player.Buffs.RemoveBuff(PassiveBuff);
            return;
        }

        // Mining/Logging/Salvage share native HarvestCount channels, but their correct Skill cannot
        // be chosen from the held tool alone (for example, a pickaxe striking a tree). Keep the
        // shared CVar neutral between harvest calls; the HarvestOnAttack prefix below selects the
        // exact Skill from both tool and target block for the duration of that native harvest call.
        SetCVar(player, ResourceHarvestCVar, 0f);
        SetCVar(player, FarmingHarvestCVar, SignedEndpoint(farming, RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive));
        float animalHarvestDelta = SignedEndpoint(animalProcessing, RebirthProgressionRuntimeConfig.AnimalHarvestNegative, RebirthProgressionRuntimeConfig.AnimalHarvestPositive);
        // Animal Processing Skill directly governs carcass yield. Field-dressing knowledge may remain
        // as domain knowledge, but it does not nullify earned core Skill scaling.
        SetCVar(player, AnimalHarvestCVar, animalHarvestDelta);
        SetCVar(player, WholeAnimalHarvestCVar, RebirthFoodFarmingButcherySignatureService.GetWholeAnimalHarvestDelta(player));
        int trackingTier=GetTrackingTier(tracking);
        float trackingDistance=GetTrackingDistance(tracking);
        trackingDistance=ApplyHabitatDistanceKnowledge(trackingDistance, RebirthKnowledgeService.HasKnowledge(player,"procedure.tracking.habitat"));
        SetCVar(player, TrackingTierCVar, trackingTier);
        SetCVar(player, TrackingDistanceCVar, trackingDistance);
        SetCVar(player, TrackingActiveCVar, UpdateTrackingActivation(player, tracking) ? 1f : 0f);

        if (!player.Buffs.HasBuff(PassiveBuff)) player.Buffs.AddBuff(PassiveBuff);
    }


    /// <summary>
    /// Opens the context for one native GameUtils.HarvestOnAttack call. This is the only place
    /// Mining/Logging/Salvage output is selected, using both the authored tool mapping and the
    /// actual target block. The CVar is reset in EndResourceHarvestAttack.
    /// </summary>
    public static void BeginResourceHarvestAttack(ItemActionData actionData)
    {
        EntityPlayer player = actionData != null && actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayer : null;
        if (player == null || player.Buffs == null || actionData.attackDetails == null) return;
        Block block = actionData.attackDetails.blockBeingDamaged.Block;
        string blockName = block != null ? block.GetBlockName() : string.Empty;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyHarvest(actionData.invData.itemValue, blockName);
        float mining, logging, salvage, farming, animal, tracking;
        if (!TryGetSixSkillValues(player, out mining, out logging, out salvage, out farming, out animal, out tracking))
        {
            SetCVar(player, ResourceHarvestCVar, 0f);
            return;
        }
        float selected = 0f;
        if (skillId == "skill.mining") selected = mining;
        else if (skillId == "skill.logging") selected = logging;
        else if (skillId == "skill.salvage") selected = salvage;
        SetCVar(player, ResourceHarvestCVar, SignedEndpoint(selected, RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive));
    }

    public static void EndResourceHarvestAttack(ItemActionData actionData)
    {
        EntityPlayer player = actionData != null && actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayer : null;
        if (player != null) SetCVar(player, ResourceHarvestCVar, 0f);
    }

    public static void RestoreResourceHarvestValue(EntityPlayer player,float priorValue)
    {
        if(player!=null&&player.Buffs!=null)SetCVar(player,ResourceHarvestCVar,priorValue);
    }

    // Animal Processing practical Skill awards are Phase-8 output evidence only.
    // Corpse-hit damage deliberately has no progression path here.

    public static bool IsTrackableAnimal(EntityAlive entity)
    {
        if (entity == null || (!(entity is EntityAnimal) && !(entity is EntityEnemyAnimal))) return false;
        string tags = entity.EntityTags.ToString() ?? string.Empty;
        return tags.IndexOf("perkAT01", StringComparison.OrdinalIgnoreCase) >= 0
            || tags.IndexOf("perkAT02", StringComparison.OrdinalIgnoreCase) >= 0
            || tags.IndexOf("perkAT03", StringComparison.OrdinalIgnoreCase) >= 0
            || tags.IndexOf("perkAT04", StringComparison.OrdinalIgnoreCase) >= 0
            || tags.IndexOf("perkAT05", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static int GetAnimalTrackingTier(EntityAlive entity)
    {
        if (!IsTrackableAnimal(entity)) return 0;
        string tags = entity.EntityTags.ToString() ?? string.Empty;
        // Native tags list every tracking rank that can detect this animal, not its difficulty.
        // Chicken/rabbit carry AT01 through AT05: their minimum required rank is one.
        if (tags.IndexOf("perkAT01", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        if (tags.IndexOf("perkAT02", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        if (tags.IndexOf("perkAT03", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (tags.IndexOf("perkAT04", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        return 5;
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Chunk 6 Resource/Field Skills] status");
        b.AppendLine("  Mining [PHASE8 ACTUAL WORLD WORK] eligible block HP normalized by live held-tool cadence; Stone Axe/Pickaxe/Auger routes authored");
        b.AppendLine("  Logging [PHASE8 ACTUAL WORLD WORK] tree/wood/stump HP normalized by live held-tool cadence; Axe/Chainsaw routes authored");
        b.AppendLine("  Salvage [PHASE8 ACTUAL OUTPUT] recovered authored salvage resources weighted by target profile and capped by committed work");
        b.AppendLine("  Farming [PHASE8 SUCCESSFUL OUTPUT] exact 33-row mature-crop witness plus Advanced Farming committed-harvest parity; fixed gain per successful harvest, bonus yield does not multiply training");
        b.AppendLine("  Animal Processing [NATIVE-7DTD butcherHarvest][PHASE8 ACTUAL OUTPUT] already-dead carcass resources only; kill shot/corpse-hit damage award zero; bonus output is capped to authored carcass work");
        b.AppendLine("  Tracking [NATIVE-7DTD Tracking+TrackDistance][NEW SKILL HOOK] perkAT01..05 animals only; zombie tracking=DEFERRED NEW-REBIRTH");
        if (player == null) return b.ToString().TrimEnd();

        float mining, logging, salvage, farming, animal, tracking;
        bool has = TryGetSixSkillValues(player, out mining, out logging, out salvage, out farming, out animal, out tracking);
        string held = player.inventory != null ? RebirthProgressionRuntimeConfig.ClassifyHarvestTool(player.inventory.holdingItemItemValue) : string.Empty;
        b.Append("  entity=").Append(player.entityId).Append(" hasCharacter=").Append(has)
            .Append(" mining=").Append(mining.ToString("0.##"))
            .Append(" logging=").Append(logging.ToString("0.##"))
            .Append(" salvage=").Append(salvage.ToString("0.##"))
            .Append(" farming=").Append(farming.ToString("0.##"))
            .Append(" animal=").Append(animal.ToString("0.##"))
            .Append(" tracking=").Append(tracking.ToString("0.##")).AppendLine();
        b.Append("  heldResourceSkill=").Append(string.IsNullOrEmpty(held) ? "<none>" : held)
            .Append(" resourceHarvestDelta=").Append(player.Buffs.GetCustomVar(ResourceHarvestCVar).ToString("0.###"))
            .Append(" farmingHarvestDelta=").Append(player.Buffs.GetCustomVar(FarmingHarvestCVar).ToString("0.###"))
            .Append(" animalHarvestDelta=").Append(player.Buffs.GetCustomVar(AnimalHarvestCVar).ToString("0.###"))
            .Append(" wholeAnimalDelta=").Append(player.Buffs.GetCustomVar(WholeAnimalHarvestCVar).ToString("0.###")).AppendLine();
        b.Append("  trackingTier=").Append(player.Buffs.GetCustomVar(TrackingTierCVar).ToString("0"))
            .Append(" distance=").Append(player.Buffs.GetCustomVar(TrackingDistanceCVar).ToString("0.##"))
            .Append(" active=").Append(player.Buffs.GetCustomVar(TrackingActiveCVar) >= 0.5f)
            .Append(" crouching=").Append(player.IsCrouching)
            .Append(" acquireSeconds=").Append(ApplyHabitatAcquireKnowledge(GetTrackingAcquireSeconds(tracking),RebirthKnowledgeService.HasKnowledge(player,"procedure.tracking.habitat")).ToString("0.##"))
            .Append(" habitat=").Append(RebirthKnowledgeService.HasKnowledge(player,"procedure.tracking.habitat"));
        return b.ToString();
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }

    private static void ResetRuntime()
    {
        TrackingByEntity.Clear(); nextPassiveSync = 0f; nextSample = 0f;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        float now = Time.realtimeSinceStartup;
        try { EntityPlayerLocal medLocal = world.GetPrimaryPlayer(); if (medLocal != null) RebirthMedicalPractice.Observe(medLocal); } catch { }   // heal-over-time credit also from the game update (the EntityAlive.OnUpdateLive postfix is not reached for the local player)
        if (world.IsRemote())
        {
            if (now >= nextPassiveSync)
            {
                nextPassiveSync = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.ResourceFieldPassiveSyncSeconds);
                EntityPlayerLocal local = world.GetPrimaryPlayer();
                if (local != null) SyncNativePassiveEffects(local);
            }
            return;
        }
        if (connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (world.Players == null || world.Players.list == null) return;
        List<EntityPlayer> players = world.Players.list;
        if (now >= nextPassiveSync)
        {
            nextPassiveSync = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.ResourceFieldPassiveSyncSeconds);
            for (int i = 0; i < players.Count; i++) SyncNativePassiveEffects(players[i]);
        }
        if (now < nextSample) return;
        nextSample = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.ResourceFieldSampleSeconds);
        for (int i = 0; i < players.Count; i++) SampleServerTracking(players[i]);
    }

    private static bool UpdateTrackingActivation(EntityPlayer player, float trackingSkill)
    {
        TrackingState state;
        if (!TrackingByEntity.TryGetValue(player.entityId, out state) || state == null)
        {
            state = new TrackingState(); TrackingByEntity[player.entityId] = state;
        }
        bool crouched = player.Buffs != null && player.IsCrouching && player.AttachedToEntity == null;
        float now = Time.realtimeSinceStartup;
        if (!crouched)
        {
            state.WasCrouched = false;
            state.CrouchStarted = now;
            state.LastTrackedEntityId = -1;
            state.FollowDistance = 0f;
            return false;
        }
        if (!state.WasCrouched)
        {
            state.WasCrouched = true;
            state.CrouchStarted = now;
            state.LastTrackedEntityId = -1;
            state.FollowDistance = 0f;
        }
        bool knowsHabitat=RebirthKnowledgeService.HasKnowledge(player,"procedure.tracking.habitat");
        float acquireSeconds=ApplyHabitatAcquireKnowledge(GetTrackingAcquireSeconds(trackingSkill),knowsHabitat);
        return now - state.CrouchStarted >= acquireSeconds;
    }

    private static void SampleServerTracking(EntityPlayer player)
    {
        if (player == null || player.Buffs == null) return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) { TrackingByEntity.Remove(player.entityId); return; }
        TrackingState state;
        if (!TrackingByEntity.TryGetValue(player.entityId, out state) || state == null)
        {
            state = new TrackingState(); TrackingByEntity[player.entityId] = state;
        }
        Vector3 pos = player.position;
        Vector3 previous = state.HasPosition ? state.LastPosition : pos;
        float moved = 0f;
        if (state.HasPosition)
        {
            float dx = pos.x - state.LastPosition.x, dz = pos.z - state.LastPosition.z;
            moved = Mathf.Sqrt(dx * dx + dz * dz);
        }
        state.LastPosition = pos; state.HasPosition = true;
        if (player.Buffs.GetCustomVar(TrackingActiveCVar) < 0.5f) { state.LastTrackedEntityId = -1; state.FollowDistance = 0f; return; }

        int tier = Mathf.Clamp((int)Math.Round(player.Buffs.GetCustomVar(TrackingTierCVar)), 1, 5);
        float distance = Mathf.Max(1f, player.Buffs.GetCustomVar(TrackingDistanceCVar));
        int targetTier;
        EntityAlive target = FindNearestTrackableAnimal(player, tier, distance, out targetTier);
        if (target == null) { state.LastTrackedEntityId = -1; state.FollowDistance = 0f; return; }

        if (state.LastTrackedEntityId != target.entityId)
        {
            state.LastTrackedEntityId = target.entityId;
            state.FollowDistance = 0f;
            float award = Mathf.Min(0.50f, RebirthProgressionRuntimeConfig.TrackingAcquireAward * (1f + 0.15f * (targetTier - 1)));
            AwardTracking(player, award, "phase8:tracking:acquire:" + target.entityId);
            return; // Movement before acquiring this target is not following it.
        }

        if (player.AttachedToEntity != null || moved < 0.10f || moved > RebirthProgressionRuntimeConfig.ResourceFieldMaxSampleDistance) return;
        float credited = RebirthTrackingFollowEvidence.Credit(previous.x,previous.z,pos.x,pos.z,
            target.position.x,target.position.z,RebirthProgressionRuntimeConfig.ResourceFieldMaxSampleDistance);
        if(credited<=0f)return;
        state.FollowDistance += credited;
        if (state.FollowDistance >= RebirthProgressionRuntimeConfig.TrackingFollowDistance)
        {
            state.FollowDistance -= RebirthProgressionRuntimeConfig.TrackingFollowDistance;
            float award = Mathf.Min(0.25f, RebirthProgressionRuntimeConfig.TrackingFollowAward * (1f + 0.10f * (targetTier - 1)));
            AwardTracking(player, award, "phase8:tracking:follow:" + target.entityId);
        }
    }

    private static void AwardTracking(EntityPlayer player,float raw,string source)
    {
        if(player==null||raw<=0f)return;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId="skill.tracking",SourceKey=source,ReferenceDescription="validated track acquisition/follow",AuthoritativeSuccess=true,
            Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw,MinimumInterval=RebirthProgressionRuntimeConfig.TrackingRepeatSeconds
        };
        RebirthSkillTrainingComputation computation;float gained,attribute;
        RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out computation,out gained,out attribute);
    }

    private static EntityAlive FindNearestTrackableAnimal(EntityPlayer player, int allowedTier, float radius, out int targetTier)
    {
        targetTier = 0;
        if (player == null || player.world == null || player.world.Entities == null || player.world.Entities.list == null) return null;
        float best = radius * radius;
        EntityAlive bestEntity = null;
        List<Entity> list = player.world.Entities.list;
        for (int i = 0; i < list.Count; i++)
        {
            EntityAlive candidate = list[i] as EntityAlive;
            if (candidate == null || candidate == player || candidate.IsDead() || !IsTrackableAnimal(candidate)) continue;
            int tier = GetAnimalTrackingTier(candidate);
            if (tier <= 0 || tier > allowedTier) continue;
            float d2 = (candidate.position - player.position).sqrMagnitude;
            if (d2 > best) continue;
            best = d2; bestEntity = candidate; targetTier = tier;
        }
        return bestEntity;
    }

    private static bool TryGetSixSkillValues(EntityPlayer player, out float mining, out float logging, out float salvage, out float farming, out float animal, out float tracking)
    {
        mining = logging = salvage = farming = animal = tracking = 0f;
        if (player == null) return false;
        if (player.world != null && player.world.IsRemote())
        {
            RebirthSurvivorOwnerStateSnapshot s = RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if (s == null || !s.RebirthModeEnabled || !s.HasCharacter || !s.DefinitionsCompatible) return false;
            mining = SnapshotSkill(s, "skill.mining"); logging = SnapshotSkill(s, "skill.logging"); salvage = SnapshotSkill(s, "skill.salvage"); farming = SnapshotSkill(s, "skill.farming"); animal = SnapshotSkill(s, "skill.animal_processing"); tracking = SnapshotSkill(s, "skill.tracking");
            return true;
        }
        RebirthWorldCharacterRecord r;
        if (!RebirthWorldCharacterService.TryGet(player, out r) || r == null || !r.IsComplete || r.Progression == null) return false;
        mining = RecordSkill(r, "skill.mining"); logging = RecordSkill(r, "skill.logging"); salvage = RecordSkill(r, "skill.salvage"); farming = RecordSkill(r, "skill.farming"); animal = RecordSkill(r, "skill.animal_processing"); tracking = RecordSkill(r, "skill.tracking");
        return true;
    }

    private static float SnapshotSkill(RebirthSurvivorOwnerStateSnapshot s, string id)
    {
        if(s==null)return 0f;
        float practical=0f,attribute=RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;bool found=false;
        if(s.Skills!=null)for(int i=0;i<s.Skills.Count;i++)if(s.Skills[i]!=null&&string.Equals(s.Skills[i].Id,id,StringComparison.OrdinalIgnoreCase)){practical=s.Skills[i].Value;found=true;break;}
        if(!found)return 0f;
        string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        if(!string.IsNullOrEmpty(attributeId)&&s.Attributes!=null)
            for(int i=0;i<s.Attributes.Count;i++)if(s.Attributes[i]!=null&&string.Equals(s.Attributes[i].Id,attributeId,StringComparison.OrdinalIgnoreCase)){attribute=s.Attributes[i].Current;break;}
        return string.IsNullOrEmpty(attributeId)?practical:RebirthSkillOutcomeValueService.ApplyAttributeContribution(id,practical,attribute);
    }

    private static float RecordSkill(RebirthWorldCharacterRecord r, string id)
    {
        if(r==null||r.Progression==null)return 0f;
        RebirthSkillRuntimeState state;if(!r.Progression.Skills.TryGetValue(id,out state)||state==null)return 0f;
        string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        float attribute=RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;
        RebirthAttributeRuntimeState a;
        if(!string.IsNullOrEmpty(attributeId)&&r.Progression.Attributes.TryGetValue(attributeId,out a)&&a!=null)attribute=a.Current;
        return string.IsNullOrEmpty(attributeId)?state.Value:RebirthSkillOutcomeValueService.ApplyAttributeContribution(id,state.Value,attribute);
    }

    private static void SetAllZero(EntityPlayer player)
    {
        SetCVar(player, ResourceHarvestCVar, 0f); SetCVar(player, FarmingHarvestCVar, 0f); SetCVar(player, AnimalHarvestCVar, 0f); SetCVar(player, WholeAnimalHarvestCVar, 0f);
        SetCVar(player, TrackingTierCVar, 0f); SetCVar(player, TrackingDistanceCVar, 0f); SetCVar(player, TrackingActiveCVar, 0f);
    }

    private static void SetCVar(EntityPlayer player, string name, float value)
    {
        if (player == null || player.Buffs == null) return;
        if (Math.Abs(player.Buffs.GetCustomVar(name) - value) > 0.0005f) player.Buffs.SetCustomVar(name, value);
    }
}
