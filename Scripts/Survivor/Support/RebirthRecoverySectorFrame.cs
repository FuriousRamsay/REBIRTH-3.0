using System;
using System.IO;

// Reads only a caller-owned detached/independently opened region stream.
public static class RebirthRecoverySectorFrame
{
    public static bool TryRead(Stream region,int localX,int localZ,out byte[] frame)
    {
        frame=null;
        if(region==null||!region.CanRead||!region.CanSeek||localX<0||localX>31||localZ<0||localZ>31)return false;
        try
        {
            long length=region.Length;region.Position=0;var header=new byte[4];if(!ReadExact(region,header)||header[0]!=55||header[1]!=114||header[2]!=103||header[3]>1)return false;
            int version=header[3];long entry=(version==0?4:4096)+4*(localX+32*localZ);
            region.Position=entry;var location=new byte[4];if(!ReadExact(region,location))return false;
            int sector=location[0]|location[1]<<8;int sectors=location[3];
            if(sector<(version==0?2:3)||sector>32767||sectors==0)return false;
            long start=sector*4096L+(version==0?4:0);int prefix=version==0?5:16;
            if(start+prefix>length)return false;
            region.Position=start;var dataHeader=new byte[prefix];if(!ReadExact(region,dataHeader))return false;
            int size=dataHeader[0]|dataHeader[1]<<8|dataHeader[2]<<16|dataHeader[3]<<24;
            if(size<9||size>RebirthRecoveryChunkPayload.MaximumCompressedBytes||size+(version==0?0:prefix)>sectors*4096L||start+prefix+size>length)return false;
            var captured=new byte[size];if(!ReadExact(region,captured))return false;
            region.Position=entry;var again=new byte[4];if(!ReadExact(region,again)||region.Length!=length)return false;
            for(int i=0;i<4;i++)if(again[i]!=location[i])return false;
            frame=captured;return true;
        }
        catch{return false;}
    }
    private static bool ReadExact(Stream input,byte[] bytes)
    {int offset=0;while(offset<bytes.Length){int count=input.Read(bytes,offset,bytes.Length-offset);if(count<=0)return false;offset+=count;}return true;}
}