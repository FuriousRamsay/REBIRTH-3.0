using System;
using System.IO;

// Detached native ttc frame decoder. Never opens, repairs or removes region files.
public static class RebirthRecoveryChunkPayload
{
    public const int MaximumCompressedBytes=16*1024*1024;
    public const int MaximumExpandedBytes=64*1024*1024;
    public static bool TryDecodeNative(byte[] frame,out uint version,out byte[] payload)
        =>TryDecode(frame,input=>new Noemax.GZip.DeflateInputStream(input,leaveOpen:true),out version,out payload);
    public static bool TryDecode(byte[] frame,Func<Stream,Stream> openInflater,out uint version,out byte[] payload)
    {
        version=0;payload=null;
        if(frame==null||frame.Length<9||frame.Length>MaximumCompressedBytes||openInflater==null||
            frame[0]!=116||frame[1]!=116||frame[2]!=99||frame[3]!=0)return false;
        uint candidate=(uint)(frame[4]|frame[5]<<8|frame[6]<<16|frame[7]<<24);
        if(candidate==0)return false;
        try
        {
            // The caller's frame cannot be mutated or retained by the inflater.
            var detached=(byte[])frame.Clone();
            using(var input=new MemoryStream(detached,8,detached.Length-8,false))
            using(var inflated=openInflater(input))
            using(var output=new MemoryStream())
            {
                if(inflated==null||!inflated.CanRead)return false;
                var buffer=new byte[8192];int count;
                while((count=inflated.Read(buffer,0,buffer.Length))>0)
                {
                    if(output.Length+count>MaximumExpandedBytes)return false;
                    output.Write(buffer,0,count);
                }
                if(output.Length==0)return false;
                var result=output.ToArray();version=candidate;payload=result;return true;
            }
        }
        catch{return false;}
    }
}