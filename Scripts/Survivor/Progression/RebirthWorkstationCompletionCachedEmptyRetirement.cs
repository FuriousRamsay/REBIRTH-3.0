using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Resource-only retirement of a physically saved cache, after the actual
    // owner terminal and private accepted empty native/UI sync both completed.
    internal static bool RetireEmptyCachedPrimaries(RebirthProductiveEarnedUiCoordinator.SavedEmptyLocalQueue proof)
    {
        if(proof?.Current()!=true)return false;
        var values=cachedCustodies.Values.Where(value=>ReferenceEquals(value.World,proof.World)&&ReferenceEquals(value.Station,proof.Station)).ToArray();
        foreach(var value in values)
        {
            if(!value.Physical||!value.Saved||!value.NativeCurrent()||value.Frame?.NativeOriginal==null||
                !(proof.PreviousScope.Members.Any(member=>ReferenceEquals(member.NativeOriginal,value.Frame.NativeOriginal)&&member.OwnerTerminalSaved)||
                    value.HostOriginal?.HasSavedLocalOwnerTerminal(proof.PreviousScope)==true))return false;
            if(!TryPublishCachedSettledNotice(value))return false;
            long physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");if(physicalLength<1)return false;
            long size=proof.MeasureImage()+physicalLength+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(value.Original)+65536;
            if(!value.ReserveRetirementWorkspace(checked(8*size)))return false;
            var physical=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical");if(physical==null)return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                new XElement(value.Original.Element("before")),new XElement("cachedEmptyRetired",new XAttribute("original",HistoricalHash(value.Image)),
                    new XAttribute("group",proof.Group.ToString("N")),new XAttribute("handoffPath",proof.Path),
                    new XElement("handoff",Convert.ToBase64String(proof.CopyImage())),new XElement("physical",physical)));
            if(!RebirthWorkstationCompletionPublication.Write(value.Path+".empty-retired",record)||
                !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".empty-retired"))||!proof.Current())return false;
        }
        // All original receipts are durable before releasing any source graph.
        foreach(var value in values)
        {cachedCustodies.Remove(value.Operation);var owner=value.Frame?.EarlierOwner;value.Frame?.CachedReentry?.Dispose();if(owner?.CachedSource!=null)owner.Dispose();value.Dispose();}
        return true;
    }
    private static bool IsEmptyCacheRetired(CachedCustody value)
    {
        try
        {
            long retiredLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".empty-retired"),physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");
            if(retiredLength<1||physicalLength<1||!value.ReserveRetirementWorkspace(checked(16*(retiredLength+physicalLength)+65536)))return false;
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".empty-retired");
            if(record==null||!XNode.DeepEquals(record.Element("before"),value.Original.Element("before"))||
                (string)record.Attribute("operation")!=value.Operation.ToString("N")||(string)record.Attribute("world")!=value.WorldGuid)return false;
            var retired=record.Element("cachedEmptyRetired");
            return retired!=null&&(string)retired.Attribute("original")==HistoricalHash(value.Image)&&
                XNode.DeepEquals(retired.Element("physical")?.Elements().SingleOrDefault(),RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical"))&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes((string)retired.Attribute("handoffPath"),Convert.FromBase64String((string)retired.Element("handoff")));
        }
        catch{return false;}
    }
}
