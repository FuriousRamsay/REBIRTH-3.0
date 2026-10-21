using System;
using System.IO;

// Private successor WAL only. No native action, setter or recovery permission.
internal static class RebirthProductiveEarnedNativeSuccessorRecord
{
    internal const int MaximumBytes=2*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+2*RebirthProductiveEarnedRowProof.MaximumFrameBytes+256;
    internal static bool IsCurrent(string path,byte[] bytes,int maximum=MaximumBytes)
    {
        try{return maximum>0&&maximum<=16*1024*1024&&bytes!=null&&bytes.Length>0&&bytes.Length<=maximum&&!File.Exists(path+".tmp")&&Matches(path,bytes);}
        catch{return false;}
    }
    internal static bool WriteNew(string path,byte[] bytes,int maximum=MaximumBytes)
    {
        try
        {
            if(maximum<1||maximum>16*1024*1024||bytes==null||bytes.Length<1||bytes.Length>maximum||!RebirthProductiveEarnedArtifact.SafePath(path)||
                !RebirthProductiveEarnedArtifact.SafePath(path+".tmp")||File.Exists(path+".tmp"))return false;
            if(File.Exists(path))return Matches(path,bytes);
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(!Matches(path+".tmp",bytes))return false;
            File.Move(path+".tmp",path);return Matches(path,bytes);
        }
        catch{return false;}
    }
    private static bool Matches(string path,byte[] bytes)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(path))return false;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length!=bytes.Length)return false;var buffer=new byte[4096];int offset=0;
            while(offset<bytes.Length)
            {
                int read=stream.Read(buffer,0,Math.Min(buffer.Length,bytes.Length-offset));if(read<1)return false;
                for(int i=0;i<read;i++)if(buffer[i]!=bytes[offset+i])return false;offset+=read;
            }
            return stream.ReadByte()==-1&&RebirthProductiveEarnedArtifact.SafePath(path);
        }
    }
}
