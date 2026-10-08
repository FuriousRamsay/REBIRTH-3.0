using System;
using System.Xml.Linq;

// Verifies an authenticated offer against the retained pre-offer intent before
// replacing an early hold. Does not apply inventory or certify server durability.
public static class RebirthGearPreparationOfferBinding
{
    public static bool MatchesBound(RebirthGearPreparationIntent intent,RebirthGearInventorySnapshot original,
        RebirthGearTransferState offer,Guid savedWorld)
    {
        if(savedWorld==Guid.Empty||offer?.PreparationRequestDigest==null||
            !Matches(intent,original,offer)||
            !RebirthGearPreparationMarker.TryEncode(savedWorld,original.OwnedBeltSlots,intent,out var marker))return false;
        try
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return offer.PreparationRequestDigest==BitConverter.ToString(hash.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(marker))).Replace("-",string.Empty).ToLowerInvariant();
        }
        catch {return false;}
    }
    public static bool Matches(RebirthGearPreparationIntent intent,RebirthGearInventorySnapshot original,
        RebirthGearTransferState offer)
    {
        if(intent==null||offer==null||offer.TransactionId!=intent.TransactionId.ToString("N")||
            !RebirthSurvivorRequestScope.Matches(intent.CreationId,offer.CreationId)||
            offer.ExpectedRevision!=intent.ExpectedRevision||offer.HasRecoveryAttempts||
            !intent.MatchesInventory(original)||!offer.TryGetPlan(out var plan))return false;
        try
        {
            if(intent.IsUnequip)
            {
                if(intent.UnequipSlot!=offer.SlotId||plan.GearBefore==null||plan.GearBefore.Count!=1||
                    plan.GearAfter==null||plan.GearAfter.Count!=0||
                    !RebirthNativeItemCodec.TryDecode(plan.GearBefore.ItemData,out var oldValue)||oldValue?.ItemClass==null||
                    !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(oldValue.ItemClass.GetItemName(),out var oldProfile)||
                    oldProfile==null||!string.Equals(oldProfile.Kind,"survivor_gear",StringComparison.OrdinalIgnoreCase)||
                    oldProfile.GearSlotId!=intent.UnequipSlot||
                    !RebirthGearInventoryPlanner.TryPlan(original,plan.GearBefore,false,-1,plan.BagSlotsAfter,plan.BeltSlotsAfter,out var removed))return false;
                return XNode.DeepEquals(RebirthGearInventoryPlanCodec.Write(removed),RebirthGearInventoryPlanCodec.Write(plan));
            }
            var source=(intent.SourceIsBag?original.Bag:original.Belt)[intent.SourceIndex];
            if(!RebirthNativeItemCodec.TryDecode(source.ItemData,out var native)||native?.ItemClass==null||
                native.type!=intent.ItemType||native.Seed!=intent.ItemSeed||
                !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(native.ItemClass.GetItemName(),out var profile)||
                profile==null||!string.Equals(profile.Kind,"survivor_gear",StringComparison.OrdinalIgnoreCase)||
                profile.GearSlotId!=offer.SlotId||
                !RebirthGearInventoryPlanner.TryPlan(original,plan.GearBefore,intent.SourceIsBag,intent.SourceIndex,
                    plan.BagSlotsAfter,plan.BeltSlotsAfter,out var expected))return false;
            // Replanning is pure and deterministic. Compare every pre/postimage,
            // displaced item and recovery entry, not merely total conservation.
            return XNode.DeepEquals(RebirthGearInventoryPlanCodec.Write(expected),RebirthGearInventoryPlanCodec.Write(plan));
        }
        catch {return false;}
    }
}