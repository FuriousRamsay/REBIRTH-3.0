using System;

// Exact final native owner file. No offer is synthesized for an unprepared refusal.
internal static class RebirthGearPreparationRefusalPlayerFileWitness
{
    internal static bool HasRetired(RebirthStablePlayerIdentity owner,RebirthGearPreparationRefusal refusal)
    {
        try{return owner!=null&&RebirthGearNativePlayerFile.TryRead(owner,out var saved)&&MatchesRetiredData(saved,refusal);}
        catch{return false;}
    }
    internal static bool HasRejectedOriginal(RebirthStablePlayerIdentity owner,RebirthGearPreparationRefusal refusal)
    {
        try{return owner!=null&&RebirthGearNativePlayerFile.TryRead(owner,out var saved)&&MatchesRejectedOriginalData(saved,refusal);}
        catch{return false;}
    }
    internal static bool MatchesRejectedOriginalData(PlayerDataFile saved,RebirthGearPreparationRefusal refusal)
    {
        try
        {
            if(refusal==null||saved?.buffData==null||saved.buffData.Length>1024*1024)return false;
            var bytes=saved.buffData.ToArray();
            if(!RebirthGearPreparationMarkerReader.TryRead(bytes,out var marker,out var world,out var owned,out var intent)||
                !refusal.MatchesOriginal(marker)||world!=refusal.SavedWorld||
                !RebirthGearReceiptReader.Contains(bytes,refusal.TransactionId.ToString("N"),RebirthGearOwnerReceipt.Rejected))return false;
            return RebirthGearInventorySnapshot.TryCapture(RebirthPlayerDataInventory.ReadSlots(saved,true),
                RebirthPlayerDataInventory.ReadSlots(saved,false),owned,out var inventory)&&intent.MatchesInventory(inventory);
        }
        catch{return false;}
    }
    // Detached predicate shared by SAME final file and frozen authenticated upload.
    // Native data identity/authentication is the caller's responsibility.
    internal static bool MatchesRetiredData(PlayerDataFile saved,RebirthGearPreparationRefusal refusal)
    {
        try
        {
            if(refusal==null||saved==null||
                !RebirthGearPreparationMarker.TryRead(refusal.OriginalMarker,1f,out var world,out var owned,out var intent)||
                world!=refusal.SavedWorld||intent.TransactionId!=refusal.TransactionId||saved.buffData==null||saved.buffData.Length>1024*1024)return false;
            var bytes=saved.buffData.ToArray();
            if(!RebirthGearPreparationMarkerReader.HasNoOriginal(bytes)||
                !RebirthGearReceiptReader.Contains(bytes,refusal.TransactionId.ToString("N"),RebirthGearOwnerReceipt.Rejected))return false;
            var bag=RebirthPlayerDataInventory.ReadSlots(saved,true);
            var belt=RebirthPlayerDataInventory.ReadSlots(saved,false);
            return RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var inventory)&&intent.MatchesInventory(inventory);
        }
        catch{return false;}
    }
}