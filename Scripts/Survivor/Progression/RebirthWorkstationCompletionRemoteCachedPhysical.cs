using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static void ObserveRemoteCachedPhysicalAccepted(RemoteNativeEvent actual)
    {
        if(actual.Phase!=RemoteNativePhase.QueueAccepted||!actual.IsActualAcceptedRead()||actual.ReportedOriginal==null||
            !cachedCustodies.TryGetValue(actual.Source.Operation,out var cache)||!cache.Active||cache.PhysicalObserved||
            !ReferenceEquals(cache.World,actual.Source.World)||!ReferenceEquals(cache.Station,actual.Source.Station)||!cache.SourceCurrent()||
            (bool?)cache.Original.Element("cachedOriginal")?.Attribute("remoteCreator")!=true)return;
        try
        {
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(actual.ReportedOriginal.Write())+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(actual.Contribution)+2L*actual.Body.Length+65536;
            if(!cache.ReserveRetirementWorkspace(checked(16*measured)))return;
            var before=actual.Contribution.Element("activeBefore")?.Elements().SingleOrDefault();if(before==null)return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),new XElement("cachedContinuation",new XElement(before)),
                new XElement("actualCachedPhysical",new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("remote",true),
                    new XAttribute("dispatch",actual.Dispatch.ToString("N")),new XAttribute("elapsedOffset",actual.Offset),
                    new XElement("confirmedOriginal",actual.ReportedOriginal.Write()),new XElement("native",Convert.ToBase64String(actual.Body))));
            string path=cache.Path+".physical-attempt";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            var body=(byte[])actual.Body.Clone();cache.Native=body;cache.Offset=actual.Offset;cache.Saved=false;cache.PhysicalObserved=true;
            cache.Watch?.Dispose();cache.Watch=null;
            // Install the exact-body observer before requesting its purpose
            // save; an earlier completed chunk write cannot satisfy it.
            cache.TrySave();cache.Chunk.isModified=true;cache.Station.setModified();
        }
        catch{/* Accepted original remains retained; never insert again. */}
    }
}
