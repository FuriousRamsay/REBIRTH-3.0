using System;
using System.IO;
using Noemax.GZip;

// Bounded decoding only. Caller still validates exact chunk/station structure.
public static class RebirthStationChunkInflate
{
    public const int MaximumDecodedBytes=32*1024*1024;
    public static bool TryDecode(byte[] payload,int maximumBytes,out byte[] decoded)
    {
        decoded=null;
        if(payload==null||payload.Length<=8||payload.Length>RebirthStationRegionPayload.MaximumPayloadBytes||
            maximumBytes<=0||maximumBytes>MaximumDecodedBytes||payload[0]!=(byte)'t'||payload[1]!=(byte)'t'||
            payload[2]!=(byte)'c'||payload[3]!=0||payload[4]!=47||payload[5]!=0||payload[6]!=0||payload[7]!=0)return false;
        try
        {
            // The stream wrapper accepts truncated input. Use the public core API
            // and require its completed-stream result plus complete input consumption.
            var inflater=new Noemax.GZip.Core.Inflate();
            if(inflater.inflateInit(-15)!=0)return false;
            inflater.next_in=payload;inflater.next_in_index=8;inflater.avail_in=payload.Length-8;
            using(var output=new MemoryStream())
            {
                var buffer=new byte[4096];
                while(true)
                {
                    int before=inflater.avail_in;
                    inflater.next_out=buffer;inflater.next_out_index=0;inflater.avail_out=buffer.Length;
                    int status=inflater.inflate(0);int produced=buffer.Length-inflater.avail_out;
                    if(produced<0||produced>buffer.Length||output.Length>maximumBytes-produced)return false;
                    if(produced>0)output.Write(buffer,0,produced);
                    if(status==1)
                    {
                        if(inflater.avail_in!=0||output.Length==0)return false;
                        decoded=output.ToArray();return true;
                    }
                    if(status!=0||(produced==0&&inflater.avail_in==before))return false;
                }
            }
        }
        catch{decoded=null;return false;}
    }
}