using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedPeerSync:IDisposable
    {
        internal CachedPhysicalSource Producer;internal byte[] Body,Image;internal int Offset,SpaceSequence;
        internal string Path;internal IDisposable Allocation;internal bool Saved;
        internal bool Current()=>Image!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        public void Dispose(){Producer=null;Body=Image=null;Allocation?.Dispose();Allocation=null;}
    }
    internal sealed partial class CachedPhysicalSource
    {
        internal CachedPeerSync peerSync;
        private bool PeerNativeCurrent()
        {
            if(peerSync?.Saved!=true||!peerSync.Current())return false;
            byte[] body=peerSync.Body;int offset=peerSync.Offset;
            if(Space!=null&&Space.Sequence>peerSync.SpaceSequence)
            {if(!SpaceSaved||!Space.Observed||!Space.Current())return false;body=Space.After;offset=Space.AfterOffset;}
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,body,offset);
        }
        internal void DropUnusedPeerFrame()
        {
            if(Frame==null)return;
            if(cards.TryGetValue(Card,out var attached)&&ReferenceEquals(attached,Frame))cards.Remove(Card);
            if(frames.TryGetValue(Operation,out attached)&&ReferenceEquals(attached,Frame))frames.Remove(Operation);
            if(nativeViewerClients.TryGetValue(Frame,out var viewer)){nativeViewerClients.Remove(Frame);viewer.Dispose();}
            RebirthRecipeOutputIntegration.EndOwningObservation(Frame.Observation);Frame.Closed=true;Frame.Observation.Dispose();Frame=null;
            readyImage=null;readyPath=null;
        }
    }
    private static void ObserveCachedPeerSync(Frame frame)
    {
        if(!IsObservingCachedRecipientSync(frame))return;var producer=frame.CachedPhysical;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(producer.Station,out var body,out int offset))return;
            foreach(var target in cachedPhysicalSources.Values.Where(value=>!ReferenceEquals(value,producer)&&value.Current()&&value.Prepared&&
                ReferenceEquals(value.World,producer.World)&&ReferenceEquals(value.Station,producer.Station)&&ReferenceEquals(value.Viewer,producer.Viewer)&&
                ReferenceEquals(value.Connection,producer.Connection)&&ReferenceEquals(value.Window,producer.Window)&&value.WindowSession==producer.WindowSession&&
                value.Frame?.Attempted!=true&&value.Frame?.Observation.RanUpdate!=true&&
                RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(value.Card,value.NativeVersion)).ToArray())
            {
                if(target.peerSync!=null&&!target.peerSync.Saved)continue;
                long size=checked(65536+2L*body.Length+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(target.Before));
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*size),out var allocation))continue;
                CachedPeerSync proof=null;
                try
                {
                    var record=new XElement("workstationCompletionIntent",new XAttribute("operation",target.Operation.ToString("N")),
                        new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(target.World)),new XElement("before",new XElement(target.Before)),
                        new XElement("actualCachedPeerSync",new XAttribute("version",1),new XAttribute("producer",producer.Operation.ToString("N")),
                            new XAttribute("producerAction",producer.Action.ToString("N")),new XAttribute("delivery",frame.ConfirmedOriginal.Digest),
                            new XAttribute("original",target.Grant.Original),new XAttribute("session",target.WindowSession),new XAttribute("nativeVersion",target.NativeVersion),
                            new XAttribute("offset",offset),new XElement("native",Convert.ToBase64String(body))));
                    string path=target.Path+".peer."+producer.Action.ToString("N");
                    if(!RebirthWorkstationCompletionPublication.Write(path,record))continue;
                    var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)continue;
                    proof=new CachedPeerSync{Producer=producer,Body=(byte[])body.Clone(),Offset=offset,SpaceSequence=target.Space?.Sequence??0,Image=image,Path=path,Allocation=allocation};allocation=null;
                    var previous=target.peerSync;target.peerSync=proof;proof=null;previous?.Dispose();
                }
                finally{proof?.Dispose();allocation?.Dispose();}
            }
        }
    }
    private static void QualifyCachedPeerSyncs(CachedPhysicalSource producer)
    {
        if(producer?.Frame?.ConfirmedOriginal==null||!producer.PhysicalSaved&&(!producer.CourierAcknowledged||
            !RebirthWorkstationCompletionOutbox.TryReadSaved(producer.Frame.ConfirmedOriginal,out var state)||!state.Acknowledged))return;
        foreach(var target in cachedPhysicalSources.Values.Where(value=>ReferenceEquals(value.peerSync?.Producer,producer)).ToArray())
        {
            var proof=target.peerSync;
            if(!target.Current()||!proof.Current()||target.Frame?.Attempted==true||target.Frame?.Observation.RanUpdate==true||
                !RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(target.Card,target.NativeVersion))continue;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(target.Station,proof.Body,proof.Offset))continue;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",target.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(target.World)),new XElement("before",new XElement(target.Before)),
                new XElement("cachedPeerPhysicalSaved",new XAttribute("version",1),new XAttribute("producer",producer.Operation.ToString("N")),
                    new XAttribute("producerAction",producer.Action.ToString("N")),new XAttribute("delivery",producer.Frame.ConfirmedOriginal.Digest),
                    new XAttribute("observed",HistoricalHash(proof.Image)),new XAttribute("original",target.Grant.Original)));
            string path=proof.Path+".saved";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
            var saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(saved==null)continue;
            target.DropUnusedPeerFrame();proof.Image=saved;proof.Path=path;proof.Saved=true;proof.Producer=null;
        }
    }
}
