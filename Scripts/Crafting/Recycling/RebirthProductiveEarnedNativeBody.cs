using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;

// Exact native TileEntity-v19/workstation-v50 derivation. The explicit
// ToServer grammar is different from Persistency; no body is fed to a native
// FromClient reader here, and no native inventory/state is modified.
internal static class RebirthProductiveEarnedNativeBody
{
    internal const int MaximumBodyBytes=2*1024*1024-64;
    [ThreadStatic] private static TileEntityWorkstation serializing;
    [ThreadStatic] private static Stream serializationStream;
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.earned.body.isolation"),typeof(Isolate));installed=true;
    }
    internal static bool TryCapturePlan(TileEntityWorkstation station,ItemStack[] afterTools,int rowIndex,float afterTimer,
        out byte[] before,out byte[] after,out int beforeElapsed,out int afterElapsed)
    {
        before=after=null;beforeElapsed=afterElapsed=-1;
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||serializing!=null||station==null||station is RebirthCamperStationState||
                afterTools==null||rowIndex<0||rowIndex>=station.Queue.Length||float.IsNaN(afterTimer)||float.IsInfinity(afterTimer)||
                !Check(station.Fuel)||!Check(station.Input)||!Check(station.Tools)||!Check(station.Output)||!Check(station.lastInput)||!Check(afterTools))return false;
            foreach(var row in station.Queue)
            {
                if(row==null||row.Multiplier<0)return false;
                if(row.Recipe?.ingredients!=null)
                    foreach(var item in row.Recipe.ingredients)if(!Check(new[]{item}))return false;
            }
            ulong firstTick=GameTimer.Instance.ticks,clock=station.lastTickTime;
            byte[] persistence=Capture(station,StreamModeWrite.Persistency,MaximumBodyBytes+18);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(persistence,out var image)||image.Clock!=clock)return false;
            before=Capture(station,StreamModeWrite.ToServer,MaximumBodyBytes);
            // The exact offset is DERIVED from the native schema, not caller supplied.
            Encode(image,0,out beforeElapsed);
            if(beforeElapsed<13||beforeElapsed>before.Length-8)return false;
            ulong elapsed=BitConverter.ToUInt64(before,beforeElapsed),lastTick=GameTimer.Instance.ticks;
            if(lastTick<firstTick||firstTick<clock||station.lastTickTime!=clock||elapsed<firstTick-clock||elapsed>lastTick-clock||
                !Encode(image,elapsed,out var qualifiedOffset).SequenceEqual(before)||qualifiedOffset!=beforeElapsed)return false;
            image.Tools=ItemStack.Clone(afterTools);image.Queue[rowIndex].CraftingTimeLeft=afterTimer;
            after=Encode(image,elapsed,out afterElapsed);
            if(!TryRead(before,beforeElapsed,out _,out _)||!TryRead(after,afterElapsed,out _,out _))return false;
            return true;
        }
        catch{before=after=null;beforeElapsed=afterElapsed=-1;return false;}
    }
    // Exact normal syncTEfromUI projection, read from its actual native UI
    // arrays/cards by the named BeforeFlush caller. No live setters occur here.
    internal static bool TryCaptureUiProjection(TileEntityWorkstation station,RecipeQueueItem[] rows,
        ItemStack[] input,ItemStack[] output,ItemStack[] fuel,ItemStack[] tools,
        out byte[] before,out byte[] after,out int beforeElapsed,out int afterElapsed)
    {
        before=after=null;beforeElapsed=afterElapsed=-1;
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||serializing!=null||station==null||station is RebirthCamperStationState||rows==null||
                rows.Length!=station.Queue?.Length||rows.Any(row=>row==null||row.RepairItem!=null||row.AmountToRepair!=0)||
                input!=null&&!Check(input)||output!=null&&!Check(output)||fuel!=null&&!Check(fuel)||tools!=null&&!Check(tools))return false;
            ulong start=GameTimer.Instance.ticks,clock=station.lastTickTime;
            var persistence=Capture(station,StreamModeWrite.Persistency,MaximumBodyBytes+18);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(persistence,out var native)||native.Clock!=clock)return false;
            before=Capture(station,StreamModeWrite.ToServer,MaximumBodyBytes);Encode(native,0,out beforeElapsed);
            if(beforeElapsed<13||beforeElapsed>before.Length-8||start<clock||station.lastTickTime!=clock)return false;
            ulong elapsed=BitConverter.ToUInt64(before,beforeElapsed),end=GameTimer.Instance.ticks;
            if(end<start||elapsed<start-clock||elapsed>end-clock||!Encode(native,elapsed,out int offset).SequenceEqual(before)||offset!=beforeElapsed)return false;
            if(input!=null)native.Input=ItemStack.Clone(input);if(output!=null)native.Output=ItemStack.Clone(output);
            if(fuel!=null)native.Fuel=ItemStack.Clone(fuel);if(tools!=null)native.Tools=ItemStack.Clone(tools);native.Queue=rows;
            after=Encode(native,elapsed,out afterElapsed);return TryRead(after,afterElapsed,out _,out _);
        }
        catch{before=after=null;beforeElapsed=afterElapsed=-1;return false;}
    }
    internal static bool TryCaptureCurrent(TileEntityWorkstation station,out byte[] bytes,out int elapsedOffset)
    {
        bytes=null;elapsedOffset=-1;
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||serializing!=null||station==null||station is RebirthCamperStationState||
                !Check(station.Fuel)||!Check(station.Input)||!Check(station.Tools)||!Check(station.Output)||!Check(station.lastInput))return false;
            ulong start=GameTimer.Instance.ticks,clock=station.lastTickTime;
            var persistence=Capture(station,StreamModeWrite.Persistency,MaximumBodyBytes+18);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(persistence,out var image)||image.Clock!=clock)return false;
            Encode(image,0,out elapsedOffset);
            bytes=Capture(station,StreamModeWrite.ToServer,MaximumBodyBytes);ulong end=GameTimer.Instance.ticks;
            if(elapsedOffset<13||elapsedOffset>bytes.Length-8||start<clock||end<start||station.lastTickTime!=clock)return false;
            ulong elapsed=BitConverter.ToUInt64(bytes,elapsedOffset);
            return elapsed>=start-clock&&elapsed<=end-clock&&Encode(image,elapsed,out int offset).SequenceEqual(bytes)&&offset==elapsedOffset;
        }
        catch{bytes=null;elapsedOffset=-1;return false;}
    }
    // Comparison identity only. No native clock or payment authority is obtained.
    internal static byte[] ComparisonDigest(byte[] bytes,int offset)
    {
        if(bytes==null||offset<13||offset>bytes.Length-8||bytes.Length>MaximumBodyBytes)throw new InvalidDataException();
        using(var sha=System.Security.Cryptography.SHA256.Create())
        {
            sha.TransformBlock(bytes,0,offset,null,0);var zero=new byte[8];sha.TransformBlock(zero,0,8,null,0);
            sha.TransformFinalBlock(bytes,offset+8,bytes.Length-offset-8);return sha.Hash;
        }
    }
    internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] expected,int elapsedOffset)
    {
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||serializing!=null||station==null||station is RebirthCamperStationState||
                !Check(station.Fuel)||!Check(station.Input)||!Check(station.Tools)||!Check(station.Output)||!Check(station.lastInput)||station.Queue==null)return false;
            foreach(var row in station.Queue)
                if(row==null||row.Multiplier<0||row.Recipe?.ingredients?.Any(item=>!Check(new[]{item}))==true)return false;
            ulong start=GameTimer.Instance.ticks,clock=station.lastTickTime;
            byte[] actual=Capture(station,StreamModeWrite.ToServer,MaximumBodyBytes);ulong end=GameTimer.Instance.ticks;
            if(!SameExceptElapsed(actual,elapsedOffset,expected,elapsedOffset)||start<clock||end<start||station.lastTickTime!=clock)return false;
            ulong elapsed=BitConverter.ToUInt64(actual,elapsedOffset);
            return elapsed>=start-clock&&elapsed<=end-clock;
        }
        catch{return false;}
    }
    private static bool Check(ItemStack[] items)
        =>items!=null&&items.Length<=byte.MaxValue&&items.All(item=>item!=null&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{item},196608,262144,out _));
    private static byte[] Capture(TileEntityWorkstation station,StreamModeWrite mode,int maximum)
    {
        using(var stream=new Limited(maximum))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            var encoding=writer.Encoding;writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);
            serializing=station;serializationStream=stream;
            try{station.write(writer,mode);writer.Flush();return stream.ToArray();}
            finally{serializing=null;serializationStream=null;writer.Encoding=encoding;}
        }
    }
    // Reconstruct only the PERSISTENCY grammar envelope to reuse the existing
    // bounded canonical item/queue parser. Synthetic clocks are detached parser
    // data, NEVER publication/current native clock evidence.
    internal static bool TryRead(byte[] native,int elapsedOffset,out RebirthRemoteResourcePlacedStationNativeReader.Image image,out ulong elapsed)
    {
        image=null;elapsed=0;
        try
        {
            if(native==null||native.Length<30||native.Length>MaximumBodyBytes||native[12]!=50||elapsedOffset<13||elapsedOffset>native.Length-8)return false;
            elapsed=BitConverter.ToUInt64(native,elapsedOffset);
            using(var stream=new Limited(MaximumBodyBytes+18))using(var writer=new BinaryWriter(stream))
            {
                writer.Write((ushort)19);writer.Write(native,0,12);writer.Write((ulong)0);writer.Write((byte)50);writer.Write((ulong)0);
                writer.Write(native,13,elapsedOffset-13);writer.Write(native,elapsedOffset+8,native.Length-elapsedOffset-8);writer.Flush();
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(stream.ToArray(),out image))return false;
            }
            return Encode(image,elapsed,out int offset).SequenceEqual(native)&&offset==elapsedOffset;
        }
        catch{image=null;elapsed=0;return false;}
    }
    // Allocation preflight only; not canonical acceptance or native permission.
    // Caller reserves the bounded detached envelope before invoking this method.
    internal static bool TryMeasure(byte[] native,int elapsedOffset,out long graph,out long scratch)
    {
        graph=scratch=0;
        try
        {
            if(native==null||native.Length<30||native.Length>MaximumBodyBytes||native[12]!=50||
                elapsedOffset<13||elapsedOffset>native.Length-8)return false;
            using(var stream=new Limited(native.Length+18))using(var writer=new BinaryWriter(stream))
            {
                writer.Write((ushort)19);writer.Write(native,0,12);writer.Write((ulong)0);writer.Write((byte)50);writer.Write((ulong)0);
                writer.Write(native,13,elapsedOffset-13);writer.Write(native,elapsedOffset+8,native.Length-elapsedOffset-8);writer.Flush();
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(stream.ToArray(),out graph,out scratch))return false;
            }
            return graph>=0&&scratch>=0;
        }
        catch{graph=scratch=0;return false;}
    }
    internal static bool ValidPlan(RebirthProductiveEarnedRowProof.Frame frame)
    {
        if(frame?.BeforeNative==null||frame.AfterNative==null||
            !TryRead(frame.BeforeNative,frame.BeforeElapsedOffset,out var before,out ulong beforeElapsed)||
            !TryRead(frame.AfterNative,frame.AfterElapsedOffset,out var after,out ulong afterElapsed)||beforeElapsed!=afterElapsed||
            frame.RowIndex<0||frame.RowIndex>=before.Queue.Length||frame.RowIndex>=after.Queue.Length||
            !RebirthProductiveEarnedRowProof.CaptureRow(before.Queue[frame.RowIndex]).SequenceEqual(frame.BeforeRow)||
            !RebirthProductiveEarnedRowProof.CaptureRow(after.Queue[frame.RowIndex]).SequenceEqual(frame.AfterRow)||
            !RebirthProductiveEarnedArtifact.SameTools(before.Tools,frame.BeforeTools)||
            !RebirthProductiveEarnedArtifact.SameTools(after.Tools,frame.AfterTools))return false;
        before.Tools=after.Tools;before.Queue[frame.RowIndex].CraftingTimeLeft=after.Queue[frame.RowIndex].CraftingTimeLeft;
        return Encode(before,beforeElapsed,out int offset).SequenceEqual(frame.AfterNative)&&offset==frame.AfterElapsedOffset;
    }
    internal static bool SameExceptElapsed(byte[] actual,int actualOffset,byte[] original,int originalOffset)
    {
        if(actual==null||original==null||actual.Length!=original.Length||actualOffset!=originalOffset||actualOffset<13||actualOffset>actual.Length-8)return false;
        for(int i=0;i<actual.Length;i++)if((i<actualOffset||i>=actualOffset+8)&&actual[i]!=original[i])return false;
        return true; // Comparison only. Retain original and actual raw frames.
    }
    internal static bool ValidPartialSuccessor(byte[] beforeBytes,int beforeOffset,byte[] afterBytes,int afterOffset,
        int index,float beforeTimer,float target,float work)
    {
        if(!TryRead(beforeBytes,beforeOffset,out var before,out ulong elapsed)||
            !TryRead(afterBytes,afterOffset,out var after,out ulong afterElapsed)||elapsed!=afterElapsed||
            index<0||index>=before.Queue.Length||index>=after.Queue.Length)return false;
        var row=before.Queue[index];
        if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||row.CraftingTimeLeft!=beforeTimer||
            after.Queue[index]?.CraftingTimeLeft!=target||
            !RebirthProductiveEarnedCoordinator.TryPartialNativeWork(beforeTimer,target,row.OneItemCraftTime,out float expected,out _)||work!=expected)return false;
        // Only detached parser images change here. The complete native frame
        // must differ solely in the frozen tools and this one positive timer.
        before.Tools=after.Tools;row.CraftingTimeLeft=target;
        return Encode(before,elapsed,out int offset).SequenceEqual(afterBytes)&&offset==afterOffset;
    }
    // Shape comparison only. An authenticated original owner transfer and its
    // positive native publication must separately qualify a pending rebase.
    // Clocks supply no productive work, payment, inventory or effect authority.
    internal static bool ValidPendingToolReplacement(byte[] beforeBytes,int beforeOffset,byte[] afterBytes,int afterOffset,
        int index,float pendingTimer)
    {
        if(float.IsNaN(pendingTimer)||float.IsInfinity(pendingTimer)||index<0||
            !TryRead(beforeBytes,beforeOffset,out var before,out _)||
            !TryRead(afterBytes,afterOffset,out var after,out ulong afterElapsed)||
            before.Queue==null||after.Queue==null||index!=before.Queue.Length-1||index!=after.Queue.Length-1||
            before.Tools==null||after.Tools==null||before.Tools.Length==0||before.Tools.Length!=after.Tools.Length)return false;
        var row=before.Queue[index];
        if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||row.CraftingTimeLeft!=pendingTimer||
            row.OneItemCraftTime<=0||float.IsNaN(row.OneItemCraftTime)||float.IsInfinity(row.OneItemCraftTime))return false;
        // Reject a clock-only/no-op record. Normalize only detached images;
        // every queue/output/receipt/input/fuel/state field remains byte exact.
        if(Encode(before,afterElapsed,out int unchangedOffset).SequenceEqual(afterBytes)&&unchangedOffset==afterOffset)return false;
        before.Tools=after.Tools;
        return Encode(before,afterElapsed,out int offset).SequenceEqual(afterBytes)&&offset==afterOffset;
    }
    internal static byte[] Encode(RebirthRemoteResourcePlacedStationNativeReader.Image image,ulong elapsed,out int elapsedOffset)
    {
        using(var stream=new Limited(MaximumBodyBytes))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            var previous=writer.Encoding;
            try
            {
                writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);
                writer.Write(image.LocalPosition.x);writer.Write(image.LocalPosition.y);writer.Write(image.LocalPosition.z);writer.Write((byte)50);
                Write(writer,image.Fuel);Write(writer,image.Input);Write(writer,image.Tools);Write(writer,image.Output);
                writer.Write((byte)image.Queue.Length);foreach(var row in image.Queue)row.Write(writer);
                writer.Write((short)image.Completions.Length);foreach(var value in image.Completions)value.Write(writer);
                writer.Write(image.Burning);writer.Write(image.BurnTime);writer.Write((byte)image.MeltTimes.Length);foreach(float value in image.MeltTimes)writer.Write(value);
                writer.Write(image.PlayerPlaced);elapsedOffset=checked((int)stream.Position);writer.Write(elapsed);Write(writer,image.LastInput);writer.Flush();return stream.ToArray();
            }
            finally{writer.Encoding=previous;}
        }
    }
    private static void Write(PooledBinaryWriter writer,ItemStack[] items)
    {if(items==null||items.Length>byte.MaxValue)throw new InvalidDataException();writer.Write((byte)items.Length);foreach(var item in items)item.Write(writer);}
    private sealed class Limited:MemoryStream
    {
        private readonly int maximum;internal Limited(int value){maximum=value;}
        private void Ensure(long end)
        {
            if(end<0||end>maximum)throw new InvalidDataException("Earned native body exceeds frame bound.");
            if(end>Capacity)Capacity=checked((int)Math.Min(maximum,Math.Max(end,Math.Max(256L,(long)Capacity*2))));
        }
        public override void Write(byte[] bytes,int offset,int count)
        {if(count<0||Position<0)throw new InvalidDataException();Ensure(Position+(long)count);base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){Ensure(Position+1);base.WriteByte(value);}
        public override void SetLength(long value){Ensure(value);base.SetLength(value);}
    }
    [HarmonyPatch(typeof(RebirthStationSnapshotEvidence),nameof(RebirthStationSnapshotEvidence.StartStationSpan))]
    private static class Isolate
    {
        private static bool Prefix(object tile,Stream stream,ref long __result)
        {if(serializing==null||!ReferenceEquals(serializing,tile)||!ReferenceEquals(serializationStream,stream))return true;__result=-1;return false;}
    }
}
