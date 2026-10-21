// Refreshed boundary shapes for the current production runtime. This fixture
// does not qualify the actual hold services, journals, wiring or background XP.
using System;
using System.Collections.Generic;
internal static class RebirthCamperStationSaveEvidence
{ internal static bool Pending;internal static bool IsVehiclePending(EntityVehicle v)=>Pending; }
internal static class RebirthCamperStationFuelOriginal
{ internal static bool Blocked;internal static bool BlocksVehicle(EntityVehicle v)=>Blocked; }
internal static class RebirthProductiveEarnedCoordinator
{ internal static void ObserveMobileVehicleTick(EntityVehicle v,RebirthCamperModuleRuntime r){} }
internal static class RebirthCamperBodyAttachment
{ internal static bool HasBody(EntityVehicle v)=>true; }
internal static class RebirthCamperRelayWiring
{ internal static IEnumerable<IRebirthCamperEnergyConsumer> MountedConsumers(EntityVehicle v)=>Array.Empty<IRebirthCamperEnergyConsumer>(); }
public interface IRebirthCamperMobileEnergyConsumer:IRebirthCamperEnergyConsumer {}
internal static class RebirthCamperTransferHistoryScope
{
    // Detached journal fixture retains its existing direct validation path.
    internal static bool TryGet(RebirthCamperTransferJournal j,out RebirthCamperTransferJournal index){index=null;return false;}
}
