using System;
using System.IO;
using System.Text;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static bool TryAdoptUiBefore(EntityPlayer actor,ClientInfo peer,Guid operation,Guid nonce,string rowHash,string uiHash,
        RebirthProductiveEarnedUiQueueOwner.Witness earlier,RebirthProductiveEarnedUiRemoteBefore.Publication publication,
        RebirthProductiveEarnedObservationBudget.Decoder decoder)
    {
        try
        {
            if(decoder?.IsCurrent!=true||publication==null||!pending.TryGetValue(operation,out var context)||!context.Current()||context.Nonce!=nonce||
                Digest(context.Image)!=rowHash||!ReferenceEquals(context.Actor,actor)||!ReferenceEquals(context.Sender,peer))return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);
            if(!publication.Matches(actor,peer,context.Station,operation,uiHash,earlier,frame.BeforeNative,frame.BeforeElapsedOffset,decoder)||!context.BeforeCurrent())return false;
            if(context.BeforePublished)return context.BeforeAccepted&&ValidateBeforeReceipt(context,ReadBeforeReceipt(context.HeaderPath+".beforeSaved"));
            var native=publication.Native;
            if(native==null||publication.TileOffset<0||publication.TileLength<1||publication.TileLength>RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18||
                native.Path==null||Encoding.UTF8.GetByteCount(native.Path)>1024)return false;
            byte[] receipt;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
            {
                writer.Write((byte)1);writer.Write(Digest(context.Header));writer.Write(Digest(context.Image));writer.Write(Digest(frame.BeforeNative));
                writer.Write(publication.AcceptedElapsed);writer.Write(native.Path);writer.Write(native.PayloadDigest);writer.Write(native.ChunkX);writer.Write(native.ChunkZ);
                writer.Write(publication.TileOffset);writer.Write(publication.TileLength);writer.Write(publication.TileDigest);writer.Flush();receipt=stream.ToArray();
            }
            if(receipt.Length>MaximumBeforeReceiptBytes||!ValidateBeforeReceipt(context,receipt)||!context.BeforeCurrent()||
                !RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(context.HeaderPath+".beforeSaved",receipt)||!context.BeforeCurrent())return false;
            // The typed issuer already observed ONE actual native accepted and
            // saved Before. Never manufacture a second native upload/save here.
            context.BeforeAccepted=true;context.BeforeAcceptedElapsed=publication.AcceptedElapsed;context.BeforePublished=true;return true;
        }
        catch{return false;}
    }
}
