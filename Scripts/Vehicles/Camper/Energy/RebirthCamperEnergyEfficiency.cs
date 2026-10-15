using System;
using UnityEngine;

// Owner capability is authoritative cached character data; never a nearby aura,
// source installer/viewer identity, client CVar or second fuel/battery multiplier.
internal static class RebirthCamperEnergyEfficiency
{
    internal static float OwnerSaving(EntityVehicle vehicle)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||vehicle==null||world==null||world.IsRemote()||
            !ReferenceEquals(vehicle.world,world)||!ReferenceEquals(world.GetEntity(vehicle.entityId),vehicle)||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            !RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return 0;
        var id=vehicle.GetOwner();if(id==null)return 0;
        var players=GameManager.Instance.GetPersistentPlayerList();var owner=players?.GetPlayerData(id);
        if(owner==null||owner.PrimaryId==null)return 0;
        var actor=world.GetEntity(owner.EntityId) as EntityPlayer;
        if(actor!=null&&ReferenceEquals(players.GetPlayerDataFromEntityID(actor.entityId),owner)&&RebirthCharacterCreationHoldService.IsHeld(actor))return 0;
        if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(owner,out var identity)||
            !RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out var record)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||record.StablePlayerKey!=identity.StorageKey||
            !string.Equals(record.Origin?.BackgroundId,"background.electrician",StringComparison.Ordinal))return 0;
        return Mathf.Clamp(RebirthWorkmanshipSignatureService.GetBonusTuning("background_bonus.power_saver","additional_power_savings",0.25f),0f,0.35f);
    }
    internal static int Demand(EntityVehicle vehicle,TileEntityPowered tile,int? expectedNativeDemand=null)
        =>Math.Max(0,RebirthInfrastructureWorkService.ReadCampPowerUsed(tile,OwnerSaving(vehicle),expectedNativeDemand));
}
