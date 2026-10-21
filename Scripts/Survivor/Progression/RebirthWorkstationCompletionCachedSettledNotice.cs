using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool TryPublishCachedSettledNotice(CachedCustody source)
    {
        if(!source.Physical||!source.PrimaryCurrent())return false;
        if(source.SettledNoticeImage!=null)return RebirthWorkstationCompletionPublication.MatchesPublishedBytes(source.Path+".settled-notice",source.SettledNoticeImage);
        try
        {
            var original=source.Original.Element("cachedOriginal");
            if(original.Element("nativeOwnerOriginal")!=null||original.Element("nativeOwnerLedger")!=null)
            {if(source.OwnerSavedImage==null||!CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage))return false;}
            else if(original.Element("remoteCachedOwnerEffect") is XElement remoteEffect)
            {if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedBoundaryOwner(remoteEffect.Elements().SingleOrDefault()))return false;}
            else if(!MatchesSavedCachedOwnerEffect(original.Element("cachedOwnerEffect")?.Elements().SingleOrDefault()))return false;
            string physicalPath=source.Path+".physical";long length=RebirthWorkstationCompletionPublication.PublishedLength(physicalPath);
            if(length<1||!source.ReserveRetirementWorkspace(checked(8*length+65536)))return false;
            var physical=RebirthWorkstationCompletionPublication.CopyPublishedBytes(physicalPath);if(physical==null)return false;
            if((bool?)original.Attribute("remoteCreator")==true&&!MatchesRemoteCachedPhysicalSeal(source.Path,HistoricalHash(source.Image),
                RebirthWorkstationCompletionPublication.ReadPrimary(physicalPath),source.ReserveRetirementWorkspace))return false;
            var notice=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement(source.Original.Element("before")),
                new XElement("cachedPrimarySettled",new XAttribute("original",HistoricalHash(source.Image)),new XAttribute("physical",HistoricalHash(physical)),
                    source.OwnerSavedPath==null?null:new XAttribute("ownerSaved",source.OwnerSavedPath),
                    source.OwnerSavedImage==null?null:new XAttribute("ownerSavedDigest",RebirthWorkstationCompletionOriginal.Hash(source.OwnerSavedImage))));
            string path=source.Path+".settled-notice";
            if(!RebirthWorkstationCompletionPublication.Write(path,notice)||!XNode.DeepEquals(notice,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            source.SettledNoticeImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);return source.SettledNoticeImage!=null;
        }
        catch{return false;}
    }
    private static void QueryCachedPrimarySettled(EntityPlayer actor,Guid operation,XElement request)
    {
        IDisposable allocation=null;
        try
        {
            if(!Guid.TryParseExact((string)request.Attribute("query"),"N",out var query)||query==Guid.Empty)return;
            string path=IntentPath("server",operation)+".cache.xml";
            long a=RebirthWorkstationCompletionPublication.PublishedLength(path),b=RebirthWorkstationCompletionPublication.PublishedLength(path+".physical"),c=RebirthWorkstationCompletionPublication.PublishedLength(path+".settled-notice");
            if(a<1||b<1||c<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*(a+b+c)+65536),out allocation))return;
            var original=RebirthWorkstationCompletionPublication.ReadPrimary(path);var notice=RebirthWorkstationCompletionPublication.ReadPrimary(path+".settled-notice");
            var settled=notice.Element("cachedPrimarySettled");var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            var physical=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".physical");
            string hash=HistoricalHash(bytes),world=RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world);
            if(settled==null||(string)original.Attribute("operation")!=operation.ToString("N")||(string)notice.Attribute("operation")!=operation.ToString("N")||
                (string)original.Attribute("world")!=world||(string)notice.Attribute("world")!=world||
                hash!=(string)request.Attribute(request.Name=="cachedOutputFinalQuery"?"cachedOriginal":"original")||hash!=(string)settled.Attribute("original")||HistoricalHash(physical)!=(string)settled.Attribute("physical"))return;
            if((bool?)original.Element("cachedOriginal")?.Attribute("remoteCreator")==true&&!MatchesRemoteCachedPhysicalSeal(path,hash,
                RebirthWorkstationCompletionPublication.ReadPrimary(path+".physical"),bytesToReserve=>
                    RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(16*(a+b+c)+bytesToReserve))))return;
            if(original.Element("cachedOriginal")?.Element("nativeOwnerOriginal")!=null||original.Element("cachedOriginal")?.Element("nativeOwnerLedger")!=null)
            {
                string ownerPath=(string)settled.Attribute("ownerSaved");if(ownerPath!=path+".owner-saved")return;
                long ownerLength=RebirthWorkstationCompletionPublication.PublishedLength(ownerPath);
                if(ownerLength<1||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(16*(a+b+c+ownerLength)+65536)))return;
                var owner=RebirthWorkstationCompletionPublication.ReadPrimary(ownerPath);
                if(owner==null||RebirthWorkstationCompletionOriginal.Hash(RebirthWorkstationCompletionPublication.ReadEvidence(ownerPath))!=(string)settled.Attribute("ownerSavedDigest"))return;
                var saved=owner.Element("cachedOwnerAlreadySaved");string ownerProof=(string)saved?.Attribute("ownerProof");
                long proofLength=RebirthWorkstationCompletionPublication.PublishedLength(ownerProof);
                if(proofLength<1||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(16*(a+b+c+ownerLength+proofLength)+65536))||
                    !CachedCustody.EvidenceCurrent(ownerProof,(string)saved.Element("proof")))return;
            }
            else if(original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect") is XElement remoteEffect)
            {if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedBoundaryOwner(remoteEffect.Elements().SingleOrDefault()))return;}
            else if(!MatchesSavedCachedOwnerEffect(original.Element("cachedOriginal")?.Element("cachedOwnerEffect")?.Elements().SingleOrDefault()))return;
            if(request.Name=="cachedOutputFinalQuery")
            {
                var final=DescribeCachedFinalQuery(actor,operation,request,path,hash,original,RebirthWorkstationCompletionPublication.ReadPrimary(path+".physical"),
                    size=>RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(16*(a+b+c)+size)));
                if(final!=null)Send(false,final,operation,actor.entityId);
                return;
            }
            // Terminal notice only. It creates no original/native source and
            // grants no craft, refund, receipt consumption or effect retry.
            Send(false,new XElement("ack",new XAttribute("world",world),new XAttribute("viewerCreation",RebirthCamperCharacterAccess.CurrentCreation(actor)),
                new XAttribute("stage","cachedPrimarySettled"),new XAttribute("digest",hash),new XAttribute("query",query.ToString("N")),
                new XAttribute("challenge",(string)request.Attribute("challenge"))),operation,actor.entityId);
        }
        catch{}
        finally{allocation?.Dispose();}
    }
    internal static void SendCachedPrimaryQuery(Guid operation,string original,Guid query,EntityPlayerLocal actor)
    {if(actor!=null&&ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)&&actor.world.IsRemote())Send(true,new XElement("cachedPrimaryQuery",new XAttribute("original",original),new XAttribute("query",query.ToString("N"))),operation,actor.entityId);}
}
