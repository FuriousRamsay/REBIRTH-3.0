#if DEBUG
using System;

// Detached scalar production predicate only; no synthesized native projection/ACK.
internal static class RebirthCompanionSnapshotRecoveryFixture
{
    internal static void Verify()
    {
        Require(RebirthCompanionSnapshotService.MatchesSnapshotStamp(7,8,9,7,8,9));
        Require(!RebirthCompanionSnapshotService.MatchesSnapshotStamp(7,8,9,6,8,9));
        Require(!RebirthCompanionSnapshotService.MatchesSnapshotStamp(7,8,9,7,6,9));
        Require(!RebirthCompanionSnapshotService.MatchesSnapshotStamp(7,8,9,7,8,8));
        Require(!RebirthCompanionSnapshotService.MatchesSnapshotStamp(7,8,9,7,8,10));
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Companion snapshot recovery stamp mismatch");}
}

#endif
