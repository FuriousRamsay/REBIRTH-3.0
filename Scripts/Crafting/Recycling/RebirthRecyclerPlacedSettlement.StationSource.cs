using System;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Two actual producers implement this station-only contract: the local
    // native owner original and an authenticated remote host original. It has
    // no owner inventory setter, synthetic player, or owner-save permission.
    internal interface IStationOriginal
    {
        Guid Operation{get;}string Purpose{get;}string Owner{get;}string Creation{get;}string WorldKey{get;}
        int Deaths{get;}World World{get;}TileEntityWorkstation Station{get;}
        byte[] BeforeNative{get;}byte[] AfterNative{get;}byte[] Image{get;}
        int BeforeOffset{get;}int AfterOffset{get;}
        bool Current();bool NativeLineageCurrent();
    }
}
