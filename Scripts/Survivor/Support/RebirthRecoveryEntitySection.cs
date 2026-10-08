using System;
using System.IO;

// Structural prefix only; never instantiates blocks, entities or tile entities.
public static class RebirthRecoveryEntitySection
{
    public static bool TryLocate(byte[] payload,uint version,out int x,out int y,out int z,out int count,out int offset)
    {
        x=y=z=count=offset=0;
        if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||version<32||version>46)return false;
        try
        {
            using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream))
            {
                int cx=reader.ReadInt32(),cy=reader.ReadInt32(),cz=reader.ReadInt32();Skip(stream,8);
                for(int i=0;i<64;i++)if(Flag(reader)){Skip(stream,Flag(reader)?1024:1);if(Flag(reader))Skip(stream,3072);}
                Channel(reader,stream,1);Skip(stream,512);if(version>41)Skip(stream,32);Skip(stream,1794);
                int custom=reader.ReadUInt16();
                for(int i=0;i<custom;i++){int length=StringLength(reader);if(length>4096)throw new InvalidDataException();Skip(stream,length+9);int bytes=reader.ReadUInt16();Skip(stream,bytes);}
                Skip(stream,768);Channel(reader,stream,1);Channel(reader,stream,1);
                if(version>=33&&version<36){Channel(reader,stream,1);Channel(reader,stream,1);}
                if(version>=36)Channel(reader,stream,2);
                if(version>=35)Channel(reader,stream,6);
                if(version>=46)Channel(reader,stream,2);
                Flag(reader);int entities=reader.ReadInt32();if(entities<0||entities>4096)throw new InvalidDataException();
                x=cx;y=cy;z=cz;count=entities;offset=checked((int)stream.Position);return true;
            }
        }
        catch{return false;}
    }
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Channel(BinaryReader reader,Stream stream,int bytes){for(int i=0;i<64;i++)Skip(stream,Flag(reader)?bytes:1024*bytes);}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position+count>stream.Length)throw new EndOfStreamException();stream.Position+=count;}
    private static int StringLength(BinaryReader reader){uint value=0;for(int i=0;i<5;i++){byte b=reader.ReadByte();if(i==4&&(b&240)!=0)throw new InvalidDataException();value|=(uint)(b&127)<<(7*i);if((b&128)==0)return checked((int)value);}throw new InvalidDataException();}
}