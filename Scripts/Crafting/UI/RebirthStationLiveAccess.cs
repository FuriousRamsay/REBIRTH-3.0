using System;

// Authority-side live custody gate. Does not debit inputs or publish a queue entry.
public static class RebirthStationLiveAccess
{
    public static bool TryResolve(EntityPlayer player, Vector3i position, string creationId,
        out TileEntityWorkstation station, out RebirthWorldCharacterRecord owner)
    {
        station=null;owner=null;
        var world=GameManager.Instance?.World;
        if(!RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)||
            player==null||player.IsDead()||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(player.entityId),player)||
            (player.position-position.ToVector3()).sqrMagnitude>64||
            !RebirthWorldCharacterService.TryGet(player,out var record)||record?.Progression==null||!record.IsComplete||
            !RebirthSurvivorRequestScope.Matches(creationId,record.Origin?.CreationId))return false;
        float distanceSquared=(player.position-position.ToVector3()).sqrMagnitude;
        if(float.IsNaN(distanceSquared)||float.IsInfinity(distanceSquared)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
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