using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    internal sealed class NativeChildDependency
    {
        internal string Path;internal long Length;internal byte[] Digest;
    }
    private static bool NativeChildDependenciesCurrent(NativeChildDependency[] dependencies)
    {
        if(dependencies==null)return true;
        if(dependencies.Length<1||dependencies.Length>7)return false;
        try
        {
            foreach(var dependency in dependencies)
            {
                if(dependency?.Digest?.Length!=32||dependency.Length<1||dependency.Length>Math.Max(MaximumRecord,16*1024*1024)||
                    !RebirthProductiveEarnedArtifact.SafePath(dependency.Path)||File.Exists(dependency.Path+".tmp"))return false;
                using(var stream=new FileStream(dependency.Path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
                    if(stream.Length!=dependency.Length||!sha.ComputeHash(stream).SequenceEqual(dependency.Digest)||
                        stream.Length!=dependency.Length||!RebirthProductiveEarnedArtifact.SafePath(dependency.Path))return false;
            }
            return true;
        }
        catch{return false;}
    }
    private static NativeChildDependency NativeChildDependencyFor(string path,byte[] bytes)
    {using(var sha=SHA256.Create())return new NativeChildDependency{Path=path,Length=bytes.Length,Digest=sha.ComputeHash(bytes)};}
    private static NativeChildDependency[] WithPendingDefinition(Evidence parent,NativeChildDependency[] dependencies)
    {
        if(parent.Dependencies==null)return dependencies;
        if(parent.Dependencies.Length<1||parent.Dependencies.Length>4||dependencies.Length+parent.Dependencies.Length>7)throw new InvalidDataException();
        return dependencies.Concat(parent.Dependencies).ToArray();
    }
    // Immutable records locate an attempted child only. The actual native
    // load/read/factory chain must still return its complete saved After and
    // exact child marker; this method never calls a live setter or world getter.
    private static bool TryReadInvokedNativeChild(Evidence parent,out Evidence child)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
        {child=null;return decoder!=null&&TryReadInvokedNativeChildCore(parent,out child);}
    }
    internal static bool TryReadStagedNativeChild(Evidence parent,Func<IDisposable> enterCallbacks,out Evidence child)
    {
        using(var callbacks=enterCallbacks?.Invoke())
        {child=null;return callbacks!=null&&TryReadInvokedNativeChildCore(parent,out child,true);}
    }
    private static bool TryReadInvokedNativeChildCore(Evidence parent,out Evidence child,bool unentered=false)
    {
        child=null;Evidence value=null;
        try
        {
            if(parent?.Terminal!=true||!parent.PendingNextWork||parent.PendingTimerAmbiguous||
                !parent.PrimaryCurrent()||!(parent.ToolsRebased?parent.Path.EndsWith(".tools-load",StringComparison.Ordinal):parent.Path.EndsWith(".terminal-load",StringComparison.Ordinal)))return false;
            string original=parent.Path.Substring(0,parent.Path.Length-(parent.ToolsRebased?".tools-load":".terminal-load").Length);
            foreach(string suffix in new[]{".partial-successor",".partial-successor.invoking"})
                if(File.Exists(original+suffix)||File.Exists(original+suffix+".tmp"))return false;
            string linkPath=original+".earned-child",invokingPath=linkPath+".invoking";
            if(File.Exists(linkPath+".tmp")||File.Exists(invokingPath+".tmp"))return false;
            byte[] link=Read(linkPath,164),invoking=null;
            if(link.Length!=164)return false;
            if(unentered){if(File.Exists(invokingPath))return false;}
            else{invoking=Read(invokingPath,164);if(!link.SequenceEqual(invoking))return false;}
            Guid birth,operation;byte[] parentHash,childHash,nativeHash;
            using(var stream=new MemoryStream(link,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31434552u||new Guid(reader.ReadBytes(16))!=parent.Frame.Birth||
                    new Guid(reader.ReadBytes(16))!=parent.Frame.Operation)return false;
                birth=new Guid(reader.ReadBytes(16));operation=new Guid(reader.ReadBytes(16));
                parentHash=reader.ReadBytes(32);childHash=reader.ReadBytes(32);nativeHash=reader.ReadBytes(32);
                if(birth==Guid.Empty||operation==Guid.Empty||stream.Position!=stream.Length)return false;
            }
            using(var sha=SHA256.Create())
                if(!sha.ComputeHash(parent.Original).SequenceEqual(parentHash)||
                    !RebirthProductiveEarnedNativeBody.ComparisonDigest(parent.Native,parent.NativeOffset).SequenceEqual(nativeHash))return false;
            string path=Path.Combine(parent.Directory,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            if(unentered)
                foreach(string suffix in new[]{".earned-load",".terminal-load",".successor-load"})
                    if(File.Exists(path+suffix)||File.Exists(path+suffix+".tmp"))return false;
            long length=new FileInfo(path).Length;
            if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var retention))return false;
            value=new Evidence(retention){Path=path,Directory=parent.Directory,WorldGuid=parent.WorldGuid,MarkerKey=parent.MarkerKey};
            value.Image=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            using(var sha=SHA256.Create())if(!sha.ComputeHash(value.Image).SequenceEqual(childHash))return false;
            value.Frame=RebirthProductiveEarnedRowProof.Decode(value.Image);
            var frame=value.Frame;
            if(parent.PendingRecipeBinding!=null&&(frame.FullRecipeBinding!=parent.PendingRecipeBinding||frame.ExtrasDefinition!=parent.PendingExtras))return false;
            if(!frame.HasNativeBodies||!frame.HasRecipeDefinition||frame.Birth!=birth||frame.Operation!=operation||frame.World!=parent.Frame.World||
                frame.RowIndex!=parent.PendingTimerIndex||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(frame.BeforeNative,frame.BeforeElapsedOffset,
                    parent.Native,parent.NativeOffset))return false;
            // Reserve actual native graph/parser work before decoding the row
            // and tool graphs. The existing shared ceiling remains unchanged.
            if(!RebirthProductiveEarnedNativeBody.TryMeasure(frame.AfterNative,frame.AfterElapsedOffset,out long graph,out long scratch)||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(retention,
                    checked(8L*length+4L*frame.AfterNative.Length+3*graph+scratch+65536)))return false;
            if(!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out var before,out var after,out _,out _)||
                before.CraftingTimeLeft!=parent.PendingTimerBefore||
                !RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(parent.PendingTimerBefore,parent.PendingTimerTarget,
                    before.OneItemCraftTime,out float work,out float target)||frame.WorkedSeconds!=work||after.CraftingTimeLeft!=target)return false;
            value.Original=value.Image;value.Marker=RebirthProductiveEarnedRowProof.Marker(frame,value.Image);
            value.Native=frame.AfterNative;value.NativeOffset=frame.AfterElapsedOffset;
            if(unentered)value.StagedChildLink=link;
            value.Dependencies=WithPendingDefinition(parent,unentered?
                new[]{NativeChildDependencyFor(parent.Path,parent.Image),NativeChildDependencyFor(linkPath,link)}:
                new[]{NativeChildDependencyFor(parent.Path,parent.Image),NativeChildDependencyFor(linkPath,link),NativeChildDependencyFor(invokingPath,invoking)});
            value.Load=new CachedLoadRequest{Custody=value,Directory=value.Directory,X=parent.Load.X,Z=parent.Load.Z,
                Snapshot=Array.Empty<byte>(),Tile=new byte[checked(value.Native.Length+10)],PredictedNative=true,
                MaximumGraph=graph,MaximumScratch=scratch};
            if(!value.PrimaryCurrent()||!parent.PrimaryCurrent()||unentered&&(File.Exists(invokingPath)||File.Exists(invokingPath+".tmp")))return false;
            child=value;value=null;return true;
        }
        catch{return false;}
        finally{value?.Dispose();}
    }
}
