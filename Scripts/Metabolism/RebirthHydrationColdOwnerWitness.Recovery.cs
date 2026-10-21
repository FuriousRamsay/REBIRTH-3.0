internal sealed partial class RebirthHydrationColdOwnerWitness
{
    internal bool CurrentForRecovery(RebirthHydrationColdRecoveryLease lease)
        =>lease!=null&&lease.Owns(this)&&CurrentCore(true)&&lease.Owns(this);
}
