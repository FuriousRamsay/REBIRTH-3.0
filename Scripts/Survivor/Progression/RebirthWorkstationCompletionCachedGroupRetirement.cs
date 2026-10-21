using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool HasRetainedRemoteCachedPrimary(NativeHostOriginal original)=>original!=null&&cachedCustodies.TryGetValue(original.Operation,out var root)&&
        root.Active&&ReferenceEquals(root.RemoteSource,original);
    internal static bool TryGetRemoteCachedPhysicalPublication(NativeHostOriginal original,out CachedPhysicalPublication publication,out bool known)
    {
        publication=null;known=HasRetainedRemoteCachedPrimary(original);if(!known)return false;
        publication=cachedCustodies[original.Operation].PhysicalPublication;return publication?.CurrentSavedSource()==true;
    }
    internal sealed class CachedGroupRetirement
    {
        private CachedCustody root;
        private CachedGroupRetirement(CachedCustody source){root=source;}
        internal void Retire(){var value=root;root=null;if(value==null)return;if(cachedCustodies.TryGetValue(value.Operation,out var found)&&ReferenceEquals(found,value))cachedCustodies.Remove(value.Operation);value.Dispose();}
        internal static CachedGroupRetirement Prepare(CachedPhysicalPublication source,RebirthProductiveEarnedUiDeferredTransport.HostCachedPhysicalReceived received)
        {
            if(source?.CurrentSavedSource()!=true||received?.Current()!=true||!ReferenceEquals(source.Previous,received.Previous))return null;
            var root=source.Root;long bound=checked(source.MeasureImage()+received.MeasureImage()+65536);
            if(!root.ReserveRetirementWorkspace(checked(16*bound))||!TryPublishCachedSettledNotice(root))return null;
            var publication=source.CopyImage();var group=received.CopyFinal();var receipt=received.CopyImage();
            if(publication==null||group==null||receipt==null)return null;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
                new XElement(root.Original.Element("before")),new XElement("cachedGroupPhysicalRetired",new XAttribute("version",1),
                    new XAttribute("original",HistoricalHash(root.Image)),new XAttribute("group",received.Group.ToString("N")),
                    new XAttribute("physicalPublication",HistoricalHash(publication)),new XAttribute("groupFinal",HistoricalHash(group)),
                    new XAttribute("received",HistoricalHash(receipt)),new XAttribute("notice",HistoricalHash(root.SettledNoticeImage))));
            string path=root.Path+".group-physical-retired";
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!received.Current())return null;
            return new CachedGroupRetirement(root);
        }
    }
    private static bool IsCachedGroupPhysicalRetired(CachedCustody root)
    {
        try
        {
            long length=RebirthWorkstationCompletionPublication.PublishedLength(root.Path+".group-physical-retired");
            if(length<1||!root.ReserveRetirementWorkspace(checked(16*length+65536)))return false;
            var retired=RebirthWorkstationCompletionPublication.ReadPrimary(root.Path+".group-physical-retired");var node=retired?.Element("cachedGroupPhysicalRetired");
            if((int?)node?.Attribute("version")!=1||(string)node.Attribute("original")!=HistoricalHash(root.Image)||
                !Guid.TryParseExact((string)node.Attribute("group"),"N",out var group)||group==Guid.Empty)return false;
            string basis=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",group.ToString("N")+".cached-physical-final.xml");
            string[] paths={root.Path+".physical-publication",basis,basis+".received",root.Path+".settled-notice",basis+".resource-commit",root.Path+".physical"};
            long total=0;foreach(string path in paths){long bytes=RebirthWorkstationCompletionPublication.PublishedLength(path);if(bytes<1)return false;total=checked(total+bytes);}
            if(!root.ReserveRetirementWorkspace(checked(16*total+65536)))return false;
            for(int index=0;index<4;index++)
            {
                string key=index==0?"physicalPublication":index==1?"groupFinal":index==2?"received":"notice";
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(paths[index]);if(image==null||HistoricalHash(image)!=(string)node.Attribute(key))return false;
            }
            var final=RebirthWorkstationCompletionPublication.ReadPrimary(basis)?.Element("before")?.Element("cachedPhysicalGroupSaved");
            var commit=RebirthWorkstationCompletionPublication.ReadPrimary(paths[4])?.Element("before")?.Element("cachedPhysicalResourcesCommitted");
            var finalImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(basis);
            if(final==null||commit==null||(string)commit.Attribute("group")!=group.ToString("N")||
                (string)commit.Attribute("final")!=RebirthProductiveEarnedUiDeferredTransport.HydrationHashForCachedRetirement(finalImage)||
                !final.Elements("physical").Any(value=>(string)value.Attribute("operation")==root.Operation.ToString("N")&&(string)value.Attribute("path")==paths[0]))return false;
            var physical=RebirthWorkstationCompletionPublication.ReadPrimary(paths[5]);var actual=physical?.Element("actualCachedPhysical");
            if(actual==null||!MatchesRemoteCachedPhysicalSeal(root.Path,HistoricalHash(root.Image),physical,root.ReserveRetirementWorkspace)||
                !RebirthWorkstationCompletionOriginal.TryRead(actual.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var original)||
                !RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _))return false;
            return RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedPhysicalActor(root.Path,HistoricalHash(root.Image),
                actual.Element("cachedPhysicalActorSaved"),ActiveBefore(physical),original.Write().Element("delivery").Elements().Single(),root.ReserveRetirementWorkspace);
        }
        catch{return false;}
    }
}
