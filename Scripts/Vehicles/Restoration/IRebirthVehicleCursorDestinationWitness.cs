using System;

// Detached native destination evidence. Matching memory alone cannot settle
// cursor custody; implementations must prove the scoped native persisted write.
internal interface IRebirthVehicleCursorDestinationWitness
{
    string Encode();
    bool MatchesLive(EntityPlayerLocal owner);
    bool RequestAndVerifySaved(EntityPlayerLocal owner, Guid operation);
}
