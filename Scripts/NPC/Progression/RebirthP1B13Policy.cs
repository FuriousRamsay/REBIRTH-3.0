using System;
using System.Collections.Generic;

#nullable disable

internal enum RebirthP1OwnerAwardState { Ready, NotYet, Never }
internal sealed class RebirthP1OwnerAwardItem { internal string SkillId; internal float CreditedWork; internal string Description; }
internal sealed class RebirthP1OwnerAwardPlan { internal bool ImplicitDiscrete; internal RebirthNpcWorkOutcomeRecord Outcome; internal List<RebirthP1OwnerAwardItem> Items = new List<RebirthP1OwnerAwardItem>(); }

// Bounded, receipt-derived owner award policy (proposal for lead review; see the P1 matrix doc). Never from assignment,
// staging, queue admission, elapsed time or an unsaved outcome. Once-only per (outcome, skill) via the B13 helper receipt.
internal static class RebirthP1B13Policy
{
    internal const float MaxManagementUnits = 20f;
    // Ceiling on the seconds-equivalent CREDIT for genuinely MEASURED effort (SupplyOwnerMetric) only. It is a credit cap, never a
    // claimed duration, and is not applied to discrete receipts.
    internal const float MaxCreditSeconds = 120f;
    // A saved one-shot completion with no truthful native duration (one witnessed relocation, one completed water-bucket refill)
    // is DISCRETE evidence: exactly one raw unit per exact saved action receipt (D-02/29/30/31/32). The framework applies its
    // own max_discrete_award ceiling; the in-game balance pass may revisit this value.
    internal const float DiscreteRawPerReceipt = 1f;
    // P10 registers the exact saved storage-move relocation witness (an existing item merely moved: Produced/Consumed are null).
    // Unregistered means storage outcomes are never awarded.
    internal static Func<RebirthNpcWorkOutcomeRecord, bool> RelocationWitness;

    private static float Sum(Dictionary<string, int> map)
    {
        long sum = 0;
        if(map != null) foreach(var pair in map) if(pair.Value > 0) sum += pair.Value;
        return sum;
    }

    internal static RebirthP1OwnerAwardState Resolve(Guid outcomeId, out RebirthP1OwnerAwardPlan plan)
    {
        plan = null;
        RebirthNpcWorkOutcomeRecord o = null;
        foreach(var row in RebirthNpcWorkOutcomeService.GetOutcomes()) if(row != null && row.OutcomeId == outcomeId) { o = row; break; }
        // Evicted from the bounded log: the B13 helper requires retention, so it can no longer be awarded.
        if(o == null || o.State != RebirthNpcWorkOutcomeState.Completed) return RebirthP1OwnerAwardState.Never;
        if(!o.DurableCompletion) return RebirthP1OwnerAwardState.NotYet;
        if(o.DefinitionId == RebirthNpcLiteratureWorkAdapter.DefinitionId || RebirthNpcRecipeStagingWork.IsDefinition(o.DefinitionId)) return RebirthP1OwnerAwardState.Never;
        float units;
        bool relocation = false;
        if(RebirthNpcRecipeQueueWork.IsDefinition(o.DefinitionId))
        {
            // The definition covers queue admission too; only a saved finished-output pickup receipt proves completion.
            units = Sum(o.Produced);
            if(units <= 0f) return RebirthP1OwnerAwardState.Never;
            if(!RebirthNpcStationQueueOriginal.TryReadPickupReceipt(o.NpcId, o.AssignmentId, out _)) return RebirthP1OwnerAwardState.NotYet;
        }
        else if(RebirthNpcStorageOrganizationWork.IsDefinition(o.DefinitionId))
        {
            if(RelocationWitness == null) return RebirthP1OwnerAwardState.Never;
            if(!RelocationWitness(o)) return RebirthP1OwnerAwardState.NotYet;
            units = 1f; // one exact saved relocation, regardless of stack size
            relocation = true;
        }
        else
        {
            // Water: one completed bucket refill is ONE unit whatever its in-game volume (no 200 to 20 mapping, no Produced gate).
            units = RebirthP8NpcWaterRefillWork.IsDefinition(o.DefinitionId) ? 1f : Sum(o.Produced) + Sum(o.Consumed);
            if(units <= 0f) return RebirthP1OwnerAwardState.Never;
        }
        plan = new RebirthP1OwnerAwardPlan { Outcome = o, ImplicitDiscrete = relocation };
        plan.Items.Add(new RebirthP1OwnerAwardItem { SkillId = RebirthSurvivorIds.SkillManagement, CreditedWork = Math.Min(MaxManagementUnits, units), Description = "NPC completed directed work: " + o.DefinitionId });
        // Leadership: every qualifying outcome here is physically completed work by the owner's companion; one flat unit per outcome,
        // with the training profile's RepetitionKey (definition|target) providing diminishing returns.
        plan.Items.Add(new RebirthP1OwnerAwardItem { SkillId = RebirthSurvivorIds.SkillLeadership, CreditedWork = 1f, Description = "Companion completed work: " + o.DefinitionId });
        return RebirthP1OwnerAwardState.Ready;
    }
}
