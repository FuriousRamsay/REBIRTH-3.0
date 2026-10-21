using System;
using System.IO;

internal static partial class RebirthProductiveEarnedCoordinator
{
    // A saved predecessor observation for native gesture capture only. This
    // object grants no owner transfer, publication, rebase or native setter.
    internal sealed partial class SavedPendingToolsObservation:IDisposable
    {
        private Attempt original;private NativeTerminal terminal;
        private RebirthProductiveEarnedArtifact.Prepared artifact;
        private World world;private byte[] originalBody;
        private IDisposable memory;
        internal readonly Guid Birth,Operation;
        private byte[] originalDigest,nativeBefore;
        internal byte[] CopyOriginalDigest()=>memory==null?null:(byte[])originalDigest?.Clone();
        internal byte[] CopyNativeBefore()=>memory==null?null:(byte[])nativeBefore?.Clone();
        internal string OriginalPath=>memory==null?null:artifact?.OriginalPath;
        internal RebirthProductiveEarnedArtifact.Prepared OriginalArtifact=>memory==null?null:artifact;
        internal byte[] CopyOriginalImage()=>memory==null?null:artifact.CopyImage();
        internal readonly int Offset,Index;internal readonly float BeforeTimer,TargetTimer;
        internal string Binding {get;private set;}internal string Extras {get;private set;}
        internal SavedPendingToolsObservation(Attempt source,IDisposable retained)
        {
            original=source;terminal=source.Terminal;mobileTerminal=source.MobileTerminal;artifact=source.Artifact;world=GameManager.Instance.World;
            originalBody=mobileTerminal?.Body??terminal.Body;
            memory=retained;Birth=artifact.Birth;Operation=artifact.Operation;originalDigest=artifact.OriginalDigest();
            nativeBefore=(byte[])originalBody.Clone();Offset=mobileTerminal!=null?0:terminal.ElapsedOffset;Index=source.PendingTimerIndex;
            BeforeTimer=source.PendingTimerBefore;TargetTimer=source.PendingTimerTarget;Binding=source.PendingRecipeBinding;Extras=source.PendingExtras;
        }
        internal bool RetainedContext()
        {
            if(mobileTerminal!=null)return MobileRetainedContext();
            try{return memory!=null&&Enabled&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            world?.IsRemote()==false&&retained.TryGetValue(original.Station,out var active)&&ReferenceEquals(active,original)&&
            ReferenceEquals(original.Terminal,terminal)&&ReferenceEquals(original.Artifact,artifact)&&ReferenceEquals(terminal.Body,originalBody)&&
            original.OutputTimerObserved&&!original.ClearedNativeMarker&&!original.PendingTimerAmbiguous&&original.PendingTimerObserved&&
            terminal.PendingNextWork&&(terminal.SavedPending||terminal.SavedOrigin!=null)&&original.PartialSuccessor==null&&original.EarnedChild==null&&
            original.PendingTimerIndex==Index&&original.PendingTimerBefore==BeforeTimer&&original.PendingTimerTarget==TargetTimer&&
            original.PendingRecipeBinding==Binding&&original.PendingExtras==Extras;}
            catch{return false;}
        }
        internal bool CurrentContext()
        {try{return RetainedContext()&&artifact.Current()&&!HasStagedToolSuccessor(artifact.OriginalPath);}catch{return false;}}
        internal bool MatchesBeforeTools(byte[] tools,RebirthProductiveEarnedObservationBudget.Decoder decoder)
        {
            if(mobileTerminal!=null)return MobileMatchesBeforeTools(tools,decoder);
            try{return decoder?.IsCurrent==true&&CurrentContext()&&terminal.Current()&&
                RebirthProductiveEarnedNativeBody.TryRead(nativeBefore,Offset,out var native,out _)&&
                RebirthProductiveEarnedArtifact.SameTools(native.Tools,tools);}
            catch{return false;}
        }
        public void Dispose()
        {
            var held=System.Threading.Interlocked.Exchange(ref memory,null);if(held==null)return;
            original=null;terminal=null;mobileTerminal=null;artifact=null;world=null;originalBody=nativeBefore=originalDigest=null;Binding=Extras=null;
            held.Dispose();
        }
    }
    private static bool HasStagedToolSuccessor(string original)
    {
        foreach(string suffix in new[]{".partial-successor",".partial-successor.invoking",".earned-child",".earned-child.invoking"})
            if(File.Exists(original+suffix)||File.Exists(original+suffix+".tmp"))return true;
        return false;
    }
    internal static bool TryObserveSavedPendingTools(TileEntityWorkstation station,
        RebirthProductiveEarnedObservationBudget.Decoder decoder,out SavedPendingToolsObservation observation)
    {
        observation=null;IDisposable memory=null;SavedPendingToolsObservation value=null;
        try
        {
            if(station is RebirthCamperStationState)return TryObserveMobilePendingTools(station,decoder,out observation);
            if(!Enabled||decoder?.IsCurrent!=true||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||
                !retained.TryGetValue(station,out var original)||!original.OutputTimerObserved||original.ClearedNativeMarker||
                original.PendingTimerAmbiguous||!original.PendingTimerObserved||original.PartialSuccessor!=null||original.EarnedChild!=null||
                original.Terminal?.PendingNextWork!=true||!(original.Terminal.SavedPending||original.Terminal.SavedOrigin!=null)||
                original.Terminal.Body==null||original.Terminal.Current()!=true||original.Artifact?.Current()!=true||
                original.PendingTimerIndex<0||original.PendingTimerIndex!=station.Queue.Length-1||
                !MatchesPendingDefinition(original,station.Queue[original.PendingTimerIndex])||HasStagedToolSuccessor(original.Artifact.OriginalPath))return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*original.Terminal.Body.Length+4L*original.Artifact.ImageLength+65536),out memory))return false;
            value=new SavedPendingToolsObservation(original,memory);memory=null;
            if(!value.CurrentContext())return false;observation=value;value=null;return true;
        }
        catch{return false;}
        finally{value?.Dispose();memory?.Dispose();}
    }
}
