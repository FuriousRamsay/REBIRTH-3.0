// TOOLS ONLY. Evidence image; never session authorization, transport acknowledgement or debit proof.
using System;
public sealed class SeedPlacementConsumedEnvelopeReview
{
    public readonly SeedPlacementOriginalCommandReview Original;
    private readonly byte[] nativeBytes, intentBytes;
    private SeedPlacementConsumedEnvelopeReview(SeedPlacementOriginalCommandReview original,byte[] native,byte[] intent)
    {Original=original??throw new ArgumentNullException(nameof(original));nativeBytes=(byte[])native.Clone();intentBytes=(byte[])intent.Clone();}
    // Internal producer factory only; root registry must verify EXACT previously retained Original reference.
    internal static SeedPlacementConsumedEnvelopeReview Capture(SeedPlacementOriginalCommandReview original,byte[] native,byte[] intent)
    {if(native==null||intent==null)throw new ArgumentNullException();return new SeedPlacementConsumedEnvelopeReview(original,native,intent);}
    public byte[] CopyNativeBytes(){return (byte[])nativeBytes.Clone();}
    public byte[] CopyIntentBytes(){return (byte[])intentBytes.Clone();}
}
public interface ISeedPlacementRetainedCommandReview
{
    void MarkNativeInvocationStarted();
    bool TryBindConsumedEnvelope(SeedPlacementConsumedEnvelopeReview capture);
    // Nominal return ONLY: native SendToServer may return without queue acceptance.
    void ObserveNativeSendReturn(SeedPlacementConsumedEnvelopeReview capture);
    void Retire(bool envelopeConsumed,bool predictionMayHaveStarted);
}

