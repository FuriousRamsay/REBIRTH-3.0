using System;
using System.Collections.Generic;
using System.Linq;

public enum RebirthVehicleTransferStep
{
    WaitForEvidence, ReserveAssembly, ApplyDebit, ApplyAssembly,
    WaitForAssemblySave, ApplyCredit, Complete, CancelRejected, Reconcile, ApplyDebitBatch
}

// Pure decision layer. Caller supplies authenticated saved receipts and an assembly
// from the same world/session. No step here performs a mutation or proves a save.
public static class RebirthVehicleTransferRecovery
{
    public static RebirthVehicleTransferStep Next(RebirthVehicleTransferPlan plan,
        RebirthVehicleAssembly observed, IDictionary<Guid, RebirthVehicleOwnerTransferResult> receipts,
        bool assemblySaveVerified, out Guid nextReceipt, float? nativeFuelLevel = null, float? nativeFuelCapacity = null, int? nativeHealth = null, int? nativeMaximumHealth = null)
    {
        nextReceipt = Guid.Empty;
        if (plan == null || observed == null || receipts == null) return RebirthVehicleTransferStep.WaitForEvidence;
        foreach (var operation in plan.Items)
        {
            RebirthVehicleOwnerTransferResult state;
            if (!receipts.TryGetValue(operation.ReceiptId, out state)) return RebirthVehicleTransferStep.WaitForEvidence;
            if (state == RebirthVehicleOwnerTransferResult.Indeterminate
                || !Enum.IsDefined(typeof(RebirthVehicleOwnerTransferResult), state)) return RebirthVehicleTransferStep.Reconcile;
        }
        var before = plan.ReadBefore();
        var after = plan.ReadAfter();
        bool original = SameSnapshot(observed, before);
        var reserved = before.DeepClone();
        if (!RebirthVehicleTransferReservation.TryReserve(reserved, plan.TransactionId, before.Revision))
            return RebirthVehicleTransferStep.Reconcile;
        bool held = SameSnapshot(observed, reserved);
        bool applied = SameSnapshot(observed, after);
        if (!original && !held && !applied) return RebirthVehicleTransferStep.Reconcile;

        if (plan.Fuel != null)
        {
            if (!nativeFuelLevel.HasValue || !nativeFuelCapacity.HasValue)
                return RebirthVehicleTransferStep.WaitForEvidence;
            float expectedLevel = applied ? plan.Fuel.AfterLevel : plan.Fuel.BeforeLevel;
            if (nativeFuelCapacity.Value != plan.Fuel.Capacity || nativeFuelLevel.Value != expectedLevel)
                return RebirthVehicleTransferStep.Reconcile;
        }

        if (plan.Health != null)
        {
            if (!nativeHealth.HasValue || !nativeMaximumHealth.HasValue) return RebirthVehicleTransferStep.WaitForEvidence;
            if (nativeMaximumHealth.Value != plan.Health.Maximum || nativeHealth.Value != (applied ? plan.Health.After : plan.Health.Before))
                return RebirthVehicleTransferStep.Reconcile;
        }
        var debits = plan.Items.Where(i => i.Debit).ToArray();
        var credits = plan.Items.Where(i => !i.Debit).ToArray();
        bool anyDebitApplied = debits.Any(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Applied);
        if (debits.Length > 1)
        {
            IReadOnlyList<RebirthVehicleTransferItem> batch;
            if (!TryGetDebitBatch(plan, out batch)
                || (anyDebitApplied && debits.Any(i => receipts[i.ReceiptId] != RebirthVehicleOwnerTransferResult.Applied)))
                return RebirthVehicleTransferStep.Reconcile;
        }
        bool debitRejected = debits.Any(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Rejected);
        bool creditMoved = credits.Any(i => receipts[i.ReceiptId] != RebirthVehicleOwnerTransferResult.Pending);
        if (debitRejected)
            return !applied && !anyDebitApplied && !creditMoved
                && debits.All(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Rejected)
                ? RebirthVehicleTransferStep.CancelRejected : RebirthVehicleTransferStep.Reconcile;
        if (!applied && creditMoved) return RebirthVehicleTransferStep.Reconcile;
        if (applied)
        {
            if (debits.Any(i => receipts[i.ReceiptId] != RebirthVehicleOwnerTransferResult.Applied)
                || credits.Any(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Rejected))
                return RebirthVehicleTransferStep.Reconcile;
            if (!assemblySaveVerified) return RebirthVehicleTransferStep.WaitForAssemblySave;
            var credit = credits.FirstOrDefault(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Pending);
            if (credit == null) return RebirthVehicleTransferStep.Complete;
            nextReceipt = credit.ReceiptId;
            return RebirthVehicleTransferStep.ApplyCredit;
        }
        if (original) return RebirthVehicleTransferStep.ReserveAssembly;
        var debit = debits.FirstOrDefault(i => receipts[i.ReceiptId] == RebirthVehicleOwnerTransferResult.Pending);
        if (debit == null) return RebirthVehicleTransferStep.ApplyAssembly;
        if (debits.Length > 1) return RebirthVehicleTransferStep.ApplyDebitBatch;
        nextReceipt = debit.ReceiptId;
        return RebirthVehicleTransferStep.ApplyDebit;
    }

    // Future transport sends the entire immutable group to OwnerDebitBatch.Apply.
    // Never extract only a pending subset: mixed saved outcomes require reconciliation.
    public static bool TryGetDebitBatch(RebirthVehicleTransferPlan plan, out IReadOnlyList<RebirthVehicleTransferItem> batch)
    {
        batch = null;
        if (plan == null) return false;
        var debits = plan.Items.Where(i => i.Debit).ToArray();
        if (debits.Length < 2 || debits.Select(i => i.Source).Distinct().Count() != 1
            || debits.Select(i => i.Slot).Distinct().Count() != debits.Length) return false;
        batch = Array.AsReadOnly(debits);
        return true;
    }

    private static bool SameSnapshot(RebirthVehicleAssembly left, RebirthVehicleAssembly right)
    {
        // Do not treat a matching revision alone as evidence of matching contents.
        // Serialization also includes full native part metadata and transfer IDs.
        return RebirthVehicleAssemblySerializer.ToBase64(left) == RebirthVehicleAssemblySerializer.ToBase64(right);
    }
}
