using System;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static Frame observingCachedRecipientSync;
    internal static bool IsObservingCachedRecipientSync(Frame frame)=>frame!=null&&ReferenceEquals(observingCachedRecipientSync,frame)&&frame.NativeAccepted&&frame.CachedPhysical!=null;
    private static void ObserveCachedRecipientSync(Frame frame)
    {
        if(frame?.CachedPhysical==null)return;
        observingCachedRecipientSync=frame;
        try{ObserveCachedPeerSync(frame);if(frame.CachedPhysical.OwnerRenewal!=null)RebirthProductiveEarnedUiDeferredTransport.ObserveActualCachedRecipientSync(frame);}
        finally{observingCachedRecipientSync=null;}
    }
}
