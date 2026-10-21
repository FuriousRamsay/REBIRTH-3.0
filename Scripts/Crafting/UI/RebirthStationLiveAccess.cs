using System;

// Authority-side live custody gate. Does not debit inputs or publish a queue entry.
public static class RebirthStationLiveAccess
{
    // Mobile callers must supply the retained full custody address. A coordinate
    // request is never silently redirected to a station inside a nearby truck.
    public static bool TryResolveMobile(EntityPlayer player, RebirthCamperStationAddress requested, string creationId,
        out TileEntityWorkstation station, out RebirthWorldCharacterRecord owner)
    {
        station = null; owner = null;
        var world = GameManager.Instance?.World;
        if (!requested.IsValid || !RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)
            || player == null || player.IsDead() || !ReferenceEquals(player.world, world)
            || !ReferenceEquals(world.GetEntity(player.entityId), player)
            || !TryGetMobileLease(player,requested,creationId,out var mobile,out var lease)
            || !RebirthWorldCharacterService.TryGet(player, out var record) || record?.Progression == null || !record.IsComplete
            || !RebirthSurvivorRequestScope.Matches(creationId, record.Origin?.CreationId)
            || !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)
            || !RebirthWorldCharacterService.TryGetIdentity(player, out var identity) || identity == null
            || identity.StorageKey != record.StablePlayerKey || identity.CanonicalId != record.StablePlayerId) return false;
        // Recheck the exact window lease after repository/identity callbacks.
        // Session admission includes vehicle ownership, range, native service
        // permission, transfer state and current character creation.
        if (!TryGetMobileLease(player,requested,creationId,out var finalStation,out var finalLease)
            || !ReferenceEquals(finalStation, mobile) || !ReferenceEquals(lease,finalLease)
            || !RebirthWorldCharacterService.TryGet(player, out var finalOwner) || !ReferenceEquals(finalOwner, record)
            || !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)
            || !RebirthSurvivorRequestScope.Matches(creationId, record.Origin?.CreationId)) return false;
        station = mobile; owner = record; return true;
    }
    internal static bool TryGetMobileLease(EntityPlayer player,RebirthCamperStationAddress requested,string creation,
        out RebirthCamperStationState station,out object lease)
    {
        station=null;lease=null;
        if(RebirthCamperStationWindowSession.TryGetCurrent(player,out var local,out var address)&&requested.Equals(address)&&
            RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var localLease))
        {station=local;lease=localLease;return true;}
        return RebirthCamperRemoteStationTransport.TryResolve(player,requested,creation,out station,out lease);
    }

    public static bool TryResolve(EntityPlayer player, Vector3i position, string creationId,
        out TileEntityWorkstation station, out RebirthWorldCharacterRecord owner)
    {
        station=null;owner=null;
        var world=GameManager.Instance?.World;
        float distanceSquared=player==null?float.PositiveInfinity:(player.position-position.ToVector3()).sqrMagnitude;
        if(!RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)||
            player==null||player.IsDead()||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(player.entityId),player)||
            float.IsNaN(distanceSquared)||float.IsInfinity(distanceSquared)||distanceSquared>64||
            !RebirthWorldCharacterService.TryGet(player,out var record)||record?.Progression==null||!record.IsComplete||
            !RebirthSurvivorRequestScope.Matches(creationId,record.Origin?.CreationId))return false;
        if(!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var characterIdentity)||characterIdentity==null||
            characterIdentity.StorageKey!=record.StablePlayerKey||characterIdentity.CanonicalId!=record.StablePlayerId)return false;
        var candidate=world.GetTileEntity(position) as TileEntityWorkstation;
        var locks=LockManager.Instance;
        bool held=false;
        if(candidate==null||locks==null||candidate.IsSharedLock(0)||
            !locks.singleLocks.TryGetByKey(player.entityId,out var entries)||entries==null)return false;
        foreach(var entry in entries)
            if(ReferenceEquals(entry.Target,candidate)&&entry.Channel==0){held=true;break;}
        if(!held)return false;
        var identity=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if(identity==null&&ReferenceEquals(world.GetPrimaryPlayer(),player))
            identity=GameManager.Instance.GetPersistentLocalPlayer()?.PrimaryId;
        string reason;
        if(identity==null||!RebirthWorkstationSecurityService.CanAccessWorkstation(world,position,player,identity,false,
            RebirthSecureAccessPurpose.WorkstationAccess,out reason))return false;
        station=candidate;owner=record;return true;
    }
}
