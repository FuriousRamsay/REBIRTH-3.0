using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Resource closure only, after a real saved next Before belonging to the
    // SAME retained earlier owner. It does not lend ownership to another row.
    private static bool RetireRemoteCachesAtSavedSuccessor(NativeHostOriginal next)
    {
        if(next?.CurrentViewer()!=true||!next.BeforeSaved||!next.IsSavedActiveBefore(next.Before))return false;
        var values=cachedCustodies.Values.Where(value=>value.Active&&value.RemoteSource!=null&&value.Operation!=next.Operation&&
            ReferenceEquals(value.World,next.World)&&ReferenceEquals(value.Station,next.Station)&&ReferenceEquals(value.RemoteSource.EarlierOwner,next.EarlierOwner)).ToArray();
        if(values.Length==0)return true;
        try
        {
            foreach(var cache in values)
            {
                if(!cache.Physical||!cache.Saved||!TryPublishCachedSettledNotice(cache))return false;
                long length=RebirthWorkstationCompletionPublication.PublishedLength(next.Path+".saved");
                if(length<1||!cache.ReserveRetirementWorkspace(checked(16*length+65536)))return false;
                var saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(next.Path+".saved");if(saved==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",cache.Operation.ToString("N")),new XAttribute("world",cache.WorldGuid),
                    new XElement("remoteCachedSuccessorRetired",new XAttribute("version",1),new XAttribute("original",HistoricalHash(cache.Image)),
                        new XAttribute("next",next.Operation.ToString("N")),new XAttribute("ownerPurpose",RebirthProductiveEarnedUiQueueOwner.Export(next.EarlierOwner)),
                        new XAttribute("savedPath",next.Path+".saved"),new XAttribute("saved",HistoricalHash(saved)),
                        new XAttribute("notice",HistoricalHash(cache.SettledNoticeImage))));
                string path=cache.Path+".remote-successor-retired";
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                    !next.CurrentViewer()||!next.IsSavedActiveBefore(next.Before))return false;
            }
            // Prepare/readback every record before dropping any original.
            foreach(var cache in values){cachedCustodies.Remove(cache.Operation);cache.Dispose();}
            return true;
        }
        catch{return false;}
    }
    private static bool IsRemoteCacheSuccessorRetired(CachedCustody cache)
    {
        try
        {
            string retiredPath=cache.Path+".remote-successor-retired";
            long retiredLength=RebirthWorkstationCompletionPublication.PublishedLength(retiredPath);
            if(retiredLength<1||!cache.ReserveRetirementWorkspace(checked(16*retiredLength+65536)))return false;
            var root=RebirthWorkstationCompletionPublication.ReadPrimary(retiredPath);var retired=root?.Element("remoteCachedSuccessorRetired");
            if((int?)retired?.Attribute("version")!=1||(string)root.Attribute("operation")!=cache.Operation.ToString("N")||
                (string)root.Attribute("world")!=(string)cache.Original.Attribute("world")||(string)retired.Attribute("original")!=HistoricalHash(cache.Image)||
                (string)retired.Attribute("ownerPurpose")!=(string)cache.Original.Element("cachedOriginal")?.Attribute("queueOwner")||
                !Guid.TryParseExact((string)retired.Attribute("next"),"N",out var next)||next==Guid.Empty||next==cache.Operation)return false;
            string path=(string)retired.Attribute("savedPath");
            if(path!=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",next.ToString("N")+".xml.saved"))return false;
            long a=RebirthWorkstationCompletionPublication.PublishedLength(path),b=RebirthWorkstationCompletionPublication.PublishedLength(cache.Path+".settled-notice");
            if(a<1||b<1||!cache.ReserveRetirementWorkspace(checked(16*(retiredLength+a+b)+65536)))return false;
            var saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);var notice=RebirthWorkstationCompletionPublication.CopyPublishedBytes(cache.Path+".settled-notice");
            var savedRoot=RebirthWorkstationCompletionPublication.ReadPrimary(path);
            var noticeRoot=RebirthWorkstationCompletionPublication.ReadPrimary(cache.Path+".settled-notice");
            return saved!=null&&notice!=null&&HistoricalHash(saved)==(string)retired.Attribute("saved")&&HistoricalHash(notice)==(string)retired.Attribute("notice")&&
                (string)savedRoot?.Attribute("operation")==next.ToString("N")&&(string)savedRoot.Attribute("world")==cache.WorldGuid&&
                savedRoot.Element("before")?.Element("remoteNativeBeforeSaved")!=null&&
                (string)noticeRoot?.Attribute("operation")==cache.Operation.ToString("N")&&(string)noticeRoot.Attribute("world")==cache.WorldGuid&&
                (string)noticeRoot.Element("cachedPrimarySettled")?.Attribute("original")==HistoricalHash(cache.Image);
        }
        catch{return false;}
    }
}
