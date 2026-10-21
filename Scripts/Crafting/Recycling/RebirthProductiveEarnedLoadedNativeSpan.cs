using System;
using System.IO;

// Actual returned native Persistency span only. Clocks are not authority here;
// all station state is compared through the exact installed native grammar.
internal static class RebirthProductiveEarnedLoadedNativeSpan
{
    internal static bool MatchesStart(Stream stream,byte[] native,int length)
    {
        if(stream==null||native==null||native.Length<30||native.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
            length!=native.Length+10||!stream.CanRead||!stream.CanSeek||stream.Position<0||stream.Position>stream.Length-length)return false;
        long start=stream.Position;
        try
        {
            if(stream.ReadByte()!=19||stream.ReadByte()!=0)return false;
            for(int i=0;i<12;i++)if(stream.ReadByte()!=native[i])return false;
            return true;
        }
        finally{stream.Position=start;}
    }
    internal static bool CaptureReturned(Stream stream,long start,byte[] destination,byte[] native,int nativeOffset,long maximumGraph,long maximumScratch)
    {
        if(stream==null||destination==null||native==null||!stream.CanRead||!stream.CanSeek||start<0||
            destination.Length!=native.Length+10||stream.Position!=start+destination.Length)return false;
        long end=stream.Position;
        try
        {
            stream.Position=start;if(!MatchesStart(stream,native,destination.Length))return false;
            int offset=0;
            while(offset<destination.Length){int read=stream.Read(destination,offset,destination.Length-offset);if(read<1)return false;offset+=read;}
            if(stream.Position!=end||!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(destination,out long graph,out long scratch)||
                graph>maximumGraph||scratch>maximumScratch||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(destination,out var image))return false;
            byte[] actual=RebirthProductiveEarnedNativeBody.Encode(image,0,out int actualOffset);
            return RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual,actualOffset,native,nativeOffset);
        }
        catch{return false;}
        finally{stream.Position=end;}
    }
}
