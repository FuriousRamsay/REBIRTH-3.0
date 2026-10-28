using System;

// PREPARED, NOT RUN. Inputs must be actual target-native-v47 captured bytes
// and original native ECD/marker edit, not a fabricated publication witness.
internal static class mpA_logistics_rich_preview_fixture
{
    internal static void Verify(byte[] actualNativeSnapshot,int x,int z,RebirthLogisticsParcelPreview.Edit[] exactEdits,bool expectedFits)
    {
        if(actualNativeSnapshot==null||actualNativeSnapshot.Length<=8*1024*1024||actualNativeSnapshot.Length>32*1024*1024)throw new Exception("Requires real rich native snapshot in explicit domain allowance.");
        var before=(byte[])actualNativeSnapshot.Clone();
        bool fits=RebirthLogisticsParcelPreview.TryBuild(actualNativeSnapshot,x,z,exactEdits,out var candidate,out int compressed);
        if(fits!=expectedFits)throw new Exception("Actual native rich preview expectation changed.");
        if(fits&&(candidate==null||candidate.Length>32*1024*1024||compressed>255*4096-16))throw new Exception("Native raw/sector bound not preserved.");
        if(!System.Linq.Enumerable.SequenceEqual(before,actualNativeSnapshot))throw new Exception("Detached preview mutated native baseline.");
        // This never grants spawn/setter permission. Real publication must
        // still pass the native same-snapshot+region readback observer.
    }
}
