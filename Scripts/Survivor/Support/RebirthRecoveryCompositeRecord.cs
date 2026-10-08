using System;
using System.Collections.Generic;
using System.IO;

// Current composite18/base19 envelope and module boundaries, no feature callbacks.
public static class RebirthRecoveryCompositeRecord
{
    public static bool TrySkip(byte[] payload,int offset,Func<int,bool> validCompositeBlock,out int endOffset)
    {
        endOffset=0;if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length||validCompositeBlock==null)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var r=new BinaryReader(stream))
        {
            stream.Position=offset;var c=new RebirthRecoveryForeignRecord.Cursor(r,1,id=>-1);
            if(r.ReadUInt16()!=19)return false;c.Skip(20);if(r.ReadUInt16()!=18)return false;
            long start=stream.Position;uint size=r.ReadUInt32();if(size<10||size>4*1024*1024||size>stream.Length-start)return false;long end=start+size;
            if(!validCompositeBlock(r.ReadInt32()))return false;
            if(c.Flag()){c.Skip(1);c.String();c.String();}if(stream.Position>=end)return false;
            int features=r.ReadByte();var hashes=new HashSet<int>();
            for(int i=0;i<features;i++)
            {
                if(end-stream.Position<8||!hashes.Add(r.ReadInt32()))return false;
                long featureStart=stream.Position;uint length=r.ReadUInt32();if(length<4||length>end-featureStart)return false;c.Skip(checked((int)length)-4);
            }
            if(stream.Position!=end)return false;endOffset=checked((int)end);return true;
        }}catch{return false;}
    }
}