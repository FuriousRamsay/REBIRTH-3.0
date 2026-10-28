using System;
using System.Collections.Generic;

// Authored, NOT RUN. Call only on the actual client main thread after an
// actual scoped native Block callback has finalized its optimistic view.
// Every vector needs a FRESH real original; invalidation is monotonic.
// Delegates perform actual native mutations/load/connection transitions on a
// disposable world. They are not substitutes for authority or save evidence.
internal static class RemoteBlockOptimisticViewChecks
{
    internal static void Invalidation(RebirthRemoteResourceClientOriginal.Intent original,
        Action actualNativeInterruption,Action restoreExactVisualImage)
    {
        if(!RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))
            throw new Exception("fixture needs actual finalized uninterrupted original");
        actualNativeInterruption();
        if(RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))
            throw new Exception("interrupted original retained view permission");
        restoreExactVisualImage();
        if(RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))
            throw new Exception("ABA restored stale view permission");
    }

    internal static void ColdNoWrite(World actualWorld,
        RebirthRemoteResourceClientOriginal.Intent original,IReadOnlyList<BlockChangeInfo> actualHostProjection)
    {
        RebirthRemoteResourceBlockOptimisticView.Retire(original);
        if(RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))
            throw new Exception("retired/cold view grants write permission");
        if(!RebirthRemoteResourceBlockOptimisticView.MatchesProjection(actualWorld,actualHostProjection))
            throw new Exception("fixture requires independently observed already-identical projection");
        // Read-only equality must not recreate a watcher or grant a later write.
        if(RebirthRemoteResourceBlockOptimisticView.Uninterrupted(original))
            throw new Exception("read-only equality recreated view authority");
    }

    // Required independent vectors: parent raw ABA; damage ABA; density ABA;
    // full paint ABA; every rotated child across chunk boundaries; layer
    // replacement; channel replacement; native chunk reset/read/copy; unload
    // and reload; disconnect and replacement connection followed by Tick;
    // concurrent second original at overlapping target. Include an unrelated
    // coordinate mutation negative control (watch remains uninterrupted).
    // End-to-end genuine MP vectors additionally delay fragmented replies,
    // mutate target before their arrival, verify no stale correction and
    // independently verify Root4 financial ACK/hold release still completes.
}
