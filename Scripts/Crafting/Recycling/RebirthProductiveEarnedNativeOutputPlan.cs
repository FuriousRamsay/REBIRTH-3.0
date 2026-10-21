using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Exact native terminal description only. Native insertion/AddCraftComplete/
// queue cycling are never invoked by this validator or by a decoded record.
internal static class RebirthProductiveEarnedNativeOutputPlan
{
    internal sealed class Description
    {
        internal Guid Operation,Birth,Nonce;internal string OriginalDigest,Binding,Extras;
        internal byte[] Native;internal int Offset,PendingIndex=-1;internal float PendingBefore,PendingTarget;
        internal ItemStack Primary;internal ItemStack[] Additional;
    }
    internal static bool TryMeasureItems(ItemStack primary,ItemStack[] additional,out long graph)
    {
        graph=0;
        if(primary==null||additional==null||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{primary},262144,349528,out var first)||
            !RebirthProductiveEarnedObservationBudget.TryMeasureTools(Convert.FromBase64String(first),out long firstGraph,out _))return false;
        graph=firstGraph;
        if(additional.Length==0)return true;
        if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(additional,262144,349528,out var rest)||
            !RebirthProductiveEarnedObservationBudget.TryMeasureTools(Convert.FromBase64String(rest),out long restGraph,out _))return false;
        graph=checked(graph+restGraph);return true;
    }
    internal static bool TryMeasureRetention(Description plan,int recordLength,out long bytes)
    {
        bytes=0;if(!TryMeasureItems(plan.Primary,plan.Additional,out long graph))return false;
        bytes=checked(4L*recordLength+plan.Native.Length+graph+2L*((plan.Binding?.Length??0)+(plan.Extras?.Length??0))+65536);return true;
    }
    internal static byte[] Encode(Description value)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31504F4Eu);writer.Write(value.Operation.ToByteArray());writer.Write(value.Birth.ToByteArray());writer.Write(value.Nonce.ToByteArray());writer.Write(value.OriginalDigest);
            writer.Write(value.Offset);Write(writer,value.Native);Write(writer,RebirthCamperInventorySnapshot.Capture(new[]{value.Primary}));Write(writer,RebirthCamperInventorySnapshot.Capture(value.Additional));
            writer.Write(value.PendingIndex);writer.Write(value.PendingBefore);writer.Write(value.PendingTarget);writer.Write(value.Binding??"");writer.Write(value.Extras??"");writer.Flush();
            if(stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryRead(byte[] bytes,RebirthProductiveEarnedRowProof.Frame frame,Guid nonce,string originalDigest,out Description value)
    {
        value=null;
        try
        {
            if(bytes==null||bytes.Length<100||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            var result=new Description();
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31504F4Eu)return false;result.Operation=new Guid(reader.ReadBytes(16));result.Birth=new Guid(reader.ReadBytes(16));result.Nonce=new Guid(reader.ReadBytes(16));
                result.OriginalDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);result.Offset=reader.ReadInt32();result.Native=Read(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                var primary=ReadGrid(reader);if(primary?.Length!=1)return false;result.Primary=primary[0];
                result.Additional=ReadGrid(reader);
                result.PendingIndex=reader.ReadInt32();result.PendingBefore=reader.ReadSingle();result.PendingTarget=reader.ReadSingle();
                result.Binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);result.Extras=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                if(stream.Position!=stream.Length||result.Operation!=frame.Operation||result.Birth!=frame.Birth||result.Nonce!=nonce||nonce==Guid.Empty||result.OriginalDigest!=originalDigest||
                    !Validate(frame,result)||!Encode(result).SequenceEqual(bytes))return false;
            }
            value=result;return true;
        }
        catch{return false;}
    }
    internal static bool Validate(RebirthProductiveEarnedRowProof.Frame frame,Description result)
    {
        if(!RebirthProductiveEarnedNativeBody.TryRead(frame.AfterNative,frame.AfterElapsedOffset,out var before,out _)||
            !RebirthProductiveEarnedNativeBody.TryRead(result.Native,result.Offset,out var after,out ulong elapsed)||
            frame.RowIndex!=before.Queue.Length-1||before.Queue.Length!=after.Queue.Length||result.Primary?.itemValue==null||result.Additional==null)return false;
        var row=before.Queue[frame.RowIndex];
        if(row?.Recipe==null||row.Multiplier!=frame.Multiplier||row.Recipe.IsScrap||row.CraftingTimeLeft>=0||
            RebirthProductiveEarnedUiQueueOwner.Binding(row)!=frame.FullRecipeBinding||RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=frame.ExtrasDefinition)return false;
        var primary=new ItemValue(row.Recipe.itemValueType);
        if(primary.ItemClass?.HasQuality==true)primary=new ItemValue(row.Recipe.itemValueType,row.Quality,row.Quality);
        primary.Seed=result.Primary.itemValue.Seed;
        if(!RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(primary,row.Recipe.count)}).SequenceEqual(RebirthCamperInventorySnapshot.Capture(new[]{result.Primary}))||
            !RebirthRecipeOutputs.TryPlan(row.Recipe,1,out var extras,out _)||extras.Count!=result.Additional.Length)return false;
        for(int i=0;i<extras.Count;i++)
        {
            var expected=extras[i].Clone();bool frozen=row.Recipe.ingredients?.Count>0&&row.Recipe.ingredients[0]?.itemValue?.HasMetadata("rebirth.recipe.outputs.frozen.v1")==true;
            if(!frozen)expected.itemValue.Seed=result.Additional[i].itemValue.Seed;
            if(!RebirthCamperInventorySnapshot.Capture(new[]{expected}).SequenceEqual(RebirthCamperInventorySnapshot.Capture(new[]{result.Additional[i]})))return false;
        }
        if(!RebirthRecipeOutputIntegration.Predict(before.Output,result.Primary,-1,result.Additional,out var output,out _))return false;
        before.Output=output;
        var receipts=before.Completions.ToList();
        var aggregate=receipts.FirstOrDefault(value=>value.CraftedItemStack.itemValue.GetItemId()==result.Primary.itemValue.GetItemId()&&value.ItemScrapped==string.Empty);
        if(aggregate!=null)aggregate.CraftedItemStack.count=checked(aggregate.CraftedItemStack.count+row.Recipe.count);
        else receipts.Add(new CraftCompleteData(row.StartingEntityId,result.Primary.Clone(),row.Recipe.GetName(),string.Empty,row.Recipe.craftExpGain,1));
        before.Completions=receipts.ToArray();row.Multiplier--;
        int pending=-1;float pendingBefore=0,pendingTarget=0;
        if(row.Multiplier>0){pending=frame.RowIndex;pendingBefore=row.CraftingTimeLeft;pendingTarget=row.CraftingTimeLeft+row.OneItemCraftTime;}
        else
        {
            float carry=row.CraftingTimeLeft+row.OneItemCraftTime;row.CraftingTimeLeft=carry;Cycle(before.Queue);
            var next=before.Queue[before.Queue.Length-1];float target=next.CraftingTimeLeft+(carry<0?carry:0);
            if(next.Recipe!=null&&next.Multiplier>0&&target<next.CraftingTimeLeft){pending=before.Queue.Length-1;pendingBefore=next.CraftingTimeLeft;pendingTarget=target;}
            else next.CraftingTimeLeft=target;
        }
        if(result.PendingIndex!=pending||result.PendingBefore!=pendingBefore||result.PendingTarget!=pendingTarget)return false;
        if(pending>=0)
        {
            var next=before.Queue[pending];if(result.Binding!=RebirthProductiveEarnedUiQueueOwner.Binding(next)||result.Extras!=RebirthRecipeOutputs.DefinitionIdentity(next.Recipe))return false;
        }
        else if(result.Binding!=""||result.Extras!="")return false;
        return RebirthProductiveEarnedNativeBody.Encode(before,elapsed,out int offset).SequenceEqual(result.Native)&&offset==result.Offset;
    }
    private static void Cycle(RecipeQueueItem[] queue)
    {
        RecipeQueueItem last=null;if(queue[queue.Length-1]!=null&&queue[queue.Length-1].Multiplier>0)return;
        for(int i=0;i<queue.Length;i++)
        {
            last=queue[queue.Length-1];if(last!=null&&last.Multiplier!=0)break;
            for(int n=queue.Length-1;n>0;n--)
            {
                var into=queue[n];var source=queue[n-1];if(source.Multiplier<0)source.Multiplier=0;
                into.Recipe=source.Recipe;into.Multiplier=source.Multiplier;into.CraftingTimeLeft=source.CraftingTimeLeft;into.IsCrafting=source.IsCrafting;
                into.Quality=source.Quality;into.OneItemCraftTime=source.OneItemCraftTime;into.StartingEntityId=source.StartingEntityId;
                queue[n]=into;queue[n-1]=new RecipeQueueItem{Recipe=null,Multiplier=0,CraftingTimeLeft=0,OneItemCraftTime=0,IsCrafting=false,Quality=0,StartingEntityId=-1};
            }
        }
        if(last?.Recipe!=null&&!last.IsCrafting&&last.Multiplier!=0)last.IsCrafting=true;
    }
    private static void Write(BinaryWriter writer,byte[] bytes){writer.Write(bytes.Length);writer.Write(bytes);}
    private static ItemStack[] ReadGrid(BinaryReader reader)=>RebirthProductiveEarnedUiRemote.ReadGrid(new XElement("native",new XElement("cells",Convert.ToBase64String(Read(reader,2*RebirthProductiveEarnedRowProof.MaximumFrameBytes)))),"cells");
    private static byte[] Read(BinaryReader reader,int maximum){int length=reader.ReadInt32();if(length<1||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();return reader.ReadBytes(length);}
}
