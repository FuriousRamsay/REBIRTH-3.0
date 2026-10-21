using System;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // Capture only after the actual original native effect returns. These
    // immutable images describe that entered effect; they never qualify a
    // save, a restored native object, or another effect invocation by themselves.
    private static bool CaptureStationOutputReturnedEffect(UiOutputOriginal original,XElement observed)
    {
        if(!original.GridCallbackFence||!original.Current()||original.World.IsRemote()||
            original.Scope.Card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity is not TileEntityWorkstation tile||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,tile,out string birth)||birth!=original.StationBirth)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            var data=new PlayerDataFile();data.FromPlayer(original.Actor);
            if(data.id!=original.Actor.entityId||data.deaths!=original.Deaths||data.equipmentData==null||
                !original.OwnerBeforeBag.SequenceEqual(OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true)))||
                !original.OwnerBeforeBelt.SequenceEqual(OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false)))||
                !original.OwnerBeforeEquipment.SequenceEqual(data.equipmentData.ToArray())||!original.Current())return false;
            var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
            if(native.Length<6||native.Length>16*1024*1024||
                !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(tile,out var body,out int elapsed)||
                !PublishStationOutputOwnerNative(original.Path+".effect-owner.native",native)||!original.Current())return false;
            observed.Add(new XElement("stationReturnedEffect",new XAttribute("birth",birth),
                new XAttribute("queueIndex",original.StationQueueIndex),new XAttribute("ownerDigest",UiOriginalHash(native)),
                new XAttribute("ownerLength",native.Length),new XAttribute("elapsedOffset",elapsed),
                new XElement("nativeBody",Convert.ToBase64String(body))));
            return true;
        }
    }
}
