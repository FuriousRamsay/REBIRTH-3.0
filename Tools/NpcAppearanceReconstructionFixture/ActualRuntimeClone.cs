public partial class RebirthNpcRuntimeState {    internal RebirthNpcRuntimeState CloneForProjection()
    {
        RebirthNpcRuntimeState copy = new RebirthNpcRuntimeState(StableId, ProfileId);
        copy.Presence = Presence; copy.OwnershipKind = OwnershipKind; copy.OwnerId = OwnerId ?? string.Empty;
        copy.Order = Order; copy.Travel = Travel; copy.GuardPosition = GuardPosition; copy.HasGuardPosition = HasGuardPosition;
        copy.PreparedRestorationPending = PreparedRestorationPending;
        copy.Revision = Revision; copy.HumanAppearance = HumanAppearance; copy.HasHumanAppearance = HasHumanAppearance; copy.Dirty = Dirty;
        return copy;
    }
}

