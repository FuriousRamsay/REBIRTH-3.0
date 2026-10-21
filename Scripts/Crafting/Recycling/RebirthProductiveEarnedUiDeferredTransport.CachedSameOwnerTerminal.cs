using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static readonly Dictionary<Guid,RebirthWorkstationCompletionPipeline.CachedPhysicalPublication> cachedSameOwnerNative=
        new Dictionary<Guid,RebirthWorkstationCompletionPipeline.CachedPhysicalPublication>();
    internal static bool TrySealCachedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.CachedPhysicalPublication proof)
    {
        if(proof?.IsSameOwner!=true||!proof.CurrentSavedSource())return false;
        var root=proof.Root;
        string originalPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",proof.Operation.ToString("N")+".xml");
        long originalLength=RebirthWorkstationCompletionPublication.PublishedLength(originalPath);
        if(originalLength<1||!root.ReserveRetirementWorkspace(checked(16*(originalLength+proof.MeasureImage()+65536))))return false;
        var original=RebirthWorkstationCompletionPublication.ReadPrimary(originalPath);
        var originalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(originalPath);
        var before=original?.Element("before")?.Element("remoteNativeOriginal");var cached=root.Original.Element("cachedOriginal");
        if(before==null||originalBytes==null||(string)cached.Attribute("source")!=RebirthWorkstationCompletionPipeline.CachedFileHash(originalBytes)||
            (string)cached.Attribute("nonce")!=(string)before.Attribute("nonce")||
            (string)before.Attribute("viewer")!=(string)root.Original.Element("before").Elements().Single().Attribute("viewer")||
            (string)before.Attribute("creation")!=(string)root.Original.Element("before").Elements().Single().Attribute("creation"))return false;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",proof.Operation.ToString("N")),new XAttribute("world",root.WorldGuid),
            new XElement("before",new XElement("nativeSameOwnerSaved",new XAttribute("version",1),new XAttribute("nonce",(string)before.Attribute("nonce")),
                new XAttribute("source",RebirthWorkstationCompletionPipeline.CachedFileHash(originalBytes)),new XAttribute("native",proof.NativeDigest),
                new XElement("cachedPhysicalPublication",new XAttribute("path",proof.Path),new XAttribute("digest",RebirthWorkstationCompletionPipeline.CachedFileHash(proof.CopyImage()))))));
        string terminal=originalPath+".same-owner-terminal";
        var prior=RebirthWorkstationCompletionPublication.ReadPrimary(terminal);
        if(prior!=null&&!XNode.DeepEquals(prior,record)||!RebirthWorkstationCompletionPublication.Write(terminal,record)||
            !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(terminal))||!proof.CurrentSavedSource())return false;
        if(cachedSameOwnerNative.TryGetValue(proof.Operation,out var retained)&&!ReferenceEquals(retained,proof))return false;
        cachedSameOwnerNative[proof.Operation]=proof;
        if(proof.NativeSource!=null)
        {
            if(!proof.NativeSource.TryReleaseStationWorkHold(proof))return false;
            if(sameOwnerNative.TryGetValue(proof.Operation,out var old)&&ReferenceEquals(old.Source,proof.NativeSource))
            {sameOwnerNative.Remove(proof.Operation);old.Dispose();}
        }
        return true;
    }
    internal static bool TryGetSavedCachedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,
        out RebirthWorkstationCompletionPipeline.CachedPhysicalPublication proof)
    {
        proof=null;
        return source!=null&&cachedSameOwnerNative.TryGetValue(source.Operation,out proof)&&ReferenceEquals(proof.NativeSource,source)&&proof.CurrentSavedSource();
    }
    internal static bool TryGetSavedCachedSameOwnerTerminal(Guid operation,out RebirthWorkstationCompletionPipeline.CachedPhysicalPublication proof)
    {proof=null;return cachedSameOwnerNative.TryGetValue(operation,out proof)&&proof.IsSameOwner&&proof.CurrentSavedSource();}
    internal static void ForgetCachedSameOwnerTerminal(RebirthWorkstationCompletionPipeline.CachedCustody source)
    {if(cachedSameOwnerNative.TryGetValue(source.Operation,out var proof)&&ReferenceEquals(proof.Root,source))cachedSameOwnerNative.Remove(source.Operation);}
}

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static string CachedFileHash(byte[] bytes)=>bytes==null?null:HistoricalHash(bytes);
    internal sealed partial class NativeHostOriginal
    {
        internal bool TryReleaseStationWorkHold(CachedPhysicalPublication proof)
        {
            if(proof==null||!ReferenceEquals(proof.NativeSource,this)||proof.Operation!=Operation||
                !ReferenceEquals(proof.World,World)||!ReferenceEquals(proof.Station,Station)||!proof.CurrentSavedSource())return false;
            stationWorkReleased=true;return true;
        }
    }
}
