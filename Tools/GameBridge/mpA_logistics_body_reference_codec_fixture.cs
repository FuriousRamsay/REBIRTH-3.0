using System;
using System.Xml.Linq;

// PREPARED NOT RUN. Pure codec cases need an actual captured canonical native
// original; this fixture creates no authority, Prepared record or native spawn.
internal static class mpA_logistics_body_reference_codec_fixture
{
    internal static void RequireExactRoundtrip(string actualCanonical,int oldOwner,int newOwner,string nonce,string binding)
    {
        var witness=RebirthLogisticsBodyReferenceWitness.Create(actualCanonical,oldOwner,newOwner,nonce,binding);
        var xml=RebirthLogisticsBodyReferenceWitness.Write(witness);
        var decoded=RebirthLogisticsBodyReferenceWitness.Read(xml);
        if(decoded.Original!=actualCanonical||decoded.Projected!=witness.Projected||decoded.HistoricalOwner!=oldOwner||decoded.NativeOwner!=newOwner)
            throw new Exception("Reference witness lost immutable original or projected bytes.");
    }
    internal static void RequireTamperRejected(XElement actualWitness)
    {
        var changed=new XElement(actualWitness);changed.SetAttributeValue("nativeOwner",(int)changed.Attribute("nativeOwner")+1);
        bool rejected=false;try{RebirthLogisticsBodyReferenceWitness.Read(changed);}catch(System.IO.InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Owner reference changed without exact projected bytes.");
    }
    internal static void RequireActualNativeMapping(EntityPlayer actual,ClientInfo sender,string owner,bool expected)
    {
        bool result=RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actual,sender,owner,out int entity);
        if(result!=expected||result&&entity!=actual.entityId)throw new Exception("Native persistent inverse mapping mismatch.");
    }
    internal static void RequireLegacyPermitClosedAfterProjection(RebirthLogisticsTransferJournal.Record actual,
        ClientInfo sender,byte[] canonical,string nonce,NetPackageRequestToSpawnEntity packet)
    {
        if(!RebirthLogisticsBodyReferenceJournal.HasCandidate(actual))throw new Exception("Actual retained projection candidate required.");
        using(var permit=RebirthLogisticsNativeInvocationJournal.PermitPrepared(actual,sender,canonical,nonce,packet))
            if(permit!=null)throw new Exception("Legacy original packet bypassed frozen projection.");
    }
    internal static void RequireProjectedPermitRejectsWrongNonce(RebirthLogisticsTransferJournal.Record actual,
        ClientInfo sender,string wrongNonce,NetPackageRequestToSpawnEntity packet)
    {
        using(var permit=RebirthLogisticsNativeInvocationJournal.PermitProjectedPrepared(actual,sender,wrongNonce,packet))
            if(permit!=null)throw new Exception("Projection accepted a nonce outside retained frozen original.");
    }
    internal static void RequireClaimedBlobLengthRejected(string actualCanonical,int oldOwner,int newOwner,string nonce,string binding)
    {
        byte[] raw=Convert.FromBase64String(actualCanonical);
        if(raw.Length<58)throw new Exception("Actual v38 Backpack canonical image required.");
        Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue),0,raw,54,4);
        bool rejected=false;
        try{RebirthLogisticsBodyReferenceWitness.Create(Convert.ToBase64String(raw),oldOwner,newOwner,nonce,binding);}
        catch(System.IO.InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Native claimed blob length was not rejected before decoder.");
    }
    internal static void RequireTruncatedNativeRejected(string actualCanonical,int oldOwner,int newOwner,string nonce,string binding)
    {
        byte[] original=Convert.FromBase64String(actualCanonical);
        // Boundary cuts cover header, pre-blob length, and final native tail.
        // No fixture constructs an inventory or invokes a body request.
        foreach(int length in new[]{1,9,53,57,original.Length-1})
        {
            if(length<1||length>=original.Length)continue;
            var cut=new byte[length];Buffer.BlockCopy(original,0,cut,0,length);
            bool rejected=false;
            try{RebirthLogisticsBodyReferenceWitness.Create(Convert.ToBase64String(cut),oldOwner,newOwner,nonce,binding);}
            catch(System.IO.InvalidDataException){rejected=true;}
            catch(System.IO.EndOfStreamException){rejected=true;}
            if(!rejected)throw new Exception("Truncated native body evidence reached projection.");
        }
    }
    // Cases for harness: same ref refuses; different bag/pose/class refuses on
    // retained original comparison; noncanonical base64/trailing native bytes,
    // unknown XML attrs/elements, over-ushort rich entityData, reused old ID.
    // This codec never decides Prepared->Attempted or mapping authentication.
}
