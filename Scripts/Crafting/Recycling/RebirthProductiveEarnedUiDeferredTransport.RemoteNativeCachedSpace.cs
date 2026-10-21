using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static bool ValidateRemoteNativeCachedSpace(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,out XElement transfer)
        =>ValidateNativeCachedSpace(source,contribution,prior,priorOffset,proposed,proposedOffset,false,out transfer);
    internal static bool ValidateSameOwnerNativeCachedSpace(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,out XElement transfer)
        =>ValidateNativeCachedSpace(source,contribution,prior,priorOffset,proposed,proposedOffset,true,out transfer);
    private static bool ValidateNativeCachedSpace(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,
        byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,bool direct,out XElement transfer)
    {
        transfer=null;
        try
        {
            var evidence=contribution.Element("actualCachedSpaceTransfer");var full=evidence?.Element("cachedFull")?.Elements().SingleOrDefault();
            if(source?.CurrentViewer()!=true||evidence==null||full?.Name!="nativeOwningFullBoundary"||
                (string)evidence.Attribute("operation")!=source.Operation.ToString("N")||(string)full.Attribute("operation")!=source.Operation.ToString("N")||
                (long?)evidence.Attribute("nativeVersion") is not long version||version<1||(long?)full.Attribute("nativeVersion")!=version||
                (long?)evidence.Attribute("session") is not long session||session<1||(bool?)full.Attribute("freshCallback")!=true&&(bool?)full.Attribute("cachedCallback")!=true||
                !source.IsSavedFullBoundaryBefore(full)||(string)full.Element("recipeFull")!=(string)source.Before.Element("recipeFull"))return false;
            var before=CachedSpaceBytes(evidence,"before");var after=CachedSpaceBytes(evidence,"after");
            int beforeOffset=(int)evidence.Element("before").Attribute("offset"),afterOffset=(int)evidence.Element("after").Attribute("offset");
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,prior,priorOffset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(after,afterOffset,proposed,proposedOffset)||
                !RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var first,out ulong elapsed)||
                !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var last,out _))return false;
            if(direct)
            {
                if(contribution.Name!="sameOwnerEffect"||source.EarlierOwner.StartingEntityId!=source.Viewer.entityId||
                    (string)contribution.Element("nativeViewerEffect")?.Element("primaryNative")!=(string)full.Element("primaryNative"))return false;
            }
            else if(!RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(contribution,"nativeAddArgument"),out var argument)||
                argument.CraftedItemStack==null||argument.CraftedItemStack.count!=source.Recipe.count||argument.CrafterEntityID!=source.EarlierOwner.StartingEntityId||
                Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{argument.CraftedItemStack}))!=(string)full.Element("primaryNative"))return false;
            first.Output=last.Output;
            var onlyOutput=RebirthProductiveEarnedNativeBody.Encode(first,elapsed,out int offset);
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(onlyOutput,offset,after,afterOffset))return false;
            byte[] fullAfter=CachedSpaceBytes(full,"nativeAfter");int fullOffset=(int)full.Attribute("nativeElapsedOffset");
            if(!RebirthProductiveEarnedNativeBody.TryRead(fullAfter,fullOffset,out var boundary,out ulong boundaryElapsed))return false;
            boundary.Output=last.Output;
            var unchanged=RebirthProductiveEarnedNativeBody.Encode(boundary,boundaryElapsed,out offset);
            if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(unchanged,offset,after,afterOffset))return false;
            foreach(string name in new[]{"bagBefore","beltBefore","bagAfter","beltAfter"})
            {
                var node=evidence.Element(name);if(node==null||node.HasElements||node.Value.Length>4*1024*1024)return false;
                // Exact full native frames, not item counts. Their native
                // player persistence remains independently required.
                RebirthProductiveEarnedUiRemote.ReadGrid(evidence,name);
            }
            transfer=evidence;return true;
        }
        catch{return false;}
    }
    private static byte[] CachedSpaceBytes(XElement parent,string name)
    {
        var node=parent.Element(name);if(node==null||node.HasElements||node.Value.Length>3*1024*1024)throw new InvalidDataException();
        var bytes=Convert.FromBase64String(node.Value);if(bytes.Length<1||bytes.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||Convert.ToBase64String(bytes)!=node.Value)throw new InvalidDataException();return bytes;
    }
}
