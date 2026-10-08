using System;

// Single game-thread execution while inventory interactions are held. The adapter
// must authenticate the offer, decode every native item, verify its applying receipt
// and prepare its native writes before invoking this sequence. No guessed rollback.
public static class RebirthGearOwnerApplySequence
{
    // Conflict is reserved for a new, untouched operation. Once intent may be
    // durable, only successful completion or reconciliation can release custody.
    public enum Result { Applied, Conflict, Pending, NeedsReconciliation }

    public static Result Execute(RebirthGearInventoryPlan plan, bool hasApplyingReceipt, Func<bool> isCurrent,
        Func<RebirthGearInventoryPlan.Stack[]> readBag, Func<RebirthGearInventoryPlan.Stack[]> readBelt,
        Func<bool> persistApplying, Action ensureBacking, Action<RebirthGearInventoryPlan.Change> write,
        Func<bool> persistApplied)
    {
        if (plan == null || !plan.IsConserved() || isCurrent == null || readBag == null || readBelt == null
            || persistApplying == null || ensureBacking == null || write == null || persistApplied == null) return hasApplyingReceipt ? Result.NeedsReconciliation : Result.Conflict;
        try
        {
            if (!isCurrent()) return hasApplyingReceipt ? Result.NeedsReconciliation : Result.Conflict;
            if (!hasApplyingReceipt && !plan.MatchesBefore(readBag(), readBelt(), plan.GearBefore,
                plan.BagSlotsBefore, plan.BeltSlotsBefore)) return Result.Conflict;
            if (!isCurrent()) return hasApplyingReceipt ? Result.NeedsReconciliation : Result.Conflict;
            // Retry confirms durable intent again before permitting any writes.
            if (!persistApplying()) return Result.Pending;
            if (!isCurrent()) return Result.NeedsReconciliation;
            ensureBacking(); // Expand only; preserve removed tails until commit.
            var bag = readBag(); var belt = readBelt();
            if (!isCurrent()) return Result.NeedsReconciliation;
            var status = plan.InspectApplication(bag, belt, hasApplyingReceipt);
            if (status == RebirthGearInventoryPlan.ApplicationState.Conflict) return Result.NeedsReconciliation;
            foreach (var change in plan.Changes)
            {
                // Native setters may invoke callbacks that change other slots.
                // Never overwrite using the snapshot from an earlier setter.
                bag = readBag(); belt = readBelt();
                if (!isCurrent()) return Result.NeedsReconciliation;
                if (plan.InspectApplication(bag, belt, true) == RebirthGearInventoryPlan.ApplicationState.Conflict)
                    return Result.NeedsReconciliation;
                var actual = (change.IsBag ? bag : belt)[change.Index];
                if (actual.Count == change.After.Count && actual.ItemData == change.After.ItemData) continue;
                if (!isCurrent()) return Result.NeedsReconciliation;
                write(change);
            }
            bag = readBag(); belt = readBelt();
            if (!isCurrent()) return Result.NeedsReconciliation;
            status = plan.InspectApplication(bag, belt, true);
            if (status == RebirthGearInventoryPlan.ApplicationState.Conflict) return Result.NeedsReconciliation;
            foreach (var change in plan.Changes)
            {
                var actual = (change.IsBag ? bag : belt)[change.Index];
                if (actual.Count != change.After.Count || actual.ItemData != change.After.ItemData) return Result.Pending;
            }
            if (!isCurrent()) return Result.NeedsReconciliation;
            bool applied = persistApplied();
            if (!isCurrent()) return Result.NeedsReconciliation;
            return applied ? Result.Applied : Result.Pending;
        }
        catch (Exception)
        {
            // Some setters may already have applied. Keep the applying marker and
            // exact pending plan; a timeout must never trigger source restoration.
            return Result.Pending;
        }
    }
}
