// Reuses the native Pack Up receipt at the existing authoritative transfer
// boundaries. A repeated acknowledgement never restores the payload twice.
internal static class RebirthCamperStoragePackedTransfer
{
    internal static void Prepare(RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        if(after.Storage.Capacity<before.Storage.Capacity)
            RebirthContainerPackUp.PublishDetached(after.Cursor);
    }
    internal static void Applied(RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        if(after.Storage.Capacity>before.Storage.Capacity)
            RebirthContainerPackUp.ConsumeDetached(before.Cursor);
    }
}
