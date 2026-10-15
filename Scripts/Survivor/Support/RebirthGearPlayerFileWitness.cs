using System;
using System.IO;

// Exact final native owner file only. Receipt and both inventory areas must be
// evidence from this one file; cache, latest packet or .bak cannot substitute.
internal static class RebirthGearPlayerFileWitness
{
    internal static bool HasApplied(RebirthStablePlayerIdentity owner, RebirthGearTransferState offer)
    { return HasSaved(owner, offer, RebirthGearOwnerReceipt.Applied); }

    internal static bool HasApplying(RebirthStablePlayerIdentity owner, RebirthGearTransferState offer)
    { return HasSaved(owner, offer, RebirthGearOwnerReceipt.Applying); }

    // Rejection is admissible only when the exact original inventory is untouched.
    internal static bool HasRejected(RebirthStablePlayerIdentity owner, RebirthGearTransferState offer)
    { return HasSaved(owner, offer, RebirthGearOwnerReceipt.Rejected); }

    internal static bool HasRetired(RebirthStablePlayerIdentity owner,RebirthGearTransferState offer,bool applied)
        => offer?.PreparationRequestDigest!=null&&HasSaved(owner,offer,applied?RebirthGearOwnerReceipt.Applied:RebirthGearOwnerReceipt.Rejected,true);

    private static bool HasSaved(RebirthStablePlayerIdentity owner, RebirthGearTransferState offer, RebirthGearOwnerReceipt stage,bool requireRetired=false)
    {
        if (owner == null || offer == null || string.IsNullOrEmpty(owner.CanonicalId) ||
            !offer.TryGetPlan(out var plan)) return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"preflight","owner-or-plan");
        try
        {
            if (!RebirthGearNativePlayerFile.TryRead(owner,out var saved,out var failure)) return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"native-final-file."+failure,"file");
            if (saved.buffData == null || saved.buffData.Length > 1024 * 1024)
                return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"buffData.shape","buffData",saved.buffData==null?-1:(int)saved.buffData.Length);
            if (!RebirthGearReceiptReader.Contains(saved.buffData.ToArray(), offer.TransactionId,stage))
                return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"receipt.Contains","buffData");
            if(requireRetired&&!RebirthGearPreparationMarkerReader.HasNoOriginal(saved.buffData.ToArray()))return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"preparation.HasNoOriginal","buffData");
            var bag = RebirthGearPlayerDataInventory.ReadSlots(saved, true);
            var belt = RebirthGearPlayerDataInventory.ReadSlots(saved, false);
            if (!RebirthGearInventorySnapshot.TryCapture(bag, belt, 4, out var snapshot)) return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"inventory.TryCapture","inventory");
            if (stage == RebirthGearOwnerReceipt.Rejected)
            {
                bool matches=plan.MatchesBefore(snapshot.Bag, snapshot.Belt, plan.GearBefore, snapshot.Bag.Length, snapshot.Belt.Length);
                if(!matches)RebirthGearSavedOwnerMismatch.Inventory(offer,stage,owner?.CanonicalId,plan,snapshot.Bag,snapshot.Belt);
                return matches;
            }
            if (stage == RebirthGearOwnerReceipt.Applied)
            {
                bool matches=plan.MatchesAppliedInventory(snapshot.Bag, snapshot.Belt);
                if(!matches)RebirthGearSavedOwnerMismatch.Inventory(offer,stage,owner?.CanonicalId,plan,snapshot.Bag,snapshot.Belt);
                return matches;
            }
            // Intent can precede expansion or follow a partial application.
            // Pad future cells only; never omit an existing removed tail.
            int maximum = Math.Max(plan.BagSlotsBefore, plan.BagSlotsAfter);
            if (snapshot.Bag.Length != plan.BagSlotsBefore && snapshot.Bag.Length != maximum) return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"applying.bag-shape.before-or-maximum","bag",snapshot.Bag.Length,maximum);
            var expanded = new RebirthGearInventoryPlan.Stack[maximum];
            for (int i = 0; i < expanded.Length; i++)
                expanded[i] = i < snapshot.Bag.Length ? snapshot.Bag[i] : new RebirthGearInventoryPlan.Stack();
            bool compatible=plan.InspectApplication(expanded, snapshot.Belt, true) != RebirthGearInventoryPlan.ApplicationState.Conflict;
            if(!compatible)RebirthGearSavedOwnerMismatch.Inventory(offer,stage,owner?.CanonicalId,plan,expanded,snapshot.Belt);
            return compatible;
        }
        catch { return RebirthGearSavedOwnerMismatch.Fail(offer,stage,owner?.CanonicalId,"witness.exception","file-or-inventory"); }
    }
}



