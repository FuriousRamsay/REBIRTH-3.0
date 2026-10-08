internal interface ISeedPlacementHostPolicyReview
{
    bool Validate(ClientInfo sender,World world,GameManager callbacks,
        SeedPlacementSessionAuthority.Reservation reservation,SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock native);
}
