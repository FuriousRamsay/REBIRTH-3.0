internal static class RebirthPoiRemainingRoomActivation
{
    private static readonly FastTags<TagGroup.Poi> HideUi=FastTags<TagGroup.Poi>.Parse("hideui");
    internal static bool Eligible(int total,int cleared,int tier)=>
        cleared>0&&total-cleared<=tier+1&&(tier<3||total>tier);
    internal static bool TryActivateOne(RebirthPoiNativeManifest manifest,EntityPlayer player)
    {
        if(!RebirthSandboxOptionManager.Current.IsPurge||player==null||!player.IsAlive()||!player.IsSpawned()||player.IsSpectator||player.AttachedToEntity!=null||player.IsFlyMode.Value||
           !ReferenceEquals(player.world,manifest.World))return false;
        // Legacy GetSleeperVolumeStats excludes hideui from automatic activation.
        // This does not suppress scanner presentation or ordinary native triggers.
        if(manifest.Prefab.prefab.Tags.Test_AnySet(HideUi))return false;
        var pos=player.GetBlockPosition();var id=manifest.Identity;
        if(pos.x<id.X||pos.x>=id.X+id.SizeX||pos.z<id.Z||pos.z>=id.Z+id.SizeZ)return false;
        // The legacy activation threshold counts all native sleeper rooms,
        // not only the combat subset used to establish clearance evidence.
        var volumes = manifest.Prefab.sleeperVolumes;
        var authored = manifest.Prefab.prefab.SleeperVolumeList;
        if (volumes == null || authored == null || volumes.Count != authored.Count) return false;
        int cleared=0;foreach(var volume in volumes) { if (volume == null) return false; if(volume.wasCleared)cleared++; }
        if(!Eligible(volumes.Count,cleared,manifest.Prefab.prefab.DifficultyTier))return false;
        foreach(var volume in volumes)
            if(!volume.wasCleared&&!volume.isSpawned&&!volume.isSpawning)
            {volume.UpdatePlayerTouched(manifest.World,player);return true;}
        return false;
    }
}

