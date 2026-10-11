// Exact native SleeperVolume.Reset terminal state, shared by reset witnesses.
internal static class RebirthPoiNativeResetPostconditions
{
    internal static bool Volume(SleeperVolume volume,bool originalRan)
    {
        return originalRan&&volume!=null&&volume.respawnTime==ulong.MaxValue&&!volume.isSpawning&&!volume.isSpawned&&!volume.wasCleared&&volume.groupCountList==null&&volume.numSpawned==0&&volume.respawnMap.Count==0&&volume.respawnList==null&&volume.pendingSpawnMap.Count==0&&volume.pendingSpawnOps.Count==0&&volume.playerTouchedToUpdate==null&&volume.playerTouchedTrigger==null;
    }
}