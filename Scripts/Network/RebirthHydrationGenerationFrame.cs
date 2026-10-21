using System;
using System.IO;
using System.Text;

internal sealed class RebirthHydrationGenerationFrame
{
    internal const int Size=113;
    internal readonly int PlayerId,Deaths;internal readonly Guid World;internal readonly string Generation,Owner;internal readonly long Sequence;
    private RebirthHydrationGenerationFrame(int player,Guid world,string generation,string owner,int deaths,long sequence)
    {PlayerId=player;World=world;Generation=generation;Owner=owner;Deaths=deaths;Sequence=sequence;}
    internal static bool TryCreate(int player,Guid world,string generation,string owner,int deaths,long sequence,out RebirthHydrationGenerationFrame frame)
    {
        frame=null;
        if(player<=0||world==Guid.Empty||!RebirthHydrationGeneration.IsCanonical(generation)||owner==null||owner.Length!=64||deaths<0||sequence<=0)return false;
        foreach(char value in owner)if(!(value>='0'&&value<='9'||value>='a'&&value<='f'))return false;
        frame=new RebirthHydrationGenerationFrame(player,world,generation,owner,deaths,sequence);return true;
    }
    internal byte[] Encode()
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8))
        {writer.Write((byte)1);writer.Write(PlayerId);writer.Write(World.ToByteArray());writer.Write(Guid.ParseExact(Generation,"N").ToByteArray());writer.Write(Encoding.ASCII.GetBytes(Owner));writer.Write(Deaths);writer.Write(Sequence);writer.Flush();return stream.ToArray();}
    }
    internal static bool TryDecode(byte[] bytes,out RebirthHydrationGenerationFrame frame)
    {
        frame=null;
        try
        {
            if(bytes==null||bytes.Length!=Size)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(reader.ReadByte()!=1)return false;int player=reader.ReadInt32();var world=new Guid(reader.ReadBytes(16));var generation=new Guid(reader.ReadBytes(16));
                byte[] owner=reader.ReadBytes(64);foreach(byte value in owner)if(value>127)return false;
                int deaths=reader.ReadInt32();long sequence=reader.ReadInt64();
                return stream.Position==stream.Length&&TryCreate(player,world,generation.ToString("N"),Encoding.ASCII.GetString(owner),deaths,sequence,out frame);
            }
        }
        catch{return false;}
    }
}
