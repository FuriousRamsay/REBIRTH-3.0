using System;
using System.IO;

// Region format 1 (native RegionFileV2), chunk format 47 only. Caller supplies
// an immutable/exclusively owned snapshot and validated region-local table indices.
// This extracts bytes only; it proves neither station contents nor disk durability.
public static class RebirthStationRegionPayload
{
    public const int SectorBytes=4096;
    public const int MaximumPayloadBytes=255*SectorBytes-16;
    private static byte[] ReadExact(Stream source,int count)
    {
        var bytes=new byte[count];int offset=0;
        while(offset<count){int read=source.Read(bytes,offset,count-offset);if(read<=0)throw new EndOfStreamException();offset+=read;}
        return bytes;
    }
    // expected must be a detached copy of the successful writer's current payload.
    // Caller owns synchronization, world/chunk identity and snapshot generation.
    public static bool MatchesWrittenPayload(Stream source,int localX,int localZ,byte[] expected)
    {
        if(expected==null||expected.Length<8||expected.Length>MaximumPayloadBytes)return false;
        byte[] actual;
        if(!TryRead(source,localX,localZ,out actual)||actual.Length!=expected.Length)return false;
        for(int i=0;i<actual.Length;i++)if(actual[i]!=expected[i])return false;
        return true;
    }
    public static bool TryRead(Stream source,int localX,int localZ,out byte[] payload)
    {
        payload=null;
        if(source==null||!source.CanRead||!source.CanSeek||localX<0||localX>=32||localZ<0||localZ>=32)return false;
        long original;
        try{original=source.Position;}catch{return false;}
        byte[] result=null;bool valid=false;
        try
        {
            long length=source.Length;if(length<3L*SectorBytes)return false;
            source.Position=0;var header=ReadExact(source,4);
            if(header[0]!=(byte)'7'||header[1]!=(byte)'r'||header[2]!=(byte)'g'||header[3]!=1)return false;
            source.Position=SectorBytes+4L*(localX+localZ*32);var location=ReadExact(source,4);
            int sector=location[0]|location[1]<<8;int sectors=location[3];
            if(sector<3||sector>short.MaxValue||sectors==0)return false;
            long start=(long)sector*SectorBytes,end=start+(long)sectors*SectorBytes;
            if(end>length)return false;
            source.Position=start;var entry=ReadExact(source,16);
            uint size=(uint)(entry[0]|entry[1]<<8|entry[2]<<16|entry[3]<<24);
            if(size<8||size>MaximumPayloadBytes||size>(uint)(sectors*SectorBytes-16))return false;
            result=ReadExact(source,(int)size);
            if(result[0]!=(byte)'t'||result[1]!=(byte)'t'||result[2]!=(byte)'c'||result[3]!=0||
                result[4]!=47||result[5]!=0||result[6]!=0||result[7]!=0)return false;
            valid=true;
        }
        catch{valid=false;}
        finally{try{source.Position=original;}catch{valid=false;}}
        if(!valid)return false;payload=result;return true;
    }
}