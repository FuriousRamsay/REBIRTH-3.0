using System;

// Independent server observation of the remote owner's final native file.
// Neither an applied packet nor latestPlayerData/mirrored entity inventory is
// substituted for this witness. Observation alone is never a client grant.
internal static class RebirthHydrationRemoteOwnerWitness
{
    internal static bool HasSavedBefore(RebirthHydrationServerSourceHold.Lease lease)
        =>HasSaved(lease,RebirthGearOwnerReceipt.Applying,true);
    internal static bool HasApplied(RebirthHydrationServerSourceHold.Lease lease)
        =>HasSaved(lease,RebirthGearOwnerReceipt.Applied,false);
    private static bool HasSaved(RebirthHydrationServerSourceHold.Lease lease,RebirthGearOwnerReceipt stage,bool beforeOnly)
    {
        try
        {
            if(lease==null||!lease.Current()||!lease.Original.TryGetPlan(out var plan)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(lease.Player,out var identity)||identity==null||
                identity.StorageKey!=lease.Original.Owner||!RebirthGearNativePlayerFile.TryRead(identity,out var file)||
                file.deaths!=lease.Original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            var original=lease.Original;byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),stage)||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=(float)stage)return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved))return false;
            bool matches=beforeOnly?plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length):
                plan.MatchesAppliedInventory(saved.Bag,saved.Belt);
            return matches&&lease.Current();
        }
        catch{return false;}
    }
}
