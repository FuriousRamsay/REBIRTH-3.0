using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Called only while describing an actual native cached hydration. This
    // preserves historical identity; it does not recreate an action or Ready.
    private static bool TryDescribeRetainedCachedAction(CachedCustody cache,out Guid ticket,out Guid action)
    {
        ticket=action=Guid.Empty;
        try{return TryDescribeRetainedCachedActionCore(cache,out ticket,out action);}
        catch{ticket=action=Guid.Empty;return false;}
    }
    private static bool TryDescribeRetainedCachedActionCore(CachedCustody cache,out Guid ticket,out Guid action)
    {
        ticket=action=Guid.Empty;
        if(cache==null||!cache.Active||!cache.SourceCurrent()||!cache.SavedCurrent()||!cache.NativeCurrent())return false;
        string path=cache.Path+".remote-action."+cache.Epoch;
        if(File.Exists(path+".tmp"))return false;
        if(!File.Exists(path))return true;
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>RebirthWorkstationCompletionPublication.MaximumBytes)return false;
        var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);
        var attempt=record?.Element("cachedPhysicalAttempt");var request=attempt?.Element("cachedPhysicalPrepare");
        // Send adds a per-connection challenge before the host sees the
        // request. Older attempt records retained that transport attribute.
        // It is not part of the immutable five-field action identity.
        if(request!=null){request=new XElement(request);request.Attribute("challenge")?.Remove();}
        var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
        if(image==null||(string)record?.Attribute("operation")!=cache.Operation.ToString("N")||(string)record.Attribute("world")!=cache.WorldGuid||
            !XNode.DeepEquals(record.Element("before"),cache.Original.Element("before"))||
            !XNode.DeepEquals(attempt?.Element("workstationCompletionIntent"),cache.Original)||
            request==null||request.HasElements||request.Attributes().Count()!=5||
            (string)request.Attribute("original")!=HistoricalHash(cache.Image)||
            !Guid.TryParseExact((string)request.Attribute("group"),"N",out var group)||group==Guid.Empty||
            (long?)request.Attribute("session") is not long session||session<1||
            !Guid.TryParseExact((string)request.Attribute("ticket"),"N",out ticket)||ticket==Guid.Empty||
            !Guid.TryParseExact((string)request.Attribute("action"),"N",out action)||action==Guid.Empty||
            !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,image))
        {ticket=action=Guid.Empty;return false;}
        return true;
    }
}
