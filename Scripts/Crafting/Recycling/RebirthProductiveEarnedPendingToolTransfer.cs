using System;

// Comparison only. Actual original owner/creation/death/upload/file/transfer
// receipts remain mandatory at the future rebase producer; arrays are not proof.
internal static class RebirthProductiveEarnedPendingToolTransfer
{
    internal static bool Conserved(ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] beforeTools,
        ItemStack[] afterBag,ItemStack[] afterBelt,ItemStack[] afterTools)
        =>ConservedGesture(beforeBag,beforeBelt,beforeTools,ItemStack.Empty,afterBag,afterBelt,afterTools,ItemStack.Empty);
    internal static bool ConservedGesture(ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] beforeTools,ItemStack beforeCursor,
        ItemStack[] afterBag,ItemStack[] afterBelt,ItemStack[] afterTools,ItemStack afterCursor)
    {
        try
        {
            if(beforeBag==null||beforeBelt==null||beforeTools==null||afterBag==null||afterBelt==null||afterTools==null||
                beforeCursor==null||afterCursor==null||beforeBag.Length>169||beforeBelt.Length>20||beforeTools.Length<1||beforeTools.Length>255||
                beforeBag.Length!=afterBag.Length||beforeBelt.Length!=afterBelt.Length||beforeTools.Length!=afterTools.Length)return false;
            // Reuse the existing bounded full-native-item conservation rule.
            // Only a bounded reference vector is combined; no live slot/item is
            // cloned, normalized, replaced, dropped, debited or awarded here.
            var ownerBefore=new ItemStack[checked(beforeBag.Length+beforeBelt.Length+1)];
            Array.Copy(beforeBag,0,ownerBefore,0,beforeBag.Length);
            Array.Copy(beforeBelt,0,ownerBefore,beforeBag.Length,beforeBelt.Length);
            ownerBefore[ownerBefore.Length-1]=beforeCursor;
            var bagAfter=new ItemStack[checked(afterBag.Length+1)];Array.Copy(afterBag,bagAfter,afterBag.Length);bagAfter[bagAfter.Length-1]=afterCursor;
            return RebirthLogisticsDeathPartition.Conserved(ownerBefore,beforeTools,bagAfter,afterBelt,afterTools);
        }
        catch{return false;}
    }
    internal static bool ConservedNativeOwnerFiles(PlayerDataFile beforeOwner,PlayerDataFile afterOwner,
        ItemStack[] beforeTools,ItemStack[] afterTools)
    {
        try
        {
            if(beforeOwner==null||afterOwner==null)return false;
            // These readers preserve full native ItemValue metadata/texture bytes.
            // File authenticity, scope and allocation are the caller's obligations;
            // a PlayerDataFile reference cannot mint a transfer receipt.
            return Conserved(RebirthGearPlayerDataInventory.ReadSlots(beforeOwner,true),RebirthGearPlayerDataInventory.ReadSlots(beforeOwner,false),beforeTools,
                RebirthGearPlayerDataInventory.ReadSlots(afterOwner,true),RebirthGearPlayerDataInventory.ReadSlots(afterOwner,false),afterTools);
        }
        catch{return false;}
    }
}
