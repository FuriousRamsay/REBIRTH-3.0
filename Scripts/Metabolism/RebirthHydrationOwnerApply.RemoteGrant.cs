internal static partial class RebirthHydrationOwnerApply
{
    internal static RebirthGearOwnerApplySequence.Result Apply(RebirthHydrationRemoteClientGrant grant)
    {
        if(grant==null)return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        return ApplyHeld(grant.Player,grant.Original,grant.Current);
    }
}
