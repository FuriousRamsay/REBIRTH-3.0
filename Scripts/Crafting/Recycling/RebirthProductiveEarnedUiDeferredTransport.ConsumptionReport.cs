using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static byte[] ReportNative(XElement node,out int offset)
    {
        offset=(int)node.Attribute("offset");string text=node.Value;
        if(node.HasElements||node.Attributes().Count()!=1||text.Length<4||text.Length>4*((RebirthProductiveEarnedNativeBody.MaximumBodyBytes+2)/3))throw new InvalidDataException();
        var bytes=Convert.FromBase64String(text);
        if(bytes.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||Convert.ToBase64String(bytes)!=text)throw new InvalidDataException();
        return bytes;
    }
    // Host entry is the authenticated original dispatch, never a parsed-source
    // factory. Client emission exists only in the actual native consumption
    // callback; this validator binds that full report to the retained host
    // original before any After-PDS scope or removal permission is armed.
    internal static bool TryValidateConsumedReport(RecipientScope scope,XElement report,out XElement ownerAfter)
    {
        ownerAfter=null;
        try
        {
            if(scope?.Current()!=true||scope.ExpectedBefore==null||report?.Name!="deferredNativeConsumption"||report.Attributes().Count()!=11||
                report.Elements().Count()!=4||new[]{"membership","nativeBefore","nativeAfter","ownerAfter"}.Any(name=>report.Elements(name).Count()!=1)||
                report.Nodes().Any(node=>!(node is XElement)&&!(node is XText text&&string.IsNullOrWhiteSpace(text.Value)))||
                (int?)report.Attribute("version")!=1||ReadGuid(report,"group")!=scope.Hydration.Group||
                (string)report.Attribute("world")!=scope.Hydration.WorldKey||(string)report.Attribute("owner")!=scope.Owner||
                (string)report.Attribute("creation")!=scope.Creation||(int?)report.Attribute("entity")!=scope.Actor.entityId||
                (int?)report.Attribute("deaths")!=scope.Deaths||((long?)report.Attribute("window")??0)<1||
                (bool?)report.Attribute("nativeCheck")!=true||
                (int?)report.Attribute("nativeGiveExpCount")!=scope.Hydration.Sources.Length||
                (string)report.Attribute("beforeDigest")!=HydrationHash(OwnerFrameBytes(scope.ExpectedBefore)))return false;
            var expected=new XElement("membership",scope.Hydration.Header.Elements("aggregate").Select(value=>new XElement(value)));
            if(!XNode.DeepEquals(expected,report.Element("membership")))return false;
            var owner=report.Element("ownerAfter");var after=owner.Elements().SingleOrDefault();if(owner.HasAttributes||!OwnerFrame(after))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                var before=ReportNative(report.Element("nativeBefore"),out int beforeOffset);
                var actual=ReportNative(report.Element("nativeAfter"),out int afterOffset);
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,scope.Hydration.NativeBefore,scope.Hydration.BeforeOffset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var image,out ulong elapsed))return false;
                var selected=image.Completions.Where(row=>scope.Hydration.Sources.Any(source=>Row(row).SequenceEqual(source.Aggregate))).ToArray();
                if(selected.Length!=scope.Hydration.Sources.Length||selected.Any(row=>row.CrafterEntityID!=scope.Actor.entityId)||
                    image.Completions.Any(row=>row.CrafterEntityID==scope.Actor.entityId&&!selected.Any(value=>ReferenceEquals(row,value))))return false;
                image.Completions=image.Completions.Where(row=>!selected.Any(value=>ReferenceEquals(row,value))).ToArray();
                var expectedNative=RebirthProductiveEarnedNativeBody.Encode(image,elapsed,out int expectedOffset);
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(actual,afterOffset,expectedNative,expectedOffset))return false;
            }
            ownerAfter=after;return true;
        }
        catch{return false;}
    }
}
