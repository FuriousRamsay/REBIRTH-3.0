using System.Linq;

internal static class RebirthPurgeSleeperPolicy
{
    internal static int ExcludedNativeMembers(SleeperVolume room)
        =>room.respawnMap.Values.Count(actor=>!RebirthPoiCombatantPolicy.IsRequired(actor.className));
    internal static bool CombatSettled(SleeperVolume room)
    {
        if(room.wasCleared)return true;
        // Existing saves may retain harmless animals from the old spawn bug.
        // Do not kill/remove them or fabricate native cleared/death flags.
        return !room.isSpawning&&room.respawnMap.Count>0&&
            room.respawnMap.Values.All(actor=>!RebirthPoiCombatantPolicy.IsRequired(actor.className))&&
            room.GetAliveCount()-room.respawnMap.Count<=0&&
            (room.minScript==null||!room.minScript.IsRunning())&&
            !room.pendingSpawnOps.Any(handle=>handle==null||!handle.IsCompleted||handle.onComplete!=null);
    }
}
