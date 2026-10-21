using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeHostOriginal
    {
        private sealed class NativeContinuation:IDisposable
        {
            internal NativeHostOriginal Source;internal XElement Before,Record,SavedRecord;internal byte[] Native,Image,SavedImage;
            internal int Offset;internal string Path;internal bool Saved;internal IDisposable Allocation;
            internal RebirthRemoteResourceNativePublication.Watch Watch;
            internal bool Current()
            {
                if(!Source.CurrentViewer()||!Source.AdmissionCurrent())return false;
                using(var callbacks=Source.Memory.TryEnterCallbacks())return callbacks!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Source.Station,Native,Offset);
            }
            public void Dispose(){Watch?.Dispose();Watch=null;Before=Record=SavedRecord=null;Native=Image=SavedImage=null;Source=null;Allocation?.Dispose();Allocation=null;}
        }
        private readonly List<NativeContinuation> nativeContinuations=new List<NativeContinuation>();
        private XElement acceptedCachedSpace;private byte[] acceptedCachedSpaceBody;private int acceptedCachedSpaceOffset;
        private IDisposable acceptedCachedSpaceAllocation;
        internal bool IsSavedActiveBefore(XElement before)=>SourceCurrent()&&
            (BeforeSaved&&XNode.DeepEquals(Before,before)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path+".saved",savedBeforeImage)||
                nativeContinuations.Any(value=>value.Saved&&XNode.DeepEquals(value.Before,before)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.Path,value.Image)&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.Path+".saved",value.SavedImage)));
        internal bool IsSavedFullBoundaryBefore(XElement full)
        {
            if(full==null)return false;
            if(IsLoadedDeferredBoundaryBefore(full))return true;
            string native=(string)full.Element("nativeBefore");
            return BeforeSaved&&(string)Before.Element("nativeBefore")==native&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path+".saved",savedBeforeImage)||
                nativeContinuations.Any(value=>value.Saved&&(string)value.Before.Element("nativeBefore")==native&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.Path,value.Image)&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.Path+".saved",value.SavedImage));
        }
        internal void ObserveAcceptedCachedSpace(RemoteNativeEvent actual)
        {
            if(!ReferenceEquals(actual.Source,this)||actual.Phase!=RemoteNativePhase.CachedSpaceTransfer||!actual.IsActualAcceptedRead())return;
            var proof=actual.Contribution.Element("actualCachedSpaceTransfer");if(proof==null)return;
            long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*size+8L*actual.Body.Length+65536),out var allocation))return;
            XElement copied=null;byte[] body=null;
            try{copied=new XElement(proof);body=(byte[])actual.Body.Clone();}
            catch{allocation.Dispose();return;}
            acceptedCachedSpace=null;acceptedCachedSpaceBody=null;acceptedCachedSpaceAllocation?.Dispose();
            acceptedCachedSpace=copied;acceptedCachedSpaceBody=body;acceptedCachedSpaceOffset=actual.Offset;acceptedCachedSpaceAllocation=allocation;
        }
        internal bool TrySealContinuation(XElement before)
        {
            if(!CurrentViewer()||!AdmissionCurrent()||before.Element("cachedPredecessor")==null)return false;
            var phase=nativeContinuations.SingleOrDefault(value=>XNode.DeepEquals(value.Before,before));
            if(phase==null)
            {
                var predecessor=before.Element("cachedPredecessor").Elements().SingleOrDefault()??before.Element("cachedPredecessor");
                var full=acceptedCachedSpace?.Element("cachedFull")?.Elements().SingleOrDefault();
                if(nativeContinuations.Count>=64||acceptedCachedSpaceBody==null||full==null||!XNode.DeepEquals(predecessor,full))return false;
                string nativeText=(string)before.Element("nativeBefore");
                if(nativeText==null||nativeText.Length>4*((RebirthProductiveEarnedNativeBody.MaximumBodyBytes+2)/3))return false;
                var previous=nativeContinuations.LastOrDefault()?.Before??Before;
                foreach(string field in new[]{"operation","world","viewer","creation","startingOwner","x","y","z","block","slot","beforeCount","extrasDefinition"})
                    if((string)before.Attribute(field)!=(string)Before.Attribute(field))return false;
                if(!QueueStableEqual((string)before.Element("nativeQueueFull"),(string)previous.Element("nativeQueueFull"))||
                    (string)before.Element("recipeFull")!=(string)Before.Element("recipeFull"))return false;
                long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before)+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(acceptedCachedSpace)+65536;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*size+24L*(nativeText.Length/4)),out var allocation))return false;
                try
                {
                    var native=Convert.FromBase64String(nativeText);int offset=(int)before.Attribute("nativeElapsedOffset");
                    if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,acceptedCachedSpaceBody,acceptedCachedSpaceOffset))return false;
                    phase=new NativeContinuation{Source=this,Before=new XElement(before),Native=native,Offset=offset,Allocation=allocation,
                        Path=Path+".phase."+(nativeContinuations.Count+1)};
                    phase.Record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                        new XElement("before",new XElement("remoteNativeCachedBefore",new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("source",SourceHash),
                            new XElement(before),new XElement(acceptedCachedSpace))));
                    nativeContinuations.Add(phase);allocation=null;
                }
                finally{allocation?.Dispose();}
            }
            if(!phase.Current())return false;if(phase.Saved)return RebirthWorkstationCompletionPublication.MatchesPublishedBytes(phase.Path+".saved",phase.SavedImage);
            if(!RebirthWorkstationCompletionPublication.Write(phase.Path,phase.Record))return false;
            if(phase.Image==null){phase.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(phase.Path);if(phase.Image==null)return false;}
            if(phase.Watch==null&&phase.SavedRecord==null)
            {
                if(!RebirthRemoteResourceNativeBinding.TryResolve(World,RemoteResourceIdentity.Workstation(Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                    CustodyBirth.ToString("N"),out var binding))return false;
                phase.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,phase.Current,Memory);
            }
            if(phase.SavedRecord==null)
            {
                if(phase.Watch==null||!phase.Watch.TryReadPublishedTileDescriptor(out var published,out long offset,out int length,out string digest)||!phase.Current())return false;
                phase.SavedRecord=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement("remoteNativeCachedBeforeSaved",new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("source",SourceHash),
                        new XAttribute("nativePath",published.Path),new XAttribute("payload",published.PayloadDigest),new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",digest))));
            }
            if(!RebirthWorkstationCompletionPublication.Write(phase.Path+".saved",phase.SavedRecord))return false;
            phase.SavedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(phase.Path+".saved");if(phase.SavedImage==null)return false;
            phase.Saved=true;phase.Watch?.Dispose();phase.Watch=null;return true;
        }
        private void ReleaseNativeContinuations()
        {
            foreach(var phase in nativeContinuations)phase.Dispose();nativeContinuations.Clear();
            acceptedCachedSpace=null;acceptedCachedSpaceBody=null;acceptedCachedSpaceAllocation?.Dispose();acceptedCachedSpaceAllocation=null;
        }
    }
}
