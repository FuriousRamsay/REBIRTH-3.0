using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private static bool TryReadInvokedNativePartial(Evidence parent,out Evidence partial)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
        {partial=null;return decoder!=null&&TryReadInvokedNativePartialCore(parent,out partial);}
    }
    internal static bool TryReadStagedNativePartial(Evidence parent,out Evidence partial)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
        {partial=null;return decoder!=null&&TryReadInvokedNativePartialCore(parent,out partial,true);}
    }
    internal static bool TryReadStagedNativePartial(Evidence parent,Func<IDisposable> enterCallbacks,out Evidence partial)
    {
        // The saved predecessor already owns the graph-bearing observation
        // phase. Borrow that exact account; a second decoder cannot enter it.
        using(var callbacks=enterCallbacks?.Invoke())
        {partial=null;return callbacks!=null&&TryReadInvokedNativePartialCore(parent,out partial,true);}
    }
    private static bool TryReadInvokedNativePartialCore(Evidence parent,out Evidence partial,bool unentered=false)
    {
        partial=null;Evidence value=null;
        try
        {
            if(parent?.Terminal!=true||!parent.PendingNextWork||parent.PendingTimerAmbiguous||parent.PendingTimerIndex<0||
                !parent.PrimaryCurrent()||!(parent.ToolsRebased?parent.Path.EndsWith(".tools-load",StringComparison.Ordinal):parent.Path.EndsWith(".terminal-load",StringComparison.Ordinal)))return false;
            string original=parent.Path.Substring(0,parent.Path.Length-(parent.ToolsRebased?".tools-load":".terminal-load").Length);
            foreach(string suffix in new[]{".earned-child",".earned-child.invoking"})
                if(File.Exists(original+suffix)||File.Exists(original+suffix+".tmp"))return false;
            string recordPath=original+".partial-successor",invokingPath=recordPath+".invoking";
            if(!RebirthProductiveEarnedArtifact.SafePath(recordPath)||File.Exists(recordPath+".tmp")||File.Exists(invokingPath+".tmp"))return false;
            long length=new FileInfo(recordPath).Length;
            if(length<120||length>RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*parent.Image.Length+4L*length+65536),out var retained))return false;
            value=new Evidence(retained){Path=parent.Path,Directory=parent.Directory,WorldGuid=parent.WorldGuid,MarkerKey=parent.MarkerKey,
                Image=parent.Image,Original=parent.Original,Frame=parent.Frame,Marker=parent.Marker,Terminal=true,ToolsRebased=parent.ToolsRebased};
            byte[] record=Read(recordPath,RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes);
            if(unentered)
            {if(File.Exists(invokingPath))return false;}
            else if(!record.SequenceEqual(Read(invokingPath,RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes)))return false;
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31535052u||new Guid(reader.ReadBytes(16))!=parent.Frame.Birth||
                    new Guid(reader.ReadBytes(16))!=parent.Frame.Operation||reader.ReadInt32()!=parent.PendingTimerIndex)return false;
                float work=reader.ReadSingle(),beforeTimer=reader.ReadSingle(),target=reader.ReadSingle();
                string binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                string extras=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                if(string.IsNullOrEmpty(binding)||string.IsNullOrEmpty(extras)||beforeTimer!=parent.PendingTimerBefore||target!=parent.PendingTimerTarget)return false;
                if(parent.PendingRecipeBinding!=null&&(binding!=parent.PendingRecipeBinding||extras!=parent.PendingExtras))return false;
                int beforeOffset=reader.ReadInt32();byte[] before=ReadBytes(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                value.NativeOffset=reader.ReadInt32();value.Native=ReadBytes(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                if(stream.Position!=stream.Length||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,parent.Native,parent.NativeOffset)||
                    !RebirthProductiveEarnedNativeBody.TryMeasure(value.Native,value.NativeOffset,out long graph,out long scratch)||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(retained,
                        checked(8L*parent.Image.Length+4L*length+4L*value.Native.Length+3*graph+scratch+65536))||
                    !RebirthProductiveEarnedNativeBody.ValidPartialSuccessor(before,beforeOffset,value.Native,value.NativeOffset,
                        parent.PendingTimerIndex,beforeTimer,target,work))return false;
                value.PendingRecipeBinding=binding;value.PendingExtras=extras;
                if(unentered)value.StagedPartialRecord=record;
                value.Dependencies=WithPendingDefinition(parent,unentered?
                    new[]{NativeChildDependencyFor(parent.Path,parent.Image),NativeChildDependencyFor(recordPath,record)}:
                    new[]{NativeChildDependencyFor(parent.Path,parent.Image),NativeChildDependencyFor(recordPath,record),NativeChildDependencyFor(invokingPath,record)});
                value.Load=new CachedLoadRequest{Custody=value,Directory=value.Directory,X=parent.Load.X,Z=parent.Load.Z,
                    Snapshot=Array.Empty<byte>(),Tile=new byte[checked(value.Native.Length+10)],PredictedNative=true,
                    MaximumGraph=graph,MaximumScratch=scratch};
            }
            if(!value.PrimaryCurrent()||!parent.PrimaryCurrent()||unentered&&(File.Exists(invokingPath)||File.Exists(invokingPath+".tmp")))return false;
            partial=value;value=null;return true;
        }
        catch{return false;}
        finally{value?.Dispose();}
    }
}
