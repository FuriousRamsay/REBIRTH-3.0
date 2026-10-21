using System;

internal static class RebirthHydrationLocalRefusedCheckpoint
{
    private static string Marker(RebirthHydrationTransferOriginal original)=>"rbHydrationRefused_"+original.Operation.ToString("N")+"_"+original.Digest;
    internal static bool TryPersist(RebirthHydrationTransferJournal.LocalPreparedRejection rejection)
    {
        try
        {
            if(rejection==null||!rejection.Current())return false;
            var player=rejection.Player;string marker=Marker(rejection.Original);
            float prior=player.Buffs.GetCustomVar(marker);if(prior!=0f&&prior!=1f)return false;
            if(prior==1f&&HasSaved(rejection))return true;
            player.Buffs.SetCustomVar(marker,1f,true);
            if(!rejection.Current()||player.Buffs.GetCustomVar(marker)!=1f)return false;
            GameManager.Instance.SaveLocalPlayerData();return HasSaved(rejection);
        }
        catch{return false;}
    }
    internal static bool HasSaved(RebirthHydrationTransferJournal.LocalPreparedRejection rejection)
    {
        try
        {
            if(rejection==null||!rejection.Current()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                owner.StorageKey!=rejection.Original.Owner||!RebirthGearNativePlayerFile.TryRead(owner,out var file)||
                file.deaths!=rejection.Original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            var original=rejection.Original;if(!original.TryGetPlan(out var plan))return false;
            byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.TryReadHydrationRefusedStage(buffs,original.Operation,original.Digest,out var refused)||refused!=1f||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=2f||
                !RebirthGearReceiptReader.TryReadHydrationSeenStage(buffs,original.Operation,original.Digest,out var seen)||seen!=0f||
                !RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applying))return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved)||
                !plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length))return false;
            // This second independent observation proves full live/saved Before
            // and persistent owner identity after the refusal-file read above.
            return rejection.Current()&&RebirthHydrationOwnerCheckpoint.HasSavedBefore(rejection.Player,original)&&
                RebirthHydrationOwnerCheckpoint.HasLiveBefore(rejection.Player,original)&&
                RebirthMetabolismStateRepository.HasCurrentLocalHydrationBefore(rejection.Player,original)&&
                rejection.Player.Buffs.GetCustomVar(Marker(original))==1f;
        }
        catch{return false;}
    }
}
