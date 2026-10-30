// Boss Watch has no WatchEntity/registered EntitySpec. These are exactly the
// no-spec branches of the shared partial, not alternate publication semantics.
public static partial class RebirthStationSnapshotEvidence
{
    private static bool ValidateEntityEvidence(Frame f,System.IO.Stream s,byte[] b)=>true;
    private static void CommitEntityEvidence(Frame f){}
    private static void InvalidateEntityEvidence(object s){}
    private static void DisposeEntityEvidence(Request r){}
}
