using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Historical terminal evidence only. This never creates a native reader,
    // creator grant, cached primary, queue witness or delivery permission.
    private static bool MatchesRemoteCachedPhysicalSeal(string path,string originalHash,XElement physical,Func<long,bool> reserve)
    {
        try
        {
            long physicalLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".physical");
            long sealLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".remote-physical-saved");
            if(physicalLength<1||sealLength<1||!reserve(checked(16*(physicalLength+sealLength)+65536)))return false;
            var seal=RebirthWorkstationCompletionPublication.ReadPrimary(path+".remote-physical-saved");var source=seal?.Element("remoteCachedPhysicalSaved");
            if(source==null||(string)source.Attribute("original")!=originalHash||
                (string)source.Attribute("physical")!=RebirthWorkstationCompletionOriginal.Hash(physical.ToString(SaveOptions.DisableFormatting)))return false;
            string savedPath=(string)source.Attribute("savedPath");
            if(savedPath!=path+".saved")
            {string prefix=path+".saved.";if(savedPath==null||!savedPath.StartsWith(prefix,StringComparison.Ordinal)||
                !int.TryParse(savedPath.Substring(prefix.Length),out int sequence)||sequence<0||sequence>=128||savedPath!=prefix+sequence)return false;}
            long savedLength=RebirthWorkstationCompletionPublication.PublishedLength(savedPath);
            if(savedLength<1||!reserve(checked(16*(physicalLength+sealLength+savedLength)+65536)))return false;
            var savedBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(savedPath);
            if(savedBytes==null||HistoricalHash(savedBytes)!=(string)source.Attribute("saved"))return false;
            var publication=RebirthWorkstationCompletionPublication.ReadPrimary(savedPath);var native=publication?.Element("cachedNativePublication");
            var delivery=physical.Element("actualCachedPhysical");
            return (string)publication?.Attribute("operation")== (string)physical.Attribute("operation")&&
                (string)publication.Attribute("world")== (string)physical.Attribute("world")&&
                (string)native?.Attribute("original")==originalHash&&(string)delivery?.Attribute("original")==originalHash&&
                (bool?)delivery.Attribute("remote")==true&&
                (string)native.Element("native")== (string)delivery.Element("native")&&
                (int?)native.Attribute("elapsedOffset")== (int?)delivery.Attribute("elapsedOffset");
        }
        catch{return false;}
    }
}
