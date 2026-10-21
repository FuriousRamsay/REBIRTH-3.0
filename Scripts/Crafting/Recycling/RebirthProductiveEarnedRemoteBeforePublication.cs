using System;
using System.IO;
using System.Linq;
using System.Text;

// Historical server publication receipt only. It never grants work, payment or
// native setters by itself: the original header/artifact/live lease still apply.
internal static partial class RebirthProductiveEarnedRemoteHost
{
    private const int MaximumBeforeReceiptBytes=8192;
    private static bool TryRetainBeforePublication(Context context)
    {
        try
        {
            if(context==null||!context.Current())return false;
            string path=context.HeaderPath+".beforeSaved";
            if(!Safe(path)||File.Exists(path+".tmp"))return false;
            if(File.Exists(path))
            {
                if(!ValidateBeforeReceipt(context,ReadBeforeReceipt(path)))return false;
                // Historical receipt alone cannot establish a fresh rebound
                // context's actual native accepted/saved Before boundary.
                if(context.BeforePublished)return context.BeforeAccepted&&context.BeforeCurrent();
            }
            if(context.BeforeAccepted&&context.BeforeCurrent()&&context.BeforeWatch==null&&context.Memory!=null)
            {
                var original=RebirthProductiveEarnedRowProof.Decode(context.Image);
                if(RebirthRemoteResourceNativeBinding.TryResolve(context.World,RemoteResourceIdentity.Workstation(context.Station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,original.Birth.ToString("N"),out var binding))
                {
                    context.BeforeAllocation=context.Memory.TryObserveNative(original.BeforeNative,original.BeforeElapsedOffset);
                    if(context.BeforeAllocation!=null)
                    {
                        RebirthRemoteResourceNativePublication.Install();
                        context.BeforeWatch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,context.BeforeCurrent,context.Memory);
                        if(context.BeforeWatch==null){context.BeforeAllocation.Dispose();context.BeforeAllocation=null;}
                    }
                }
            }
            if(!context.BeforeAccepted||!context.BeforeCurrent()||context.BeforeWatch==null||
                !context.BeforeWatch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string tileDigest)||
                publication==null||offset<0||length<1||length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18||
                publication.Path==null||Encoding.UTF8.GetByteCount(publication.Path)>1024)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);
            byte[] receipt;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
            {
                writer.Write((byte)1);writer.Write(Digest(context.Header));writer.Write(Digest(context.Image));
                writer.Write(Digest(frame.BeforeNative));writer.Write(context.BeforeAcceptedElapsed);
                writer.Write(publication.Path);writer.Write(publication.PayloadDigest);writer.Write(publication.ChunkX);writer.Write(publication.ChunkZ);
                writer.Write(offset);writer.Write(length);writer.Write(tileDigest);writer.Flush();receipt=stream.ToArray();
            }
            if(receipt.Length>MaximumBeforeReceiptBytes||!ValidateBeforeReceipt(context,receipt)||!context.BeforeCurrent())return false;
            if(File.Exists(path))
            {if(!ValidateBeforeReceipt(context,ReadBeforeReceipt(path)))return false;}
            else
            {
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(receipt,0,receipt.Length);stream.Flush(true);}
                if(!ReadBeforeReceipt(path+".tmp").SequenceEqual(receipt)||!context.BeforeCurrent())return false;
                File.Move(path+".tmp",path);
                if(!ReadBeforeReceipt(path).SequenceEqual(receipt))return false;
            }
            if(!context.Current()||!context.BeforeCurrent())return false;
            context.BeforePublished=true;
            context.BeforeWatch?.Dispose();context.BeforeWatch=null;context.BeforeAllocation?.Dispose();context.BeforeAllocation=null;return true;
        }
        catch{return false;} // Unknown candidate never becomes permission.
    }
    private static bool ValidateBeforeReceipt(Context context,byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumBeforeReceiptBytes)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
        {
            if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(context.Header)||
                RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(context.Image))return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);
            if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(frame.BeforeNative))return false;
            reader.ReadUInt64(); // Historical accepted elapsed, never current clock authority.
            string nativePath=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024);
            string payload=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            int x=reader.ReadInt32(),z=reader.ReadInt32();long offset=reader.ReadInt64();int length=reader.ReadInt32();
            string tile=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            var position=context.Station.ToWorldPos();
            return stream.Position==stream.Length&&!string.IsNullOrEmpty(nativePath)&&Safe(nativePath)&&
                HashText(payload)&&HashText(tile)&&x==(position.x>>4)&&z==(position.z>>4)&&offset>=0&&
                length>0&&length<=RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18;
        }
    }
    private static bool HashText(string value)
    {return value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F');}
    private static byte[] ReadBeforeReceipt(string path)
    {
        if(!Safe(path))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>MaximumBeforeReceiptBytes)throw new InvalidDataException();
            var bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException();return bytes;
        }
    }
}
