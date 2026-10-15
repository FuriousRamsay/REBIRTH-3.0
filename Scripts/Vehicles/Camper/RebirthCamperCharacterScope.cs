using System;

// Uses the existing authoritative character repository; never creates a second
// character identity or accepts a creation identifier supplied by the window.
public static class RebirthCamperCharacterScope
{
    public static string ResolveLocal(EntityPlayerLocal player)
    {
        var world = GameManager.Instance?.World;
        if (!ThreadManager.IsMainThread() || player == null || player.IsDead()
            || world == null || !ReferenceEquals(player.world, world)
            || !ReferenceEquals(world.GetPrimaryPlayer(), player)) return null;
        if(world.IsRemote())
        {
            if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())
                return RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)?"native-mode:"+identity.CanonicalId:null;
            return RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out var projected)?projected:null;
        }
        var nativeOwner = GameManager.Instance.getPersistentPlayerID(null);
        if (nativeOwner == null || string.IsNullOrWhiteSpace(nativeOwner.CombinedString)) return null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return "native-mode:" + nativeOwner.CombinedString;
        RebirthWorldCharacterRecord record;
        string creation;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete
            || !RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId, out creation)) return null;
        return creation;
    }
}
