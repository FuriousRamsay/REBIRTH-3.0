using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk-B server authority for the normal Animal Handling Skill and persistent dog training.
/// Player Skill, dog Training and dog Bond are deliberately separate progression surfaces.
/// </summary>
public static class RebirthAnimalHandlingService
{
    public const string SkillId = "skill.animal_handling";
    public const string KnowledgeBasicDogTraining = "knowledge.animal_handling.basic_dog_training";
    public const string KnowledgeWorkingDogTraining = "knowledge.animal_handling.working_dog_training";
    public const string KnowledgeAnimalBehavior = "knowledge.animal_handling.animal_behavior";

    public const float GuardSkillRequired = 10f;
    public const float GuardAreaSkillRequired = 15f;
    public const float HuntingSkillRequired = 25f;

    private const float StayExerciseSeconds = 8f;
    private const float StayExerciseMaximumSeconds = 45f;
    private const float OwnerMoveDistance = 8f;
    private const float DogHoldTolerance = 1.75f;
    private const float DogFailureDistance = 3.5f;
    private const float TrainingPerSuccess = 4f;
    private const float BondPerSuccess = 1f;
    private const float SkillProgressPerSuccess = .60f;
    public static float SuccessfulExerciseReferenceGain { get { return SkillProgressPerSuccess; } }

    private sealed class StayExercise
    {
        public string Key;
        public int OwnerEntityId;
        public Vector3 Anchor;
        public float Started;
        public bool OwnerMovedAway;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, StayExercise> StayExercises = new Dictionary<string, StayExercise>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> LastSuccessfulExercise = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public static bool CanUseDogCommand(EntityPlayer player, RebirthDogPersistentRecordView dog, RebirthCompanionCommand command, out string reason)
    {
        reason = string.Empty;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        if (player == null || dog == null) { reason = L("xuiRebirthDogCommandUnavailable", "Dog command unavailable."); return false; }

        // Safety/recovery/basic handling can never be progression-gated.
        if (command == RebirthCompanionCommand.Recall || command == RebirthCompanionCommand.FullControl ||
            command == RebirthCompanionCommand.Follow || command == RebirthCompanionCommand.Stay ||
            command == RebirthCompanionCommand.Stop || command == RebirthCompanionCommand.Resume)
            return true;

        if (dog.LegacyCommandGrandfathered && (command == RebirthCompanionCommand.Guard || command == RebirthCompanionCommand.GuardArea || command == RebirthCompanionCommand.Hunting)) return true;

        float skill = GetPlayerSkill(player);
        if (command == RebirthCompanionCommand.Guard)
            return CheckAdvancedCommand(player, dog, skill, GuardSkillRequired, KnowledgeBasicDogTraining, RebirthDogTrainingCommandIds.OwnerPositionStay, out reason);
        if (command == RebirthCompanionCommand.GuardArea)
            return CheckAdvancedCommand(player, dog, skill, GuardAreaSkillRequired, KnowledgeWorkingDogTraining, RebirthDogTrainingCommandIds.GuardArea, out reason);
        if (command == RebirthCompanionCommand.Hunting)
            return CheckAdvancedCommand(player, dog, skill, HuntingSkillRequired, KnowledgeAnimalBehavior, RebirthDogTrainingCommandIds.Hunting, out reason);
        return true;
    }

    public static bool CanUseDogCommandForUi(EntityPlayer player, RebirthCompanionListEntry selected, RebirthCompanionCommand command)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || selected == null || !selected.IsDog) return true;
        if (command == RebirthCompanionCommand.Recall || command == RebirthCompanionCommand.FullControl ||
            command == RebirthCompanionCommand.Follow || command == RebirthCompanionCommand.Stay ||
            command == RebirthCompanionCommand.Stop || command == RebirthCompanionCommand.Resume)
            return true;
        if (selected.DogLegacyCommandGrandfathered && (command == RebirthCompanionCommand.Guard || command == RebirthCompanionCommand.GuardArea || command == RebirthCompanionCommand.Hunting)) return true;
        float skill = GetPlayerSkill(player);
        if (command == RebirthCompanionCommand.Guard)
            return skill >= GuardSkillRequired && RebirthKnowledgeService.HasKnowledge(player, KnowledgeBasicDogTraining) && selected.DogKnowsOwnerPositionStay;
        if (command == RebirthCompanionCommand.GuardArea)
            return skill >= GuardAreaSkillRequired && RebirthKnowledgeService.HasKnowledge(player, KnowledgeWorkingDogTraining) && selected.DogKnowsGuardArea;
        if (command == RebirthCompanionCommand.Hunting)
            return skill >= HuntingSkillRequired && RebirthKnowledgeService.HasKnowledge(player, KnowledgeAnimalBehavior) && selected.DogKnowsHunting;
        return true;
    }

    public static void BeginStayExercise(EntityPlayer owner, EntityRebirthDogCompanion dog)
    {
        if (owner == null || dog == null || dog.RebirthRuntimeState == null || dog.world == null || dog.world.IsRemote()) return;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId) ||
            !string.Equals(ownerId ?? string.Empty, dog.RebirthRuntimeState.OwnerId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return;

        string key = dog.RebirthRuntimeState.StableId.ToString();
        float now = Time.realtimeSinceStartup;
        lock (Gate)
        {
            StayExercise existing;
            if (StayExercises.TryGetValue(key, out existing) && now - existing.Started < StayExerciseMaximumSeconds) return;
            StayExercises[key] = new StayExercise { Key = key, OwnerEntityId = owner.entityId, Anchor = dog.position, Started = now };
        }
    }

    public static void TickDogTraining(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView dogRecord)
    {
        if (dog == null || dogRecord == null || dog.RebirthRuntimeState == null || dog.world == null || dog.world.IsRemote()) return;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        string key = dog.RebirthRuntimeState.StableId.ToString();
        StayExercise exercise;
        lock (Gate) { if (!StayExercises.TryGetValue(key, out exercise)) return; }

        float now = Time.realtimeSinceStartup;
        if (now - exercise.Started > StayExerciseMaximumSeconds || dog.RebirthRuntimeState.Order != RebirthNpcOrderState.Stay)
        { lock (Gate) StayExercises.Remove(key); return; }
        if ((dog.position - exercise.Anchor).sqrMagnitude > DogFailureDistance * DogFailureDistance)
        { lock (Gate) StayExercises.Remove(key); return; }

        EntityPlayer owner = dog.world.GetEntity(exercise.OwnerEntityId) as EntityPlayer;
        if (owner == null || owner.IsDead()) { lock (Gate) StayExercises.Remove(key); return; }
        if ((owner.position - exercise.Anchor).sqrMagnitude >= OwnerMoveDistance * OwnerMoveDistance) exercise.OwnerMovedAway = true;
        if (!exercise.OwnerMovedAway || now - exercise.Started < StayExerciseSeconds ||
            (dog.position - exercise.Anchor).sqrMagnitude > DogHoldTolerance * DogHoldTolerance) return;

        string antiFarmKey;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(owner, out identity, out record) || identity == null)
        { lock (Gate) StayExercises.Remove(key); return; }
        antiFarmKey = identity.StorageKey + "|" + key;
        lock (Gate)
        {
            float last;
            if (LastSuccessfulExercise.TryGetValue(antiFarmKey, out last) && now - last < RebirthAdvancedDisciplineRegistry.AntiFarmTargetWindowSeconds)
            { StayExercises.Remove(key); return; }
            LastSuccessfulExercise[antiFarmKey] = now;
            StayExercises.Remove(key);
        }

        // Actual observed Stay behavior is the LBD event; simply toggling the command awards nothing.
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId=SkillId,SourceKey="phase8:animal-handling:stay:"+key,ReferenceDescription="successful observed Stay exercise",
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=SkillProgressPerSuccess
        };
        RebirthSkillTrainingComputation computation;float appliedSkillProgress,appliedAttributeProgress;
        RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(owner,evidence,out computation,out appliedSkillProgress,out appliedAttributeProgress);
        RebirthKnowledgeService.Grant(owner, KnowledgeBasicDogTraining, "animal-handling:successful-stay-exercise");
        RebirthDogStateService.ApplyTrainingProgress(dog.RebirthRuntimeState.StableId, TrainingPerSuccess, BondPerSuccess, null);
        PromoteTrainingMilestones(owner, dog.RebirthRuntimeState.StableId);
        RebirthDogStateService.CaptureRuntime(dog);
        GameManager.ShowTooltipMP(owner, L("xuiRebirthDogTrainingSuccess", "Dog training exercise completed."), "ui_success");
    }

    private static void PromoteTrainingMilestones(EntityPlayer owner, RebirthNpcStableId dogId)
    {
        float skill = GetPlayerSkill(owner);
        float training = RebirthDogStateService.GetTraining(dogId);
        float bond = RebirthDogStateService.GetBond(dogId);
        List<string> commands = new List<string>();

        if (skill >= GuardSkillRequired && training >= 12f && RebirthKnowledgeService.HasKnowledge(owner, KnowledgeBasicDogTraining))
            commands.Add(RebirthDogTrainingCommandIds.OwnerPositionStay);

        if (skill >= GuardAreaSkillRequired && training >= 28f)
        {
            RebirthKnowledgeService.Grant(owner, KnowledgeWorkingDogTraining, "animal-handling:working-dog-milestone");
            if (RebirthKnowledgeService.HasKnowledge(owner, KnowledgeWorkingDogTraining)) commands.Add(RebirthDogTrainingCommandIds.GuardArea);
        }

        if (skill >= 20f && training >= 40f && bond >= 10f)
            RebirthKnowledgeService.Grant(owner, KnowledgeAnimalBehavior, "animal-handling:behavior-milestone");
        if (skill >= HuntingSkillRequired && training >= 45f && RebirthKnowledgeService.HasKnowledge(owner, KnowledgeAnimalBehavior))
            commands.Add(RebirthDogTrainingCommandIds.Hunting);

        if (commands.Count > 0) RebirthDogStateService.ApplyTrainingProgress(dogId, 0f, 0f, commands);
    }

    private static bool CheckAdvancedCommand(EntityPlayer player, RebirthDogPersistentRecordView dog, float skill, float requiredSkill, string knowledgeId, string learnedCommandId, out string reason)
    {
        if (skill + 0.0001f < requiredSkill)
        { reason = string.Format(L("xuiRebirthDogCommandRequiresAnimalHandling", "Requires Animal Handling {0}."), requiredSkill.ToString("0", CultureInfo.InvariantCulture)); return false; }
        if (!RebirthKnowledgeService.HasKnowledge(player, knowledgeId))
        { reason = L("xuiRebirthDogCommandRequiresTrainingKnowledge", "Requires additional dog-training techniques."); return false; }
        if (!dog.LearnedCommands.Contains(learnedCommandId))
        { reason = L("xuiRebirthDogCommandNotLearned", "This dog has not learned that command yet."); return false; }
        reason = string.Empty; return true;
    }

    public static float GetPlayerSkill(EntityPlayer player)
    {
        if (player == null) return 0f;
        if (player.world != null && !player.world.IsRemote() && RebirthWorldCharacterRepository.IsServerAuthority)
        {
            RebirthWorldCharacterRecord record;
            if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.Progression != null)
            { RebirthSkillRuntimeState state; if (record.Progression.Skills.TryGetValue(SkillId, out state) && state != null) return state.Value; }
            return 0f;
        }
        RebirthSurvivorOwnerScalars scalars;
        float value;
        return RebirthSurvivorClientState.TryGetOwnerScalars(player, out scalars)
            && scalars.TryGetSkill(SkillId, out value) ? value : 0f;
    }

    public static string BuildDebugSummary(EntityPlayer player, RebirthNpcStableId dogId)
    {
        return "[REBIRTH AnimalHandling] skill=" + GetPlayerSkill(player).ToString("0.##", CultureInfo.InvariantCulture) +
               " dog=" + dogId + " training=" + RebirthDogStateService.GetTraining(dogId).ToString("0.##", CultureInfo.InvariantCulture) +
               " bond=" + RebirthDogStateService.GetBond(dogId).ToString("0.##", CultureInfo.InvariantCulture) +
               " legacyGrandfather=" + (RebirthDogStateService.IsLegacyCommandGrandfathered(dogId) ? "true" : "false") +
               " learned=" + RebirthDogStateService.GetLearnedCommandCount(dogId).ToString(CultureInfo.InvariantCulture);
    }

    public static string RunVectors()
    {
        List<string> failures = new List<string>();

        // Keep these acceptance checks runtime-evaluated. The values are authored as constants,
        // so comparing them directly makes the compiler correctly flag the failure branches as
        // unreachable (CS0162), which adds noise to release builds without improving coverage.
        float[] commandThresholds = { GuardSkillRequired, GuardAreaSkillRequired, HuntingSkillRequired };
        if (!(commandThresholds[0] < commandThresholds[1] && commandThresholds[1] < commandThresholds[2]))
            failures.Add("command Skill thresholds are not ordered");

        if (Math.Abs(SkillProgressPerSuccess - .60f) > .0001f) failures.Add("Animal Handling success award must be .60");
        if (Math.Abs(RebirthAdvancedDisciplineRegistry.AntiFarmTargetWindowSeconds - 180f) > .0001f) failures.Add("Animal Handling same-target anti-farm window must be 180s");

        int[] persistenceSchema = { RebirthDogPersistentRecord.CurrentSchemaVersion };
        if (persistenceSchema[0] < 3)
            failures.Add("dog persistence schema is not Chunk-B current");

        RebirthDogPersistentRecord fresh = new RebirthDogPersistentRecord();
        if (!fresh.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Follow) || !fresh.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Stay)) failures.Add("fresh dog is missing basic commands");
        if (fresh.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Hunting)) failures.Add("fresh dog must not start with Hunting learned");
        return "[REBIRTH AnimalHandling] vectors=" + (failures.Count == 0 ? "PASS" : "FAIL") + " failures=" + failures.Count + (failures.Count == 0 ? string.Empty : " " + string.Join(" | ", failures.ToArray()));
    }

    public static void ClearRuntime()
    { lock (Gate) { StayExercises.Clear(); LastSuccessfulExercise.Clear(); } }

    private static string L(string key, string fallback)
    { string value = Localization.Get(key); return string.IsNullOrEmpty(value) || value == key ? fallback : value; }
}
