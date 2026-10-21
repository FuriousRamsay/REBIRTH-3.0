using System;
public sealed partial class RebirthCamperModuleRuntime {
    internal void TickStations()
    {
        // A brief exact energy receipt/lease save hold stabilizes native station
        // metadata for the shared native vehicle save witness. Energy mode alone
        // never pauses crafting and this does not alter any other custody hold.
        if(RebirthCamperEnergyRuntime.IsHeld(vehicle)||RebirthCamperStationSaveEvidence.IsVehiclePending(vehicle)){energyClockHeld=true;return;}
        if(energyClockHeld)
        {
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                if(Stations.GetSlot(slot)!=null)Stations.GetSlot(slot).State.lastTickTime=0;
            lastProcessingTick=0;energyClockHeld=false;
        }
        if(TransferPending)
        {
        BindStations();
#if !REBIRTH_CAMPER_DIAGNOSTICS
        RebirthProductiveEarnedCoordinator.ObserveMobileVehicleTick(vehicle,this);
#endif
        if(TransferPending)return;
        }
        ulong now=GameTimer.Instance.ticks;
        if(now>=lastProcessingTick && now-lastProcessingTick<5)return;
        lastProcessingTick=now;
        BindStations();
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            Stations.GetSlot(slot)?.State.TickMobile(vehicle);
        Persist();
    }

}