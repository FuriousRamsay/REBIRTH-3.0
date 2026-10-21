using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class CachedOutputTransfer:IDisposable
    {
        internal Frame Frame;internal XUiC_WorkstationOutputGrid Grid;internal XUiC_WorkstationWindowGroup Window;
        internal long Session,NativeVersion;internal object Connection;internal byte[] Before,After;internal int BeforeOffset,AfterOffset;
        internal string BagBefore,BeltBefore;internal XElement Evidence;internal IDisposable Allocation;internal bool Observed,Ended,InvocationEnded;
        private int references=1;private bool ownerReleased;
        internal bool Current()=>!Ended&&IsKnownRemoteCachedFrame(Frame)&&
            RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(Frame.Observation.Card,NativeVersion)&&
            ReferenceEquals(Grid.windowGroup?.Controller,Window)&&ReferenceEquals(Window.WorkstationData.TileEntity,Frame.Observation.Station)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var session)&&session==Session&&
            ReferenceEquals(CurrentNativeServerConnection(),Connection);
        internal IDisposable Retain(){if(Ended||ownerReleased)return null;references++;return new TransferLease(this);}
        private sealed class TransferLease:IDisposable{private CachedOutputTransfer source;internal TransferLease(CachedOutputTransfer value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        private void Release(){if(--references!=0)return;Ended=true;Evidence=null;Before=After=null;BagBefore=BeltBefore=null;Frame=null;Grid=null;Window=null;Connection=null;Allocation?.Dispose();Allocation=null;}
        public void Dispose(){InvocationEnded=true;if(ownerReleased)return;ownerReleased=true;Release();}
    }
    [ThreadStatic]private static CachedOutputTransfer cachedOutputTransfer;
    private static bool IsKnownRemoteCachedFrame(Frame frame)=>frame!=null&&(frame.NativeOriginal!=null?
        frame.NativeOriginal.SourceCurrentWithoutRow()&&frame.NativeOriginal.IsKnownCachedCard(frame.Observation.Card):
        (frame.NativeViewerEffect?.SourceCurrentWithoutRow()==true||IsLoadedDeferredCachedFrame(frame))&&frame.Observation.FullBoundary&&!frame.Observation.Continued&&
            !frame.Observation.Accepted&&!frame.NativeAccepted&&frame.ConfirmedOriginal==null);
    internal static object BeginRemoteCachedOutputTransfer(XUiC_WorkstationOutputGrid grid,ItemStack[] argument,bool run)
    {
        var local=BeginCachedPrimarySpace(grid,argument,run);if(local!=null)return local;
        var reentry=BeginCachedPhysicalSpace(grid,argument,run);if(reentry!=null)return reentry;
        CachedOutputTransfer scope=null;
        try
        {
            if(!run||!ThreadManager.IsMainThread()||cachedOutputTransfer!=null||current!=null||grid?.windowGroup?.Controller is not XUiC_WorkstationWindowGroup window||argument==null)return null;
            var matches=frames.Values.Where(value=>!value.Closed&&value.RemoteNativeNonce!=Guid.Empty&&value.Observation.World.IsRemote()&&
                ReferenceEquals(value.Observation.Window,window)&&IsKnownRemoteCachedFrame(value)&&
                value.SavedFullBoundaryVersion==value.Observation.NativeVersionAfter&&
                RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(value.Observation.Card,value.Observation.NativeVersionAfter)).ToArray();
            if(matches.Length!=1||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024+65536,out var allocation))return null;
            var frame=matches[0];scope=new CachedOutputTransfer{Frame=frame,Grid=grid,Window=window,Session=session,NativeVersion=frame.Observation.NativeVersionAfter,Allocation=allocation,
                Connection=CurrentNativeServerConnection()};
            if(scope.Connection==null||!scope.Current())return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(frame.Observation.Station,out scope.Before,out scope.BeforeOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(scope.Before,scope.BeforeOffset,out var image,out ulong elapsed))return null;
                if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(argument,262144,349528,out _))return null;
                image.Output=ItemStack.Clone(argument);scope.After=RebirthProductiveEarnedNativeBody.Encode(image,elapsed,out scope.AfterOffset);
                if(scope.After.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return null;
                if(!TryCaptureCachedTransferGrid(frame.Observation.Viewer.bag.ItemGrid.items,out scope.BagBefore)||
                    !TryCaptureCachedTransferGrid(frame.Observation.Viewer.inventory.ItemGrid.items,out scope.BeltBefore))return null;
            }
            cachedOutputTransfer=scope;var result=scope;scope=null;return result;
        }
        catch{return null;}
        finally{scope?.Dispose();}
    }
    internal static void ObserveRemoteCachedOutputTransfer(XUiC_WorkstationOutputGrid grid,bool run,object state)
    {
        if(state is CachedPhysicalSpace reentry){ObserveCachedPhysicalSpace(grid,run,reentry);return;}
        if(state is CachedSpace){ObserveCachedPrimarySpace(grid,run,state);return;}
        if(state is not CachedOutputTransfer scope||!ReferenceEquals(cachedOutputTransfer,scope)||!ReferenceEquals(scope.Grid,grid)||!run||!scope.Current())return;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(scope.Frame.Observation.Station,scope.After,scope.AfterOffset))return;
            string bagAfter,beltAfter;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!TryCaptureCachedTransferGrid(scope.Frame.Observation.Viewer.bag.ItemGrid.items,out bagAfter)||
                    !TryCaptureCachedTransferGrid(scope.Frame.Observation.Viewer.inventory.ItemGrid.items,out beltAfter))return;
            scope.Evidence=new XElement("actualCachedSpaceTransfer",new XAttribute("operation",scope.Frame.Observation.Operation.ToString("N")),
                new XAttribute("nativeVersion",scope.Frame.Observation.NativeVersionAfter),new XAttribute("session",scope.Session),
                new XElement("cachedFull",new XElement(scope.Frame.Observation.FullBoundaryEvidence)),
                new XElement("before",new XAttribute("offset",scope.BeforeOffset),Convert.ToBase64String(scope.Before)),
                new XElement("after",new XAttribute("offset",scope.AfterOffset),Convert.ToBase64String(scope.After)),
                new XElement("bagBefore",scope.BagBefore),new XElement("beltBefore",scope.BeltBefore),new XElement("bagAfter",bagAfter),new XElement("beltAfter",beltAfter));
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(scope.Evidence);
            if(measured>NetPackageRebirthCompletionNativeV1.Maximum||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scope.Allocation,checked(64L*1024*1024+65536+16*measured)))return;
            var durable=new XElement("workstationCompletionIntent",new XAttribute("operation",scope.Frame.Observation.Operation.ToString("N")),
                new XAttribute("world",scope.Frame.Observation.WorldGuid),new XElement("before",new XElement(scope.Evidence)));
            if(!RebirthWorkstationCompletionPublication.Write(scope.Frame.Path+".space."+Guid.NewGuid().ToString("N")+".xml",durable))return;
            scope.Observed=true;ResumePhysicalNativePackets(scope.Frame,RemoteNativePhase.CachedSpaceTransfer);
        }
        catch{/* Exact original native cached value remains; no inferred resume. */}
    }
    internal static void EndRemoteCachedOutputTransfer(object state)
    {
        if(state is CachedPhysicalSpace reentry){EndCachedPhysicalSpace(reentry);return;}
        if(state is CachedSpace local){local.Dispose();return;}
        if(state is not CachedOutputTransfer scope)return;
        if(ReferenceEquals(cachedOutputTransfer,scope))cachedOutputTransfer=null;
        scope.Dispose();
    }
    private static bool TryCaptureCachedTransferGrid(ItemStack[] slots,out string image)
    {
        image=null;if(slots==null||slots.Length>byte.MaxValue||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(slots,262144,349528,out _))return false;
        byte[] bytes=RebirthCamperInventorySnapshot.Capture(slots);if(bytes.Length>524288)return false;
        image=Convert.ToBase64String(bytes);return true;
    }
}
