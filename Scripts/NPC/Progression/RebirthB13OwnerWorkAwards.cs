using System;
using System.Collections.Generic;

#nullable disable

// B13's narrow bridge from a saved physical NPC-work outcome to the directing
// player's existing Skill training pipeline. The work executor owns the proof
// of credited work and its live work rate; this class cannot infer either from
// a queued assignment, timer, container click, or generic WorkCompleted event.
internal static class RebirthB13OwnerWorkAwards
{
    private const string ReceiptPrefix = "b13:owner-work:";
    private static readonly object RegistrationGate = new object();
    private static readonly Func<HashSet<string>, ISet<string>> ProtectOwnerReceipts = Protect;
    private static bool registered;

    // Call during progression installation, before other player Skill awards
    // may prune receipts. The same protection is installed lazily on first use.
    internal static void EnsureReceiptProtection()
    {
        lock (RegistrationGate)
        {
            if (registered) return;
            RebirthTeachingOutcomeStore.RegisterReceiptProtection(ProtectOwnerReceipts);
            registered = true;
        }
    }

    // A work package may call this only after its native physical action has
    // produced the exact committed outcome and an action-derived work metric.
    // Offline owners, uncheckpointed outcomes and missing metrics remain held
    // for retry by the caller; a false return is never permission to substitute
    // a nearby player or award based on elapsed assignment time.
    internal static bool TryAwardCommitted(
        RebirthNpcWorkOutcomeRecord outcome, string skillId,
        float creditedWork, float liveWorkRate, string referenceDescription,
        out float appliedSkillProgress)
        => TryAwardCommittedCore(outcome, skillId, creditedWork, liveWorkRate, 0f,
            RebirthSkillTrainingEvidenceMode.ContinuousWork, referenceDescription,
            out appliedSkillProgress);

    // A saved, one-shot physical action has no truthful work rate. Reuse the
    // exact same owner/outcome/receipt proof as continuous work, but feed the
    // existing discrete training mode rather than inventing elapsed seconds.
    internal static bool TryAwardCommittedDiscrete(
        RebirthNpcWorkOutcomeRecord outcome, string skillId,
        float discreteRawAward, string referenceDescription,
        out float appliedSkillProgress)
        => TryAwardCommittedCore(outcome, skillId, 1f, 0f, discreteRawAward,
            RebirthSkillTrainingEvidenceMode.DiscreteAward, referenceDescription,
            out appliedSkillProgress);

    private static bool TryAwardCommittedCore(
        RebirthNpcWorkOutcomeRecord outcome, string skillId,
        float creditedWork, float liveWorkRate, float discreteRawAward,
        RebirthSkillTrainingEvidenceMode mode, string referenceDescription,
        out float appliedSkillProgress)
    {
        appliedSkillProgress = 0f;
        if (!ThreadManager.IsMainThread()) return false;
        World world = GameManager.Instance?.World;
        if (world == null || world.IsRemote() || outcome == null ||
            outcome.OutcomeId == Guid.Empty || outcome.AssignmentId == 0 ||
            outcome.NpcId.IsEmpty || !outcome.DurableCompletion ||
            outcome.State != RebirthNpcWorkOutcomeState.Completed ||
            string.IsNullOrWhiteSpace(outcome.DefinitionId) ||
            string.IsNullOrWhiteSpace(outcome.DurableWitness) ||
            string.IsNullOrWhiteSpace(referenceDescription) ||
            !FinitePositive(creditedWork) ||
            (mode == RebirthSkillTrainingEvidenceMode.ContinuousWork
                ? !FinitePositive(liveWorkRate)
                : mode != RebirthSkillTrainingEvidenceMode.DiscreteAward || !FinitePositive(discreteRawAward)) ||
            (skillId != RebirthSurvivorIds.SkillManagement &&
             skillId != RebirthSurvivorIds.SkillLeadership)) return false;

        // Literature and recipe staging record knowledge/input handling, not a
        // finished production or movement action. The recipe-queue definition
        // also represents completed output pickup, but that path needs its
        // separate, saved physical pickup receipt before it can award B13.
        if (outcome.DefinitionId == RebirthNpcLiteratureWorkAdapter.DefinitionId ||
            RebirthNpcRecipeStagingWork.IsDefinition(outcome.DefinitionId)) return false;
        // Moving an existing item is eligible owner work only when the
        // storage executor proves its exact saved source debit, destination
        // credit and bag-clear witness. A completed-looking outcome alone is
        // not a physical relocation receipt.
        if (RebirthNpcStorageOrganizationWork.IsDefinition(outcome.DefinitionId) &&
            !RebirthNpcStorageOrganizationWork.AllowsOutcome(outcome)) return false;
        if (RebirthNpcRecipeQueueWork.IsDefinition(outcome.DefinitionId))
        {
            // The same definition is also used for queue admission. Bind the
            // finished pickup to this exact outcome, item and saved witness;
            // merely finding some completed pickup for the assignment is not
            // enough to prove that the offered outcome credits production.
            if (!RebirthNpcStationQueueOriginal.TryReadPickupReceipt(
                    outcome.NpcId, outcome.AssignmentId, out var pickup) ||
                pickup == null || pickup.Operation != outcome.OutcomeId ||
                pickup.AssignmentId != outcome.AssignmentId || pickup.Npc != outcome.NpcId ||
                pickup.CreditId == Guid.Empty || pickup.CompletedUtcTicks != outcome.CompletedUtcTicks ||
                string.IsNullOrEmpty(pickup.ItemName) ||
                outcome.Produced == null || outcome.Produced.Count != 1 ||
                !outcome.Produced.TryGetValue(pickup.ItemName, out int produced) ||
                produced < 1 || creditedWork > produced ||
                !PickupStackMatches(pickup.Frame, pickup.ItemName, produced) ||
                !outcome.DurableWitness.EndsWith(
                    "|output:" + pickup.OutputProof + "|pickup:" + pickup.PickupProof,
                    StringComparison.Ordinal)) return false;
        }

        RebirthSkillTrainingProfile profile;
        if (!RebirthSkillTrainingProfileRegistry.TryGet(skillId, out profile) ||
            profile == null || profile.State != "LOCKED" ||
            profile.Family != "companion_direction") return false;

        RebirthNpcWorkAssignment assignment;
        // A committed physical action may finish while its recurring assignment
        // remains active; completion of the whole assignment is not the award
        // boundary. Bind to the original authorized assignment and the exact
        // saved action receipt instead.
        if (!RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId, out assignment) ||
            assignment == null ||
            assignment.NpcId != outcome.NpcId ||
            assignment.CreatedUtcTicks <= 0 || outcome.StartedUtcTicks != assignment.CreatedUtcTicks ||
            outcome.CompletedUtcTicks < outcome.StartedUtcTicks ||
            !string.Equals(assignment.DefinitionId, outcome.DefinitionId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(assignment.ActorId)) return false;

        // The outcome must still be retained and match the current primary
        // save. Never trust a caller-supplied object or a backup alone.
        bool retained = false;
        foreach (var row in RebirthNpcWorkOutcomeService.GetOutcomes())
            if (row != null && row.OutcomeId == outcome.OutcomeId &&
                row.CompletedUtcTicks == outcome.CompletedUtcTicks &&
                RebirthNpcWorkOutcomeService.SameCompletionWitness(row, outcome))
            { retained = true; break; }
        if (!retained || !RebirthNpcWorkOutcomePersistenceStore.IsCurrentPrimaryCommitted(outcome)) return false;

        EntityPlayer owner = RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
        if (owner == null || owner.IsDead() || !ReferenceEquals(owner.world, world)) return false;

        EnsureReceiptProtection();
        string receipt = Receipt(outcome.OutcomeId, skillId);
        var evidence = new RebirthSkillTrainingEvidence
        {
            SkillId = skillId,
            SourceKey = "b13:completed:" + outcome.DefinitionId,
            RepetitionKey = outcome.DefinitionId + "|" + (outcome.TargetId ?? string.Empty),
            DurableReceiptId = receipt,
            ReferenceDescription = referenceDescription,
            AuthoritativeSuccess = true,
            Mode = mode,
            CreditedWork = creditedWork,
            LiveWorkRate = liveWorkRate,
            DiscreteRawAward = discreteRawAward,
            MinimumInterval = 0f
        };
        return RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(
            owner, evidence, out _, out appliedSkillProgress, out _);
    }

    private static string Receipt(Guid outcomeId, string skillId) =>
        ReceiptPrefix + outcomeId.ToString("N") + ":" + skillId;

    private static bool PickupStackMatches(string frame, string itemName, int count)
    {
        if (string.IsNullOrEmpty(frame) || string.IsNullOrEmpty(itemName) || count < 1) return false;
        try
        {
            var stack = RebirthHumanEquipment.Decode(frame);
            return stack != null && !stack.IsEmpty() && stack.count == count &&
                string.Equals(stack.itemValue.ItemClass.GetItemName(), itemName, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private static ISet<string> Protect(HashSet<string> receipts)
    {
        var protectedIds = new HashSet<string>(StringComparer.Ordinal);
        if (receipts == null || receipts.Count == 0) return protectedIds;
        // Work-outcome history is bounded and may evict a committed row. A
        // receipt is the only durable once-only guard after that eviction, so
        // protect every B13 owner receipt for the character's lifetime.
        foreach (string receipt in receipts)
            if (receipt != null && receipt.StartsWith(ReceiptPrefix, StringComparison.Ordinal))
                protectedIds.Add(receipt);
        return protectedIds;
    }

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
