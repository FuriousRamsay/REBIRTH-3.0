internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostTerminalPublication
    {
        // The epoch was captured in the actual complete native removal reader;
        // the saved token cannot renew it from a later equal current body.
        internal bool TryGetCachedOwnerReadFence(out long fence)
        {
            fence=0;if(removal?.HasReadFence!=true||!Current()||
                !TryReadNativeObservationFence(removal.Scope.World,removal.Scope.Station,out var current)||current!=removal.ReadFence)return false;
            fence=removal.ReadFence;return true;
        }
    }
}
