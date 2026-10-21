using System;

// Separate purpose receipt; shared Applying markers are never downgraded.
internal static class RebirthHydrationRefusedCheckpoint
{
    internal static string Marker(RebirthHydrationTransferOriginal original)=>"rbHydrationRefused_"+original.Operation.ToString("N")+"_"+original.Digest;
    internal static bool TryPersist(RebirthHydrationClientPreparedRejection rejection)
    {
        try
        {
            if(rejection==null||!rejection.Current())return false;
            var player=rejection.Player;string marker=Marker(rejection.Original);float previous=player.Buffs.GetCustomVar(marker);
            if(previous!=0f&&previous!=1f)return false;
            if(previous==1f&&HasSaved(rejection))return true;
            player.Buffs.SetCustomVar(marker,1f,true);
            if(!rejection.Current()||player.Buffs.GetCustomVar(marker)!=1f)return false;
            GameManager.Instance.SaveLocalPlayerData();return HasSaved(rejection);
        }
        catch{return false;}
    }
    internal static bool HasSaved(RebirthHydrationClientPreparedRejection rejection)
        =>rejection!=null&&HasSavedCore(rejection.Original,()=>rejection.Current());
    internal static bool HasSavedHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
        =>player!=null&&original!=null&&HasSavedCore(original,()=>
            RebirthHydrationProposalClient.TryGetCurrent(player,out var intent)&&intent.Original?.Digest==original.Digest&&
            player.Buffs?.GetCustomVar(Marker(original))==1f);
    private static bool HasSavedCore(RebirthHydrationTransferOriginal original,Func<bool> current)
    {
        try
        {
            if(!current()||!original.TryGetPlan(out var plan)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=original.Owner||
                !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.TryReadHydrationRefusedStage(buffs,original.Operation,original.Digest,out var refused)||refused!=1f||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=2f||
                !RebirthGearReceiptReader.TryReadHydrationSeenStage(buffs,original.Operation,original.Digest,out var seen)||seen!=0f||
                !RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applying))return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            return RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved)&&
                plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length)&&current();
        }
        catch{return false;}
    }
}
