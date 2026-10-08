using System;
using System.IO;

// Session-owned wire comparison only. Not a native saved-state receipt.
internal sealed class RebirthStationNativeInputSnapshot
{
    private readonly byte[] image;
    private RebirthStationNativeInputSnapshot(byte[] bytes){image=bytes;}
    internal static bool TryCapture(ItemStack[] input,out RebirthStationNativeInputSnapshot snapshot)
    {
        snapshot=null;
        try
        {
            if(input==null||input.Length<1||input.Length>255)return false;
            using(var stream=new BoundedStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((byte)input.Length);
                foreach(var stack in input)
                {if(stack?.itemValue==null||stack.count<0)return false;stack.Write(writer);}
                writer.Flush();snapshot=new RebirthStationNativeInputSnapshot(stream.ToArray());return true;
            }
        }
        catch{return false;}
    }
    internal bool Matches(ItemStack[] inputs)
    {return TryCapture(inputs,out var current)&&MatchesSerializedInput(current.image);}
    internal bool MatchesSerializedInput(byte[] bytes)
    {if(bytes==null||bytes.Length!=image.Length)return false;for(int i=0;i<image.Length;i++)if(bytes[i]!=image[i])return false;return true;}
    private sealed class BoundedStream:MemoryStream
    {
        private const int MaximumBytes=256*1024;
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>MaximumBytes-count)throw new InvalidDataException("Station input snapshot exceeds bound");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=MaximumBytes)throw new InvalidDataException("Station input snapshot exceeds bound");base.WriteByte(value);}
    }
}