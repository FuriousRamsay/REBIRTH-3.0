using System;public interface ISeedPlacementReviewAdmission : IDisposable
{
    void CompleteObservedCommit();
}
public interface ISeedPlacementReviewOwner
{
    bool TryAdmit(ClientInfo sender, World world, GameManager callbacks, ulong epoch, ulong nonce,
        SeedPlacementIntentCodecReview.Intent intent, NetPackageSetBlock original, out ISeedPlacementReviewAdmission admission);
    void ReleaseOrRetainNativeAndReconcile(ClientInfo sender, World world, ulong epoch, ulong nonce,
        SeedPlacementIntentCodecReview.Intent? intent, NetPackageSetBlock original, ISeedPlacementReviewAdmission admission);
}



