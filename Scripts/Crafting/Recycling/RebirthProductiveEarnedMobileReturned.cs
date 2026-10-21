using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    // Called only by the actual authoritative TickMobile finally, with its
    // own success bit set after material, fuel and clock suffix returned.
    internal static void ObserveMobileReturned(RebirthCamperStationState station,bool returned)
    {
        if(!Enabled||station?.MobileOwner==null||!ReferenceEquals(station.MobileOwner,station.BoundVehicle)||
            !retained.TryGetValue(station,out var original)||original.Artifact==null)return;
        if(!returned){if(original.MobileAwaitingReturn||original.OutputTimerObserved)original.MobileReturnUnknown=true;return;}
        if(original.MobileReturnUnknown)return;
        if((original.OutputTimerObserved||original.MobileAwaitingReturn)&&!FreezeMobileReturned(original))return;
        RetryMobileReturnedPublication(original);
    }
    private static bool FreezeMobileReturned(Attempt original)
    {
        if(original.MobileReturnedImage!=null)return true;
        try
        {
            var station=(RebirthCamperStationState)original.Station;
            // This serializer workspace and one complete frozen RCS image were
            // charged before admitting original timer/tool work. No decoder or
            // later mutable current-state adoption is needed at return.
            int length=RebirthCamperStationSnapshot.MeasureLength(station.InstallationId,station,RebirthStationSnapshotEvidence.MaximumSnapshotBytes);
            original.MobileReturnedImage=RebirthCamperStationSnapshot.Capture(station.InstallationId,station);
            if(original.MobileReturnedImage.Length!=length)throw new InvalidOperationException("Mobile returned image changed.");
            return true;
        }
        catch{original.MobileReturnUnknown=true;return false;}
    }
    private static void RetryMobileReturnedPublication(Attempt original)
    {
        if(original.MobileReturnUnknown||original.MobileReturnedImage==null)return;
        if(original.OutputTimerObserved){CaptureReturnedMobileTerminal(original);return;}
        if(!original.MobileAwaitingReturn)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedMobileOrigin.PublishInitialReturned((RebirthCamperStationState)original.Station,
                original.Artifact,original.Frame,original.MobileReturnedImage))return;
        }
        original.MobileAwaitingReturn=false;original.MobileReturnedPublication=true;
        try{original.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(original.Station,original.Artifact);}
        finally{original.MobileReturnedPublication=false;}
    }
    internal static bool IsMobileReturnedPublication(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
        =>station is RebirthCamperStationState&&retained.TryGetValue(station,out var original)&&
            ReferenceEquals(original.Artifact,artifact)&&original.MobileReturnedPublication&&!original.MobileAwaitingReturn;
    internal static bool BlocksMobileGrid(RebirthCamperStationState station)
        =>Enabled&&station!=null&&(retained.ContainsKey(station)||TryNativeMarker(station,out _));
}
