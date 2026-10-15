using System;

// Server-authoritative current Background ownership, no client CVar/perk/name hint.
public static class RebirthCamperEnergyCapacity
{
    public const int Normal=3;
    public const int Electrician=6;
    public static int ForActor(EntityPlayer player)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||player==null||world==null||world.IsRemote()||
            !ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead())return Normal;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||RebirthCharacterCreationHoldService.IsHeld(player)||
            !RebirthWorldCharacterService.TryGet(player,out var record)||record==null||!record.IsComplete||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return Normal;
        return string.Equals(record.Origin?.BackgroundId,"background.electrician",StringComparison.Ordinal)?Electrician:Normal;
    }
}
