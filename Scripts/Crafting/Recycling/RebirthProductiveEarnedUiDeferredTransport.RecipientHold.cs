using System;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // These holds belong only to the actual authenticated recipient read. An
    // offline owner, a parsed purpose file, or a queued host contribution does
    // not stop an ordinary workstation. Unknown after that read stays owned.
    private static bool HasRetainedRecipient(TileEntityWorkstation station,bool includeLocal=true)
    {
        if(station==null||!ThreadManager.IsMainThread())return false;
        if(HoldsCachedPrimary(station))return true;
        if(HoldsCachedPhysicalOwnerGroup(station))return true;
        if(HostReadyHold(station))return true;
        if(includeLocal&&RebirthProductiveEarnedUiOwnerFile.HoldsLocalOwner(station))return true;
        foreach(var original in clientHydrations.Values)
            if(!original.Closed&&!original.HandoffAccepted&&ReferenceEquals(original.Station,station)&&
                ReferenceEquals(original.World,GameManager.Instance?.World))return true;
        return false;
    }
    internal static bool CanSyncKnownNativeReceipt(XUiC_WorkstationWindowGroup group)=>
        RebirthWorkstationCompletionPipeline.CanSyncCachedUnused(group)||
        RebirthWorkstationCompletionPipeline.CanSyncCachedPhysical(group)||
        CanSyncRemoteRecipientClose(group)||!HasRetainedRecipient(group?.WorkstationData?.TileEntity)||!HasRetainedRecipient(group?.WorkstationData?.TileEntity,false)&&
        RebirthProductiveEarnedUiOwnerFile.CanSyncLocalCachedReceipt(group);
    internal static bool CanAdvanceKnownReceiptQueue(XUiC_RecipeStack card)=>
        RebirthWorkstationCompletionPipeline.CanAdvanceCachedPhysical(card)||
        !HasRetainedRecipient(card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity)||
        !HasRetainedRecipient(card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity,false)&&
        RebirthProductiveEarnedUiOwnerFile.CanAdvanceLocalCachedReceipt(card);
    internal static bool CanRotateKnownReceiptQueue(XUiC_CraftingQueue queue)=>
        !HasRetainedRecipient(queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    internal static bool CanTickKnownNativeReceipt(TileEntityWorkstation station)=>!HasRetainedRecipient(station);
}
