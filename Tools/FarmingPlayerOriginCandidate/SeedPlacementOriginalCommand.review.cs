// TOOLS ONLY. Original command image; never an authorization or inventory mutation receipt.
using System;
using System.IO;
public sealed class SeedPlacementOriginalCommandReview
{
    public readonly ulong Epoch, Nonce;
    public readonly int Actor, Slot, OriginalCount;
    public readonly byte Flags;
    public readonly sbyte Density;
    public readonly long Texture;
    public readonly Vector3i Position;
    public readonly BlockValue Target, ExpectedOld;
    private readonly byte[] seed;
    private SeedPlacementOriginalCommandReview(ulong epoch,ulong nonce,int actor,int count,
        SeedPlacementIntentCodecReview.Intent intent)
    {
        Epoch=epoch;Nonce=nonce;Actor=actor;Slot=intent.Slot;OriginalCount=count;
        Position=intent.Position;Target=intent.Target;ExpectedOld=intent.ExpectedOld;
        Flags=intent.Flags;Density=(Flags&4)!=0?intent.Density:(sbyte)0;Texture=(Flags&32)!=0?intent.Texture:0;
        seed=(byte[])intent.Seed.Clone();
    }
    public byte[] CopySeedIdentity(){return (byte[])seed.Clone();}
    public bool MatchesSeedIdentity(byte[] candidate)
    {
        if(candidate==null||candidate.Length!=seed.Length)return false;
        for(int i=0;i<seed.Length;i++)if(candidate[i]!=seed[i])return false;
        return true;
    }
    // A capture factory validates shape only. Callers MUST establish session/sender/slot custody separately.
    public static SeedPlacementOriginalCommandReview Capture(ulong epoch,ulong nonce,int actor,int originalCount,
        SeedPlacementIntentCodecReview.Intent intent)
    {
        if(epoch==0||nonce==0||actor<=0||originalCount<=0||intent.Slot<0||intent.Seed==null||
            intent.Seed.Length==0||intent.Seed.Length>SeedPlacementIntentCodecReview.MaxSeedBytes)
            throw new InvalidDataException("Invalid original seed command image.");
        return new SeedPlacementOriginalCommandReview(epoch,nonce,actor,originalCount,intent);
    }
}
// Proposed outcome ABI; no public outcome factory and no inventory debit claim.
// Wire transport must authenticate host/session and correlate the entire original command.
public enum SeedPlacementHostDispositionReview : byte
{
    RejectedBeforeExecution=0,
    Committed=1,
    NativeExecutionUncertain=2
}
public sealed class SeedPlacementHostOutcomeReview
{
    public readonly SeedPlacementOriginalCommandReview Original;
    public readonly SeedPlacementHostDispositionReview Disposition;
    public readonly BlockValue ObservedBlock;
    public readonly ulong PlantIncarnation, StateRevision;
    internal SeedPlacementHostOutcomeReview(SeedPlacementOriginalCommandReview original,
        SeedPlacementHostDispositionReview disposition,BlockValue observed,ulong incarnation,ulong revision)
    {
        if(original==null)throw new ArgumentNullException(nameof(original));
        if(disposition<SeedPlacementHostDispositionReview.RejectedBeforeExecution||
            disposition>SeedPlacementHostDispositionReview.NativeExecutionUncertain)
            throw new ArgumentOutOfRangeException(nameof(disposition));
        if(disposition==SeedPlacementHostDispositionReview.Committed&&
            (incarnation==0||revision==0||observed.rawData!=original.Target.rawData||observed.damage!=original.Target.damage))
            throw new InvalidDataException("Committed origin requires an exact observed target identity.");
        Original=original;Disposition=disposition;ObservedBlock=observed;
        PlantIncarnation=incarnation;StateRevision=revision;
    }
}