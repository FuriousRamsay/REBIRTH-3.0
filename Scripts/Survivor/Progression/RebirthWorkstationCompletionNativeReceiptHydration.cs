using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class NativeReceiptHydrationPublication:IDisposable
    {
        private NativeHostOriginal source;private ClientInfo peer;private INetConnection connection;private Guid dispatch;
        private XElement image;private IDisposable allocation,retention;
        private NativeReceiptHydrationPublication(){}
        internal XElement Image=>image==null?null:new XElement(image);
        internal bool Current()=>SourceCurrent()&&ReferenceEquals(executingRemoteNative?.Event.Source,source)&&executingRemoteNative.Event.Dispatch==dispatch&&
            executingRemoteNative.ReadCompleted&&executingRemoteNative.Reads==1;
        internal bool SourceCurrent()=>source?.CurrentViewer()==true&&ReferenceEquals(source.Sender,peer)&&peer.netConnection!=null&&
            peer.netConnection.Any(value=>ReferenceEquals(value,connection));
        public void Dispose(){image=null;source=null;peer=null;connection=null;retention?.Dispose();retention=null;allocation?.Dispose();allocation=null;}
        internal static NativeReceiptHydrationPublication Capture(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection)
        {
            var actual=executingRemoteNative?.Event;
            if(actual==null||!HasNativeReceiptHydrationCandidate(world,station,peer,connection))return null;
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(actual.Contribution);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured+65536),out var allocation))return null;
            NativeReceiptHydrationPublication result=null;
            try
            {
                result=new NativeReceiptHydrationPublication{source=actual.Source,peer=peer,connection=connection,dispatch=actual.Dispatch,allocation=allocation};allocation=null;
                result.retention=actual.Source.Retain();if(result.retention==null)return null;
                var canonical=CanonicalNativeReceiptLedger(actual.Contribution);
                result.image=new XElement("nativeOriginalReceipts",new XAttribute("version",1),new XAttribute("observationOnly",true),new XElement("original",
                    new XAttribute("operation",actual.Source.Operation.ToString("N")),new XAttribute("nonce",actual.Source.Nonce.ToString("N")),
                    new XAttribute("source",actual.Source.SourceHash),new XAttribute("birth",actual.Source.Birth.ToString("N")),
                    new XAttribute("ledger",RebirthWorkstationCompletionOriginal.Hash(canonical.ToString(SaveOptions.DisableFormatting))),
                    new XAttribute("aggregate",RebirthWorkstationCompletionOriginal.Hash((string)actual.Contribution.Element("aggregate"))),
                    canonical.Element("members").Elements("member").Select(member=>new XElement("member",new XAttribute("operation",(string)member.Attribute("operation")),
                        new XAttribute("kind",(string)member.Attribute("kind")),new XAttribute("source",(string)member.Attribute("source")),
                        new XAttribute("original",RebirthWorkstationCompletionOriginal.Hash(member.ToString(SaveOptions.DisableFormatting)))))));
                long bytes=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(result.image);
                canonical=null;
                if(bytes>24*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(result.allocation,checked(4*bytes+65536))||!result.Current())return null;
                var published=result;result=null;return published;
            }
            finally{result?.Dispose();allocation?.Dispose();}
        }
    }
    internal static bool HasNativeReceiptHydrationCandidate(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection)
    {
        var read=executingRemoteNative;var actual=read?.Event;
        if(!ThreadManager.IsMainThread()||actual==null||!read.ReadCompleted||read.Reads!=1||!ReferenceEquals(actual.Source.World,world)||
            !ReferenceEquals(actual.Source.Station,station)||!actual.Source.CurrentViewer())return false;
        if(peer==null&&connection==null)return true;
        return ReferenceEquals(peer,actual.Source.Sender)&&peer.netConnection!=null&&connection!=null&&peer.netConnection.Any(value=>ReferenceEquals(value,connection));
    }
    internal static bool TryCaptureNativeReceiptHydration(World world,TileEntityWorkstation station,ClientInfo peer,INetConnection connection,out NativeReceiptHydrationPublication proof)
    {proof=NativeReceiptHydrationPublication.Capture(world,station,peer,connection);return proof!=null;}
    internal static XElement CanonicalNativeReceiptLedger(XElement contribution)
    {
        var result=new XElement(contribution);result.Attribute("phase")?.Remove();result.Elements("physicalDelivery").Remove();result.Elements("activeBefore").Remove();
        result.Elements("actualCachedSpaceTransfer").Remove();return result;
    }
    internal static bool TryFindRemoteNativeReceipt(Guid operation,World world,TileEntityWorkstation station,out RebirthProductiveEarnedUiQueueOwner.DeferredReceipt token)
    {token=null;return remoteNativeReceipts.TryGetValue(operation,out token)&&ReferenceEquals(token.Source.World,world)&&ReferenceEquals(token.Source.Station,station)&&token.CurrentSourceWithoutRow();}
}
