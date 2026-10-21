using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool HasActualCachedPhysicalCustody(CachedCustody source)=>source?.Active==true&&source.PhysicalObserved&&source.SavedCurrent()&&
        source.SourceCurrent()&&source.NativeCurrent()&&
        (source.Load?.Loaded?.Installed(source.World)==true||cachedPhysicalActions.TryGetValue(source.Operation,out var action)&&
            ReferenceEquals(action.Issue?.Cache,source)&&action.PhysicalFinalObserved&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(action.FinalAcceptedPath,action.FinalAcceptedImage)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(source.World,source.Station,out var fence)&&fence==action.ReadFence);
    private static bool RetainActualSavedCachedPhysical(CachedCustody source)
    {
        if(!TryReadSavedCachedPhysical(source,out var original,out _))return false;
        var state=RebirthWorkstationCompletionOutbox.Retain(original,"server",source.World,source.Station);
        return state!=null&&(state.Saved||RebirthWorkstationCompletionOutbox.Save(state));
    }
    private static bool TryReadSavedCachedPhysical(CachedCustody source,out RebirthWorkstationCompletionOriginal original,out XElement physical)
    {
        original=null;physical=null;
        if(!HasActualCachedPhysicalCustody(source)||!source.Physical)return false;
        string path=source.Path+".physical";long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||!source.ReserveRetirementWorkspace(checked(16*length+65536)))return false;
        var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var observed=record?.Element("actualCachedPhysical");
        if(observed==null||(string)observed.Attribute("original")!=HistoricalHash(source.Image)||
            !RebirthWorkstationCompletionOriginal.TryRead(observed.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var value)||
            value.Operation!=source.Operation.ToString("N")||value.WorldGuid!=source.WorldGuid)return false;
        var initial=source.Original.Element("before").Elements().Single();var active=ActiveBefore(record);
        var delivery=value.Write().Element("delivery").Elements().Single();
        if(value.OwnerKey!=(string)initial.Attribute("startingOwnerKey")||value.Creation!=(string)initial.Attribute("startingOwnerCreation")||
            value.ViewerKey!=(string)initial.Attribute("viewer")||value.ViewerCreation!=(string)initial.Attribute("creation")||
            !RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedPhysicalActor(source.Path,HistoricalHash(source.Image),
                observed.Element("cachedPhysicalActorSaved"),active,delivery,source.ReserveRetirementWorkspace)||
            !MatchesRemoteCachedPhysicalSeal(source.Path,HistoricalHash(source.Image),record,source.ReserveRetirementWorkspace))return false;
        var native=ReadCachedSpaceBody(observed,"native");int offset=(int)observed.Attribute("elapsedOffset");
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,source.Native,source.Offset))return false;
        original=value;physical=record;return true;
    }
    private static bool TryAcceptCachedPhysicalNotice(EntityPlayer viewer,Guid operation,RebirthWorkstationCompletionOriginal reported,string challenge)
    {
        if(!cachedCustodies.TryGetValue(operation,out var source)||!TryReadSavedCachedPhysical(source,out var actual,out _)||
            actual.Digest!=reported.Digest)return false;
        var state=RebirthWorkstationCompletionOutbox.Retain(actual,"server",source.World,source.Station);
        if(state==null||!state.Saved&&!RebirthWorkstationCompletionOutbox.Save(state))return true;
        if(!RebirthWorkstationCompletionFixture.Hold("acceptedAck",actual.Operation))
            Ack(viewer,operation,"accepted",actual.OwnerKey,actual.Creation,actual.Digest,challenge);
        Settle(state);return true;
    }
}
