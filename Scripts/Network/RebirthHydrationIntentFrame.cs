using System;
using System.IO;
using System.Text;

// Bounded user request description only, not inventory/source authority.
internal sealed class RebirthHydrationIntentFrame
{
    internal const int MaximumBytes=512;
    internal readonly Guid Operation;internal readonly string Creation,Locks;internal readonly int Deaths,Index;internal readonly bool Bag,Returning;
    private RebirthHydrationIntentFrame(Guid op,string creation,int deaths,bool bag,int index,bool returning,string locks)
    {Operation=op;Creation=creation;Deaths=deaths;Bag=bag;Index=index;Returning=returning;Locks=locks;}
    internal static bool TryCreate(Guid op,string creation,int deaths,bool bag,int index,bool returning,string locks,out RebirthHydrationIntentFrame frame)
    {
        frame=null;
        if(op==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(creation,out var canonical)||canonical!=creation||deaths<0||
            locks==null||locks.Length<44||locks.Length>169||(returning?!bag||index!=-1:index<0||index>=(bag?locks.Length:18)))return false;
        foreach(char value in locks)if(value!='0'&&value!='1')return false;
        frame=new RebirthHydrationIntentFrame(op,creation,deaths,bag,index,returning,locks);return true;
    }
    internal byte[] Encode()
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8))
        {
            writer.Write((byte)1);writer.Write(Operation.ToByteArray());writer.Write((byte)Creation.Length);writer.Write(Encoding.ASCII.GetBytes(Creation));writer.Write(Deaths);
            writer.Write((byte)(Bag?1:0));writer.Write((short)Index);writer.Write((byte)(Returning?1:0));writer.Write((byte)Locks.Length);writer.Write(Encoding.ASCII.GetBytes(Locks));writer.Flush();return stream.ToArray();
        }
    }
    internal static bool TryDecode(byte[] bytes,out RebirthHydrationIntentFrame frame)
    {
        frame=null;
        try
        {
            if(bytes==null||bytes.Length<1||bytes.Length>MaximumBytes)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(reader.ReadByte()!=1)return false;byte[] guid=reader.ReadBytes(16);if(guid.Length!=16)return false;
                int length=reader.ReadByte();if(length<1||length>71)return false;byte[] text=reader.ReadBytes(length);if(text.Length!=length)return false;
                foreach(byte value in text)if(value>127)return false;
                string creation=Encoding.ASCII.GetString(text);int deaths=reader.ReadInt32();byte bag=reader.ReadByte();int index=reader.ReadInt16();byte returning=reader.ReadByte();
                int slots=reader.ReadByte();if(slots<44||slots>169||bag>1||returning>1)return false;
                byte[] bits=reader.ReadBytes(slots);if(bits.Length!=slots||stream.Position!=stream.Length)return false;
                foreach(byte value in bits)if(value!='0'&&value!='1')return false;
                return TryCreate(new Guid(guid),creation,deaths,bag==1,index,returning==1,Encoding.ASCII.GetString(bits),out frame);
            }
        }
        catch{return false;}
    }
}
