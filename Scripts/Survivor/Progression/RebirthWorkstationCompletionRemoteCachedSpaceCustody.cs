using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static void ObserveRemoteCachedCustodySpace(RemoteNativeEvent actual)
    {
        if(actual.Phase!=RemoteNativePhase.CachedSpaceTransfer||!actual.IsActualAcceptedRead()||
            !cachedCustodies.TryGetValue(actual.Source.Operation,out var cache)||!cache.Active||cache.Physical||
            !ReferenceEquals(cache.World,actual.Source.World)||!ReferenceEquals(cache.Station,actual.Source.Station)||
            (bool?)cache.Original.Element("cachedOriginal")?.Attribute("remoteCreator")!=true||!cache.SourceCurrent()||cache.Epoch>=64)return;
        try
        {
            var transfer=actual.Contribution.Element("actualCachedSpaceTransfer");if(transfer==null)return;
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(transfer)+2L*actual.Body.Length+65536;
            if(!cache.ReserveRetirementWorkspace(checked(16*measured)))return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),new XElement("remoteCachedSpaceAccepted",new XAttribute("version",1),
                    new XAttribute("original",HistoricalHash(cache.Image)),new XAttribute("dispatch",actual.Dispatch.ToString("N")),
                    new XAttribute("epoch",cache.Epoch+1),new XElement(transfer)));
            string path=cache.Path+".remote-space."+(cache.Epoch+1);
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            string image=RebirthWorkstationCompletionPublication.ReadEvidence(path);if(image==null)return;
            // Only this actual accepted transfer advances the saved native
            // preimage. An equal later getter/read cannot adopt the change.
            var body=(byte[])actual.Body.Clone();
            cache.Watch?.Dispose();cache.Watch=null;cache.Saved=false;cache.Native=body;cache.Offset=actual.Offset;
            cache.SpacePath=path;cache.SpaceImage=image;cache.Epoch++;
        }
        catch{/* Prior source is retained; no receipt absence permits retry. */}
    }
}
