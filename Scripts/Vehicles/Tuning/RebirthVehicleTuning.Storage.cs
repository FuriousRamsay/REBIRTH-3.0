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
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=0,SourceProposal=true,LabelKey="xuiRbTuneStorageBaseProposal",HelpKey="xuiRbTuneStorageBaseProposalHelp",Unit=Localization.Get("xuiRbTuneSlots"),Minimum=1,Maximum=RebirthCamperStorageCapacity.MaximumNativeSlots,OriginalNumber=originalBaseSlots,
            ReadNumberOverride=()=>proposedBaseSlots,WriteNumberOverride=SetStorageProposal});
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=10,LabelKey="xuiRbTuneStorageActualBase",HelpKey="xuiRbTuneStorageActualBaseHelp",Unit=Localization.Get("xuiRbTuneSlots"),Minimum=0,Maximum=originalBaseSlots,OriginalNumber=originalBaseSlots,ReadNumberOverride=()=>RebirthCamperStorageCapacity.BaseSlots});
        string originalContainers=Localization.Get("xuiRbTuneUnavailable");
        try{var info=RebirthVehicleTuningStorageInfo.Capture(Entity);if(info.ModuleRuntimeKnown)originalContainers=info.InstalledAddedSlots+" "+Localization.Get("xuiRbTuneSlots");}catch{}
        Controls.Add(new Control{Owner=this,Category=StorageCategory,Icon="rb_tuner_storage",OrderHint=30,Numeric=false,LabelKey="xuiRbTuneStorageContainers",HelpKey="xuiRbTuneStorageContainersHelp",CapturedText=originalContainers,ReadTextOverride=()=>
        {var info=RebirthVehicleTuningStorageInfo.Capture(Entity);return info.ModuleRuntimeKnown?info.InstalledAddedSlots+" "+Localization.Get("xuiRbTuneSlots"):Localization.Get("xuiRbTuneUnavailable");}});
    }
    private void SetStorageProposal(float number)
    {
        // Logging only: no runtime owner, cargo inspection, resize or save mutation.
        if(float.IsNaN(number)||float.IsInfinity(number)||number<1||number>RebirthCamperStorageCapacity.MaximumNativeSlots)
            throw new ArgumentException(Localization.Get("xuiRbTuneStorageMaximum"));
        int value=(int)Math.Round(number,MidpointRounding.AwayFromZero);
        proposedBaseSlots=value;
    }
    private void ResetStorageProposal(){if(camperStorage)proposedBaseSlots=originalBaseSlots;}
    private void ExportStorage(StringBuilder report)
    {
        if(!camperStorage){report.Append("STORAGE_READONLY actualSlots=").Append(Entity.bag?.SlotCount??0).AppendLine();return;}
        // Export the chosen number even when live module observations are unavailable.
        report.Append("STORAGE_SOURCE_PROPOSAL slotsToExport=").Append(proposedBaseSlots).Append(" baseSlots=").Append(proposedBaseSlots).Append(" original=").Append(originalBaseSlots)
            .Append(" proposalSlotEncodingMaximum=").Append(RebirthCamperStorageCapacity.MaximumNativeSlots).AppendLine(" liveApplied=false");
        report.AppendLine("STORAGE_LIMITS proposalOnly=true; native slot-count encoding ceiling=32767; full EntityCreationData save/spawn and VehicleDataSync frames each have a 65535-byte ceiling; actual usable runtime capacity depends on cargo, item metadata, locks, preferences and entity overhead. Proposal is not a validated runtime capacity. Installed container capacity is additional to this base-slot proposal.");
        RebirthVehicleTuningStorageInfo.Snapshot info;
        try{info=RebirthVehicleTuningStorageInfo.Capture(Entity);}catch(Exception error){report.Append("STORAGE_UNAVAILABLE ").Append(error.Message).AppendLine();return;}
        report.Append("STORAGE_ACTUAL baseSlots=").Append(info.BaseSlots).Append(" actualBagSlots=").Append(info.ActualBagSlots)
            .Append(" authoritativeTotalSlots=").Append(info.AuthoritativeTotalSlots).Append(" moduleRuntimeKnown=").Append(info.ModuleRuntimeKnown)
            .Append(" installedContainers=").Append(info.InstalledContainers).Append(" containerPositions=").Append(info.ContainerPositions)
            .Append(" containerAddedSlots=").Append(info.InstalledAddedSlots).Append(" slotEncodingCeiling=").Append(info.SlotEncodingCeiling)
            .Append(" syncPayloadByteCeiling=").Append(info.VehicleSyncPayloadByteCeiling).AppendLine();
    }
}
#endif

