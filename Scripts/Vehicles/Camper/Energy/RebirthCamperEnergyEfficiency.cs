using System;
using System.Collections.Generic;
using UnityEngine;

// Owner capability is authoritative cached character data; never a nearby aura,
// source installer/viewer identity, client CVar or second fuel/battery multiplier.
internal static class RebirthCamperEnergyEfficiency
{
    // Background selection is immutable after Survivor creation. Keep the
    // resolved owner bonus for this World, including while that owner is offline.
    private static World bonusWorld;
    private static readonly Dictionary<PersistentPlayerData,float> ownerBonuses=new Dictionary<PersistentPlayerData,float>();
    internal static void ResetOwnerBonuses()
    {
        bonusWorld=null;
        ownerBonuses.Clear();
    }
    internal static void PrimeOwner(ClientInfo client)
    {
        if(client?.InternalId==null||!ThreadManager.IsMainThread())return;
        var player=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerData(client.InternalId);
        if(player!=null)ResolveOwnerBonus(GameManager.Instance.World,player);
    }
    private static float ResolveOwnerBonus(World world,PersistentPlayerData owner)
    {
        if(!ReferenceEquals(bonusWorld,world))
        {
            ownerBonuses.Clear();
            bonusWorld=world;
        }
        if(ownerBonuses.TryGetValue(owner,out var saving))return saving;
        var players=GameManager.Instance.GetPersistentPlayerList();
        var actor=world.GetEntity(owner.EntityId) as EntityPlayer;
        if(actor!=null&&ReferenceEquals(players.GetPlayerDataFromEntityID(actor.entityId),owner)&&RebirthCharacterCreationHoldService.IsHeld(actor))return 0;
        if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(owner,out var identity)||
            !RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out var record)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||record.StablePlayerKey!=identity.StorageKey)return 0;
        saving=string.Equals(record.Origin?.BackgroundId,"background.electrician",StringComparison.Ordinal)
            ?Mathf.Clamp(RebirthWorkmanshipSignatureService.GetBonusTuning("background_bonus.power_saver","additional_power_savings",0.25f),0f,0.35f):0f;
        ownerBonuses.Add(owner,saving);
        return saving;
    }
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
        return ResolveOwnerBonus(world,owner);
    }
    internal static int Demand(EntityVehicle vehicle,TileEntityPowered tile,int? expectedNativeDemand=null)
        =>Demand(tile,OwnerSaving(vehicle),expectedNativeDemand);
    internal static int Demand(TileEntityPowered tile,float ownerSaving,int? expectedNativeDemand=null)
        =>Math.Max(0,RebirthInfrastructureWorkService.ReadCampPowerUsed(tile,ownerSaving,expectedNativeDemand));
}
