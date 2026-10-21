using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    // Fresh positive native publication is required once per rebound/cold
    // context. Persisted receipt alone never sets AfterPublished on a new context.
    private static bool TryRetainAfterPublication(Context context)
    {
        try
        {
            if(context==null||!context.Accepted||!context.Current()||!context.AfterCurrent()||
                !RebirthProductiveEarnedCoordinator.TryNativeMarker(context.Station,out var nativeMarker)||!context.Artifact.MatchesMarker(nativeMarker))return false;
            string path=context.HeaderPath+".afterSaved";
            if(!Safe(path)||File.Exists(path+".tmp"))return false;
            if(context.AfterPublished&&RebirthProductiveEarnedSavedNativeSource.HasRegisteredEarnedOriginal(context.Artifact))return File.Exists(path)&&ValidateAfterReceipt(context,ReadBeforeReceipt(path));
            if(context.Observation==null)
            {
                // Retry observation after bounded allocation pressure, never the
                // native upload/work. Exact accepted After+marker above remain mandatory.
                var previous=uploading;uploading=context;
                try{context.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(context.Station,context.Artifact);}
                finally{uploading=previous;}
            }
            if(context.Observation?.Placed==null||!context.Observation.Placed.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string tileDigest)||
                publication==null||offset<0||length<1||length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18||
                publication.Path==null||Encoding.UTF8.GetByteCount(publication.Path)>1024)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);byte[] receipt;
            if(!RebirthProductiveEarnedSavedNativeSource.Publish(context.Artifact,context.Memory,context.Observation.Placed,frame.AfterNative,
                frame.AfterElapsedOffset,false,context.Observation.Current))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
            {
                writer.Write((byte)1);writer.Write(Digest(context.Header));writer.Write(Digest(context.Image));writer.Write(Digest(frame.AfterNative));
                writer.Write(Digest(nativeMarker));writer.Write(publication.Path);writer.Write(publication.PayloadDigest);writer.Write(publication.ChunkX);writer.Write(publication.ChunkZ);
                writer.Write(offset);writer.Write(length);writer.Write(tileDigest);writer.Flush();receipt=stream.ToArray();
            }
            if(receipt.Length>MaximumBeforeReceiptBytes||!ValidateAfterReceipt(context,receipt)||!context.AfterCurrent())return false;
            if(File.Exists(path))
            {
                // A new real publication can have another region payload after
                // unrelated chunk changes. Keep the immutable original receipt;
                // fresh descriptor above independently reproved this context.
                if(!ValidateAfterReceipt(context,ReadBeforeReceipt(path)))return false;
            }
            else
            {
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(receipt,0,receipt.Length);stream.Flush(true);}
                if(!ReadBeforeReceipt(path+".tmp").SequenceEqual(receipt)||!context.AfterCurrent())return false;
                File.Move(path+".tmp",path);if(!ReadBeforeReceipt(path).SequenceEqual(receipt))return false;
            }
            if(!context.Current()||!context.AfterCurrent())return false;
            context.AfterPublished=true;context.Observation.Dispose();context.Observation=null;return true;
        }
        catch{return false;}
    }
    private static bool ValidateAfterReceipt(Context context,byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumBeforeReceiptBytes)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
        {
            if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(context.Header)||
                RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(context.Image))return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);
            if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(frame.AfterNative)||
                RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(context.Artifact.Marker(frame)))return false;
            string nativePath=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024),payload=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            int x=reader.ReadInt32(),z=reader.ReadInt32();long offset=reader.ReadInt64();int length=reader.ReadInt32();
            string tile=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);var position=context.Station.ToWorldPos();
            return stream.Position==stream.Length&&!string.IsNullOrEmpty(nativePath)&&Safe(nativePath)&&HashText(payload)&&HashText(tile)&&
                x==(position.x>>4)&&z==(position.z>>4)&&offset>=0&&length>0&&length<=RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18;
        }
    }
}
