using System;
using System.IO;

// Immutable output/completion wire observation only; never a saved terminal proof or mutation permit.
internal sealed class RebirthStationTerminalSnapshot
{
    private const int MaximumBytes=256*1024;
    private readonly byte[] output,completion;
    private RebirthStationTerminalSnapshot(byte[] output,byte[] completion){this.output=output;this.completion=completion;}
    internal static bool TryCapture(TileEntityWorkstation station,out RebirthStationTerminalSnapshot snapshot)
    {
        snapshot=null;
        try
        {
            if(station?.Output==null||station.Output.Length<1||station.Output.Length>255||
                station.CraftCompleteList!=null&&station.CraftCompleteList.Count>short.MaxValue)return false;
            byte[] outputs,receipts;
            using(var stream=new BoundedStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((byte)station.Output.Length);
                foreach(var stack in station.Output)
                {if(stack?.itemValue==null||stack.count<0)return false;stack.Write(writer);}
                writer.Flush();outputs=stream.ToArray();
            }
            using(var stream=new BoundedStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);writer.Write((short)(station.CraftCompleteList?.Count??0));
                if(station.CraftCompleteList!=null)foreach(var data in station.CraftCompleteList)
                {if(data?.CraftedItemStack?.itemValue==null||data.CraftedItemStack.count<0)return false;data.Write(writer);}
                writer.Flush();receipts=stream.ToArray();
            }
            snapshot=new RebirthStationTerminalSnapshot(outputs,receipts);return true;
        }
        catch{return false;}
    }
    internal string Digest()
    {
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream))
        using(var sha=System.Security.Cryptography.SHA256.Create())
        {
            writer.Write(output.Length);writer.Write(output);writer.Write(completion.Length);writer.Write(completion);writer.Flush();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
        }
    }
    internal bool Matches(TileEntityWorkstation station)
    {return TryCapture(station,out var current)&&Same(output,current.output)&&Same(completion,current.completion);}
    internal bool MatchesSerializedTerminal(byte[] outputs,byte[] receipts)
    {return outputs!=null&&receipts!=null&&Same(output,outputs)&&Same(completion,receipts);}
    private static bool Same(byte[] left,byte[] right)
    {if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
    private sealed class BoundedStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>MaximumBytes-count)throw new InvalidDataException("Station terminal snapshot exceeds bound");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=MaximumBytes)throw new InvalidDataException("Station terminal snapshot exceeds bound");base.WriteByte(value);}
    }
}