#if REBIRTH_DEBUG
using System;
using System.Globalization;
using System.Text;

internal sealed partial class RebirthVehicleTuningSession
{
    private int proposedBaseSlots,originalBaseSlots;
    private bool camperStorage;
    private void BuildStorageControls()
    {
        camperStorage=string.Equals(Entity.vehicle.GetName(),"rebirthCamper80s",StringComparison.OrdinalIgnoreCase);
        int originalTotal=Entity.bag?.SlotCount??0;
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=20,LabelKey="xuiRbTuneStorageActualTotal",HelpKey="xuiRbTuneStorageActualTotalHelp",Unit=Localization.Get("xuiRbTuneSlots"),Minimum=0,Maximum=Math.Max(1,originalTotal),OriginalNumber=originalTotal,ReadNumberOverride=()=>Entity.bag?.SlotCount??0});
        if(!camperStorage)return;
        originalBaseSlots=proposedBaseSlots=RebirthCamperStorageCapacity.BaseSlots;
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=0,SourceProposal=true,LabelKey="xuiRbTuneStorageBaseProposal",HelpKey="xuiRbTuneStorageBaseProposalHelp",Unit=Localization.Get("xuiRbTuneSlots"),Minimum=1,Maximum=originalBaseSlots,OriginalNumber=originalBaseSlots,
            ReadNumberOverride=()=>proposedBaseSlots,WriteNumberOverride=SetStorageProposal});
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=10,LabelKey="xuiRbTuneStorageActualBase",HelpKey="xuiRbTuneStorageActualBaseHelp",Unit=Localization.Get("xuiRbTuneSlots"),Minimum=0,Maximum=originalBaseSlots,OriginalNumber=originalBaseSlots,ReadNumberOverride=()=>RebirthCamperStorageCapacity.BaseSlots});
        string originalContainers=Localization.Get("xuiRbTuneUnavailable");
        try{var info=RebirthVehicleTuningStorageInfo.Capture(Entity);if(info.ModuleRuntimeKnown)originalContainers=info.InstalledAddedSlots+" "+Localization.Get("xuiRbTuneSlots");}catch{}
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=30,Numeric=false,LabelKey="xuiRbTuneStorageContainers",HelpKey="xuiRbTuneStorageContainersHelp",CapturedText=originalContainers,ReadTextOverride=()=>
        {var info=RebirthVehicleTuningStorageInfo.Capture(Entity);return info.ModuleRuntimeKnown?info.InstalledAddedSlots+" "+Localization.Get("xuiRbTuneSlots"):Localization.Get("xuiRbTuneUnavailable");}});
    }
    private void SetStorageProposal(float number)
    {
        int value=(int)Math.Round(number,MidpointRounding.AwayFromZero);
        var info=RebirthVehicleTuningStorageInfo.Capture(Entity);
        if(!info.ModuleRuntimeKnown||!info.ActualMatchesAuthoritative)throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageUnavailable"));
        if(value<1||value>RebirthCamperStorageCapacity.BaseSlots)throw new ArgumentException(Localization.Get("xuiRbTuneStorageMaximum"));
        var slots=Entity.bag.ItemGrid?.items;
        if(slots==null||slots.Length!=info.ActualBagSlots)throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageUnavailable"));
        // Check the base segment, never reinterpret container-owned cargo as base cargo.
        for(int slot=value;slot<info.BaseSlots&&slot<slots.Length;slot++)
            if(slots[slot]==null)throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageUnavailable"));
            else if(!slots[slot].IsEmpty())throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageOccupied"));
        proposedBaseSlots=value;
    }
    private void ResetStorageProposal(){if(camperStorage)proposedBaseSlots=originalBaseSlots;}
    private void ExportStorage(StringBuilder report)
    {
        if(!camperStorage){report.Append("STORAGE_READONLY actualSlots=").Append(Entity.bag?.SlotCount??0).AppendLine();return;}
        RebirthVehicleTuningStorageInfo.Snapshot info;
        try{info=RebirthVehicleTuningStorageInfo.Capture(Entity);}catch(Exception error){report.Append("STORAGE_UNAVAILABLE ").Append(error.Message).AppendLine();return;}
        report.Append("STORAGE_SOURCE_PROPOSAL baseSlots=").Append(proposedBaseSlots).Append(" original=").Append(originalBaseSlots)
            .Append(" currentSupportedBaseMaximum=").Append(RebirthCamperStorageCapacity.BaseSlots).AppendLine(" liveApplied=false");
        report.Append("STORAGE_ACTUAL baseSlots=").Append(info.BaseSlots).Append(" actualBagSlots=").Append(info.ActualBagSlots)
            .Append(" authoritativeTotalSlots=").Append(info.AuthoritativeTotalSlots).Append(" moduleRuntimeKnown=").Append(info.ModuleRuntimeKnown)
            .Append(" installedContainers=").Append(info.InstalledContainers).Append(" containerPositions=").Append(info.ContainerPositions)
            .Append(" containerAddedSlots=").Append(info.InstalledAddedSlots).Append(" slotEncodingCeiling=").Append(info.SlotEncodingCeiling)
            .Append(" syncPayloadByteCeiling=").Append(info.VehicleSyncPayloadByteCeiling).AppendLine();
    }
}
#endif

