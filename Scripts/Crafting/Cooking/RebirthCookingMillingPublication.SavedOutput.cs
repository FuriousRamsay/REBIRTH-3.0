using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthCookingMillingPublication
{
    // Actual native sync return qualifies a projection, not disk persistence.
    // Both managed milling sources retain their combined output/queue until
    // the owning native chunk or vehicle file independently proves that image.
    private static bool SavedOutputCurrent(Original original)
    {
        if(original?.NativeAccepted!=true||!original.MetadataCurrent()||!original.Terminal()||original.SavedBody==null)return false;
        if(original.Vehicle!=null)
            return original.Station is RebirthCamperStationState mobile&&ReferenceEquals(mobile.BoundVehicle,original.Vehicle)&&
                !RebirthCamperModuleRuntime.For(original.Vehicle).TransferPending&&
                RebirthCamperStationSnapshot.Capture(mobile.InstallationId,mobile).SequenceEqual(original.SavedBody);
        return RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,original.Station,out var birth)&&birth==original.StationBirth&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.SavedBody,original.SavedOffset);
    }
    private static bool ObserveSavedNativeOutput(Original original)
    {
        if(!original.NativeAccepted||!original.MetadataCurrent()||!original.Terminal())return false;
        if(original.SavedBody==null)
        {
            byte[] body;int offset=-1;
            if(original.Station is RebirthCamperStationState mobile)body=RebirthCamperStationSnapshot.Capture(mobile.InstallationId,mobile);
            else if(!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out body,out offset))return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*body.Length+65536),out var allocation))return false;
            original.SavedBody=body;original.SavedOffset=offset;original.SavedMemory=allocation;
        }
        if(!SavedOutputCurrent(original))return false;
        XElement saved;
        if(original.Vehicle!=null)
        {
            if(original.SavedVehicle==null)original.SavedVehicle=RebirthCamperSavedVehicle.RequestSave(original.Vehicle);
            if(!RebirthCamperSavedVehicle.Verify(original.Vehicle,original.SavedVehicle,()=>SavedOutputCurrent(original)))return false;
            saved=new XElement("cookingMillingSavedVehicle",new XAttribute("entity",original.SavedVehicle.Id),
                new XAttribute("class",original.SavedVehicle.Class),new XElement("vehicleBag",Convert.ToBase64String(original.SavedVehicle.Bag)),
                new XElement("vehicleEntity",Convert.ToBase64String(original.SavedVehicle.Entity)));
        }
        else
        {
            if(original.SavedWatch?.Current()!=true)
            {
                original.SavedWatch?.Dispose();original.SavedWatch=null;
                if(!RebirthRemoteResourceNativeBinding.TryResolve(original.World,RemoteResourceIdentity.Workstation(original.Station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,original.StationBirth,out var binding))return false;
                original.SavedWatch=RebirthRemoteResourceNativePublication.Begin(binding,()=>SavedOutputCurrent(original));
            }
            if(original.SavedWatch==null||!original.SavedWatch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string digest))return false;
            saved=new XElement("cookingMillingSavedTile",new XAttribute("birth",original.StationBirth),
                new XAttribute("nativePath",publication.Path),new XAttribute("nativeDigest",publication.PayloadDigest),
                new XAttribute("tileOffset",offset),new XAttribute("tileLength",length),new XAttribute("tileDigest",digest));
        }
        saved.Add(new XAttribute("original",original.Path),new XAttribute("elapsedOffset",original.SavedOffset),
            new XElement("nativeBody",Convert.ToBase64String(original.SavedBody)),new XElement("queue",Convert.ToBase64String(original.QueueAfter)),
            new XElement("combinedOutput",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Output))));
        var record=Envelope(original,original.OriginalImage,saved);
        return SavedOutputCurrent(original)&&RebirthWorkstationCompletionPublication.Write(original.Path+".saved",record)&&
            XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".saved"))&&SavedOutputCurrent(original);
    }
}
