using System;

// Binds the dog Smart Looting row (RebirthDogSmartLootTransfer) to the shared native source lease.
// This adapter adds no authority: every call goes through the same Lease the human leg uses, whose
// ApplyStage needs the row SAVED at phase 1 / attempted 1 (NativeOriginal.MayApply) and a one-use
// permit consumed by the installed source setter guard.
internal sealed class RebirthRemoteResourceDogSourceCustody : RebirthDogSmartLootTransfer.ISourceCustody
{
    private sealed class DogLease : RebirthDogSmartLootTransfer.ISourceLease
    {
        private readonly RebirthRemoteResourceSourceCustody.Lease lease;
        internal DogLease(RebirthRemoteResourceSourceCustody.Lease value) { lease = value; }

        public bool TryObserveSaved(int stage, out string proof)
        {
            proof = null;
            // The dog leg uses stage 0 (untouched) and stage 1 (whole-stack debit) only.
            return (stage == 0 || stage == 1) && lease.TryObserveSaved(stage, out proof);
        }

        public bool ApplyStage(int stage) => stage == 1 && lease.ApplyStage(1);

        // Dispose keeps the hold unless the saved row is terminal ("unknown disposal never unlocks").
        public void Release() => lease.Dispose();
    }

    public bool TryReserve(EntityPlayer actor, RebirthDogSmartLootOriginal row, Func<bool> current,
        out RebirthDogSmartLootTransfer.ISourceLease lease)
    {
        lease = null;
        if (!RebirthRemoteResourceSourceCustody.TryReserveDogSmartLooting(actor, row, current, out var bound) || bound == null)
            return false;
        lease = new DogLease(bound);
        return true;
    }
}
