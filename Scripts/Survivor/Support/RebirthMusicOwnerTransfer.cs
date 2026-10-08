using System;
using System.IO;

public enum RebirthMusicOwnerTransferResult { Pending, Applied, Rejected }

// Invoked only for an authenticated server offer matching the current character.
// Receipt and native inventory live in the same owning player's save data.
public static class RebirthMusicOwnerTransfer
{
    public static RebirthMusicOwnerTransferResult Apply(EntityPlayerLocal player,
        string currentCreationId, RebirthMusicTransferState offer)
    {
        Guid transaction;
        if (player == null || player.Buffs == null || offer == null
            || !RebirthSurvivorRequestScope.Matches(currentCreationId,offer.CreationId)
            || !Guid.TryParse(offer.TransactionId, out transaction) || transaction == Guid.Empty)
            return RebirthMusicOwnerTransferResult.Pending;
        RebirthMusicTransferState validated;
        if(!RebirthMusicTransferState.TryRead(offer.ToXml(),out validated))
            return RebirthMusicOwnerTransferResult.Pending;
        offer=validated;
        string receipt = "rbMusic_" + transaction.ToString("N");
        float previous = player.Buffs.GetCustomVar(receipt);
        if (previous == 1f) return RebirthMusicOwnerTransferResult.Applied;
        if (previous == -1f) return RebirthMusicOwnerTransferResult.Rejected;
        // A malformed receipt is ambiguous custody, never permission to retry inventory.
        if (previous != 0f) return RebirthMusicOwnerTransferResult.Pending;
        if (!player.IsSpawned() || player.IsDead() || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return RebirthMusicOwnerTransferResult.Pending;

        ItemValue value;
        // Conservative canonical-domain refusal is not proof of rejected custody.
        if (!RebirthNativeItemCodec.TryDecode(offer.ItemData, out value))
            return RebirthMusicOwnerTransferResult.Pending;
        if (value == null || value.IsEmpty() || value.ItemClass?.GetItemName() != offer.ItemId
            || !IsAllowedCassette(offer)) return Reject(player, receipt);
        if (offer.Operation == 1)
        {
            var slots = offer.SourceIsBag ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items;
            if (slots == null || offer.SourceIndex < 0 || offer.SourceIndex >= slots.Length)
                return Reject(player, receipt);
            if (!offer.SourceIsBag && offer.SourceIndex >= RebirthToolbeltCapacity.GetOwnedSlotCount(player, slots.Length))
                return Reject(player, receipt);
            var source = slots[offer.SourceIndex];
            if (source == null || source.IsEmpty() || RebirthMusicLibraryService.Encode(source.itemValue) != offer.ItemData)
                return Reject(player, receipt);
            var remaining = source.Clone();
            remaining.count--;
            if (remaining.count == 0) remaining = ItemStack.Empty.Clone();
            return Commit(player,receipt,offer.SourceIsBag,offer.SourceIndex,remaining);
        }
        else if (offer.Operation == 2)
        {
            ItemStack replacement;
            int destination = RebirthMusicLibraryService.FindReturnSlot(player.bag?.ItemGrid.items, value, out replacement);
            if (destination < 0) return RebirthMusicOwnerTransferResult.Pending;
            return Commit(player,receipt,true,destination,replacement);
        }
        else return Reject(player, receipt);

    }

    private static bool IsAllowedCassette(RebirthMusicTransferState offer)
    {
        if(!offer.IsAudiobook)return RebirthMusicLibraryService.IsMusicCassette(offer.ItemId);
        // A title removed from current configuration can still be returned to its owner.
        if(offer.Operation==2)return true;
        RebirthAudiobookDefinition definition;
        return RebirthProgressionRuntimeConfig.TryGetAudiobook(offer.ItemId,out definition)&&definition!=null;
    }

    private static RebirthMusicOwnerTransferResult Commit(EntityPlayerLocal player,string receipt,bool bag,int slot,ItemStack replacement)
    {
        try
        {
            // Native setters may mutate inventory before a listener throws. Record an
            // uncertain marker first so a repeated offer cannot repeat that mutation.
            player.Buffs.SetCustomVar(receipt,2f,true);
            if(bag)player.bag.SetSlot(slot,replacement);
            else player.inventory.SetItem(slot,replacement);
            player.Buffs.SetCustomVar(receipt,1f,true);
            return RebirthMusicOwnerTransferResult.Applied;
        }
        catch(Exception)
        {
            // Neither applied nor rejected is established. Retain marker2 for recovery.
            return RebirthMusicOwnerTransferResult.Pending;
        }
    }
    private static RebirthMusicOwnerTransferResult Reject(EntityPlayerLocal player, string receipt)
    {
        // A moved source must not later become valid on a delayed duplicate offer.
        player.Buffs.SetCustomVar(receipt, -1f, true);
        return RebirthMusicOwnerTransferResult.Rejected;
    }
}
