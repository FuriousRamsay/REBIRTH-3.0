using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool HasSavedCachedOwnerEvent(CachedCustody source)
    {
        if(source.Original.Element("cachedOriginal")?.Element("nativeOwnerLedger")==null)return true;
        return source.OwnerSavedImage!=null&&CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage)&&
            (source.OwnerTerminal!=null?source.OwnerTerminal.HasSavedCachedIdentity(source):
                source.HasOwnerReadFence||source.Load?.Loaded?.Installed(source.World)==true||
                cachedPhysicalActions.TryGetValue(source.Operation,out var action)&&ReferenceEquals(action.Issue?.Cache,source)&&
                action.SavedOwnerBeforeAction&&action.PhysicalFinalObserved&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(action.FinalAcceptedPath,action.FinalAcceptedImage));
    }
    internal sealed partial class CachedPhysicalSource
    {
        private int ownerRenewalSpaceSequence;
        private string ownerRenewalPath;
        private byte[] ownerRenewalImage;
        private bool OwnerRenewalNativeCurrent()
        {
            if(OwnerRenewal?.CurrentSavedSource()!=true||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ownerRenewalPath,ownerRenewalImage))return false;
            byte[] body=OwnerRenewal.Body;int offset=OwnerRenewal.Offset;
            if(Space!=null&&Space.Sequence>ownerRenewalSpaceSequence)
            {
                if(!SpaceSaved||!Space.Observed||!Space.Current())return false;
                body=Space.After;offset=Space.AfterOffset;
            }
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,body,offset);
        }
        internal bool ObserveOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication proof)
        {
            if(ReferenceEquals(OwnerRenewal,proof))return true;
            if(OwnerRenewal!=null||!Current()||!proof.Current()||!Grant.CurrentWindow()||
                !ReferenceEquals(proof.World,World)||!ReferenceEquals(proof.Station,Station)||!ReferenceEquals(proof.Actor,Viewer)||
                !ReferenceEquals(proof.Connection,Connection)||!ReferenceEquals(proof.Window,Window)||proof.WindowSession!=WindowSession||
                !proof.Source.Members.Any(member=>member.Operation==Operation)||Frame?.Observation.RanUpdate==true||
                !RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(Card,NativeVersion))return false;
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,
                checked(16*Grant.MeasureImage()+16*proof.MeasureImage()+65536)))return false;
            IDisposable lease=proof.Source.Retain();if(lease==null)return false;
            try
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),
                    new XElement("before",new XElement(Before)),
                    new XElement("cachedPhysicalOwnerRenewal",new XAttribute("action",Action.ToString("N")),
                        new XAttribute("ticket",Grant.Ticket.ToString("N")),new XAttribute("session",WindowSession),
                        new XAttribute("nativeVersion",NativeVersion),new XAttribute("terminal",proof.Path),proof.Image));
                string path=Path+".owner-renewal";
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!proof.Current()||
                    !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;
                // The old Before was positively unused: no native Update ran
                // and the exact observed card version still has not advanced.
                if(Frame!=null)
                {
                    if(cards.TryGetValue(Card,out var attached)&&ReferenceEquals(attached,Frame))cards.Remove(Card);
                    if(frames.TryGetValue(Operation,out attached)&&ReferenceEquals(attached,Frame))frames.Remove(Operation);
                    if(nativeViewerClients.TryGetValue(Frame,out var viewer)){nativeViewerClients.Remove(Frame);viewer.Dispose();}
                    RebirthRecipeOutputIntegration.EndOwningObservation(Frame.Observation);Frame.Observation.Dispose();Frame=null;
                }
                readyImage=null;readyPath=null;ownerRenewalPath=path;ownerRenewalImage=bytes;
                ownerRenewalSpaceSequence=Space?.Sequence??0;OwnerRenewal=proof;OwnerRenewalRetention=lease;lease=null;
                return true;
            }
            finally{lease?.Dispose();}
        }
    }
    // A known cached primary keeps its own physical hold. A native recipient
    // terminal cannot hand that still-pending primary to queue rotation.
    internal static bool ObserveCachedRecipientTerminal(RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication proof)
    {
        var sources=cachedPhysicalSources.Values.Where(source=>ReferenceEquals(source.World,proof.World)&&
            ReferenceEquals(source.Station,proof.Station)&&ReferenceEquals(source.Viewer,proof.Actor)&&
            proof.Source.Members.Any(member=>member.Operation==source.Operation)).ToArray();
        if(sources.Length==0)return false;
        foreach(var source in sources)source.ObserveOwnerTerminal(proof);
        return true;
    }
}
