using System;
using System.IO;
using System.Linq;

// Native closed-loop completion has no early owning-UI reward. The station's
// actual saved output/queue/CraftCompleteList is its handoff to native owner
// consumption. This observer never calls AddCraftComplete, GiveExp or a setter.
internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static void PumpNativeTerminals()
    {
        if(!Enabled||!ThreadManager.IsMainThread())return;ObserveWorld();
        PumpLoadedNativeCleanup();
        RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();
        foreach(var original in retained.Values.ToArray())
            if(original.OutputTimerObserved)TryFinishNativeTerminal(original);
    }
    internal sealed class NativeTerminal:IDisposable
    {
        internal Attempt Original;
        internal World World;
        internal byte[] Body;
        internal int ElapsedOffset;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal bool Closed;
        internal bool PendingNextWork;
        internal bool SavedPending;
        internal RebirthProductiveEarnedSavedNativeSource.Evidence SavedOrigin;
        internal Func<bool> RemoteSavedSource;
        internal bool RemoteToolsRebased;
        internal byte[] ToolRebaseRecord;internal IDisposable ToolRebaseRetention;
        internal bool ToolRebaseCurrent()=>RemoteToolsRebased&&RemoteSavedSource?.Invoke()==true||SavedOrigin?.ToolsRebased==true&&SavedOrigin.PrimaryCurrent()||ToolRebaseRecord!=null&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".tools-rebased",ToolRebaseRecord)&&
            RebirthProductiveEarnedSavedNativeSource.ToolsPublicationCurrent(Original.Artifact.Birth,Original.Artifact.Operation);
        internal bool Current()
        {
            try
            {
                using(var callbacks=Memory?.TryEnterCallbacks())
                    return callbacks!=null&&!Closed&&Original?.OutputTimerObserved==true&&
                        ReferenceEquals(World,GameManager.Instance?.World)&&World?.IsRemote()==false&&
                        Original.Artifact?.Current()==true&&TryNativeMarker(Original.Station,out var marker)&&
                        (ToolRebaseRecord==null||ToolRebaseCurrent())&&(RemoteSavedSource==null||RemoteSavedSource())&&
                        Original.Artifact.MatchesMarker(marker)&&
                        (SavedOrigin==null||SavedOrigin.CurrentIdentity(World,Original.Station,Original.Artifact.Birth,Original.Artifact.Operation))&&
                        RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Body,ElapsedOffset);
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(Closed)return;Closed=true;var watch=Watch;Watch=null;var allocation=Allocation;Allocation=null;var memory=Memory;Memory=null;
            var toolRetention=ToolRebaseRetention;ToolRebaseRetention=null;ToolRebaseRecord=null;
            Body=null;Original=null;SavedOrigin=null;RemoteSavedSource=null;
            try{watch?.Dispose();}finally{try{allocation?.Dispose();}finally{try{memory?.Dispose();}finally{toolRetention?.Dispose();}}}
        }
    }
    internal static bool IsNativeTerminalTimer(TileEntityWorkstation station,RecipeQueueItem row)
        =>Enabled&&retained.TryGetValue(station,out var original)&&original.OutputTimerObserved&&original.NativeOutput!=null&&
            ReferenceEquals(original.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(station))&&
            ReferenceEquals(original.Row,row)&&ReferenceEquals(original.Recipe,row.Recipe)&&row.Multiplier==original.Multiplier-1;
    internal static void ObserveDeferredNativeTimer(TileEntityWorkstation station,RecipeQueueItem row,float before,float target)
    {
        if(!Enabled||!retained.TryGetValue(station,out var original)||!original.OutputTimerObserved||original.NativeOutput==null||
            !ReferenceEquals(original.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(station))||
            row?.Recipe==null||row.Multiplier<=0||float.IsNaN(target)||float.IsInfinity(target)||target>=before)return;
        int index=-1;for(int i=0;i<station.Queue.Length;i++)if(ReferenceEquals(station.Queue[i],row)){if(index!=-1){original.PendingTimerAmbiguous=true;return;}index=i;}
        if(index<0)return;
        if(original.PendingTimerObserved&&(original.PendingTimerIndex!=index||original.PendingTimerBefore!=before||original.PendingTimerTarget!=target))
        {original.PendingTimerAmbiguous=true;return;}
        if(!CapturePendingDefinition(original,row)){original.PendingTimerAmbiguous=true;return;}
        original.PendingTimerObserved=true;original.PendingTimerIndex=index;original.PendingTimerBefore=before;original.PendingTimerTarget=target;
    }

    internal static void ObserveNativeReturned(TileEntityWorkstation station,bool returned)
    {
        RebirthProductiveEarnedRemoteClient.ObserveNativeOutputReturned(station,returned);
        if(!Enabled||!RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||
            !retained.TryGetValue(station,out var original)||!original.OutputTimerObserved)return;
        // An exception after an insertion is retained unknown. It cannot mint a
        // later current-state terminal or another native output attempt.
        if(!returned||!ReferenceEquals(original.NativeOutput,RebirthRecyclerNativeLoop.InvocationToken(station)))return;
        if(station is RebirthCamperStationState){original.MobileOutputReturned=true;return;}
        if(original.Terminal==null&&DeferNativeTerminalToOuterTick(station,original,original.NativeOutput))return;
        CaptureReturnedNativeTerminal(original);
    }
    private static void CaptureReturnedNativeTerminal(Attempt original)
    {
        var station=original.Station;
        if(original.Terminal!=null){TryFinishNativeTerminal(original);return;}
        NativeTerminal terminal=null;
        try
        {
            if(GameManager.Instance?.World?.IsRemote()!=false||station is RebirthCamperStationState||original.Artifact?.Current()!=true)return;
            original.Observation?.Dispose();original.Observation=null;
            byte[] body;int offset;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out body,out offset))return;
                // A genuine enclosing UpdateTick captures after its returned
                // material/body work. A standalone real remote queue handler
                // retains its own immediate native invocation boundary.
            }
            terminal=new NativeTerminal{Original=original,World=GameManager.Instance.World,Body=body,ElapsedOffset=offset};
            var next=station.Queue?.LastOrDefault();
            terminal.PendingNextWork=original.PendingTimerObserved||original.PendingTimerAmbiguous||
                next?.Recipe!=null&&next.Multiplier>0&&next.CraftingTimeLeft<=0&&!RebirthRecyclerNativeLoop.HasObservedEarned(station,next);
            terminal.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(original.Artifact,original.Frame);
            if(terminal.Memory==null)return;
            terminal.Allocation=terminal.Memory.TryObserveNative(body,offset);
            if(terminal.Allocation==null||!terminal.Current()||
                !RebirthRemoteResourceNativeBinding.TryResolve(terminal.World,RemoteResourceIdentity.Workstation(station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,original.Artifact.CustodyBirth.ToString("N"),out var binding))return;
            var p=station.localChunkPos;string key="RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z;
            terminal.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,terminal.Current,terminal.Memory,key);
            if(terminal.Watch==null||!terminal.Current())return;
            original.Terminal=terminal;terminal=null;
            original.Observation?.Dispose();original.Observation=null;
            TryFinishNativeTerminal(original);
        }
        catch{/* Retain original/marker and attempted output; no replay. */}
        finally{terminal?.Dispose();}
    }
    private static bool TryFinishNativeTerminal(Attempt original)
    {
        if(original?.Station is RebirthCamperStationState)return PumpMobileTerminal(original);
        if(original?.Artifact!=null&&(RebirthProductiveEarnedRemoteHost.NativeViewerToolsRuntimeHeld(original.Station)||RebirthProductiveEarnedRemoteHost.NativeViewerToolsHeld(original.Artifact.OriginalPath)))return false;
        if(original?.ClearedNativeMarker==true)return TryFinishClearedNativeMetadata(original);
        if(original?.PartialSuccessor!=null)return TryFinishPartialNativeSuccessor(original);
        if(original?.EarnedChild?.Entered==true)return TryFinishEarnedNativeChild(original);
        var terminal=original?.Terminal;
        try
        {
            if(terminal?.SavedOrigin!=null||terminal?.SavedPending==true)
            {
                if(!RebirthProductiveEarnedRemoteHost.ObserveNativeInheritedCompletion(original.Station,original.Artifact,terminal.Body,terminal.ElapsedOffset,terminal.Memory))return false;
                if(!terminal.PendingNextWork)return false;
                return ContinueSavedNativePending(original);
            }
            if(terminal?.PendingNextWork==true&&!PublishPendingDefinition(original))return false;
            if(terminal?.Current()!=true||terminal.Watch==null||
                !terminal.Watch.TryReadPublishedTileDescriptor(out var publication,out long tileOffset,out int tileLength,out string tileDigest)||
                !terminal.Current())return false;
            if(!RebirthProductiveEarnedSavedNativeSource.Publish(original.Artifact,terminal.Memory,terminal.Watch,
                terminal.Body,terminal.ElapsedOffset,true,terminal.Current,terminal.PendingNextWork,
                original.PendingTimerIndex,original.PendingTimerBefore,original.PendingTimerTarget,original.PendingTimerAmbiguous))return false;
            using(var callbacks=terminal.Memory.TryEnterCallbacks())
            {
                if(callbacks==null||!terminal.Memory.TryReserveAllocation(checked(4L*(original.Artifact.ImageLength+terminal.Body.Length+8192)),out var workspace))return false;
                using(workspace)
                    if(!original.Artifact.TryPublishNativeTerminal(terminal.Body,terminal.ElapsedOffset,
                        publication,tileOffset,tileLength,tileDigest))return false;
            }
            if(terminal.PendingNextWork)
            {
                if(!RebirthProductiveEarnedRemoteHost.ObserveNativeInheritedCompletion(original.Station,original.Artifact,terminal.Body,terminal.ElapsedOffset,terminal.Memory))return false;
                terminal.SavedPending=true;return ContinueSavedNativePending(original);
            }
            if(!RebirthProductiveEarnedRemoteHost.ObserveNativeInheritedCompletion(original.Station,original.Artifact,terminal.Body,terminal.ElapsedOffset,terminal.Memory))return false;
            if(!terminal.Current())return false;
            byte[] completedMarker=original.Artifact.Marker(original.Frame);
            var retention=original.Artifact.CaptureCompletionRetention(completedMarker);
            if(retention==null||!ClearExactMarker(original.Station,completedMarker))return false;
            original.CompletionRetention=retention;original.ClearedNativeMarker=true;
            return TryFinishClearedNativeMetadata(original);
        }
        catch{return false;}
    }
    private static bool ContinueSavedNativeSuccessor(Attempt original)
    {
        // A native gesture journal owns the replacement boundary, including
        // an uncertain preparation. Ordinary successor fallback cannot bypass it.
        string gestures=original.Artifact.OriginalPath+".tool-gesture";
        if((File.Exists(gestures)||File.Exists(gestures+".tmp"))&&original.Terminal?.ToolRebaseCurrent()!=true)return false;
        if(TryApplyPartialNativeSuccessor(original))return true;
        // A retained partial plan owns this continuation even if parsing or
        // application refuses. Never prepare a different child around it.
        string partial=original.Artifact.OriginalPath+".partial-successor";
        if(original.PartialSuccessor!=null||File.Exists(partial)||File.Exists(partial+".tmp")||
            File.Exists(partial+".invoking")||File.Exists(partial+".invoking.tmp"))return false;
        if(TryPrepareEarnedNativeChild(original))TryApplyEarnedNativeChild(original);
        return false; // Child preparation/application is not a native output permit.
    }
    private static bool TryRetireObservedNativeTerminal(TileEntityWorkstation station)
    {
        try
        {
            if(!RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||!source.Terminal||source.PendingNextWork||
                !source.CurrentIdentity(GameManager.Instance?.World,station,source.Frame.Birth,source.Frame.Operation))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset)||
                    !RebirthProductiveEarnedRemoteHost.ObserveLoadedNativeInheritedCompletion(station,source)||
                    !ClearExactMarker(station,source.Marker))return false;
            }
            RebirthProductiveEarnedSavedNativeSource.Release(source);return true;
        }
        catch{return false;}
    }
}
