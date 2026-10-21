using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool HasPendingRemoteCachedPrimary(NativeHostOriginal source)=>source!=null&&
        cachedCustodies.TryGetValue(source.Operation,out var cache)&&cache.Active&&!cache.Physical&&
        ReferenceEquals(cache.World,source.World)&&ReferenceEquals(cache.Station,source.Station)&&
        (bool?)cache.Original.Element("cachedOriginal")?.Attribute("remoteCreator")==true;
    private static void ObserveRemoteCachedBoundaryAccepted(RemoteNativeEvent actual)
    {
        var proof=actual.CachedBoundary;
        if(proof==null||!actual.IsActualAcceptedRead()||!proof.Current())return;
        if(cachedCustodies.TryGetValue(actual.Source.Operation,out var prior))
        {RefreshRemoteCachedBoundary(prior,actual);return;}
        if(cachedCustodies.Count>=4)return;
        CachedCustody value=null;
        try
        {
            var source=actual.Source;
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof.Full)+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof.Before)+
                (proof.Ledger==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof.Ledger))+
                (proof.Owner?.MeasureEvidence()??0)+2L*proof.Body.Length+65536;
            long reserved=checked(64L*1024*1024+16*measured);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(reserved,out var allocation))return;
            value=new CachedCustody{Operation=source.Operation,World=source.World,Station=source.Station,Chunk=source.Station.GetChunk(),
                WorldGuid=source.WorldGuid,Save=source.Save,Path=IntentPath("server",source.Operation)+".cache.xml",Allocation=allocation,Reserved=reserved,
                Memory=source.Memory,OwnMemory=false,BorrowedMemory=source.Retain(),RemoteSource=source};
            if(value.BorrowedMemory==null)return;
            XElement ownerEvidence=proof.Owner?.CopyEvidence();
            if(proof.Owner!=null&&(ownerEvidence==null||!proof.Owner.Current()))return;
            value.Original=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                new XElement("before",new XElement(proof.Before)),new XElement("cachedOriginal",new XAttribute("owner",source.EarlierOwner.OriginalOwnerKey),
                new XAttribute("creation",source.EarlierOwner.Creation),new XAttribute("queueOwner",RebirthProductiveEarnedUiQueueOwner.Export(source.EarlierOwner)),
                new XAttribute("remoteCreator",true),new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("source",source.SourceHash),
                new XElement(proof.Full),proof.Owner==null?null:new XElement("remoteCachedOwnerEffect",ownerEvidence),
                proof.Ledger==null?null:new XElement("nativeOwnerLedger",CachedRemoteLedger(proof.Ledger))));
            if(!RebirthWorkstationCompletionPublication.Write(value.Path,value.Original))return;
            value.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(value.Path);if(value.Image==null)return;
            value.CurrentFull=new XElement(proof.Full);value.Native=(byte[])proof.Body.Clone();value.Offset=proof.Offset;
            if(proof.Ledger!=null)
            {
                if(source.Station.CraftCompleteList?.Count>4||!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(source.World,source.Station,out var fence))return;
                value.RemoteReceiptRows=source.Station.CraftCompleteList?.ToArray();value.RemoteReceiptReadFence=fence;
            }
            value.MarkerKey="RebirthCompletionCachedV1:"+source.Operation.ToString("N");
            value.Marker=Encoding.ASCII.GetBytes(source.Operation.ToString("N")+":"+HistoricalHash(value.Image));
            if(value.Chunk.ChunkCustomData.dict.TryGetValue(value.MarkerKey,out var old))
            {if(old.TriggerWriteDataDelegate!=null||old.data==null||!old.data.SequenceEqual(value.Marker))return;}
            else
            {value.Chunk.ChunkCustomData.Set(value.MarkerKey,new ChunkCustomData(value.MarkerKey,ulong.MaxValue,false){data=(byte[])value.Marker.Clone()});value.Chunk.isModified=true;value.Station.setModified();}
            if(!value.NativeCurrent())return;
            cachedCustodies.Add(value.Operation,value);value=null;
        }
        catch{/* Native receipt/effects remain owned; this never grants retry. */}
        finally{value?.Dispose();}
    }
    private static XElement CachedRemoteLedger(XElement ledger)
    {var copy=new XElement(ledger);copy.SetAttributeValue("cachedOriginal",true);copy.Elements("fullBoundary").Remove();copy.Elements("activeBefore").Remove();return copy;}
    private static void RefreshRemoteCachedBoundary(CachedCustody cache,RemoteNativeEvent actual)
    {
        var proof=actual.CachedBoundary;
        if(!cache.Active||cache.Physical||cache.Epoch>=64||!cache.SourceCurrent()||
            !ReferenceEquals(cache.World,actual.Source.World)||!ReferenceEquals(cache.Station,actual.Source.Station)||
            (bool?)cache.Original.Element("cachedOriginal")?.Attribute("remoteCreator")!=true||
            (string)proof.Full.Element("primaryNative")!=(string)cache.Original.Element("cachedOriginal").Element("nativeOwningFullBoundary").Element("primaryNative"))return;
        try
        {
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(proof.Full)+(proof.Owner?.MeasureEvidence()??0)+2L*actual.Body.Length+65536;
            if(!cache.ReserveRetirementWorkspace(checked(16*measured)))return;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                new XElement(cache.Original.Element("before")),new XElement("remoteCachedFullAccepted",new XAttribute("original",HistoricalHash(cache.Image)),
                    new XAttribute("dispatch",actual.Dispatch.ToString("N")),new XAttribute("epoch",cache.Epoch+1),new XElement(proof.Full)));
            string path=cache.Path+".remote-full."+(cache.Epoch+1);
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            var image=RebirthWorkstationCompletionPublication.ReadEvidence(path);if(image==null)return;
            var full=new XElement(proof.Full);var native=(byte[])actual.Body.Clone();
            cache.Watch?.Dispose();cache.Watch=null;cache.Saved=false;cache.Native=native;cache.Offset=actual.Offset;
            cache.CurrentFull=full;cache.SpacePath=path;cache.SpaceImage=image;cache.Epoch++;
            if(proof.Ledger!=null&&cache.Station.CraftCompleteList?.Count<=4&&
                RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(cache.World,cache.Station,out var fence))
            {cache.RemoteReceiptRows=cache.Station.CraftCompleteList.ToArray();cache.RemoteReceiptReadFence=fence;}
        }
        catch{/* Keep original primary and all unresolved custody. */}
    }
}
