using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Pure exact-native DTO codec shared by purpose-specific original protocols.
// Decoding never grants target access, original payment or mutation authority.
internal static class RebirthOriginalNativeBlockChange
{
    internal const int MaximumChanges=16,MaximumBytes=65536;
    internal static bool TryEncode(IReadOnlyList<BlockChangeInfo> changes,out string image)
    {
        image=null;
        try
        {
            if(!ThreadManager.IsMainThread()||changes==null||changes.Count<1||changes.Count>MaximumChanges||changes.Any(change=>change==null))return false;
            using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((byte)1);writer.Write((byte)changes.Count);
                foreach(var change in changes)change.Write(writer);writer.Flush();
                if(stream.Length>MaximumBytes)return false;image=Convert.ToBase64String(stream.ToArray());return true;
            }
        }
        catch{image=null;return false;}
    }
    internal static bool TryDecode(string image,out List<BlockChangeInfo> changes)
    {
        changes=null;
        try
        {
            if(!ThreadManager.IsMainThread()||image==null||image.Length>((MaximumBytes+2)/3)*4)return false;
            byte[] bytes=Convert.FromBase64String(image);if(bytes.Length>MaximumBytes||Convert.ToBase64String(bytes)!=image)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=1)return false;int count=reader.ReadByte();if(count<1||count>MaximumChanges)return false;
                var result=new List<BlockChangeInfo>(count);
                for(int index=0;index<count;index++){var change=new BlockChangeInfo();change.Read(reader);result.Add(change);}
                if(stream.Position!=stream.Length||!TryEncode(result,out string canonical)||canonical!=image)return false;
                changes=result;return true;
            }
        }
        catch{changes=null;return false;}
    }
}
