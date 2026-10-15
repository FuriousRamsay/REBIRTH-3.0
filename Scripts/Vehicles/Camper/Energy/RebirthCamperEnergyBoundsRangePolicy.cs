using System;

// Range belongs to the actual native camper owner, never the observing actor.
// Cached character lookup only: no loading, character creation or mutation.
public static class RebirthCamperEnergyBoundsRangePolicy
{
    public static float ForVehicle(EntityVehicle vehicle)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||vehicle==null||world==null||!ReferenceEquals(vehicle.world,world)
            ||!ReferenceEquals(world.GetEntity(vehicle.entityId),vehicle))return 0;
        if(world.IsRemote())return RebirthCamperClaimProjection.TryReadRadius(vehicle,out float projected)?projected:0;
        float normal=RebirthCamperEnergyConfiguration.Current.Range;
        if(SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return normal;
        var ownerId=vehicle.GetOwner();if(ownerId==null)return normal;
        var players=GameManager.Instance.GetPersistentPlayerList();var owner=players?.GetPlayerData(ownerId);
        if(owner==null||owner.PrimaryId==null)return normal;
        var actor=world.GetEntity(owner.EntityId) as EntityPlayer;
        if(actor!=null&&ReferenceEquals(players.GetPlayerDataFromEntityID(actor.entityId),owner)
            &&RebirthCharacterCreationHoldService.IsHeld(actor))return normal;
        if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(owner,out var identity)
            ||!RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out var record)
            ||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)
            ||record.StablePlayerKey!=identity.StorageKey)return normal;
        return string.Equals(record.Origin?.BackgroundId,"background.engineer",StringComparison.Ordinal)
            ?RebirthCamperEnergyConfiguration.Current.EngineerRange:normal;
    }
}
