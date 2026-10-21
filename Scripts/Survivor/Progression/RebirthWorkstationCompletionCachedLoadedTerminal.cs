using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool TryRetireActualLoadedCachedPhysical(CachedCustody source,XUiC_WorkstationWindowGroup window,long session)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualCachedWindowOpen(window,session)||
            !(source.Load?.Loaded?.Installed(source.World)==true&&ReferenceEquals(source.Load.Loaded.Tile,source.Station)||
                source.Frame!=null&&ReferenceEquals(source.Frame.Observation.Station,source.Station)&&ReferenceEquals(source.Frame.Observation.World,source.World))||
            !source.Saved||!source.PhysicalObserved||!source.NativeCurrent())return false;
        try
        {
            string physicalPath=source.Path+".physical",intentPath=source.Path.Substring(0,source.Path.Length-".cache.xml".Length);
            string archive=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intentArchive",System.IO.Path.GetFileName(intentPath));
            if(File.Exists(intentPath+".tmp")||File.Exists(archive+".tmp"))return false;
            string delivered=File.Exists(intentPath)?intentPath:archive;
            long physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(physicalPath),intentLength=RebirthWorkstationCompletionPublication.PublishedLength(delivered);
            if(physicalLength>0&&intentLength<1)
            {
                if(!source.ReserveRetirementWorkspace(checked(16*physicalLength+65536)))return false;
                var loadedPhysical=RebirthWorkstationCompletionPublication.ReadPrimary(physicalPath);
                return loadedPhysical?.Element("actualCachedPhysical")?.Element("cachedPhysicalActorSaved")!=null&&RetireLoadedCachedActorPhysical(source,loadedPhysical);
            }
            if(physicalLength<1||intentLength<1||!source.ReserveRetirementWorkspace(checked(16*(physicalLength+intentLength)+65536)))return false;
            var physical=RebirthWorkstationCompletionPublication.ReadPrimary(physicalPath);var intent=RebirthWorkstationCompletionPublication.ReadPrimary(delivered);
            var eventImage=physical?.Element("actualCachedPhysical");
            if(eventImage?.Element("cachedPhysicalActorSaved")!=null)
                return RetireLoadedCachedActorPhysical(source,physical);
            if(eventImage==null||(string)eventImage.Attribute("original")!=HistoricalHash(source.Image)||
                !RebirthWorkstationCompletionOriginal.TryRead(eventImage.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var confirmed)||
                confirmed.Operation!=source.Operation.ToString("N")||!ValidateDelivery(intent,confirmed)||
                !XNode.DeepEquals(eventImage.Element("tileAccepted"),intent.Element("tileAccepted")))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(source.Native,source.Offset,
                    Convert.FromBase64String((string)eventImage.Element("native")),(int)eventImage.Attribute("elapsedOffset")))return false;
            }
            var original=source.Original.Element("cachedOriginal");
            if(original.Element("nativeOwnerOriginal")!=null)
            {if(source.OwnerSavedImage==null||!CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage))return false;}
            else if(!MatchesSavedCachedOwnerEffect(original.Element("cachedOwnerEffect")?.Elements().SingleOrDefault()))return false;
            // Parsing reconstructs immutable evidence, never the outbox's saved
            // authority. Only the actual installed native custody and all saved
            // delivery/owner proofs above may retain this original. Require its
            // primary readback before dropping the cache's only live root.
            var outbox=RebirthWorkstationCompletionOutbox.Retain(confirmed,"server",source.World,source.Station);
            if(outbox==null||!outbox.Saved&&!RebirthWorkstationCompletionOutbox.Save(outbox)||
                !outbox.Original.IsConfirmed||outbox.Original.Digest!=confirmed.Digest)return false;
            confirmed=outbox.Original;
            source.Physical=true;if(!TryPublishCachedSettledNotice(source))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),new XElement("cachedLoadedPhysicalRetired",new XAttribute("original",HistoricalHash(source.Image)),
                    new XAttribute("intent",delivered),new XElement("confirmedOriginal",confirmed.Write()),new XElement("physical",physical)));
            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".loaded-physical-retired",record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".loaded-physical-retired"))||!source.NativeCurrent())return false;
            // This completes no gameplay action. The saved original delivery
            // still drives independent idempotent outbox/side-effect recovery.
            cachedCustodies.Remove(source.Operation);source.Dispose();return true;
        }
        catch{return false;}
    }
    private static bool RetireLoadedCachedActorPhysical(CachedCustody source,XElement physical)
    {
        bool prior=source.Physical;source.Physical=true;
        try
        {
            if(!TryReadSavedCachedPhysical(source,out var original,out var actual)||!XNode.DeepEquals(physical,actual)||!HasSavedCachedOwnerEvent(source))return false;
            var outbox=RebirthWorkstationCompletionOutbox.Retain(original,"server",source.World,source.Station);
            if(outbox==null||!outbox.Saved&&!RebirthWorkstationCompletionOutbox.Save(outbox)||!outbox.Original.IsConfirmed||!TryPublishCachedSettledNotice(source))return false;
            if(source.Original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect")!=null&&
                (!CachedPhysicalPublication.TryIssue(source,out var terminal)||!RebirthProductiveEarnedUiDeferredTransport.TrySealCachedSameOwnerTerminal(terminal)))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),new XElement("cachedLoadedPhysicalRetired",new XAttribute("original",HistoricalHash(source.Image)),
                    new XElement("confirmedOriginal",outbox.Original.Write()),new XElement("physical",physical)));
            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".loaded-physical-retired",record)||!source.NativeCurrent()||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".loaded-physical-retired")))return false;
            cachedCustodies.Remove(source.Operation);source.Dispose();return true;
        }
        finally{if(source.Active)source.Physical=prior;}
    }
    private static bool IsLoadedPhysicalCacheRetired(CachedCustody source)
    {
        try
        {
            long retiredLength=RebirthWorkstationCompletionPublication.PublishedLength(source.Path+".loaded-physical-retired"),physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(source.Path+".physical");
            if(retiredLength<1||physicalLength<1||!source.ReserveRetirementWorkspace(checked(16*(retiredLength+physicalLength)+65536)))return false;
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".loaded-physical-retired");var retired=record?.Element("cachedLoadedPhysicalRetired");
            return retired!=null&&(string)record.Attribute("operation")==source.Operation.ToString("N")&&(string)record.Attribute("world")==source.WorldGuid&&
                XNode.DeepEquals(record.Element("before"),source.Original.Element("before"))&&(string)retired.Attribute("original")==HistoricalHash(source.Image)&&
                XNode.DeepEquals(retired.Element("physical")?.Elements().SingleOrDefault(),RebirthWorkstationCompletionPublication.ReadPrimary(source.Path+".physical"));
        }
        catch{return false;}
    }
}
