using System;
partial class RebirthCamperTransferCommit
{
    internal static void AssertCargoPolicy(ItemStackGrid grid, ItemStack[] before, ItemStack[] after)
    {
        if (grid == null || before == null || after == null || grid.Length != before.Length)
            throw new InvalidOperationException("Native cargo policy is unavailable.");
        var locks = grid.SlotLocks;
        if (locks != null)
        {
            if (locks.Length != before.Length)
                throw new InvalidOperationException("Cargo slot locks do not match the native inventory.");
            for (int slot = 0; slot < locks.Length; slot++)
                if (locks[slot] && (slot >= after.Length || !Equal(
                    RebirthCamperInventorySnapshot.Capture(new[] { before[slot] }),
                    RebirthCamperInventorySnapshot.Capture(new[] { after[slot] }))))
                    throw new InvalidOperationException("Cargo slot "+(slot+1)+" is locked and would move or leave the retained capacity. Unlock that affected slot before changing storage.");
        }
        // Native AnyPreferences means any non-null array, including an entirely
        // empty saved bag image and unrelated equipment/toolbelt preferences.
        // Resize keeps this tracker unchanged. Protect actual cargo positions,
        // not the mere presence of a tracker or another owner's preference area.
        var preferences=grid.Preferences?.bag;
        if(before.Length!=after.Length&&preferences!=null)
            for(int slot=0;slot<preferences.Length;slot++)
            {
                var preferred=preferences[slot];
                if(preferred==null)throw new InvalidOperationException("Cargo position preference is malformed.");
                if(preferred.IsEmpty())continue;
                if(slot>=before.Length||slot>=after.Length||!Equal(
                    RebirthCamperInventorySnapshot.Capture(new[]{before[slot]}),
                    RebirthCamperInventorySnapshot.Capture(new[]{after[slot]})))
                    throw new InvalidOperationException("Cargo slot "+(slot+1)+" has an item preference and would move or leave the retained capacity. Clear that affected preference before changing storage.");
            }
    }
}
