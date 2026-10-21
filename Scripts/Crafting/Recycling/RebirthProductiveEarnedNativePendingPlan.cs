using System;
using System.IO;
using System.Linq;
using System.Text;

// Original native debt continuation description. No field or decoded image
// supplies owner authority, a native setter or a fresh completion permission.
internal static class RebirthProductiveEarnedNativePendingPlan
{
    internal sealed class Description
    {
        internal Guid Operation,Birth,Nonce;internal string Digest,Binding,Extras;
        internal int Index,BeforeOffset,AfterOffset;internal float Work,BeforeTimer,AfterTimer;
        internal byte[] Before,After,ChildImage;internal RebirthProductiveEarnedRowProof.Frame Child;
        internal ItemStack[] Tools;
    }
    internal static bool TryCreate(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame original,Guid nonce,string digest,
        RebirthProductiveEarnedNativeOutputPlan.Description paid,out Description plan)
    {
        plan=null;
        if(paid==null||paid.PendingIndex<0||paid.PendingIndex!=station.Queue.Length-1||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,paid.Native,paid.Offset))return false;
        var row=station.Queue[paid.PendingIndex];if(row?.Recipe==null||row.Multiplier<1||row.RepairItem!=null||row.AmountToRepair!=0||
            row.CraftingTimeLeft!=paid.PendingBefore||RebirthProductiveEarnedUiQueueOwner.Binding(row)!=paid.Binding||RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=paid.Extras)return false;
        float work,target;bool child=false;
        if(!RebirthProductiveEarnedCoordinator.TryPartialNativeWork(paid.PendingBefore,paid.PendingTarget,row.OneItemCraftTime,out work,out target))
        {child=true;if(!RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(paid.PendingBefore,paid.PendingTarget,row.OneItemCraftTime,out work,out target))return false;}
        if(RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work)return false;
        var tools=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work);
        if(!RebirthProductiveEarnedNativeBody.TryCapturePlan(station,tools,paid.PendingIndex,target,out var before,out var after,out int beforeOffset,out int afterOffset))return false;
        var value=new Description{Operation=original.Operation,Birth=original.Birth,Nonce=nonce,Digest=digest,Index=paid.PendingIndex,
            Work=work,BeforeTimer=paid.PendingBefore,AfterTimer=target,Binding=paid.Binding,Extras=paid.Extras,
            Before=before,After=after,BeforeOffset=beforeOffset,AfterOffset=afterOffset,Tools=tools};
        if(child)
        {
            var afterRow=new RecipeQueueItem{Recipe=row.Recipe,Multiplier=row.Multiplier,CraftingTimeLeft=target,IsCrafting=row.IsCrafting,
                Quality=row.Quality,StartingEntityId=row.StartingEntityId,OneItemCraftTime=row.OneItemCraftTime,RepairItem=row.RepairItem,AmountToRepair=row.AmountToRepair};
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(station.Tools,262144,349528,out var beforeTools)||
                !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(tools,262144,349528,out var afterTools))return false;
            value.Child=new RebirthProductiveEarnedRowProof.Frame{Birth=original.Birth,Operation=Guid.NewGuid(),World=original.World,RowIndex=value.Index,
                Multiplier=row.Multiplier,WorkedSeconds=work,BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row),AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(afterRow),
                BeforeTools=Convert.FromBase64String(beforeTools),AfterTools=Convert.FromBase64String(afterTools),FullRecipeBinding=paid.Binding,ExtrasDefinition=paid.Extras,
                BeforeNative=before,BeforeElapsedOffset=beforeOffset,AfterNative=after,AfterElapsedOffset=afterOffset};
            value.ChildImage=RebirthProductiveEarnedRowProof.Encode(value.Child);
        }
        if(!TryRead(Encode(value),original,nonce,digest,paid,station.block,out _))return false;
        plan=value;return true;
    }
    internal static byte[] Encode(Description plan)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x314E5052u);writer.Write(plan.Operation.ToByteArray());writer.Write(plan.Birth.ToByteArray());writer.Write(plan.Nonce.ToByteArray());writer.Write(plan.Digest);
            writer.Write(plan.Index);writer.Write(plan.Work);writer.Write(plan.BeforeTimer);writer.Write(plan.AfterTimer);writer.Write(plan.Binding);writer.Write(plan.Extras);
            writer.Write(plan.ChildImage?.Length??0);
            if(plan.ChildImage!=null)writer.Write(plan.ChildImage);
            else{writer.Write(plan.BeforeOffset);Write(writer,plan.Before);writer.Write(plan.AfterOffset);Write(writer,plan.After);}
            writer.Flush();if(stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryMeasureRetention(Description plan,int recordLength,out long bytes)
    {
        bytes=0;
        if(plan?.Tools==null||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(plan.Tools,262144,349528,out var tools)||
            !RebirthProductiveEarnedObservationBudget.TryMeasureTools(Convert.FromBase64String(tools),out long graph,out _))return false;
        bytes=checked(4L*recordLength+plan.Before.Length+plan.After.Length+(plan.ChildImage?.Length??0)+graph+2L*(plan.Binding.Length+plan.Extras.Length)+65536);return true;
    }
    internal static bool TryRead(byte[] record,RebirthProductiveEarnedRowProof.Frame original,Guid nonce,string digest,
        RebirthProductiveEarnedNativeOutputPlan.Description paid,Block block,out Description plan)
    {
        plan=null;
        try
        {
            if(record==null||record.Length<100||record.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||paid?.PendingIndex<0)return false;
            var value=new Description();
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x314E5052u)return false;
                value.Operation=new Guid(reader.ReadBytes(16));value.Birth=new Guid(reader.ReadBytes(16));value.Nonce=new Guid(reader.ReadBytes(16));value.Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                value.Index=reader.ReadInt32();value.Work=reader.ReadSingle();value.BeforeTimer=reader.ReadSingle();value.AfterTimer=reader.ReadSingle();
                value.Binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);value.Extras=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                int child=reader.ReadInt32();
                if(child<0||child>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||child>stream.Length-stream.Position)return false;
                if(child>0)
                {
                    value.ChildImage=reader.ReadBytes(child);value.Child=RebirthProductiveEarnedRowProof.Decode(value.ChildImage);
                    value.Before=value.Child.BeforeNative;value.BeforeOffset=value.Child.BeforeElapsedOffset;value.After=value.Child.AfterNative;value.AfterOffset=value.Child.AfterElapsedOffset;
                }
                else{value.BeforeOffset=reader.ReadInt32();value.Before=Read(reader);value.AfterOffset=reader.ReadInt32();value.After=Read(reader);}
                if(stream.Position!=stream.Length||value.Operation!=original.Operation||value.Birth!=original.Birth||value.Nonce!=nonce||nonce==Guid.Empty||value.Digest!=digest||
                    value.Index!=paid.PendingIndex||value.BeforeTimer!=paid.PendingBefore||value.Binding!=paid.Binding||value.Extras!=paid.Extras||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Before,value.BeforeOffset,paid.Native,paid.Offset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(value.Before,value.BeforeOffset,out var before,out _)||
                    !RebirthProductiveEarnedNativeBody.TryRead(value.After,value.AfterOffset,out var after,out ulong elapsed)||
                    value.Index!=before.Queue.Length-1||after.Queue.Length!=before.Queue.Length)return false;
                var row=before.Queue[value.Index];
                if(row?.Recipe==null||row.Multiplier<1||row.RepairItem!=null||row.AmountToRepair!=0||
                    RebirthProductiveEarnedUiQueueOwner.Binding(row)!=paid.Binding||RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=paid.Extras)return false;
                float work,target;
                bool valid=value.Child==null?RebirthProductiveEarnedCoordinator.TryPartialNativeWork(paid.PendingBefore,paid.PendingTarget,row.OneItemCraftTime,out work,out target):
                    RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(paid.PendingBefore,paid.PendingTarget,row.OneItemCraftTime,out work,out target);
                if(!valid||work!=value.Work||target!=value.AfterTimer||row.CraftingTimeLeft!=value.BeforeTimer||
                    RebirthProductiveToolWork.Limit(before.Tools,row.Recipe,block,work)!=work)return false;
                var tools=RebirthProductiveToolWork.Wear(before.Tools,row.Recipe,block,work);
                before.Tools=tools;row.CraftingTimeLeft=target;
                if(!RebirthProductiveEarnedNativeBody.Encode(before,elapsed,out int expectedOffset).SequenceEqual(value.After)||expectedOffset!=value.AfterOffset)return false;
                if(value.Child!=null&&(value.Child.Operation==Guid.Empty||value.Child.Operation==original.Operation||value.Child.Birth!=original.Birth||value.Child.World!=original.World||
                    value.Child.RowIndex!=value.Index||value.Child.Multiplier!=row.Multiplier||value.Child.WorkedSeconds!=work||value.Child.FullRecipeBinding!=value.Binding||value.Child.ExtrasDefinition!=value.Extras||
                    !RebirthProductiveEarnedNativeBody.ValidPlan(value.Child)))return false;
                if(!Encode(value).SequenceEqual(record))return false;value.Tools=after.Tools;
            }
            plan=value;return true;
        }
        catch{return false;}
    }
    internal static byte[] PartialRecord(Description value)
    {
        if(value.Child!=null)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31535052u);writer.Write(value.Birth.ToByteArray());writer.Write(value.Operation.ToByteArray());writer.Write(value.Index);writer.Write(value.Work);writer.Write(value.BeforeTimer);writer.Write(value.AfterTimer);
            writer.Write(value.Binding);writer.Write(value.Extras);writer.Write(value.BeforeOffset);Write(writer,value.Before);writer.Write(value.AfterOffset);Write(writer,value.After);writer.Flush();return stream.ToArray();
        }
    }
    internal static byte[] ChildLink(byte[] original,Description value)
    {
        if(value.Child==null)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        using(var sha=System.Security.Cryptography.SHA256.Create())
        {
            writer.Write(0x31434552u);writer.Write(value.Birth.ToByteArray());writer.Write(value.Operation.ToByteArray());
            writer.Write(value.Child.Birth.ToByteArray());writer.Write(value.Child.Operation.ToByteArray());
            writer.Write(sha.ComputeHash(original));writer.Write(sha.ComputeHash(value.ChildImage));
            writer.Write(RebirthProductiveEarnedNativeBody.ComparisonDigest(value.Before,value.BeforeOffset));writer.Flush();return stream.ToArray();
        }
    }
    private static void Write(BinaryWriter writer,byte[] bytes){writer.Write(bytes.Length);writer.Write(bytes);}
    private static byte[] Read(BinaryReader reader){int count=reader.ReadInt32();if(count<1||count>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();return reader.ReadBytes(count);}
}
