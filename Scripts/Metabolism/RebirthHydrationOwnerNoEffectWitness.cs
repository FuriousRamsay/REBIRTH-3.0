using System;

// Positive native Before observation only. A server's exact saved Rejected
// original must independently authorize a purpose-specific refusal checkpoint.
// Never downgrade the shared native Applying receipt or infer rollback.
internal static class RebirthHydrationOwnerNoEffectWitness
{
    internal static bool HasBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(player?.world?.IsRemote()!=true||original==null||!RebirthHydrationOwnerHold.Matches(player,original)||
                RebirthHydrationRemoteClientGrant.HasIssued(original.Operation)||!original.TryGetPlan(out var plan)||
                player.Buffs==null||player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))!=2f||
                player.Buffs.GetCustomVar(RebirthHydrationOwnerCheckpoint.Marker(original))!=2f||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,original.OwnedBelt,out var live)||
                !plan.MatchesBefore(live.Bag,live.Belt,plan.GearBefore,live.Bag.Length,live.Belt.Length)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=original.Owner||
                !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=original.Deaths||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applying)||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=2f||
                !RebirthGearReceiptReader.TryReadHydrationSeenStage(buffs,original.Operation,original.Digest,out var seen)||seen!=0f)return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            return RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved)&&
                plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length)&&
                RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,original.OwnedBelt,out var finalLive)&&
                plan.MatchesBefore(finalLive.Bag,finalLive.Belt,plan.GearBefore,finalLive.Bag.Length,finalLive.Belt.Length)&&
                player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==2f&&player.Buffs.GetCustomVar(RebirthHydrationOwnerCheckpoint.Marker(original))==2f&&
                !RebirthHydrationRemoteClientGrant.HasIssued(original.Operation)&&RebirthHydrationOwnerHold.Matches(player,original);
        }
        catch{return false;}
    }
}
