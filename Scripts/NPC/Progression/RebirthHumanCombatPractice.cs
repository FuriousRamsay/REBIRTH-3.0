using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// Observes the existing native damage-result hook used by player combat LBD.
// Only actual health removed by an authoritative human NPC weapon hit counts.
internal static class RebirthHumanCombatPractice
{
    private sealed class PendingAward
    {
        internal RebirthNpcStableId NpcId;
        internal string SkillId;
        internal float SkillAtBatchStart;
        internal float Raw;
        internal string Receipt = "npc:combat:" + Guid.NewGuid().ToString("N");
    }
    private static readonly Dictionary<string, PendingAward> Pending = new Dictionary<string, PendingAward>(StringComparer.Ordinal);
    private static World scope;
    private static float nextFlush;

    internal static void Tick()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return;
        // An unsettled batch is scoped to the old world's canonical NPC row.
        // It cannot be reassigned to an identically named entity in a new world.
        if(!ReferenceEquals(scope, world)) { scope = world; Pending.Clear(); nextFlush = 0f; }
        if(Time.unscaledTime < nextFlush) return;
        nextFlush = Time.unscaledTime + 1f;
        foreach(var pair in new List<KeyValuePair<string, PendingAward>>(Pending))
        {
            var award = pair.Value;
            if(award.Raw <= 0f) { Pending.Remove(pair.Key); continue; }
            if(!RebirthSkillTrainingProfileRegistry.TryGet(award.SkillId, out _)) continue;
            float amount = Math.Min(award.Raw, RebirthSkillTrainingProfileRegistry.MaxDiscreteAward);
            if(amount <= 0f) continue;
            if(!RebirthHumanProgression.Award(award.NpcId, Evidence(award, amount))) continue;
            award.Raw -= amount;
            award.Receipt = "npc:combat:" + Guid.NewGuid().ToString("N");
            if(award.Raw <= 0.000001f) Pending.Remove(pair.Key);
        }
    }

    private static RebirthSkillTrainingEvidence Evidence(PendingAward award, float amount) => new RebirthSkillTrainingEvidence
    {
        SkillId = award.SkillId,
        SourceKey = "npc:combat:" + award.SkillId,
        DurableReceiptId = award.Receipt,
        ReferenceDescription = "native NPC combat damage committed against a live target",
        AuthoritativeSuccess = true,
        Mode = RebirthSkillTrainingEvidenceMode.DiscreteAward,
        CreditedWork = amount,
        DiscreteRawAward = amount
    };

    internal static void OnDamageCommitted(EntityAlive target, DamageResponse response, float healthLoss, int preHitHealth)
    {
        if(target?.world == null || target.world.IsRemote() || !ThreadManager.IsMainThread() ||
           !ReferenceEquals(target.world, GameManager.Instance?.World) || response.Source == null || healthLoss <= 0f) return;
        var attacker = target.world.GetEntity(response.Source.getEntityId()) as EntityRebirthHumanoidNPC;
        if(attacker == null || attacker == target || attacker.IsDead() || attacker.RebirthRuntimeState == null ||
           !ReferenceEquals(target.world.GetEntity(attacker.entityId), attacker)) return;
        var item = response.Source.AttackingItem;
        if(item == null) return;
        string skillId = RebirthG02WeaponFamilies.Classify(item);
        if(!RebirthWeaponFamilySkillService.IsChunk7Skill(skillId)) return;
        float sustainedDps;
        bool normalized = RebirthWeaponFamilySkillService.IsMeleeSkill(skillId)
            ? RebirthWeaponSustainedDpsService.TryGetControlledMeleeDps(attacker, item, out sustainedDps)
            : RebirthWeaponSustainedDpsService.TryGetControlledRangedDps(attacker, item, out sustainedDps);
        if(!normalized) return;
        var npcId = attacker.RebirthRuntimeState.StableId;
        if(!ReferenceEquals(scope, target.world)) { scope = target.world; Pending.Clear(); nextFlush = 0f; }
        string key = npcId + "|" + skillId;
        PendingAward pending;
        if(!Pending.TryGetValue(key, out pending))
        {
            if(!RebirthHumanProgression.TryGet(npcId, out var character) ||
               !character.Progression.Skills.TryGetValue(skillId, out var skill)) return;
            Pending[key] = pending = new PendingAward
            {
                NpcId = npcId, SkillId = skillId, SkillAtBatchStart = skill.Value + skill.Progress
            };
        }
        float credited = Math.Min(healthLoss, Math.Max(0, preHitHealth));
        float raw = RebirthWeaponSustainedDpsService.CalculateCombatProgress(credited, sustainedDps, pending.SkillAtBatchStart);
        if(raw <= 0f) return;
        pending.Raw += raw;
    }
}
