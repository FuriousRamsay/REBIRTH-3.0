using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static CachedPhysicalPublication issuingCachedPhysicalReceived;
    private sealed class CachedPhysicalReceived
    {
        internal readonly CachedPhysicalPublication Source;internal readonly string Path;internal readonly XElement Image;
        private CachedPhysicalReceived(CachedPhysicalPublication source,string path,XElement image){Source=source;Path=path;Image=new XElement(image);}
        internal bool Current()=>ReferenceEquals(issuingCachedPhysicalReceived,Source)&&Source.CurrentSavedSource()&&
            XNode.DeepEquals(Image,RebirthWorkstationCompletionPublication.ReadPrimary(Path));
        internal static CachedPhysicalReceived Issue(CachedPhysicalPublication source,string path,XElement image)=>
            ReferenceEquals(issuingCachedPhysicalReceived,source)?new CachedPhysicalReceived(source,path,image):null;
    }
    private static bool RetireCachedSameOwnerReceived(CachedPhysicalReceived received)
    {
        if(received?.Current()!=true||!received.Source.IsSameOwner)return false;
        var proof=received.Source;var root=proof.Root;
        string terminal=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",root.Operation.ToString("N")+".xml.same-owner-terminal");
        if(received.Path!=terminal.Substring(0,terminal.Length-".same-owner-terminal".Length)+".same-owner-received")return false;
        long a=RebirthWorkstationCompletionPublication.PublishedLength(terminal),b=RebirthWorkstationCompletionPublication.PublishedLength(received.Path);
        if(a<1||b<1||!root.ReserveRetirementWorkspace(checked(16*(a+b+proof.MeasureImage()+65536))))return false;
        var terminalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(terminal);var receivedBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(received.Path);
        var physicalBytes=proof.CopyImage();
        if(terminalBytes==null||receivedBytes==null||physicalBytes==null||!TryPublishCachedSettledNotice(root))return false;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",root.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
            new XElement(root.Original.Element("before")),
            new XElement("cachedPhysicalQueryRetired",new XAttribute("version",1),new XAttribute("original",HistoricalHash(root.Image)),
                new XAttribute("physicalPublication",HistoricalHash(physicalBytes)),new XAttribute("terminal",HistoricalHash(terminalBytes)),
                new XAttribute("received",HistoricalHash(receivedBytes)),new XAttribute("notice",HistoricalHash(root.SettledNoticeImage))));
        string path=root.Path+".physical-query-retired";
        if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!received.Current())return false;
        // Every metadata query source and financial original is durable before
        // closing either native root. No fallible I/O follows resource closure.
        var native=proof.NativeSource;cachedCustodies.Remove(root.Operation);root.Dispose();native?.Dispose();return true;
    }
    private static bool IsCachedPhysicalQueryRetired(CachedCustody root)
    {
        try
        {
            string terminal=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",root.Operation.ToString("N")+".xml.same-owner-terminal");
            string received=terminal.Substring(0,terminal.Length-".same-owner-terminal".Length)+".same-owner-received";
            string[] files={root.Path+".physical-query-retired",root.Path+".physical-publication",terminal,received,root.Path+".settled-notice",root.Path+".physical"};
            long bytes=0;foreach(string file in files){long size=RebirthWorkstationCompletionPublication.PublishedLength(file);if(size<1)return false;bytes=checked(bytes+size);}
            if(!root.ReserveRetirementWorkspace(checked(16*bytes+65536)))return false;
            var retired=RebirthWorkstationCompletionPublication.ReadPrimary(files[0]);var node=retired?.Element("cachedPhysicalQueryRetired");
            if((int?)node?.Attribute("version")!=1||(string)retired.Attribute("operation")!=root.Operation.ToString("N")||
                (string)retired.Attribute("world")!=root.WorldGuid||(string)node.Attribute("original")!=HistoricalHash(root.Image))return false;
            for(int i=1;i<=4;i++)
            {
                string key=i==1?"physicalPublication":i==2?"terminal":i==3?"received":"notice";
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(files[i]);if(image==null||HistoricalHash(image)!=(string)node.Attribute(key))return false;
            }
            var physical=RebirthWorkstationCompletionPublication.ReadPrimary(files[5]);var actual=physical?.Element("actualCachedPhysical");
            if(actual==null||!MatchesRemoteCachedPhysicalSeal(root.Path,HistoricalHash(root.Image),physical,root.ReserveRetirementWorkspace)||
                !RebirthWorkstationCompletionOriginal.TryRead(actual.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var original)||
                !RebirthWorkstationCompletionOutbox.TryReadSaved(original,out _))return false;
            var active=ActiveBefore(physical);var delivery=original.Write().Element("delivery").Elements().Single();
            return RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedPhysicalActor(root.Path,HistoricalHash(root.Image),
                actual.Element("cachedPhysicalActorSaved"),active,delivery,root.ReserveRetirementWorkspace)&&
                root.Original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect") is XElement owner&&
                RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedBoundaryOwner(owner.Elements().SingleOrDefault());
        }
        catch{return false;}
    }
}
