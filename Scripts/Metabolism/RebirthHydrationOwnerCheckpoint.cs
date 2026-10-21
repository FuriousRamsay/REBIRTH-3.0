using System;

// Native owner inventory leg only. This does not commit hydration/metabolism,
// acknowledge a server grant, retire custody or move any inventory item.
internal static class RebirthHydrationOwnerCheckpoint
{
    internal static string Marker(RebirthHydrationTransferOriginal original)=>
        "rbHydration_"+original.Operation.ToString("N")+"_"+original.Digest;
    internal static bool TryPersist(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage)
    {
        try
        {
            if((stage!=RebirthGearOwnerReceipt.Applying&&stage!=RebirthGearOwnerReceipt.Applied)||
                !RebirthHydrationOwnerHold.Matches(player,original)||player.Buffs==null||!original.TryGetPlan(out var plan))return false;
            string marker=Marker(original),receipt="rbGear_"+original.Operation.ToString("N");
            float previous=player.Buffs.GetCustomVar(marker),nativePrevious=player.Buffs.GetCustomVar(receipt);
            if(previous!=0f&&previous!=2f&&previous!=1f||previous==1f&&stage!=RebirthGearOwnerReceipt.Applied||
                stage==RebirthGearOwnerReceipt.Applied&&previous!=2f&&previous!=1f||
                nativePrevious!=0f&&nativePrevious!=2f&&nativePrevious!=1f||nativePrevious==1f&&stage!=RebirthGearOwnerReceipt.Applied||
                stage==RebirthGearOwnerReceipt.Applied&&nativePrevious!=2f&&nativePrevious!=1f)return false;
            if(!LiveMatches(player,original,plan,stage,nativePrevious==0f))return false;
            if(previous==(float)stage&&nativePrevious==(float)stage&&HasSaved(player,original,stage))
                return LiveMatches(player,original,plan,stage,false);
            // Purpose native checkpoint: only receipts and the actual player
            // save are written. No gear offer, mode gate or item setter is used.
            player.Buffs.SetCustomVar(marker,(float)stage,true);
            if(!RebirthHydrationOwnerHold.Matches(player,original)||player.Buffs.GetCustomVar(marker)!=(float)stage)return false;
            player.Buffs.SetCustomVar(receipt,(float)stage,true);
            if(!RebirthHydrationOwnerHold.Matches(player,original)||player.Buffs.GetCustomVar(receipt)!=(float)stage||
                !LiveMatches(player,original,plan,stage,nativePrevious==0f))return false;
            GameManager.Instance.SaveLocalPlayerData();
            return HasSaved(player,original,stage)&&LiveMatches(player,original,plan,stage,false);
        }
        catch{return false;}
    }
    internal static bool HasSaved(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage)
        =>HasSavedCore(player,original,stage,false);
    internal static bool HasSavedBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try{return HasSavedCore(player,original,RebirthGearOwnerReceipt.Applying,true)&&HasLiveBefore(player,original);}
        catch{return false;}
    }
    internal static bool HasSavedApplyingAfter(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try{return HasSavedCore(player,original,RebirthGearOwnerReceipt.Applying,false,true)&&original.TryGetPlan(out var plan)&&
            LiveMatches(player,original,plan,RebirthGearOwnerReceipt.Applied,false)&&
            player.Buffs.GetCustomVar(Marker(original))==2f&&player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==2f;}
        catch{return false;}
    }
    internal static bool HasSavedUntouchedBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!RebirthHydrationOwnerHold.Matches(player,original)||player.Buffs==null||!NativeOwnerMatches(original)||
                player.Buffs.GetCustomVar(Marker(original))!=0f||player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))!=0f||
                player.Buffs.GetCustomVar("rbHydrationRefused_"+original.Operation.ToString("N")+"_"+original.Digest)!=0f||!original.TryGetPlan(out var plan)||
                !LiveMatches(player,original,plan,RebirthGearOwnerReceipt.Applying,true)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var file)||
                file.deaths!=original.Deaths||file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            var buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.TryReadStage(buffs,original.Operation.ToString("N"),out var gear)||gear!=0f||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=0f||
                !RebirthGearReceiptReader.TryReadHydrationRefusedStage(buffs,original.Operation,original.Digest,out var refused)||refused!=0f)return false;
            return RebirthGearInventorySnapshot.TryCapture(RebirthGearPlayerDataInventory.ReadSlots(file,true),RebirthGearPlayerDataInventory.ReadSlots(file,false),original.OwnedBelt,out var saved)&&
                plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length)&&
                LiveMatches(player,original,plan,RebirthGearOwnerReceipt.Applying,true)&&
                player.Buffs.GetCustomVar(Marker(original))==0f&&player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==0f&&
                player.Buffs.GetCustomVar("rbHydrationRefused_"+original.Operation.ToString("N")+"_"+original.Digest)==0f;
        }
        catch{return false;}
    }
    internal static bool HasLiveBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try{return original!=null&&player?.Buffs!=null&&
            player.Buffs.GetCustomVar(Marker(original))==2f&&player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==2f&&
            original.TryGetPlan(out var plan)&&LiveMatches(player,original,plan,RebirthGearOwnerReceipt.Applying,true);}
        catch{return false;}
    }
    private static bool HasSavedCore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage,bool beforeOnly,bool afterOnly=false)
    {
        try
        {
            if((stage!=RebirthGearOwnerReceipt.Applying&&stage!=RebirthGearOwnerReceipt.Applied)||
                !RebirthHydrationOwnerHold.Matches(player,original)||!original.TryGetPlan(out var plan)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=original.Owner||
                GameManager.Instance?.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId||
                !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=original.Deaths||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var marker)||marker!=(float)stage||
                !RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),stage))return false;
            // Both receipts and both native grids come from THIS one final file.
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved))return false;
            bool matches=beforeOnly?plan.MatchesBefore(saved.Bag,saved.Belt,plan.GearBefore,saved.Bag.Length,saved.Belt.Length):
                stage==RebirthGearOwnerReceipt.Applied||afterOnly?plan.MatchesAppliedInventory(saved.Bag,saved.Belt):
                plan.InspectApplication(saved.Bag,saved.Belt,true)!=RebirthGearInventoryPlan.ApplicationState.Conflict;
            return matches&&RebirthHydrationOwnerHold.Matches(player,original)&&NativeOwnerMatches(original);
        }
        catch{return false;}
    }
    private static bool LiveMatches(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearInventoryPlan plan,RebirthGearOwnerReceipt stage,bool fresh)
    {
        if(!RebirthHydrationOwnerHold.Matches(player,original)||!NativeOwnerMatches(original)||!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,
            player.inventory?.ItemGrid?.items,original.OwnedBelt,out var live))return false;
        bool matches=stage==RebirthGearOwnerReceipt.Applied?plan.MatchesAppliedInventory(live.Bag,live.Belt):
            fresh?plan.MatchesBefore(live.Bag,live.Belt,plan.GearBefore,live.Bag.Length,live.Belt.Length):
            plan.InspectApplication(live.Bag,live.Belt,true)!=RebirthGearInventoryPlan.ApplicationState.Conflict;
        return matches&&RebirthHydrationOwnerHold.Matches(player,original)&&NativeOwnerMatches(original);
    }
    private static bool NativeOwnerMatches(RebirthHydrationTransferOriginal original)=>
        RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==original.Owner&&
        GameManager.Instance?.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId;
}
