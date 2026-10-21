using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiOriginal
{
    internal static bool ValidateRemoteBeforeImage(byte[] bytes,EntityPlayer actor,TileEntityWorkstation station,Guid operation,Guid birth,string owner,
        string creation,int index,byte[] target,int targetOffset)
    {
        try
        {
            if(bytes==null||bytes.Length<128||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedNativeBody.TryRead(target,targetOffset,out var projected,out _))return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31464255u||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,16)!="before"||
                    new Guid(reader.ReadBytes(16))!=operation||new Guid(reader.ReadBytes(16))!=birth||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=owner||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44)!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation||reader.ReadInt32()!=actor.Died)return false;
                reader.ReadInt64();if(reader.ReadInt32()!=index||!new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()).Equals(station.ToWorldPos()))return false;
                int count=reader.ReadInt32();if(count<1||count>byte.MaxValue||count!=projected.Queue.Length)return false;
                for(int i=0;i<count;i++)
                {
                    string definition=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);byte[] row=BeforeField(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                    if(!row.SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(projected.Queue[i]))||
                        definition!=(projected.Queue[i].Recipe==null?"empty":DefinitionHash(projected.Queue[i].Recipe)))return false;
                }
                var grids=new byte[4][];for(int i=0;i<grids.Length;i++)grids[i]=BeforeField(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                int beforeOffset=reader.ReadInt32();byte[] before=BeforeField(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int afterOffset=reader.ReadInt32();byte[] after=BeforeField(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                if(stream.Position!=stream.Length||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(after,afterOffset,target,targetOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var original,out ulong elapsed))return false;
                var actual=new[]{projected.Input,projected.Output,projected.Fuel,projected.Tools};
                for(int i=0;i<grids.Length;i++)if(grids[i].Length>0&&!grids[i].SequenceEqual(Grid(actual[i])))return false;
                if(grids[0].Length>0)original.Input=projected.Input;if(grids[1].Length>0)original.Output=projected.Output;
                if(grids[2].Length>0)original.Fuel=projected.Fuel;if(grids[3].Length>0)original.Tools=projected.Tools;original.Queue=projected.Queue;
                return RebirthProductiveEarnedNativeBody.Encode(original,elapsed,out int derived).SequenceEqual(after)&&derived==afterOffset;
            }
        }
        catch{return false;}
    }
    private static byte[] BeforeField(BinaryReader reader,int maximum)
    {
        int length=reader.ReadInt32();if(length<0||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return bytes;
    }
}
