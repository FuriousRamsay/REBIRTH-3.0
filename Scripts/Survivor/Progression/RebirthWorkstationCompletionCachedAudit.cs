using System;
using System.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Explicit read-only audit. These observations never issue a source or
    // create an absent outbox/receipt and are not consumed by gameplay gates.
    internal static object CaptureCachedCompletions(string operation)
    {
        return new
        {
            retired=CaptureRetiredCachedCompletion(operation),
            metadata=RebirthProductiveEarnedUiDeferredTransport.CaptureHydrationMetadataAudit(),
            host=cachedCustodies.Values.Where(value=>operation==null||value.Operation.ToString("N")==operation).Select(value=>new
            {
                operation=value.Operation.ToString("N"),original=value.Image==null?null:HistoricalHash(value.Image),path=value.Path,
                active=value.Active,saved=value.SavedCurrent(),physicalObserved=value.PhysicalObserved,physical=value.Physical,
                nativeCurrent=value.NativeCurrent(),actualLoaded=value.Load?.Loaded?.Installed(value.World)==true,
                ownerSaved=value.OwnerSavedImage!=null&&CachedCustody.EvidenceCurrent(value.OwnerSavedPath,value.OwnerSavedImage),
                creatorCloseSaved=value.CreatorCloseObserved&&value.CreatorCloseImage!=null&&
                    RebirthWorkstationCompletionPublication.MatchesPublishedBytes(value.CreatorClosePath,value.CreatorCloseImage),
                restoreAttempted=value.RestoreAttempted,epoch=value.Epoch,
                originalPrimary=(string)((value.CurrentFull??value.Original.Element("cachedOriginal")?.Element("nativeOwningFullBoundary"))?.Element("primaryNative"))
            }).ToArray(),
            creator=cachedPhysicalSources.Values.Where(value=>operation==null||value.Operation.ToString("N")==operation).Select(value=>new
            {
                operation=value.Operation.ToString("N"),action=value.Action.ToString("N"),original=value.Grant.Original,
                group=value.Grant.Group.ToString("N"),
                ticket=value.Grant.Ticket.ToString("N"),session=value.WindowSession,nativeVersion=value.NativeVersion,
                current=value.Current(),prepared=value.Prepared,ready=value.Ready,physicalSaved=value.PhysicalSaved,
                courierAcknowledged=value.CourierAcknowledged,ownerRenewal=value.OwnerRenewal!=null,
                spaceSequence=value.Space?.Sequence??0,spaceSaved=value.SpaceSaved,
                attempted=value.Frame?.Attempted??false,nativeRan=value.Frame?.Observation.RanUpdate??false,
                fullPrimary=(string)value.Full.Element("primaryNative")
            }).ToArray()
        };
    }
    private static object CaptureRetiredCachedCompletion(string operation)
    {
        if(!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty||cachedCustodies.ContainsKey(id))return null;
        CachedCustody value=null;
        try
        {
            string path=IntentPath("server",id)+".cache.xml";long bytes=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(bytes<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8*bytes),out var memory))return null;
            value=new CachedCustody{Path=path,Operation=id,Closed=true,Allocation=memory,Reserved=checked(64L*1024*1024+8*bytes)};
            value.Original=RebirthWorkstationCompletionPublication.ReadPrimary(path);value.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            value.WorldGuid=(string)value.Original?.Attribute("world");
            if(value.WorldGuid!=RebirthWorkstationCompletionOutbox.CurrentGuid(GameManager.Instance?.World)||
                !(IsCachedPhysicalQueryRetired(value)||IsCachedGroupPhysicalRetired(value)||IsLoadedPhysicalCacheRetired(value)))return null;
            return new{operation,original=HistoricalHash(value.Image),physicalSaved=true,resourceRetired=true,
                originalPrimary=(string)value.Original.Element("cachedOriginal")?.Element("nativeOwningFullBoundary")?.Element("primaryNative")};
        }
        catch{return null;}
        finally{value?.Dispose();}
    }
}
