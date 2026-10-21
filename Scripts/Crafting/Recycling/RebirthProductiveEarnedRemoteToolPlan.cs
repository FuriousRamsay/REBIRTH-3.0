using System;
using System.IO;
using System.Linq;
using System.Text;

// A description of a returned native inventory gesture. Native accepted owner
// files and the separately accepted/saved workstation purpose supply authority.
internal static class RebirthProductiveEarnedRemoteToolPlan
{
    internal sealed class Description
    {
        internal Guid Operation,Birth,Nonce,Gesture;internal string Digest;
        internal string BeforeBag,BeforeBelt,AfterBag,AfterBelt,Tools;
        internal int BagCount,BeltCount,ToolCount,OwnedBelt,BeforeOffset,AfterOffset;internal byte[] Before,After;
    }
    internal static bool TryCapture(EntityPlayerLocal actor,XUiC_WorkstationToolGrid grid,out string bag,out string belt,out string tools)
    {
        bag=belt=tools=null;
        return actor?.bag?.ItemGrid?.items!=null&&actor.inventory?.ItemGrid?.items!=null&&grid?.GetSlots()!=null&&
            actor.bag.ItemGrid.items.Length<=169&&actor.inventory.ItemGrid.items.Length<=20&&grid.GetSlots().Length<=255&&
            grid.xui?.DragAndDropWindow?.CurrentStack?.IsEmpty()==true&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(actor.bag.ItemGrid.items,262144,349528,out bag)&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(actor.inventory.ItemGrid.items,262144,349528,out belt)&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(grid.GetSlots(),262144,349528,out tools);
    }
    internal static byte[] Encode(Description value)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31545252u);writer.Write(value.Operation.ToByteArray());writer.Write(value.Birth.ToByteArray());writer.Write(value.Nonce.ToByteArray());writer.Write(value.Gesture.ToByteArray());writer.Write(value.Digest);
            writer.Write(value.BagCount);writer.Write(value.BeltCount);writer.Write(value.ToolCount);writer.Write(value.OwnedBelt);writer.Write(value.BeforeBag);writer.Write(value.BeforeBelt);writer.Write(value.AfterBag);writer.Write(value.AfterBelt);writer.Write(value.Tools);
            writer.Write(value.BeforeOffset);writer.Write(value.Before.Length);writer.Write(value.Before);writer.Write(value.AfterOffset);writer.Write(value.After.Length);writer.Write(value.After);writer.Flush();
            if(stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryRead(byte[] bytes,Guid operation,Guid birth,Guid nonce,string digest,byte[] terminal,int terminalOffset,out Description plan)
    {
        plan=null;
        try
        {
            if(bytes==null||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                var value=new Description();if(reader.ReadUInt32()!=0x31545252u)return false;
                value.Operation=new Guid(reader.ReadBytes(16));value.Birth=new Guid(reader.ReadBytes(16));value.Nonce=new Guid(reader.ReadBytes(16));value.Gesture=new Guid(reader.ReadBytes(16));value.Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                value.BagCount=reader.ReadInt32();value.BeltCount=reader.ReadInt32();value.ToolCount=reader.ReadInt32();value.OwnedBelt=reader.ReadInt32();
                value.BeforeBag=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,349528);value.BeforeBelt=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,349528);
                value.AfterBag=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,349528);value.AfterBelt=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,349528);value.Tools=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,349528);
                value.BeforeOffset=reader.ReadInt32();value.Before=Read(reader);value.AfterOffset=reader.ReadInt32();value.After=Read(reader);
                if(stream.Position!=stream.Length||value.Operation!=operation||value.Birth!=birth||value.Nonce!=nonce||nonce==Guid.Empty||value.Gesture==Guid.Empty||value.Digest!=digest||
                    value.BagCount<1||value.BagCount>169||value.BeltCount<1||value.BeltCount>20||value.OwnedBelt<1||value.OwnedBelt>value.BeltCount||value.ToolCount<1||value.ToolCount>255||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Before,value.BeforeOffset,terminal,terminalOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(value.Before,value.BeforeOffset,out var before,out _)||
                    !RebirthProductiveEarnedNativeBody.TryRead(value.After,value.AfterOffset,out var after,out ulong elapsed)||before.Tools.Length!=value.ToolCount||
                    before.Queue.Length<1||!RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(value.Before,value.BeforeOffset,value.After,value.AfterOffset,before.Queue.Length-1,before.Queue.Last().CraftingTimeLeft))return false;
                if(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(value.BeforeBag,value.BagCount,262144,349528,out var beforeBag)||
                    !RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(value.BeforeBelt,value.BeltCount,262144,349528,out var beforeBelt)||
                    !RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(value.AfterBag,value.BagCount,262144,349528,out var afterBag)||
                    !RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(value.AfterBelt,value.BeltCount,262144,349528,out var afterBelt)||
                    !RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(value.Tools,value.ToolCount,262144,349528,out var tools)||
                    !RebirthProductiveEarnedArtifact.SameTools(after.Tools,Convert.FromBase64String(value.Tools))||
                    !RebirthProductiveEarnedPendingToolTransfer.Conserved(beforeBag,beforeBelt,before.Tools,afterBag,afterBelt,tools))return false;
                for(int index=value.OwnedBelt;index<value.BeltCount;index++)
                    if(!RebirthCamperInventorySnapshot.Capture(new[]{beforeBelt[index]}).SequenceEqual(RebirthCamperInventorySnapshot.Capture(new[]{afterBelt[index]})))return false;
                before.Tools=tools;if(!RebirthProductiveEarnedNativeBody.Encode(before,elapsed,out int offset).SequenceEqual(value.After)||offset!=value.AfterOffset||!Encode(value).SequenceEqual(bytes))return false;
                plan=value;return true;
            }
        }
        catch{return false;}
    }
    internal static bool MatchesOwner(PlayerDataFile data,Description plan,bool after)
        =>data!=null&&RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,true),Convert.FromBase64String(after?plan.AfterBag:plan.BeforeBag))&&
            RebirthProductiveEarnedArtifact.SameTools(RebirthGearPlayerDataInventory.ReadSlots(data,false),Convert.FromBase64String(after?plan.AfterBelt:plan.BeforeBelt));
    private static byte[] Read(BinaryReader reader)
    {int length=reader.ReadInt32();if(length<1||length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();return reader.ReadBytes(length);}
}
