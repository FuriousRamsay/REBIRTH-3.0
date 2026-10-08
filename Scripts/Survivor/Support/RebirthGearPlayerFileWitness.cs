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
            !offer.TryGetPlan(out var plan)) return false;
        try
        {
            if (!RebirthGearNativePlayerFile.TryRead(owner,out var saved)) return false;
            if (saved.buffData == null || saved.buffData.Length > 1024 * 1024 ||
                !RebirthGearReceiptReader.Contains(saved.buffData.ToArray(), offer.TransactionId,
                    stage)) return false;
            if(requireRetired&&!RebirthGearPreparationMarkerReader.HasNoOriginal(saved.buffData.ToArray()))return false;
            var bag = RebirthPlayerDataInventory.ReadSlots(saved, true);
            var belt = RebirthPlayerDataInventory.ReadSlots(saved, false);
            if (!RebirthGearInventorySnapshot.TryCapture(bag, belt, 4, out var snapshot)) return false;
            if (stage == RebirthGearOwnerReceipt.Rejected)
                return plan.MatchesBefore(snapshot.Bag, snapshot.Belt, plan.GearBefore, snapshot.Bag.Length, snapshot.Belt.Length);
            if (stage == RebirthGearOwnerReceipt.Applied) return plan.MatchesAppliedInventory(snapshot.Bag, snapshot.Belt);
            // Intent can precede expansion or follow a partial application.
            // Pad future cells only; never omit an existing removed tail.
            int maximum = Math.Max(plan.BagSlotsBefore, plan.BagSlotsAfter);
            if (snapshot.Bag.Length != plan.BagSlotsBefore && snapshot.Bag.Length != maximum) return false;
            var expanded = new RebirthGearInventoryPlan.Stack[maximum];
            for (int i = 0; i < expanded.Length; i++)
                expanded[i] = i < snapshot.Bag.Length ? snapshot.Bag[i] : new RebirthGearInventoryPlan.Stack();
            return plan.InspectApplication(expanded, snapshot.Belt, true) !=
                RebirthGearInventoryPlan.ApplicationState.Conflict;
        }
        catch { return false; }
    }
}