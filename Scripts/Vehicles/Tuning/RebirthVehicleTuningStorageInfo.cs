#if REBIRTH_DEBUG
using System;

// Read-only observations and source proposals only. Never resizes native storage,
// changes module metadata, creates a runtime owner, moves cargo or writes saves.
internal static class RebirthVehicleTuningStorageInfo
{
    internal sealed class Snapshot
    {
        internal int BaseSlots, ContainerPositions, InstalledContainers, InstalledAddedSlots;
        internal int ActualBagSlots, AuthoritativeTotalSlots;
        internal bool ModuleRuntimeKnown, ActualMatchesAuthoritative;
        internal int SlotEncodingCeiling = short.MaxValue;
        internal int VehicleSyncPayloadByteCeiling = ushort.MaxValue;
    }

    internal static Snapshot Capture(EntityVehicle vehicle)
    {
        if (!ThreadManager.IsMainThread() || vehicle?.vehicle == null || vehicle.bag == null)
            throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageUnavailable"));
        if (!string.Equals(vehicle.vehicle.GetName(), "rebirthCamper80s", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization.Get("xuiRbTuneStorageCamperOnly"));
        var info = new Snapshot {
            BaseSlots = RebirthCamperStorageCapacity.BaseSlots,
            ContainerPositions = RebirthCamperStorageCapacity.ModuleSlots,
            ActualBagSlots = vehicle.bag.SlotCount
        };
        // Do not call For: constructing an owner may bind station state and write
        // transfer-pending metadata. Unknown remains explicit, not assumed empty.
        if (!RebirthCamperModuleRuntime.TryGetExisting(vehicle, out var runtime) || runtime?.Storage == null)
            return info;
        info.ModuleRuntimeKnown = true;
        info.AuthoritativeTotalSlots = runtime.Storage.Capacity;
        info.InstalledAddedSlots = info.AuthoritativeTotalSlots - info.BaseSlots;
        for (int slot = 0; slot < info.ContainerPositions; slot++)
            if (runtime.Storage.GetSlot(slot) != null) info.InstalledContainers++;
        info.ActualMatchesAuthoritative = info.AuthoritativeTotalSlots>=info.BaseSlots && info.AuthoritativeTotalSlots<=info.SlotEncodingCeiling && info.ActualBagSlots == info.AuthoritativeTotalSlots;
        return info;
    }

}
#endif
