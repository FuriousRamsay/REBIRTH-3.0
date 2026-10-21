using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool IsLoadedDeferredFrame(Frame frame)
        =>frame!=null&&!frame.Closed&&frame.NativeOriginal==null&&frame.NativeViewerEffect==null&&frame.CachedPhysical==null&&
            frame.Observation.RemoteOutputRestart?.IsDeferred==true&&frame.Observation.RemoteOutputRestart.BindingCurrent()&&
            frame.Observation.RemoteOutputRestart.Operation==frame.Observation.Operation;
    private static bool IsLoadedDeferredCachedFrame(Frame frame)
        =>IsLoadedDeferredFrame(frame)&&!frame.Observation.RemoteOutputRestart.FinalCallbackSaved&&frame.Observation.FullBoundary&&
            !frame.Observation.Continued&&!frame.Observation.Accepted&&!frame.NativeAccepted&&frame.ConfirmedOriginal==null;
    private sealed class LoadedDeferredLease:IDisposable
    {
        private IDisposable owner,memory;private RebirthRecipeOutputIntegration.RemoteOutputColdClient source;
        internal LoadedDeferredLease(RebirthRecipeOutputIntegration.RemoteOutputColdClient source,IDisposable owner,IDisposable memory)
        {this.source=source;this.owner=owner;this.memory=memory;}
        public void Dispose(){owner?.Dispose();memory?.Dispose();owner=memory=null;source=null;}
    }
    private static bool TryCopyLoadedDeferredContribution(Frame frame,out XElement contribution,out IDisposable retained)
    {
        contribution=null;retained=null;IDisposable memory=null,owner=null;
        try
        {
            if(!IsLoadedDeferredFrame(frame))return false;
            var source=frame.Observation.RemoteOutputRestart;var value=source.DeferredContribution;
            if(value==null||value.Attribute("phase")!=null||value.Element("physicalDelivery")!=null||
                value.Element("activeBefore")!=null||value.Element("actualCachedSpaceTransfer")!=null||
                (string)value.Attribute("operation")!=source.Operation.ToString("N")||
                (string)value.Attribute("nonce")!=frame.RemoteNativeNonce.ToString("N")||
                (string)value.Attribute("original")!=frame.RemoteNativeSourceHash||
                (string)value.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(frame.EarlierOwner))return false;
            long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(value);
            if(size<1||size>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*size+65536),out memory)||
                (owner=frame.EarlierOwner.RetainPublishedSource())==null||!IsLoadedDeferredFrame(frame))return false;
            contribution=new XElement(value);retained=new LoadedDeferredLease(source,owner,memory);owner=memory=null;return true;
        }
        catch{return false;}
        finally{owner?.Dispose();memory?.Dispose();}
    }
}
