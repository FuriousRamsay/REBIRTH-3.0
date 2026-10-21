using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // This token owns an original native packet, not a permission to replay a
    // later snapshot. Admission remains disabled until the complete remote
    // native source has returned its private nonce and source digest.
    private sealed class RemoteNativeSetup
    {
        internal RebirthProductiveEarnedUiQueueOwner.ActualPendingAdd Call;
        internal NativeAddOriginal Source;internal NativeViewerEffectSource Effect;internal CachedPhysicalSource Cached;internal Frame Frame;internal RemoteNativePhase Phase;
        internal RebirthRecipeOutputIntegration.RemoteOutputColdClient LoadedDeferred;
        internal TileEntityWorkstation Station=>Cached?.Station??Frame.Observation.Station;
        internal Guid Operation=>Cached?.Operation??Frame.Observation.Operation;
        internal Guid Nonce=>Cached?.Action??Frame.RemoteNativeNonce;
        internal CachedOutputTransfer Transfer;
        internal CachedPhysicalSpace CachedSpace;
        internal CachedUnusedClose CachedClose;
        internal NetPackageTileEntity Packet;internal ulong Clock,First,Last;
        internal byte Handle;internal int Block,ElapsedOffset=-1;internal Vector3i Position;
        internal bool Ran;
        internal bool Current()=>CachedClose!=null?CachedClose.Current()&&ReferenceEquals(closingCachedUnused,CachedClose)&&CachedClose.SyncEntered&&
            Packet.handle==Handle&&Packet.teBlockId==Block&&Packet.teWorldPos.Equals(Position):CachedSpace!=null?Cached?.Current()==true&&CachedSpace.Current()&&ReferenceEquals(CachedSpace.Source,Cached)&&
            Packet.handle==Handle&&Packet.teBlockId==Block&&Packet.teWorldPos.Equals(Position):Frame!=null&&(Cached!=null?Source==null&&Effect==null&&LoadedDeferred==null&&ReferenceEquals(Frame.CachedPhysical,Cached)&&Cached.Ready&&Cached.Current()&&
                (!IsRemoteOutputQueueOnly(Frame)||Phase==RemoteNativePhase.QueueAccepted):Source!=null?Effect==null&&LoadedDeferred==null&&ReferenceEquals(Frame.NativeOriginal,Source)&&Source.SourceCurrentWithoutRow():LoadedDeferred!=null?Effect==null&&ReferenceEquals(Frame.Observation.RemoteOutputRestart,LoadedDeferred)&&IsLoadedDeferredFrame(Frame)&&
                (Phase==RemoteNativePhase.QueueAccepted||!LoadedDeferred.FinalCallbackSaved&&
                    (Phase==RemoteNativePhase.PhysicalAccepted||Phase==RemoteNativePhase.CachedBoundary&&IsCurrentRemoteCachedBoundary(Frame)||
                        Phase==RemoteNativePhase.CachedSpaceTransfer&&Transfer?.Current()==true)):Effect!=null&&ReferenceEquals(Frame.NativeViewerEffect,Effect)&&Effect.SourceCurrentWithoutRow())&&
            Frame.RemoteNativeNonce!=Guid.Empty&&!string.IsNullOrEmpty(Frame.RemoteNativeSourceHash)&&
            (Phase==RemoteNativePhase.CachedBoundary?ReferenceEquals(selectingCachedBoundary,Frame)&&IsCurrentRemoteCachedBoundary(Frame)||IsSavedCachedBoundaryResume(this):Call!=null?Call.Current():Transfer!=null?ReferenceEquals(cachedOutputTransfer,Transfer)&&Transfer.Current()||IsSavedEffectTransferResume(this):ReferenceEquals(current,Frame)&&RebirthRecipeOutputIntegration.IsActualOwningUpdate(Frame.Observation.Card)||ReferenceEquals(remoteNativeSync?.Frame,Frame)||IsSavedNativeViewerResume(Frame,Packet))&&
            Packet.handle==Handle&&Packet.teBlockId==Block&&Packet.teWorldPos.Equals(Position);
    }
    private sealed class PendingNativeUpload
    {
        internal RemoteNativeSetup Setup;internal byte[] Body;internal IDisposable Allocation,SourceLease,TransferLease;
        internal bool Flush,NativeHold;internal RebirthProductiveEarnedUiQueueOwner.DeferredReceipt Receipt;
        internal bool Ended,Unknown;
    }
    private static readonly ConditionalWeakTable<NetPackageTileEntity,RemoteNativeSetup> remoteNativeSetups=new ConditionalWeakTable<NetPackageTileEntity,RemoteNativeSetup>();
    private static readonly List<PendingNativeUpload> pendingNativeUploads=new List<PendingNativeUpload>();
    private static bool remoteNativeSendObserved;
    [ThreadStatic]private static PendingNativeUpload resumingNativeUpload;
    [ThreadStatic]private static NativeSync remoteNativeSync;
    private static readonly Dictionary<Guid,RebirthProductiveEarnedUiQueueOwner.DeferredReceipt> remoteNativeReceipts=new Dictionary<Guid,RebirthProductiveEarnedUiQueueOwner.DeferredReceipt>();
    private static void ForgetRemoteNativeReceipt(NativeAddOriginal original)
    {if(remoteNativeReceipts.TryGetValue(original.Operation,out var receipt)&&ReferenceEquals(receipt.NativeOriginal,original))remoteNativeReceipts.Remove(original.Operation);}
    internal static void BeginRemoteNativeSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        ForgetRemoteNativeSetup(packet);
        if(!ThreadManager.IsMainThread()||mode!=StreamModeWrite.ToServer||tile is not TileEntityWorkstation station)return;
        if(closingCachedUnused?.Current()==true&&closingCachedUnused.SyncEntered&&ReferenceEquals(closingCachedUnused.Source.Station,station))
        {
            var close=closingCachedUnused;close.Setups++;
            remoteNativeSetups.Add(packet,new RemoteNativeSetup{Packet=packet,Cached=close.Source,CachedClose=close,Frame=close.Source.Frame,
                Phase=RemoteNativePhase.CachedUnusedClose,Clock=station.lastTickTime,First=GameTimer.Instance.ticks,
                Handle=station.lockHandleWaitingFor,Block=station.blockValue.type,Position=station.ToWorldPos()});return;
        }
        if(cachedPhysicalSpace?.Current()==true&&ReferenceEquals(cachedPhysicalSpace.Source.Station,station))
        {
            var physical=cachedPhysicalSpace.Source;
            remoteNativeSetups.Add(packet,new RemoteNativeSetup{Packet=packet,Cached=physical,CachedSpace=cachedPhysicalSpace,Frame=physical.Frame,
                Phase=RemoteNativePhase.CachedSpaceTransfer,Clock=station.lastTickTime,First=GameTimer.Instance.ticks,
                Handle=station.lockHandleWaitingFor,Block=station.blockValue.type,Position=station.ToWorldPos()});return;
        }
        RebirthProductiveEarnedUiQueueOwner.TryGetActualPendingAdd(station,out var call);
        var frame=selectingCachedBoundary??(call?.NativeOriginal!=null?current:cachedOutputTransfer?.Frame??remoteNativeSync?.Frame??current);var source=call?.NativeOriginal??frame?.NativeOriginal;
        NativeViewerEffectSource effect=null;if(source==null&&frame!=null)TryGetNativeViewerEffect(frame,out effect);
        bool loadedDeferred=source==null&&effect==null&&IsLoadedDeferredFrame(frame);
        if(frame==null||source==null&&effect==null&&frame.CachedPhysical==null&&!loadedDeferred||
            loadedDeferred&&(call!=null||frame.Observation.RemoteOutputRestart.FinalCallbackSaved&&(cachedOutputTransfer!=null||selectingCachedBoundary!=null)||
                cachedOutputTransfer!=null&&!ReferenceEquals(cachedOutputTransfer.Frame,frame)||selectingCachedBoundary!=null&&!IsCurrentRemoteCachedBoundary(frame))||
            !frame.Observation.World.IsRemote()||!ReferenceEquals(frame.Observation.Station,station)||frame.RemoteNativeNonce==Guid.Empty||string.IsNullOrEmpty(frame.RemoteNativeSourceHash))return;
        if(!remoteNativeSendObserved)
        {
            remoteNativeSendObserved=true;
            RebirthProductiveEarnedUiQueueOwner.ActualAddObserved+=ObservePendingNativeAdd;
            RebirthProductiveEarnedUiQueueOwner.ActualAddEnded+=EndPendingNativeAdd;
        }
        remoteNativeSetups.Add(packet,new RemoteNativeSetup{Packet=packet,Call=source==null?null:call,Source=source,Effect=effect,Cached=frame.CachedPhysical,Frame=frame,
            LoadedDeferred=loadedDeferred?frame.Observation.RemoteOutputRestart:null,
            Transfer=cachedOutputTransfer,Phase=selectingCachedBoundary!=null?RemoteNativePhase.CachedBoundary:call!=null?RemoteNativePhase.ReceiptAccepted:cachedOutputTransfer!=null?RemoteNativePhase.CachedSpaceTransfer:remoteNativeSync!=null?RemoteNativePhase.QueueAccepted:RemoteNativePhase.PhysicalAccepted,
            Clock=station.lastTickTime,First=GameTimer.Instance.ticks,
            Handle=station.lockHandleWaitingFor,Block=station.blockValue.type,Position=station.ToWorldPos()});
    }
    internal static void ObserveRemoteNativeSetup(NetPackageTileEntity packet,bool run)
    {
        if(!remoteNativeSetups.TryGetValue(packet,out var source))return;
        source.Handle=packet.handle;source.Last=GameTimer.Instance.ticks;source.Ran=run&&source.Station.lastTickTime==source.Clock&&packet.handle==source.Station.lockHandleWaitingFor&&
            source.Current()&&source.First>=source.Clock&&source.Last>=source.First;
        if(!source.Ran)return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(source.Station,out var body,out int offset)||
                    packet.ms==null||packet.ms.Length!=body.Length){source.Ran=false;return;}
                lock(packet.ms)
                {
                    long prior=packet.ms.Position;ulong elapsed=0;
                    try{packet.ms.Position=0;for(int i=0;i<body.Length;i++){int next=packet.ms.ReadByte();if(next<0){source.Ran=false;return;}
                        if(i>=offset&&i<offset+8)elapsed|=(ulong)(byte)next<<(8*(i-offset));else if(next!=body[i]){source.Ran=false;return;}}}
                    finally{packet.ms.Position=prior;}
                    source.Ran=elapsed>=source.First-source.Clock&&elapsed<=source.Last-source.Clock;
                }
                if(source.Ran)source.ElapsedOffset=offset;
            }
        }
        catch{source.Ran=false;}
    }
    internal static void ForgetRemoteNativeSetup(NetPackage packet)
    {if(packet is NetPackageTileEntity native)remoteNativeSetups.Remove(native);}
    internal static bool TryHoldPendingNativeAdd(NetPackage packet,bool flush,bool run)
    {
        if(ReferenceEquals(resumingNativeUpload?.Setup?.Packet,packet))return false;
        if(!run||packet is not NetPackageTileEntity native||!remoteNativeSetups.TryGetValue(native,out var source)||!source.Current())return false;
        if(source.Phase==RemoteNativePhase.PhysicalAccepted&&IsRemoteOutputQueueOnly(source.Frame))
        {
            // The authenticated final owner PDS already saved this physical
            // effect. Suppress only this genuine never-enqueued redundant
            // native packet; the actual queue packet remains independently owned.
            if(native.inSendQueuesCount==0)DiscardUnqueuedNative(native);
            return true;
        }
        // A positively known admitted invocation must not escape when its
        // detached budget is busy. If complete custody cannot be charged,
        // discard only the never-enqueued packet and retain the unknown
        // original action; no financial evidence follows from suppression.
        if(pendingNativeUploads.Exists(value=>ReferenceEquals(value.Setup.Packet,native)))return true;
        if(!source.Ran||native.inSendQueuesCount!=0||native.ms==null||native.ms.Length<8||native.ms.Length>2*1024*1024||pendingNativeUploads.Count>=4)
        {DiscardUnqueuedNative(native);return true;}
        IDisposable allocation=null,lease=null,transferLease=null;bool held=false;
        try
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*native.ms.Length+65536),out allocation)){DiscardUnqueuedNative(native);return true;}
            if(source.LoadedDeferred!=null)
            {if(!TryCopyLoadedDeferredContribution(source.Frame,out _,out lease)){DiscardUnqueuedNative(native);return true;}}
            else lease=source.CachedClose!=null?source.CachedClose.Source.Grant.Retain():source.Cached!=null?source.Cached.Retain():source.Source!=null?source.Source.RetainForCompletion():source.Effect?.RetainForCompletion();
            if(lease==null){DiscardUnqueuedNative(native);return true;}
            if(source.CachedSpace!=null){transferLease=source.CachedSpace.Retain();if(transferLease==null){DiscardUnqueuedNative(native);return true;}}
            if((source.Effect!=null||source.LoadedDeferred!=null)&&source.Transfer!=null){transferLease=source.Transfer.Retain();if(transferLease==null){DiscardUnqueuedNative(native);return true;}}
            byte[] body;
            lock(native.ms)
            {
                long position=native.ms.Position;
                try
                {
                    native.ms.Position=0;body=new byte[checked((int)native.ms.Length)];int offset=0;
                    while(offset<body.Length){int read=native.ms.Read(body,offset,body.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}
                }
                finally{native.ms.Position=position;}
            }
            // Only the native queue reference counter may pin this pooled
            // object across the synchronous caller. It is balanced only by a
            // future exact selected-send completion or world abandonment.
            native.RegisterSendQueue();held=true;
            var pending=new PendingNativeUpload{Setup=source,Body=body,Allocation=allocation,SourceLease=lease,TransferLease=transferLease,Flush=flush,NativeHold=true};
            pendingNativeUploads.Add(pending);allocation=lease=transferLease=null;held=false;
            // The close sync must actually enqueue before native Unlock.
            // Its same-envelope header arms the host while the original lock
            // is still live; returned OnClose is a later metadata witness.
            if(source.CachedClose!=null)ResumePendingNative(pending);
            return true;
        }
        catch{if(!held)DiscardUnqueuedNative(native);return true;}
        finally{if(held)native.SendQueueHandled();transferLease?.Dispose();lease?.Dispose();allocation?.Dispose();}
    }
    private static void DiscardUnqueuedNative(NetPackageTileEntity packet)
    {
        // No output/reward is replayed after failure to retain a complete
        // proof. Only this exact never-enqueued pooled packet may be freed.
        remoteNativeSetups.Remove(packet);if(packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);
    }
    private static void ObservePendingNativeAdd(RebirthProductiveEarnedUiQueueOwner.ActualPendingAdd call)
    {
        if(!call.Current()||call.PositiveToken?.CurrentAggregateMembership()!=true)return;
        if(call.NativeOriginal!=null)remoteNativeReceipts[call.NativeOriginal.Operation]=call.PositiveToken;
        foreach(var pending in pendingNativeUploads.ToArray())
            if(ReferenceEquals(pending.Setup.Call,call))
            {
                pending.Receipt=call.PositiveToken;
                ResumePendingNative(pending);
            }
    }
    private static void ResumePendingNative(PendingNativeUpload pending)
    {
        if(resumingNativeUpload!=null||pending.Unknown)return;
        resumingNativeUpload=pending;
        try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(pending.Setup.Packet,pending.Flush);}
        catch{pending.Unknown=true;}
        finally{resumingNativeUpload=null;}
    }
    private static void ResumePhysicalNativePackets(Frame frame,RemoteNativePhase phase)
    {
        if(phase==RemoteNativePhase.PhysicalAccepted&&IsRemoteOutputQueueOnly(frame))return;
        if(!NativeViewerAllowsPhysical(frame))return;
        if(phase==RemoteNativePhase.CachedSpaceTransfer&&(!frame.CachedBoundaryEnqueued||frame.CachedBoundaryVersion!=frame.SavedFullBoundaryVersion))return;
        if((phase==RemoteNativePhase.CachedSpaceTransfer||phase==RemoteNativePhase.CachedBoundary)&&frame.NativeViewerEffect!=null&&!NativeViewerCachedEffectSaved(frame))return;
        RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt=null;
        if(frame.NativeOriginal!=null){if(!remoteNativeReceipts.TryGetValue(frame.NativeOriginal.Operation,out receipt)||receipt.CurrentAggregateMembership()!=true)return;}
        else if(frame.CachedPhysical!=null){if(!frame.CachedPhysical.Ready||!frame.CachedPhysical.Current())return;}
        else if(frame.NativeViewerEffect?.SourceCurrentWithoutRow()!=true&&!IsLoadedDeferredFrame(frame))return;
        foreach(var pending in pendingNativeUploads.ToArray())if(ReferenceEquals(pending.Setup.Frame,frame)&&pending.Setup.Phase==phase)
        {pending.Receipt=receipt;ResumePendingNative(pending);}
    }
    private static bool IsRemoteOutputQueueOnly(Frame frame)
        =>frame?.Observation.RemoteOutputRestart?.FinalCallbackSaved==true&&frame.Observation.RemoteOutputRestart.QueueOnlyAuthorized&&frame.Observation.RemoteOutputRestart.BindingCurrent();
    private static bool IsSavedEffectTransferResume(RemoteNativeSetup setup)
    {
        var transfer=setup.Transfer;
        if((setup.Effect==null&&(setup.LoadedDeferred==null||setup.LoadedDeferred.FinalCallbackSaved||!IsLoadedDeferredFrame(setup.Frame)))||
            transfer==null||!transfer.InvocationEnded||!transfer.Observed||transfer.Evidence==null||
            !ReferenceEquals(resumingNativeUpload?.Setup,setup)||resumingNativeUpload.TransferLease==null||
            setup.Effect!=null&&!NativeViewerCachedEffectSaved(setup.Frame)||!transfer.Current())return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return decoder!=null&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(setup.Station,transfer.After,transfer.AfterOffset);
    }
    internal static void EndRemoteNativeSync(NativeSync snapshot){if(ReferenceEquals(remoteNativeSync,snapshot))remoteNativeSync=null;EndSavedCachedEmptySync(snapshot?.CachedEmpty);if(snapshot!=null)snapshot.CachedEmpty=null;}
    private static void EndPendingNativeAdd(RebirthProductiveEarnedUiQueueOwner.ActualPendingAdd call,Exception exception)
    {
        foreach(var pending in pendingNativeUploads)if(ReferenceEquals(pending.Setup.Call,call))
        {pending.Ended=true;pending.Unknown=exception!=null||pending.Receipt==null;}
        // No automatic resend here: the authenticated unchanged-native
        // envelope must first own this exact captured packet and its proof.
    }
    private static void SweepPendingNativeWorld(World currentWorld)
    {
        foreach(var pending in pendingNativeUploads.ToArray())
        {
            if(ReferenceEquals(pending.Setup.Cached?.World??pending.Setup.Frame.Observation.World,currentWorld))continue;
            pendingNativeUploads.Remove(pending);var packet=pending.Setup.Packet;
            pending.Body=null;pending.Receipt=null;pending.Setup=null;pending.TransferLease?.Dispose();pending.TransferLease=null;
            pending.SourceLease?.Dispose();pending.SourceLease=null;
            pending.Allocation?.Dispose();pending.Allocation=null;
            if(pending.NativeHold){pending.NativeHold=false;packet.SendQueueHandled();}
        }
        foreach(var operation in new List<Guid>(remoteNativeReceipts.Keys))if(!ReferenceEquals(remoteNativeReceipts[operation].NativeOriginal?.World,currentWorld))remoteNativeReceipts.Remove(operation);
    }
}
