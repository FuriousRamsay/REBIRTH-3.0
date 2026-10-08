using System;

// Operates on a detached candidate. Caller must durably save custody and publish the
// reservation before dispatching owner inventory work. These transitions do not save.
public static class RebirthVehicleTransferReservation
{
    public static bool TryReserve(RebirthVehicleAssembly candidate, Guid transactionId, long expectedRevision)
    {
        if (candidate == null || candidate.AssemblyId == Guid.Empty || transactionId == Guid.Empty
            || expectedRevision < 0 || candidate.Revision != expectedRevision
            || candidate.LastAppliedInventoryTransferId == transactionId) return false;
        if (candidate.PendingInventoryTransferId != Guid.Empty)
            return candidate.PendingInventoryTransferId == transactionId;
        candidate.PendingInventoryTransferId = transactionId;
        return true;
    }

    // Invoke only after the authenticated saved owner receipt and planned assembly
    // change are verified. Persist this marker together with that assembly change.
    public static bool TryMarkApplied(RebirthVehicleAssembly candidate, Guid transactionId)
    {
        if (candidate == null || candidate.AssemblyId == Guid.Empty || transactionId == Guid.Empty) return false;
        if (candidate.PendingInventoryTransferId == Guid.Empty)
            return candidate.LastAppliedInventoryTransferId == transactionId;
        if (candidate.PendingInventoryTransferId != transactionId) return false;
        candidate.PendingInventoryTransferId = Guid.Empty;
        candidate.LastAppliedInventoryTransferId = transactionId;
        return true;
    }
}
