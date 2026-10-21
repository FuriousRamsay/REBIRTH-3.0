using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedUiOriginal
{
    // UIO1 is an owning-card original, never a REW2 transport request. Its
    // embedded earned row is only a bounded native mathematical proof.
    internal sealed class UiFrame
    {
        internal Guid Operation,Birth;internal string Owner,World,Creation,OwnerPurpose;internal int Deaths,Index;
        internal byte[][] Cards;internal byte[] Input,Output,Fuel,Tools,AfterTools,CardAfter,RowImage,OwnerBefore,Bag,Belt;
        internal RebirthProductiveEarnedRowProof.Frame Earned;
    }
    private static byte[] ReadUiBytes(BinaryReader reader,int maximum)
    {
        int length=reader.ReadInt32();
        if(length<0||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return bytes;
    }
    internal static UiFrame ReadUiArtifact(byte[] bytes)
    {
        // Caller holds the productive decoder reservation before hydration and
        // charges the returned detached frame before retaining it across ticks.
        if(bytes==null||bytes.Length<128||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
        {
            if(reader.ReadUInt32()!=0x314F4955u)throw new InvalidDataException();
            var result=new UiFrame{Operation=new Guid(reader.ReadBytes(16)),Birth=new Guid(reader.ReadBytes(16)),
                Owner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),World=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44),
                Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512),Deaths=reader.ReadInt32(),Index=reader.ReadInt32()};
            int count=reader.ReadInt32();
            if(result.Operation==Guid.Empty||result.Birth==Guid.Empty||result.Deaths<0||count<1||count>byte.MaxValue||result.Index<0||result.Index>=count||
                !RebirthOriginalActionScope.Validate(result.Owner,result.Creation)||Convert.FromBase64String(result.World).Length!=32)throw new InvalidDataException();
            result.Cards=new byte[count][];for(int i=0;i<count;i++)result.Cards[i]=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
            result.Input=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);result.Output=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
            result.Fuel=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);result.Tools=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
            result.AfterTools=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);result.CardAfter=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
            result.RowImage=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            result.OwnerBefore=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            result.Bag=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);result.Belt=ReadUiBytes(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
            result.OwnerPurpose=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,4096);
            if(result.OwnerBefore.Length<1)throw new InvalidDataException();
            if(stream.Position!=stream.Length)throw new InvalidDataException();
            result.Earned=RebirthProductiveEarnedRowProof.Decode(result.RowImage);var earned=result.Earned;
            if(earned.Operation!=result.Operation||earned.Birth!=result.Birth||earned.World!=result.World||earned.RowIndex!=result.Index||
                !earned.BeforeRow.SequenceEqual(result.Cards[result.Index])||!earned.AfterRow.SequenceEqual(result.CardAfter)||
                !earned.BeforeTools.SequenceEqual(result.Tools)||!earned.AfterTools.SequenceEqual(result.AfterTools)||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(earned,out _,out _,out _,out _)||
                !RebirthProductiveEarnedNativeBody.TryRead(earned.BeforeNative,earned.BeforeElapsedOffset,out var before,out _)||
                !RebirthProductiveEarnedNativeBody.TryRead(earned.AfterNative,earned.AfterElapsedOffset,out var after,out _)||
                before.Queue.Length!=count||after.Queue.Length!=count||result.Index!=count-1)throw new InvalidDataException();
            for(int i=0;i<count;i++)
                if(!RebirthProductiveEarnedRowProof.CaptureRow(before.Queue[i]).SequenceEqual(result.Cards[i])||
                    !RebirthProductiveEarnedRowProof.CaptureRow(after.Queue[i]).SequenceEqual(i==result.Index?result.CardAfter:result.Cards[i]))throw new InvalidDataException();
            if(!UiGridMatches(before.Input,result.Input)||!UiGridMatches(after.Input,result.Input)||
                !UiGridMatches(before.Output,result.Output)||!UiGridMatches(after.Output,result.Output)||
                !UiGridMatches(before.Fuel,result.Fuel)||!UiGridMatches(after.Fuel,result.Fuel))throw new InvalidDataException();
            return result;
        }
    }
    private static bool UiGridMatches(ItemStack[] slots,byte[] image)
        =>image.Length==0||RebirthProductiveEarnedArtifact.SameTools(slots,image);
}
