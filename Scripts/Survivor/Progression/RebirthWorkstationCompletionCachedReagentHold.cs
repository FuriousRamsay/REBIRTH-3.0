using System;
using System.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Independent durable reagent settlement must not rewrite the station
    // underneath another positively retained original-primary/native Before.
    // Deferral leaves its own outbox decision/attempt/receipt untouched.
    internal static bool CanSettleReagentOriginal(TileEntityWorkstation station)=>station!=null&&
        CanTickRemoteNativeCompletion(station)&&RebirthProductiveEarnedUiDeferredTransport.CanTickAuthorityNative(station)&&
        RebirthProductiveEarnedUiDeferredTransport.CanTickKnownNativeReceipt(station)&&
        !frames.Values.Any(frame=>!frame.Closed&&!frame.NativeAccepted&&frame.Observation.FullBoundary&&
            ReferenceEquals(frame.Observation.World,GameManager.Instance?.World)&&ReferenceEquals(frame.Observation.Station,station));
}
