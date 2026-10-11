internal static class RebirthPoiRemainingRoomActivation
{
    internal static bool Eligible(int total,int cleared,int tier)=>
        cleared>0&&total-cleared<=tier+1&&(tier<3||total>tier);
    internal static bool TryActivateOne(RebirthPoiNativeManifest manifest,EntityPlayer player)
    {
        if(!RebirthSandboxOptionManager.Current.IsPurge||player==null||!player.IsAlive()||player.IsSpectator||
           !ReferenceEquals(player.world,manifest.World))return false;
        var pos=player.GetBlockPosition();var id=manifest.Identity;
        if(pos.x<id.X||pos.x>=id.X+id.SizeX||pos.z<id.Z||pos.z>=id.Z+id.SizeZ)return false;
        int cleared=0;foreach(var volume in manifest.Volumes)if(volume.wasCleared)cleared++;
        if(!Eligible(manifest.Volumes.Length,cleared,manifest.Prefab.prefab.DifficultyTier))return false;
        foreach(var volume in manifest.Volumes)
            if(!volume.wasCleared&&!volume.isSpawned&&!volume.isSpawning)
            {volume.UpdatePlayerTouched(manifest.World,player);return true;}
        return false;
    }
}
