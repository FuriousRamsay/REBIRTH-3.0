using System;
// Typed same-source enrollment. Construction internal; matching is NOT host/peer authority.
public sealed class SeedOutgoingEnrollment
{
    internal readonly ConnectedSeedRecoveryOwner Owner;
    internal readonly ConnectedSeedRecoveryOwner.Pending Pending;
    public readonly SeedPlacementOriginalCommandReview Original;
    readonly byte[] intent,native;
    internal SeedOutgoingEnrollment(ConnectedSeedRecoveryOwner owner,ConnectedSeedRecoveryOwner.Pending pending,
        SeedPlacementOriginalCommandReview original,byte[] intentBytes,byte[] nativeBytes)
    {Owner=owner;Pending=pending;Original=original;intent=(byte[])intentBytes.Clone();native=(byte[])nativeBytes.Clone();}
        public ulong Epoch=>Original.Epoch;public ulong Nonce=>Original.Nonce;public int Actor=>Original.Actor;
    public System.Guid Creation=>Pending.CharacterId;public System.Guid ServerWorld=>Pending.WorldId;
    public World World=>Pending.Capture.World;public EntityPlayerLocal Player=>Pending.Capture.Player;public object WorldState=>Pending.WorldState;
    public bool IsRegisteredCurrent()=>Owner!=null&&Owner.MatchesRetainedOriginal(Original)&&System.Object.ReferenceEquals(Pending.Outgoing,this);
    public byte[] CopyIntent()=> (byte[])intent.Clone();public byte[] CopyNative()=> (byte[])native.Clone();
    internal bool MatchesFrame(SeedPlacementOutcomeFrameReview f)=>f!=null&&f.Creation==Pending.CharacterId&&f.ServerWorld==Pending.WorldId&&
        SeedPlacementOutcomeWireReview.SameCommand(Original,f.Outcome?.Original);
}
public static class SeedNativeAffectedImage
{
    // Canonical full affected image used by native snapshot provider AND independently authenticated ACK.
    public static string Encode(BlockValue b,sbyte density,long texture,bool tileAbsent,ulong incarnation,ulong revision)
    =>b.rawData+":"+b.damage+":"+density+":"+texture+":"+(tileAbsent?"absent":"plant")+":"+incarnation+":"+revision;
}
// Root receive transport constructs this link only after one-use actual-peer receipt pairing.
public sealed class SeedMatched
{
    internal readonly FarmingOutcomePeerReceipt PeerReceipt;
    public readonly SeedOutgoingEnrollment Registered;
    internal SeedMatched(FarmingOutcomePeerReceipt receipt,SeedOutgoingEnrollment enrollment)
    {PeerReceipt=receipt;Registered=enrollment;}
    public bool IsCurrent()=>PeerReceipt!=null&&PeerReceipt.IsConsumed&&PeerReceipt.IsAuthenticatedEnrollmentCurrent(Registered)&&Registered!=null&&Registered.IsRegisteredCurrent();
}
public sealed class SeedMatchedOutcomeEnrollment
{
    internal readonly SeedMatched PeerLink;internal bool Delivered;
    public readonly SeedOutgoingEnrollment Registered;
    public readonly SeedPlacementOutcomeFrameReview Frame;
    internal SeedMatchedOutcomeEnrollment(SeedMatched link,SeedOutgoingEnrollment registered,SeedPlacementOutcomeFrameReview frame)
    {PeerLink=link;Registered=registered;Frame=frame;}
}


