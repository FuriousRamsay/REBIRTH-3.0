using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthBoundUndeadInteractionKind : byte
{
    Condition = 0,
    Bind = 1
}

public sealed class RebirthBoundUndeadTierRule
{
    public string TierId;
    public float SkillRequired;
    public string KnowledgeId;
    public int CapacityCost;
}

public sealed class RebirthBlackMagicSummonDefinition
{
    public string EntityClassId;
    public string KnowledgeId;
    public float SkillRequired;
    public int CapacityCost;
    public string Role;
    public bool AuthoredAvailable;
    public bool RuntimeAvailable;
    public string Audit;
}

/// <summary>
/// PC009 / Advanced Disciplines Chunk G authority for individual undead Conditioning,
/// permanent Binding, Bound Undead Capacity and the source-audited summoning ledger.
///
/// Temporary domination remains owned by RebirthBlackMagicService. This service only
/// creates a permanent companion identity after a fully conditioned target passes the
/// final server-side binding commit. Bound undead intentionally have no dog inventory,
/// no Beastmaster Animal Capacity and no Charismatic Nature dependency.
/// </summary>
public static class RebirthBoundUndeadService
{
    private sealed class ConditioningExercise
    {
        public RebirthNpcStableId StableId;
        public int OwnerEntityId;
        public float DominationStartedRealtime;
        public bool Completed;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, RebirthBoundUndeadTierRule> TierRules = new Dictionary<string, RebirthBoundUndeadTierRule>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> KnowledgeMilestones = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<RebirthBlackMagicSummonDefinition> Summons = new List<RebirthBlackMagicSummonDefinition>();
    private static readonly Dictionary<int, ConditioningExercise> Exercises = new Dictionary<int, ConditioningExercise>();
    private static readonly Dictionary<int, RebirthNpcStableId> LiveByEntity = new Dictionary<int, RebirthNpcStableId>();
    private static readonly Dictionary<RebirthNpcStableId, int> EntityByStable = new Dictionary<RebirthNpcStableId, int>();
    private sealed class FollowPathStamp { public Vector3 OwnerPosition; public float SubmittedRealtime; }
    private static readonly Dictionary<RebirthNpcStableId, FollowPathStamp> FollowPathByStable = new Dictionary<RebirthNpcStableId, FollowPathStamp>();

    private static bool ready, eventsInstalled;
    private static float maxDistance = 6f, conditioningPerExercise = 20f, conditioningRequired = 100f;
    private static int baseBoundCapacity = 1, maxBoundCapacity = 5;
    private static float capacitySkillStep = 25f, followDistance = 5f, recallDistance = 60f, reportHealthFraction = .60f;
    private static float nextTick;
    private static long exercisesArmed, exercisesCompleted, bindingsAttempted, bindingsCompleted, bindingDeniedCapacity, bindingDeniedGlobal, reportsForDuty, lifecycleDeaths;

    public static string Install(Harmony harmony)
    {
        string report = LoadDefinitions();
        if (!eventsInstalled)
        {
            ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
            ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
            ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
            ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
            eventsInstalled = true;
        }
        ClearRuntime();
        return report;
    }

    public static string LoadDefinitions()
    {
        TierRules.Clear(); KnowledgeMilestones.Clear(); Summons.Clear(); ready = false;
        string path = RebirthAdvancedDisciplineRegistry.SourcePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Advanced Disciplines configuration is unavailable for Bound Undead.", path);
        XDocument doc = XDocument.Load(path); XElement root = doc.Root, wd = root == null ? null : root.Element("witch_doctor");
        XElement binding = wd == null ? null : wd.Element("binding");
        if (binding == null) throw new InvalidDataException("advanced_disciplines.xml is missing witch_doctor/binding definitions");
        maxDistance = Math.Max(2f, F(wd, "maximum_interaction_distance", 6f));
        conditioningPerExercise = Math.Max(1f, F(binding, "conditioning_progress_per_exercise", 20f));
        conditioningRequired = Mathf.Clamp(F(binding, "conditioning_required", 100f), 1f, 100f);
        baseBoundCapacity = Math.Max(1, I(binding, "base_bound_capacity", 1));
        capacitySkillStep = Math.Max(1f, F(binding, "capacity_skill_step", 25f));
        maxBoundCapacity = Math.Max(baseBoundCapacity, I(binding, "max_bound_capacity", 5));
        followDistance = Math.Max(2f, F(binding, "follow_distance", 5f));
        recallDistance = Math.Max(followDistance + 5f, F(binding, "recall_distance", 60f));
        reportHealthFraction = Mathf.Clamp01(F(binding, "report_for_duty_health_fraction", .60f));
        foreach (XElement e in binding.Elements("tier"))
        {
            string id = A(e, "id").ToLowerInvariant(); if (id.Length == 0) continue;
            TierRules[id] = new RebirthBoundUndeadTierRule { TierId = id, SkillRequired = F(e, "skill", 100f), KnowledgeId = A(e, "knowledge"), CapacityCost = Math.Max(1, I(e, "capacity", 1)) };
        }
        foreach (XElement e in binding.Elements("knowledge_milestone")) { string id = A(e, "id"); if (id.Length > 0) KnowledgeMilestones[id] = F(e, "skill", 100f); }
        XElement summoning = wd.Element("summoning");
        if (summoning != null)
        {
            foreach (XElement e in summoning.Elements("summon"))
            {
                string cls = A(e, "entity_class"); if (cls.Length == 0) continue;
                bool authored = B(e, "authored_available", false); int classId = EntityClass.FromString(cls); EntityClass definition = null;
                bool present = classId > 0 && EntityClass.list != null && EntityClass.list.TryGetValue(classId, out definition) && definition != null;
                Summons.Add(new RebirthBlackMagicSummonDefinition { EntityClassId = cls, KnowledgeId = A(e, "knowledge"), SkillRequired = F(e, "skill", 100f), CapacityCost = Math.Max(1, I(e, "capacity", 1)), Role = A(e, "role"), AuthoredAvailable = authored, RuntimeAvailable = authored && present, Audit = A(e, "audit") + (present ? ";entityClassPresent=true" : ";entityClassPresent=false") });
            }
        }
        List<string> errors = ValidateAuthoring(); if (errors.Count > 0) throw new InvalidDataException("Bound Undead authoring invalid: " + string.Join(" | ", errors.ToArray()));
        ready = true;
        return "boundUndead tiers=" + TierRules.Count + " capacity=" + baseBoundCapacity + ".." + maxBoundCapacity + " summons=" + Summons.Count + " available=" + CountAvailableSummons();
    }

    public static int GetBoundCapacity(EntityPlayer player)
    {
        Ensure(); if (!RebirthBlackMagicService.HasWitchDoctor(player)) return 0;
        float skill = RebirthBlackMagicService.GetBlackMagicSkill(player);
        int cap = baseBoundCapacity + (int)Math.Floor(Math.Max(0f, skill) / capacitySkillStep);
        return Math.Max(0, Math.Min(maxBoundCapacity, cap));
    }

    public static bool TryGetBoundCapacityUsage(EntityPlayer player, out int used, out int capacity, out int count)
    {
        used = 0; count = 0; capacity = GetBoundCapacity(player); if (player == null) return false;
        string owner; if (!TryOwnerId(player, out owner)) return false;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (r == null || r.BoundUndead == null || !r.BoundUndead.IsBound || r.Ownership == null || r.Lifecycle != null && r.Lifecycle.TombstoneState) continue;
            if (!string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase)) continue;
            used += Math.Max(1, r.BoundUndead.CapacityCost); count++;
        }
        return true;
    }

    public static bool CanPresentConditioning(EntityAlive target, EntityPlayer player, out string reason)
    {
        Ensure(); reason = string.Empty; if (!BasicInteraction(target, player, out reason)) return false;
        int owner; string tier; int cost; float started;
        if (!RebirthBlackMagicService.TryGetControlInfo(target.entityId, out owner, out tier, out cost, out started) || owner != player.entityId) { reason = "Temporary domination by you is required."; return false; }
        RebirthBlackMagicService.RefreshKnowledgeMilestones(player);
        if (!RebirthKnowledgeService.HasKnowledge(player, RebirthSurvivorIds.KnowledgeBlackMagicUndeadConditioning)) { reason = Localization.Get("xuiRebirthKnowledgeUndeadConditioning") + " required."; return false; }
        RebirthNpcStableId stable; RebirthNpcPersistentRecordView record;
        if (TryResolveRecord(target, out stable, out record) && record != null && record.BoundUndead != null && record.BoundUndead.IsBound) { reason = "Target is already permanently bound."; return false; }
        reason = "Arm a combat-obedience Conditioning exercise."; return true;
    }

    public static bool CanPresentBinding(EntityAlive target, EntityPlayer player, out string reason)
    {
        Ensure(); reason = string.Empty; if (!BasicInteraction(target, player, out reason)) return false;
        int owner; string tier; int dominationCost; float started;
        if (!RebirthBlackMagicService.TryGetControlInfo(target.entityId, out owner, out tier, out dominationCost, out started) || owner != player.entityId) { reason = "Temporary domination by you is required."; return false; }
        RebirthNpcStableId stable; RebirthNpcPersistentRecordView record;
        if (!TryResolveRecord(target, out stable, out record) || record == null || record.BoundUndead == null) { reason = "This individual has not been conditioned."; return false; }
        if (record.BoundUndead.IsBound) { reason = "Target is already permanently bound."; return false; }
        if (record.BoundUndead.ConditioningProgress + .001f < conditioningRequired) { reason = "Conditioning " + record.BoundUndead.ConditioningProgress.ToString("0", CultureInfo.InvariantCulture) + "/" + conditioningRequired.ToString("0", CultureInfo.InvariantCulture) + "."; return false; }
        RebirthBoundUndeadTierRule rule; if (!TierRules.TryGetValue(tier, out rule)) { reason = "No binding rule for tier " + tier + "."; return false; }
        RebirthBlackMagicService.RefreshKnowledgeMilestones(player); float skill = RebirthBlackMagicService.GetBlackMagicPracticalSkill(player);
        if (skill + .001f < rule.SkillRequired) { reason = "Black Magic " + skill.ToString("0.##", CultureInfo.InvariantCulture) + "/" + rule.SkillRequired.ToString("0", CultureInfo.InvariantCulture) + " for " + tier + " binding."; return false; }
        if (!RebirthKnowledgeService.HasKnowledge(player, rule.KnowledgeId)) { reason = RebirthKnowledgeService.GetDisplayName(rule.KnowledgeId) + " required."; return false; }
        if (!RebirthSpecialPantherService.ValidateInfectedAnimalBindingHandling(target, player, out reason)) return false;
        int boundCost; if (!TryResolveBoundCost(target, tier, rule.CapacityCost, out boundCost, out reason)) return false;
        int used, cap, count; if (!TryGetBoundCapacityUsage(player, out used, out cap, out count)) { reason = "Bound Undead Capacity unavailable."; return false; }
        if (used + boundCost > cap) { reason = "Bound Undead Capacity " + used + " + " + boundCost + " / " + cap + "."; return false; }
        int dogs, total, globalDogCap; if (!RebirthDogLifecycleService.TryGetOwnershipCounts(player, out dogs, out total, out globalDogCap)) { reason = "Global companion capacity unavailable."; return false; }
        if (total + 1 > RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit) { reason = Localization.Get("xuiRebirthCompanionSafetyLimitReached"); return false; }
        reason = "Ready for Binding Ritual."; return true;
    }

    public static bool TryInteract(World world, EntityPlayer player, int targetEntityId, RebirthBoundUndeadInteractionKind kind, out string reason)
    {
        reason = string.Empty; if (world == null || world.IsRemote() || player == null || !RebirthWorldCharacterRepository.IsServerAuthority) { reason = "server authority required"; return false; }
        EntityAlive target = world.GetEntity(targetEntityId) as EntityAlive; if (target == null) { reason = "target unavailable"; return false; }
        return kind == RebirthBoundUndeadInteractionKind.Condition ? TryArmConditioning(target, player, out reason) : TryBind(world, target, player, out reason);
    }

    private static bool TryArmConditioning(EntityAlive target, EntityPlayer player, out string reason)
    {
        if (!CanPresentConditioning(target, player, out reason)) return false;
        int owner; string tier; int cost; float started; if (!RebirthBlackMagicService.TryGetControlInfo(target.entityId, out owner, out tier, out cost, out started) || owner != player.entityId) { reason = "control state changed; retry"; return false; }
        RebirthNpcStableId stable; RebirthNpcPersistentRecordView record; if (!EnsureConditioningRecord(target, player, tier, out stable, out record, out reason)) return false;
        lock (Gate) Exercises[target.entityId] = new ConditioningExercise { StableId = stable, OwnerEntityId = player.entityId, DominationStartedRealtime = started, Completed = false };
        exercisesArmed++;
        float progress = record != null && record.BoundUndead != null ? record.BoundUndead.ConditioningProgress : 0f;
        reason = "Conditioning exercise armed. Have this dominated undead damage a valid hostile target. Current Conditioning " + progress.ToString("0", CultureInfo.InvariantCulture) + "/" + conditioningRequired.ToString("0", CultureInfo.InvariantCulture) + ".";
        return true;
    }

    public static void OnDominatedCombatDamage(EntityAlive attacker, EntityAlive victim, DamageResponse response)
    {
        if (attacker == null || victim == null || attacker.world == null || attacker.world.IsRemote() || response.Strength <= 0) return;
        ConditioningExercise exercise; lock (Gate) { if (!Exercises.TryGetValue(attacker.entityId, out exercise) || exercise == null || exercise.Completed) return; }
        int owner; string tier; int cost; float started; if (!RebirthBlackMagicService.TryGetControlInfo(attacker.entityId, out owner, out tier, out cost, out started) || owner != exercise.OwnerEntityId || Math.Abs(started - exercise.DominationStartedRealtime) > .01f) return;
        if (victim.entityId == owner || victim.IsDead() || RebirthBlackMagicService.ShouldBlockControlledAttack(attacker, victim) || ShouldBlockAttack(attacker, victim)) return;
        RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(exercise.StableId, out record) || record == null || record.BoundUndead == null || record.BoundUndead.IsBound) return;
        float newProgress = Math.Min(conditioningRequired, record.BoundUndead.ConditioningProgress + conditioningPerExercise);
        bool changed = RebirthNpcAggregatePersistenceStore.Mutate(exercise.StableId, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.BoundUndead == null) return; r.BoundUndead.ConditioningProgress = newProgress; r.BoundUndead.ConditioningStage = RebirthBoundUndeadPersistentRecord.StageFor(newProgress); r.BoundUndead.LastConditioningUtcTicks = DateTime.UtcNow.Ticks; r.BoundUndead.Training = Math.Min(100f, r.BoundUndead.Training + 5f); r.BoundUndead.Revision++; if (r.Audit != null) { r.Audit.LastMutationKind = "black-magic-conditioning-success"; r.Audit.LastMutationWorldTime = DateTime.UtcNow.Ticks; }
        });
        if (changed) { lock (Gate) { exercise.Completed = true; Exercises[attacker.entityId] = exercise; } exercisesCompleted++; }
    }

    private static bool TryBind(World world, EntityAlive target, EntityPlayer player, out string reason)
    {
        bindingsAttempted++; if (!CanPresentBinding(target, player, out reason)) return false;
        int owner; string tier; int dominationCost; float started; if (!RebirthBlackMagicService.TryGetControlInfo(target.entityId, out owner, out tier, out dominationCost, out started) || owner != player.entityId) { reason = "control state changed; retry"; return false; }
        RebirthNpcStableId stable; RebirthNpcPersistentRecordView existing; if (!TryResolveRecord(target, out stable, out existing) || existing == null || existing.BoundUndead == null) { reason = "conditioning identity unavailable"; return false; }
        RebirthBoundUndeadTierRule rule; if (!TierRules.TryGetValue(tier, out rule)) { reason = "binding rule unavailable"; return false; }
        if (!RebirthSpecialPantherService.ValidateInfectedAnimalBindingHandling(target, player, out reason)) return false;
        int boundCost; if (!TryResolveBoundCost(target, tier, rule.CapacityCost, out boundCost, out reason)) return false;
        // Final atomic-ish server commit rechecks both capacity domains immediately before transfer.
        int used, cap, count; TryGetBoundCapacityUsage(player, out used, out cap, out count); if (used + boundCost > cap) { bindingDeniedCapacity++; reason = "Bound Undead Capacity changed before commit: " + used + " + " + boundCost + " / " + cap; return false; }
        int dogs, total, dogCap; if (!RebirthDogLifecycleService.TryGetOwnershipCounts(player, out dogs, out total, out dogCap) || total + 1 > RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit) { bindingDeniedGlobal++; reason = Localization.Get("xuiRebirthCompanionSafetyLimitReached"); return false; }
        string ownerId; if (!TryOwnerId(player, out ownerId)) { reason = "persistent owner identity unavailable"; return false; }
        string transferredTier; if (!RebirthBlackMagicService.TryTransferToPermanentBinding(world, target.entityId, player.entityId, out transferredTier, out reason)) return false;
        if (!string.Equals(transferredTier, tier, StringComparison.OrdinalIgnoreCase)) { reason = "tier changed during binding commit"; return false; }
        long now = DateTime.UtcNow.Ticks; string classId = target.EntityClass != null ? (target.EntityClass.entityClassName ?? string.Empty) : string.Empty; int maxHealth = target.GetMaxHealth(); int health = Math.Max(1, target.Health);
        RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Identity == null) return;
            r.Identity.Category = "BoundUndead"; r.Identity.Species = "Undead"; if (string.IsNullOrEmpty(r.Identity.GeneratedOrAssignedDisplayName)) r.Identity.GeneratedOrAssignedDisplayName = "Bound " + classId;
            if (r.Profile != null) { r.Profile.ProfileId = "companion.bound_undead"; r.Profile.ProfileSchemaVersionAtBind = 1; }
            r.Ownership = new RebirthNpcOwnershipRecord { OwnershipState = "Bound", OwnerPlatformIdOrPersistentPlayerId = ownerId, OwnerRevision = r.Ownership != null ? r.Ownership.OwnerRevision + 1U : 1U, PermissionPolicyId = "rebirth.bound-undead.owner", PartyAccessMode = "owner" };
            r.Order = new RebirthNpcOrderRecord { OrderState = RebirthNpcOrderState.Follow.ToString(), PreviousOrderState = string.Empty, OrderRevision = r.Order != null ? r.Order.OrderRevision + 1U : 1U };
            if (r.Lifecycle != null) { r.Lifecycle.LifecycleDomain = "bound-undead"; r.Lifecycle.LifecycleRevision++; r.Lifecycle.TombstoneState = false; }
            if (r.Presence != null) { r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString(); r.Presence.PresenceRevision++; r.Presence.SuspendedRuntimeEntityId = target.entityId; }
            if (r.Transform != null) { r.Transform.WorldPosition = target.position; r.Transform.RotationYaw = target.rotation.y; r.Transform.LastSafePosition = target.position; r.Transform.LastSafePositionWorldTime = now; r.Transform.TransformRevision++; }
            if (r.Vitals != null) { r.Vitals.CurrentHealth = health; r.Vitals.MaximumHealthAtSave = maxHealth; r.Vitals.DeathOrIncapacitationState = "Alive"; r.Vitals.VitalsRevision++; }
            r.Inventory = null; r.Equipment = null;
            if (r.BoundUndead == null) r.BoundUndead = new RebirthBoundUndeadPersistentRecord();
            r.BoundUndead.SourceEntityClass = classId; r.BoundUndead.TierId = tier; r.BoundUndead.CapacityCost = boundCost; r.BoundUndead.ConditioningProgress = Math.Max(conditioningRequired, r.BoundUndead.ConditioningProgress); r.BoundUndead.ConditioningStage = "Ready for Binding"; r.BoundUndead.IsBound = true; r.BoundUndead.IsSummoned = false; r.BoundUndead.Lifecycle = "Active"; r.BoundUndead.LastEntityId = target.entityId; r.BoundUndead.BoundUtcTicks = now; r.BoundUndead.BoundFactionId = player.factionId; r.BoundUndead.BoundFactionRank = player.factionRank; r.BoundUndead.BindingStability = 100f; r.BoundUndead.Revision++;
            if (r.Audit != null) { r.Audit.LastMutationKind = "black-magic-binding-commit"; r.Audit.LastMutationWorldTime = now; }
        });
        target.SetSpawnerSource(EnumSpawnerSource.StaticSpawner); target.factionId = player.factionId; target.factionRank = player.factionRank; target.SetAttackTarget(null, 0); target.SetRevengeTarget(null); WriteStableId(target, stable); target.Buffs.SetCustomVar("$RB_BoundUndead", 1f); target.Buffs.SetCustomVar("$RB_UndeadConditioning", 1f); target.Buffs.SetCustomVar("$RB_BoundUndeadOwner", player.entityId);
        RegisterLive(target, stable); bindingsCompleted++;
        RebirthBlackMagicService.AwardBindingCompletion(player,stable.ToString()); RebirthBlackMagicService.RefreshKnowledgeMilestones(player);
        reason = "Binding Ritual complete. " + tier + " undead permanently bound. Bound Undead Capacity " + (used + boundCost) + "/" + cap + "."; return true;
    }

    private static bool EnsureConditioningRecord(EntityAlive target, EntityPlayer player, string tier, out RebirthNpcStableId stable, out RebirthNpcPersistentRecordView record, out string reason)
    {
        reason = string.Empty; record = null; if (!TryReadStableId(target, out stable)) { stable = RebirthNpcStableId.NewId(); WriteStableId(target, stable); }
        string ownerId; if (!TryOwnerId(player, out ownerId)) { reason = "persistent owner identity unavailable"; return false; }
        RebirthNpcPersistentRecordView existing; if (RebirthNpcAggregatePersistenceStore.TryGetView(stable, out existing) && existing != null && existing.Ownership != null && !string.IsNullOrEmpty(existing.Ownership.OwnerPlatformIdOrPersistentPlayerId) && !string.Equals(existing.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase)) { reason = "This individual is being conditioned by another survivor."; return false; }
        long now = DateTime.UtcNow.Ticks; string cls = target.EntityClass != null ? (target.EntityClass.entityClassName ?? string.Empty) : string.Empty;
        RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r)
        {
            if (r.Identity != null) { r.Identity.Category = "UndeadConditioning"; r.Identity.Species = "Undead"; if (string.IsNullOrEmpty(r.Identity.GeneratedOrAssignedDisplayName)) r.Identity.GeneratedOrAssignedDisplayName = "Conditioning " + cls; }
            if (r.Profile != null) { r.Profile.ProfileId = "blackmagic.conditioning"; r.Profile.ProfileSchemaVersionAtBind = 1; }
            r.Ownership = new RebirthNpcOwnershipRecord { OwnershipState = "Conditioning", OwnerPlatformIdOrPersistentPlayerId = ownerId, OwnerRevision = r.Ownership != null ? r.Ownership.OwnerRevision + 1U : 1U, PermissionPolicyId = "rebirth.blackmagic.conditioning", PartyAccessMode = "owner" };
            if (r.Lifecycle != null) { r.Lifecycle.LifecycleDomain = "undead-conditioning"; r.Lifecycle.LifecycleRevision++; }
            if (r.Presence != null) { r.Presence.PresenceState = "Conditioning"; r.Presence.PresenceRevision++; }
            if (r.Transform != null) { r.Transform.WorldPosition = target.position; r.Transform.RotationYaw = target.rotation.y; r.Transform.TransformRevision++; }
            if (r.Vitals != null) { r.Vitals.CurrentHealth = Math.Max(0, target.Health); r.Vitals.MaximumHealthAtSave = target.GetMaxHealth(); r.Vitals.VitalsRevision++; }
            if (r.BoundUndead == null) r.BoundUndead = new RebirthBoundUndeadPersistentRecord();
            r.BoundUndead.SourceEntityClass = cls; r.BoundUndead.TierId = tier; r.BoundUndead.IsBound = false; r.BoundUndead.IsSummoned = false; r.BoundUndead.Lifecycle = "Conditioning"; r.BoundUndead.LastEntityId = target.entityId; r.BoundUndead.LastConditioningUtcTicks = now; r.BoundUndead.ConditioningStage = RebirthBoundUndeadPersistentRecord.StageFor(r.BoundUndead.ConditioningProgress); r.BoundUndead.Revision++;
            if (r.Audit != null) { r.Audit.LastMutationKind = "black-magic-conditioning-arm"; r.Audit.LastMutationWorldTime = now; }
        });
        // Once an individual enters Conditioning it must remain the same individual across
        // ordinary chunk unload/reload. Dynamic hostile spawns are not saved alive by the native
        // EntityEnemy persistence rule, so promote only the spawner source; faction/hostility remain unchanged.
        target.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
        target.Buffs.SetCustomVar("$RB_UndeadConditioning", 1f); RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record); exercisesArmed += 0; return true;
    }

    public static void OnTemporaryControlEnded(int targetEntityId, int ownerEntityId)
    {
        lock (Gate) Exercises.Remove(targetEntityId);
    }

    public static void OnEntityAdded(EntityAlive entity)
    {
        if (entity == null || entity.world == null || entity.world.IsRemote()) return; RebirthNpcStableId stable; if (!TryReadStableId(entity, out stable)) return;
        RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record) || record == null || record.BoundUndead == null) return;
        if (!record.BoundUndead.IsBound)
        {
            // Conditioning is not ownership: preserve the hostile faction and normal AI, but keep
            // the exact individual saveable and refresh its runtime identity after chunk reload.
            entity.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            entity.Buffs.SetCustomVar("$RB_UndeadConditioning", 1f);
            RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r)
            {
                if (r.BoundUndead != null) { r.BoundUndead.LastEntityId = entity.entityId; r.BoundUndead.Lifecycle = "Conditioning"; r.BoundUndead.Revision++; }
                if (r.Presence != null) { r.Presence.PresenceState = "Conditioning"; r.Presence.SuspendedRuntimeEntityId = entity.entityId; r.Presence.PresenceRevision++; }
            });
            return;
        }
        if (!string.Equals(record.BoundUndead.SourceEntityClass ?? string.Empty, entity.EntityClass != null ? entity.EntityClass.entityClassName : string.Empty, StringComparison.OrdinalIgnoreCase)) return;
        entity.SetSpawnerSource(EnumSpawnerSource.StaticSpawner); entity.factionId = record.BoundUndead.BoundFactionId; entity.factionRank = record.BoundUndead.BoundFactionRank; entity.Buffs.SetCustomVar("$RB_BoundUndead", 1f); entity.Buffs.SetCustomVar("$RB_UndeadConditioning", 1f);
        EntityPlayer owner = FindOwnerPlayer(entity.world, record.Ownership != null ? record.Ownership.OwnerPlatformIdOrPersistentPlayerId : string.Empty); entity.Buffs.SetCustomVar("$RB_BoundUndeadOwner", owner != null ? owner.entityId : 0f); RegisterLive(entity, stable);
        RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r) { if (r.BoundUndead != null) { r.BoundUndead.LastEntityId = entity.entityId; r.BoundUndead.Lifecycle = "Active"; r.BoundUndead.Revision++; } if (r.Presence != null) { r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString(); r.Presence.SuspendedRuntimeEntityId = entity.entityId; r.Presence.PresenceRevision++; } });
    }

    public static void OnEntityUnloading(EntityAlive entity)
    {
        if (entity == null || entity.world == null || entity.world.IsRemote()) return; RebirthNpcStableId stable; if (!TryReadStableId(entity, out stable)) return; RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record) || record == null || record.BoundUndead == null) return;
        if (!record.BoundUndead.IsBound)
        {
            if (entity.IsDead()) { RebirthNpcAggregatePersistenceStore.Remove(stable); ClearStableId(entity); }
            else
            {
                RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r)
                {
                    if (r.BoundUndead != null) { r.BoundUndead.LastEntityId = entity.entityId; r.BoundUndead.Lifecycle = "Conditioning"; r.BoundUndead.Revision++; }
                    if (r.Transform != null) { r.Transform.WorldPosition = entity.position; r.Transform.RotationYaw = entity.rotation.y; r.Transform.LastSafePosition = entity.position; r.Transform.LastSafePositionWorldTime = DateTime.UtcNow.Ticks; r.Transform.TransformRevision++; }
                    if (r.Vitals != null) { r.Vitals.CurrentHealth = Math.Max(0, entity.Health); r.Vitals.MaximumHealthAtSave = entity.GetMaxHealth(); r.Vitals.VitalsRevision++; }
                    if (r.Presence != null) { r.Presence.PresenceState = "Conditioning"; r.Presence.SuspendedRuntimeEntityId = entity.entityId; r.Presence.PresenceRevision++; }
                });
            }
            lock (Gate) Exercises.Remove(entity.entityId); return;
        }
        bool dead = entity.IsDead(); PersistRuntime(stable, entity, dead ? "AwaitingReturn" : "Active"); UnregisterLive(entity.entityId, stable); if (dead) lifecycleDeaths++;
    }

    public static bool ShouldBlockAttack(EntityAlive controller, EntityAlive proposedTarget)
    {
        if (controller == null || proposedTarget == null) return false; RebirthNpcStableId stable; lock (Gate) { if (!LiveByEntity.TryGetValue(controller.entityId, out stable)) return false; }
        RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record) || record == null || record.BoundUndead == null || !record.BoundUndead.IsBound) return false;
        if(record.BoundUndead.AttackStopped)return true;
        EntityPlayer owner = FindOwnerPlayer(controller.world, record.Ownership != null ? record.Ownership.OwnerPlatformIdOrPersistentPlayerId : string.Empty); if (owner != null && proposedTarget.entityId == owner.entityId) return true;
        RebirthNpcStableId otherStable; lock (Gate) if (LiveByEntity.TryGetValue(proposedTarget.entityId, out otherStable)) { RebirthNpcPersistentRecordView other; if (RebirthNpcAggregatePersistenceStore.TryGetView(otherStable, out other) && SameOwner(record, other)) return true; }
        if (owner != null && proposedTarget is EntityPlayer && proposedTarget.factionId == owner.factionId) return true; return false;
    }

    public static bool IsBoundEntity(int entityId, out RebirthNpcStableId stable)
    { lock (Gate) return LiveByEntity.TryGetValue(entityId, out stable); }

    public static bool TryGetLiveEntity(World world, RebirthNpcStableId stable, out EntityAlive entity)
    {
        entity = null; int id; lock (Gate) if (!EntityByStable.TryGetValue(stable, out id)) return false; entity = world != null ? world.GetEntity(id) as EntityAlive : null; return entity != null && !entity.IsDead();
    }

    public static bool TryProcessCompanionCommand(World world, EntityPlayer player, RebirthNpcStableId stable, RebirthCompanionCommand command, out string reason)
    {
        reason = string.Empty; if (world == null || world.IsRemote() || player == null) { reason = "server authority required"; return false; }
        RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record) || !IsOwnedBound(record, player)) { reason = "bound undead ownership unavailable"; return false; }
        if (string.Equals(record.BoundUndead.Lifecycle, "AwaitingReturn", StringComparison.OrdinalIgnoreCase))
        {
            if (command != RebirthCompanionCommand.ReportForDuty) { reason = "Bound undead is awaiting Report for Duty."; return false; }
            EntityAlive restored; return TryReportForDuty(world, player, stable, out restored, out reason);
        }
        EntityAlive live; TryGetLiveEntity(world, stable, out live);
        switch (command)
        {
            case RebirthCompanionCommand.Follow: return SetOrder(stable, live, RebirthNpcOrderState.Follow, false, out reason);
            case RebirthCompanionCommand.Stay: return SetOrder(stable, live, RebirthNpcOrderState.Stay, false, out reason);
            case RebirthCompanionCommand.Stop: return SetOrder(stable, live, null, true, out reason);
            case RebirthCompanionCommand.Resume: return SetOrder(stable, live, null, false, out reason);
            case RebirthCompanionCommand.Recall:
                if (live == null) { reason = "bound undead is not currently loaded"; return false; }
                RebirthNpcOrderState order; if (!TryOrder(record, out order) || order != RebirthNpcOrderState.Follow) { reason = "Recall requires Follow order."; return false; }
                Vector3 spawn; if (!TrySafePosition(world, player.position, out spawn)) { reason = "no safe recall position"; return false; } live.SetPosition(spawn, true); live.SetAttackTarget(null, 0); reason = "Bound undead recalled."; return true;
            default: reason = "Command is not authored for bound undead."; return false;
        }
    }

    public static bool TryReportForDuty(World world, EntityPlayer player, RebirthNpcStableId stable, out EntityAlive restored, out string reason)
    {
        restored = null; reason = string.Empty; RebirthNpcPersistentRecordView record; if (!RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record) || !IsOwnedBound(record, player)) { reason = "bound undead ownership unavailable"; return false; }
        if (!string.Equals(record.BoundUndead.Lifecycle, "AwaitingReturn", StringComparison.OrdinalIgnoreCase)) { reason = "bound undead is not awaiting return"; return false; }
        int classId = EntityClass.FromString(record.BoundUndead.SourceEntityClass); EntityClass definition; if (classId <= 0 || EntityClass.list == null || !EntityClass.list.TryGetValue(classId, out definition) || definition == null) { reason = "source entity class unavailable: " + record.BoundUndead.SourceEntityClass; return false; }
        Vector3 spawn; if (!TrySafePosition(world, player.position, out spawn)) { reason = "no safe Report for Duty position"; return false; }
        EntityAlive created = EntityFactory.CreateEntity(classId, spawn, new Vector3(0f, player.rotation.y, 0f)) as EntityAlive; if (created == null) { reason = "failed to create bound undead body"; return false; }
        created.SetSpawnerSource(EnumSpawnerSource.StaticSpawner); created.factionId = player.factionId; created.factionRank = player.factionRank; WriteStableId(created, stable); created.Buffs.SetCustomVar("$RB_BoundUndead", 1f); created.Buffs.SetCustomVar("$RB_UndeadConditioning", 1f); created.Buffs.SetCustomVar("$RB_BoundUndeadOwner", player.entityId); world.SpawnEntityInWorld(created); created.Health = Math.Max(1, Math.Min(created.GetMaxHealth(), Mathf.RoundToInt(created.GetMaxHealth() * reportHealthFraction)));
        RegisterLive(created, stable); RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r) { if (r.BoundUndead != null) { r.BoundUndead.Lifecycle = "Active"; r.BoundUndead.LastEntityId = created.entityId; r.BoundUndead.BoundFactionId = player.factionId; r.BoundUndead.BoundFactionRank = player.factionRank; r.BoundUndead.Revision++; } if (r.Presence != null) { r.Presence.PresenceState = RebirthNpcPresenceState.Active.ToString(); r.Presence.SuspendedRuntimeEntityId = created.entityId; r.Presence.PresenceRevision++; } if (r.Vitals != null) { r.Vitals.CurrentHealth = created.Health; r.Vitals.MaximumHealthAtSave = created.GetMaxHealth(); r.Vitals.DeathOrIncapacitationState = "Alive"; r.Vitals.VitalsRevision++; } });
        restored = created; reportsForDuty++; reason = "Bound undead reported for duty."; return true;
    }

    private static bool SetOrder(RebirthNpcStableId stable, EntityAlive live, RebirthNpcOrderState? order, bool attackStopped, out string reason)
    {
        bool ok = RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r) { if (order.HasValue) { if (r.Order == null) r.Order = new RebirthNpcOrderRecord(); r.Order.PreviousOrderState = r.Order.OrderState ?? string.Empty; r.Order.OrderState = order.Value.ToString(); r.Order.OrderRevision++; } if (r.BoundUndead != null) { r.BoundUndead.AttackStopped = attackStopped; r.BoundUndead.Revision++; } });
        if (!ok) { reason = "bound undead record unavailable"; return false; } if (live != null && attackStopped) { live.SetAttackTarget(null, 0); live.SetRevengeTarget(null); }
        reason = order.HasValue ? "Bound undead order: " + order.Value + "." : (attackStopped ? "Bound undead attacks stopped." : "Bound undead attacks resumed."); return true;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ClearRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { CaptureAll(); ClearRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ClearRuntime(); }
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance; if (c == null || !c.IsServer || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return; float now = Time.realtimeSinceStartup; if (now < nextTick) return; nextTick = now + .35f; World world = GameManager.Instance != null ? GameManager.Instance.World : null; if (world == null) return;
        KeyValuePair<int, RebirthNpcStableId>[] live; lock (Gate) { live = new KeyValuePair<int, RebirthNpcStableId>[LiveByEntity.Count]; int n = 0; foreach (KeyValuePair<int, RebirthNpcStableId> p in LiveByEntity) live[n++] = p; }
        for (int i = 0; i < live.Length; i++)
        {
            EntityAlive entity = world.GetEntity(live[i].Key) as EntityAlive; RebirthNpcPersistentRecordView record;
            if (entity == null || !RebirthNpcAggregatePersistenceStore.TryGetView(live[i].Value, out record) || record == null || record.BoundUndead == null || !record.BoundUndead.IsBound) { UnregisterLive(live[i].Key, live[i].Value); continue; }
            if (entity.IsDead()) { PersistRuntime(live[i].Value, entity, "AwaitingReturn"); UnregisterLive(entity.entityId, live[i].Value); lifecycleDeaths++; continue; }
            EntityPlayer owner = FindOwnerPlayer(world, record.Ownership != null ? record.Ownership.OwnerPlatformIdOrPersistentPlayerId : string.Empty); if (owner == null || owner.IsDead()) continue;
            entity.factionId = record.BoundUndead.BoundFactionId != 0 ? record.BoundUndead.BoundFactionId : owner.factionId; entity.factionRank = record.BoundUndead.BoundFactionRank; entity.Buffs.SetCustomVar("$RB_BoundUndeadOwner", owner.entityId);
            if (record.BoundUndead.AttackStopped) { entity.SetAttackTarget(null, 0); entity.SetRevengeTarget(null); }
            RebirthNpcOrderState order; if (!TryOrder(record, out order)) order = RebirthNpcOrderState.Follow;
            if (order == RebirthNpcOrderState.Follow)
            {
                float dist = Vector3.Distance(entity.position, owner.position); if (dist > recallDistance) { Vector3 safe; if (TrySafePosition(world, owner.position, out safe)) entity.SetPosition(safe, true); }
                else if (dist > followDistance)
                {
                    bool submit=false; lock(Gate){FollowPathStamp stamp;if(!FollowPathByStable.TryGetValue(live[i].Value,out stamp)||stamp==null||(stamp.OwnerPosition-owner.position).sqrMagnitude>1f||now-stamp.SubmittedRealtime>=1.5f){FollowPathByStable[live[i].Value]=new FollowPathStamp{OwnerPosition=owner.position,SubmittedRealtime=now};submit=true;}}
                    if(submit){try { entity.FindPath(owner.position, Math.Max(.05f, entity.moveSpeed), true, null); } catch { }}
                }
            }
        }
    }

    private static void CaptureAll()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null; if (world == null) return; KeyValuePair<int, RebirthNpcStableId>[] live; lock (Gate) { live = new KeyValuePair<int, RebirthNpcStableId>[LiveByEntity.Count]; int n = 0; foreach (KeyValuePair<int, RebirthNpcStableId> p in LiveByEntity) live[n++] = p; }
        for (int i = 0; i < live.Length; i++) { EntityAlive entity = world.GetEntity(live[i].Key) as EntityAlive; if (entity != null) PersistRuntime(live[i].Value, entity, entity.IsDead() ? "AwaitingReturn" : "Active"); }
        RebirthNpcAggregatePersistenceStore.Save();
    }

    private static void PersistRuntime(RebirthNpcStableId stable, EntityAlive entity, string lifecycle)
    {
        if (entity == null) return; long now = DateTime.UtcNow.Ticks; RebirthNpcAggregatePersistenceStore.Mutate(stable, delegate(RebirthNpcPersistentRecord r) { if (r.BoundUndead != null) { r.BoundUndead.Lifecycle = lifecycle; r.BoundUndead.LastEntityId = string.Equals(lifecycle, "Active", StringComparison.OrdinalIgnoreCase) ? entity.entityId : -1; r.BoundUndead.Revision++; } if (r.Transform != null) { r.Transform.WorldPosition = entity.position; r.Transform.RotationYaw = entity.rotation.y; r.Transform.LastSafePosition = entity.position; r.Transform.LastSafePositionWorldTime = now; r.Transform.TransformRevision++; } if (r.Vitals != null) { r.Vitals.CurrentHealth = Math.Max(0, entity.Health); r.Vitals.MaximumHealthAtSave = entity.GetMaxHealth(); r.Vitals.DeathOrIncapacitationState = string.Equals(lifecycle, "AwaitingReturn", StringComparison.OrdinalIgnoreCase) ? "Dead" : "Alive"; r.Vitals.VitalsRevision++; } if (r.Presence != null) { r.Presence.PresenceState = string.Equals(lifecycle, "AwaitingReturn", StringComparison.OrdinalIgnoreCase) ? "AwaitingReturn" : RebirthNpcPresenceState.Active.ToString(); r.Presence.SuspendedRuntimeEntityId = string.Equals(lifecycle, "Active", StringComparison.OrdinalIgnoreCase) ? (int?)entity.entityId : null; r.Presence.PresenceRevision++; } });
    }

    public static string BuildStatus(EntityPlayer player)
    {
        Ensure(); int used, cap, count; TryGetBoundCapacityUsage(player, out used, out cap, out count); StringBuilder b = new StringBuilder(); b.Append("[REBIRTH BlackMagic Binding] boundCapacity=").Append(used).Append('/').Append(cap).Append(" boundCount=").Append(count).Append(" exercises=").Append(exercisesCompleted).Append('/').Append(exercisesArmed).Append(" bindings=").Append(bindingsCompleted).Append('/').Append(bindingsAttempted).Append(" deaths=").Append(lifecycleDeaths).Append(" reports=").Append(reportsForDuty); b.Append("\n  summoning available=").Append(CountAvailableSummons()).Append('/').Append(Summons.Count); for (int i = 0; i < Summons.Count; i++) b.Append("\n    ").Append(Summons[i].EntityClassId).Append(" role=").Append(Summons[i].Role).Append(" authored=").Append(Summons[i].AuthoredAvailable).Append(" runtime=").Append(Summons[i].RuntimeAvailable).Append(" audit=").Append(Summons[i].Audit); return b.ToString();
    }

    public static string BuildEntityStatus(EntityAlive target, EntityPlayer player)
    {
        if (target == null) return "target unavailable"; RebirthNpcStableId stable; RebirthNpcPersistentRecordView record; bool has = TryResolveRecord(target, out stable, out record); string c, b; bool canC = CanPresentConditioning(target, player, out c); bool canB = CanPresentBinding(target, player, out b); return "entity=" + target.entityId + " stable=" + (has ? stable.ToString() : "<none>") + " conditioning=" + (record != null && record.BoundUndead != null ? record.BoundUndead.ConditioningProgress.ToString("0", CultureInfo.InvariantCulture) + "/" + record.BoundUndead.ConditioningStage : "<none>") + " bound=" + (record != null && record.BoundUndead != null && record.BoundUndead.IsBound) + " conditionAction=" + canC + ":" + c + " bindAction=" + canB + ":" + b;
    }

    public static string RunVectors()
    {
        Ensure(); List<string> e = ValidateAuthoring(); string[] order = { "normal", "feral", "radiated", "charged", "infernal" }; float[] minimum = { 20, 40, 60, 80, 100 }; for (int i = 0; i < order.Length; i++) { RebirthBoundUndeadTierRule r; if (!TierRules.TryGetValue(order[i], out r)) e.Add("missing binding tier " + order[i]); else if (r.SkillRequired + .001f < minimum[i]) e.Add("binding tier unlocks too early: " + order[i]); }
        if (CountAvailableSummons() != 0) e.Add("PC009 current asset audit expected zero available legacy summons"); if (conditioningPerExercise <= 0f || conditioningRequired < 100f) e.Add("conditioning progression invalid"); return "[REBIRTH BlackMagic] chunkGVectors=" + (e.Count == 0 ? "PASS" : "FAIL") + " failures=" + e.Count + (e.Count == 0 ? string.Empty : " " + string.Join(" | ", e.ToArray()));
    }

    private static List<string> ValidateAuthoring()
    {
        List<string> e = new List<string>(); string[] required = { "normal", "feral", "radiated", "charged", "infernal" }; float last = -1f; for (int i = 0; i < required.Length; i++) { RebirthBoundUndeadTierRule r; if (!TierRules.TryGetValue(required[i], out r)) { e.Add("missing tier " + required[i]); continue; } if (r.SkillRequired < last) e.Add("non-monotonic tier " + required[i]); last = r.SkillRequired; if (string.IsNullOrEmpty(r.KnowledgeId)) e.Add("missing Knowledge " + required[i]); if (r.CapacityCost != i + 1) e.Add("unexpected bound capacity weight " + required[i]); }
        if (maxBoundCapacity >= RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit + 1) e.Add("bound capacity must remain below global safety limit"); return e;
    }

    private static bool BasicInteraction(EntityAlive target, EntityPlayer player, out string reason)
    { reason = string.Empty; if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) { reason = "Rebirth progression required"; return false; } if (target == null || player == null || target.IsDead()) { reason = "target unavailable"; return false; } if (!RebirthBlackMagicService.HasWitchDoctor(player)) { reason = "Witch Doctor discipline required"; return false; } if ((target.position - player.position).sqrMagnitude > maxDistance * maxDistance) { reason = "move closer"; return false; } return true; }

    private static bool TryResolveBoundCost(EntityAlive target, string tier, int defaultCost, out int cost, out string reason)
    { cost = Math.Max(1, defaultCost); reason = string.Empty; RebirthBlackMagicZombieAnimalDefinition animal; string cls = target != null && target.EntityClass != null ? target.EntityClass.entityClassName : string.Empty; if (RebirthBlackMagicTargetClassifier.TryGetZombieAnimalDefinition(cls, out animal)) { if (!animal.BlackMagicEligible || !animal.BindingCandidate || animal.Protected) { reason = "This zombie animal is not an authored Black Magic binding candidate."; return false; } cost = Math.Max(1, animal.BoundCapacityCost); } return true; }

    private static bool TryResolveRecord(EntityAlive target, out RebirthNpcStableId stable, out RebirthNpcPersistentRecordView record)
    { record = null; if (!TryReadStableId(target, out stable)) return false; return RebirthNpcAggregatePersistenceStore.TryGetView(stable, out record); }

    private static bool IsOwnedBound(RebirthNpcPersistentRecordView record, EntityPlayer player)
    { if (record == null || record.BoundUndead == null || !record.BoundUndead.IsBound || record.Ownership == null || player == null) return false; string owner; return TryOwnerId(player, out owner) && string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase); }
    private static bool SameOwner(RebirthNpcPersistentRecordView a, RebirthNpcPersistentRecordView b) { return a != null && b != null && a.Ownership != null && b.Ownership != null && string.Equals(a.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, b.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, StringComparison.OrdinalIgnoreCase); }
    private static bool TryOwnerId(EntityPlayer player, out string owner) { owner = string.Empty; if (player == null || GameManager.Instance == null) return false; PersistentPlayerData pp = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId); if (pp?.PrimaryId == null) return false; owner = pp.PrimaryId.ToString(); return !string.IsNullOrEmpty(owner); }
    private static EntityPlayer FindOwnerPlayer(World world, string ownerId) { if (world == null || world.Players == null || world.Players.list == null || string.IsNullOrEmpty(ownerId)) return null; for (int i = 0; i < world.Players.list.Count; i++) { EntityPlayer p = world.Players.list[i]; if (p == null) continue; PersistentPlayerData pp = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(p.entityId); if (pp?.PrimaryId != null && string.Equals(pp.PrimaryId.ToString(), ownerId, StringComparison.OrdinalIgnoreCase)) return p; } return null; }
    private static bool TryOrder(RebirthNpcPersistentRecordView record, out RebirthNpcOrderState order) { order = RebirthNpcOrderState.Follow; return record != null && record.Order != null && Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order); }

    private static bool TrySafePosition(World world, Vector3 anchor, out Vector3 position)
    { if (RebirthDogRuntimeService.TryFindSafeAtExactXZ(world, anchor, out position)) return true; Vector3[] offsets = { new Vector3(2,0,0), new Vector3(-2,0,0), new Vector3(0,0,2), new Vector3(0,0,-2), new Vector3(2,0,2), new Vector3(-2,0,-2) }; for (int i = 0; i < offsets.Length; i++) if (RebirthDogRuntimeService.TryFindSafeAtExactXZ(world, anchor + offsets[i], out position)) return true; position = anchor; return false; }

    private static void RegisterLive(EntityAlive entity, RebirthNpcStableId stable) { if (entity == null || stable.IsEmpty) return; lock (Gate) { RebirthNpcStableId old; if (LiveByEntity.TryGetValue(entity.entityId, out old) && old != stable) EntityByStable.Remove(old); int oldId; if (EntityByStable.TryGetValue(stable, out oldId) && oldId != entity.entityId) LiveByEntity.Remove(oldId); LiveByEntity[entity.entityId] = stable; EntityByStable[stable] = entity.entityId; } }
    private static void UnregisterLive(int entityId, RebirthNpcStableId stable) { lock (Gate) { LiveByEntity.Remove(entityId); int current; if (EntityByStable.TryGetValue(stable, out current) && current == entityId) EntityByStable.Remove(stable); Exercises.Remove(entityId); FollowPathByStable.Remove(stable); } }
    private static void ClearRuntime() { lock (Gate) { Exercises.Clear(); LiveByEntity.Clear(); EntityByStable.Clear(); FollowPathByStable.Clear(); } nextTick = 0f; }

    public static bool TryReadStableId(EntityAlive entity, out RebirthNpcStableId stable)
    {
        stable = default(RebirthNpcStableId); if (entity == null || entity.Buffs == null || entity.Buffs.GetCustomVar("$RB_UndeadIdentity") < .5f) return false; ulong high = 0, low = 0; for (int i = 0; i < 4; i++) { high |= ((ulong)(ushort)Mathf.RoundToInt(entity.Buffs.GetCustomVar("$RB_UndeadIdH" + i))) << (i * 16); low |= ((ulong)(ushort)Mathf.RoundToInt(entity.Buffs.GetCustomVar("$RB_UndeadIdL" + i))) << (i * 16); } stable = new RebirthNpcStableId(high, low); return !stable.IsEmpty;
    }
    public static void WriteStableId(EntityAlive entity, RebirthNpcStableId stable) { if (entity == null || entity.Buffs == null || stable.IsEmpty) return; entity.Buffs.SetCustomVar("$RB_UndeadIdentity", 1f); for (int i = 0; i < 4; i++) { entity.Buffs.SetCustomVar("$RB_UndeadIdH" + i, (float)((stable.High >> (i * 16)) & 0xffffUL)); entity.Buffs.SetCustomVar("$RB_UndeadIdL" + i, (float)((stable.Low >> (i * 16)) & 0xffffUL)); } }
    private static void ClearStableId(EntityAlive entity) { if (entity == null || entity.Buffs == null) return; entity.Buffs.SetCustomVar("$RB_UndeadIdentity", 0f); entity.Buffs.SetCustomVar("$RB_UndeadConditioning", 0f); entity.Buffs.SetCustomVar("$RB_BoundUndead", 0f); entity.Buffs.SetCustomVar("$RB_BoundUndeadOwner", 0f); for (int i = 0; i < 4; i++) { entity.Buffs.SetCustomVar("$RB_UndeadIdH" + i, 0f); entity.Buffs.SetCustomVar("$RB_UndeadIdL" + i, 0f); } }

    private static int CountAvailableSummons() { int n = 0; for (int i = 0; i < Summons.Count; i++) if (Summons[i].RuntimeAvailable) n++; return n; }
    private static void Ensure() { if (!ready) LoadDefinitions(); }
    private static string A(XElement e, string n) { return e == null ? string.Empty : ((string)e.Attribute(n) ?? string.Empty).Trim(); }
    private static float F(XElement e, string n, float d) { float v; return float.TryParse(A(e, n), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : d; }
    private static int I(XElement e, string n, int d) { int v; return int.TryParse(A(e, n), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : d; }
    private static bool B(XElement e, string n, bool d) { bool v; return bool.TryParse(A(e, n), out v) ? v : d; }
}

[Preserve]
public sealed class NetPackageRebirthBoundUndeadInteractionRequest : NetPackage
{
    private int playerEntityId, targetEntityId; private PlatformUserIdentifierAbs userId; private RebirthBoundUndeadInteractionKind kind;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthBoundUndeadInteractionRequest Setup(int playerId, PlatformUserIdentifierAbs user, int targetId, RebirthBoundUndeadInteractionKind interaction) { playerEntityId = playerId; userId = user; targetEntityId = targetId; kind = interaction; return this; }
    public override void read(PooledBinaryReader reader) { BinaryReader b = (BinaryReader)reader; playerEntityId = b.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(b); targetEntityId = b.ReadInt32(); kind = (RebirthBoundUndeadInteractionKind)b.ReadByte(); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(playerEntityId); userId.ToStream(b); b.Write(targetEntityId); b.Write((byte)kind); }
    public override void ProcessPackage(World world, GameManager callbacks) { if (world == null || world.IsRemote() || userId == null || !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId)) return; EntityPlayer p = world.GetEntity(playerEntityId) as EntityPlayer; PersistentPlayerData pp = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId); if (p == null || pp?.PrimaryId == null || !pp.PrimaryId.Equals(userId)) return; string reason; bool ok = RebirthBoundUndeadService.TryInteract(world, p, targetEntityId, kind, out reason); if (!string.IsNullOrEmpty(reason)) GameManager.ShowTooltipMP(p, reason, ok ? "ui_success" : "ui_denied"); }
    public int GetLength() => 0;
}
