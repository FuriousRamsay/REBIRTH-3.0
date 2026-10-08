using System;

// Builds an authenticated remote equip offer. No live inventory, gear, revision,
// recovery or journal mutation occurs here. Saving and owner receipts follow later.
public static class RebirthRemoteGearEquipPlan
{
    public static bool TryBuild(EntityPlayer player, ClientInfo sender, string creation,
        int type, ushort seed, long expectedRevision, out RebirthGearTransferState offer)
        => TryBuild(player, sender, creation, type, seed, expectedRevision, Guid.NewGuid(), out offer);

    // The live intent dispatcher supplies its retained original transaction. Never
    // mint another identity when retrying an upload or an uncertain preparation.
    public static bool TryBuild(EntityPlayer player, ClientInfo sender, string creation,
        int type, ushort seed, long expectedRevision, Guid originalTransaction, out RebirthGearTransferState offer)
        => TryBuildCore(player,sender,creation,type,seed,expectedRevision,originalTransaction,null,out offer);

    public static bool TryBuild(EntityPlayer player,ClientInfo sender,RebirthGearPreparationIntent intent,out RebirthGearTransferState offer)
    {
        offer=null;
        if(intent!=null&&intent.IsUnequip)return TryBuildUnequip(player,sender,intent,out offer);
        return intent!=null && TryBuildCore(player,sender,intent.CreationId,intent.ItemType,intent.ItemSeed,
            intent.ExpectedRevision,intent.TransactionId,intent,out offer);
    }
    private static bool TryBuildCore(EntityPlayer player,ClientInfo sender,string creation,int type,ushort seed,
        long expectedRevision,Guid originalTransaction,RebirthGearPreparationIntent intent,out RebirthGearTransferState offer)
    {
        offer = null;
        if (originalTransaction == Guid.Empty || type <= 0 || expectedRevision < 0 || expectedRevision == long.MaxValue ||
            !RebirthRemoteGearInventorySource.TryCapture(player, sender, creation, out var snapshot))
            return false;
        if (intent!=null && !intent.MatchesInventory(snapshot)) return false;
        if (!RebirthWorldCharacterService.TryGet(player, out var record) || record?.Support == null ||
            record.Origin == null || !RebirthSurvivorRequestScope.Matches(creation, record.Origin.CreationId) ||
            record.Support.GearRevision != expectedRevision ||
            record.Support.LastGearSettlement?.TransactionId == originalTransaction.ToString("N") ||
            record.Support.PendingGearTransfer != null || record.Support.PendingLibraryTransfer != null ||
            record.Support.PendingMusicTransfer != null) return false;

        RebirthGearInventoryPlan.Stack source = null;
        bool isBag = false;
        int index = -1;
        ItemValue incoming = null;
        for (int area = 0; area < 2 && source == null; area++)
        {
            var slots = area == 0 ? snapshot.Belt : snapshot.Bag;
            int limit = area == 0 ? snapshot.OwnedBeltSlots : slots.Length;
            for (int i = 0; i < limit; i++)
            {
                if(intent!=null && (intent.SourceIsBag!=(area==1)||intent.SourceIndex!=i))continue;
                var entry = slots[i];
                if (entry == null || entry.Count <= 0) continue;
                if (!RebirthNativeItemCodec.TryDecode(entry.ItemData, out var value)) return false;
                if (value.type != type || value.Seed != seed) continue;
                source = entry; incoming = value; isBag = area == 1; index = i; break;
            }
        }
        if (source == null || incoming?.ItemClass == null) return false;
        string itemId = incoming.ItemClass.GetItemName();
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId, out var profile) ||
            profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(profile.GearSlotId) ||
            Attribute(record, "strength") + .001f < profile.GearMinStrength ||
            Attribute(record, "constitution") + .001f < profile.GearMinConstitution) return false;
        if (profile.GearSlotId == RebirthSurvivorGearService.SupportSlotId &&
            RebirthMetabolismStateRepository.TryGet(player, out var metabolism) &&
            metabolism?.HydrationSlotItem != null && !metabolism.HydrationSlotItem.IsEmpty()) return false;

        var state = record.Support;
        state.EquippedGearBySlot.TryGetValue(profile.GearSlotId, out var oldId);
        state.EquippedGearItemDataBySlot.TryGetValue(profile.GearSlotId, out var oldData);
        if (string.Equals(oldId, itemId, StringComparison.OrdinalIgnoreCase)) return false;
        var equipped = new RebirthGearInventoryPlan.Stack();
        if (!string.IsNullOrEmpty(oldId))
        {
            // Legacy name-only gear must first acquire exact persisted native metadata.
            if (!RebirthNativeItemCodec.TryDecode(oldData, out var oldValue) ||
                !string.Equals(oldValue.ItemClass.GetItemName(), oldId, StringComparison.OrdinalIgnoreCase) ||
                !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(oldId, out var oldProfile) ||
                oldProfile == null || oldProfile.GearSlotId != profile.GearSlotId) return false;
            equipped.ItemData = oldData; equipped.Count = 1;
        }
        else if (!string.IsNullOrEmpty(oldData)) return false;

        int bagSlots = RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record, profile.GearSlotId, itemId);
        int gearBeltBonus = profile.GearSlotId == RebirthSurvivorGearService.BeltSlotId
            ? profile.GearToolbeltSlotBonus : RebirthSurvivorGearService.GetToolbeltBonus(player);
        int beltSlots = Math.Min(RebirthToolbeltCapacity.MaximumSlots,
            RebirthToolbeltCapacity.GetSlotsForLevel(1) +
            RebirthBackgroundStorageService.ToolbeltBonus(player) + gearBeltBonus);
        if (!RebirthGearInventoryPlanner.TryPlan(snapshot, equipped, isBag, index,
            bagSlots, beltSlots, out var plan)) return false;
        // Capture preserved the complete native ItemValue. Item type/seed only select
        // the source; they never substitute for its actual item identity in the offer.
        return RebirthGearTransferState.TryCreate(originalTransaction.ToString("N"),
            record.Origin.CreationId, profile.GearSlotId, expectedRevision, plan, out offer);
    }

    private static bool TryBuildUnequip(EntityPlayer player,ClientInfo sender,RebirthGearPreparationIntent intent,
        out RebirthGearTransferState offer)
    {
        offer=null;
        if(intent==null||!intent.IsUnequip||intent.TransactionId==Guid.Empty||
            !RebirthRemoteGearInventorySource.TryCapture(player,sender,intent.CreationId,out var snapshot)||
            !intent.MatchesInventory(snapshot)||!RebirthWorldCharacterService.TryGet(player,out var record)||
            record?.Support==null||record.Origin==null||
            !RebirthSurvivorRequestScope.Matches(intent.CreationId,record.Origin.CreationId)||
            record.Support.GearRevision!=intent.ExpectedRevision||
            record.Support.LastGearSettlement?.TransactionId==intent.TransactionId.ToString("N")||
            record.Support.PendingGearTransfer!=null||record.Support.PendingLibraryTransfer!=null||
            record.Support.PendingMusicTransfer!=null)return false;
        var slot=intent.UnequipSlot;var state=record.Support;
        if(!state.EquippedGearBySlot.TryGetValue(slot,out var oldId)||string.IsNullOrEmpty(oldId)||
            !state.EquippedGearItemDataBySlot.TryGetValue(slot,out var oldData)||
            !RebirthNativeItemCodec.TryDecode(oldData,out var oldValue)||oldValue?.ItemClass==null||
            !string.Equals(oldValue.ItemClass.GetItemName(),oldId,StringComparison.OrdinalIgnoreCase)||
            !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(oldId,out var profile)||profile==null||
            !string.Equals(profile.Kind,"survivor_gear",StringComparison.OrdinalIgnoreCase)||profile.GearSlotId!=slot)return false;
        if(slot==RebirthSurvivorGearService.SupportSlotId&&
            RebirthMetabolismStateRepository.TryGet(player,out var metabolism)&&
            metabolism?.HydrationSlotItem!=null&&!metabolism.HydrationSlotItem.IsEmpty())return false;
        int bagSlots=RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record,slot,null);
        int beltBonus=slot==RebirthSurvivorGearService.BeltSlotId?0:RebirthSurvivorGearService.GetToolbeltBonus(player);
        int beltSlots=Math.Min(RebirthToolbeltCapacity.MaximumSlots,RebirthToolbeltCapacity.GetSlotsForLevel(1)+
            RebirthBackgroundStorageService.ToolbeltBonus(player)+beltBonus);
        if(!RebirthGearInventoryPlanner.TryPlan(snapshot,new RebirthGearInventoryPlan.Stack{ItemData=oldData,Count=1},
            false,-1,bagSlots,beltSlots,out var plan))return false;
        return RebirthGearTransferState.TryCreate(intent.TransactionId.ToString("N"),record.Origin.CreationId,
            slot,intent.ExpectedRevision,plan,out offer);
    }
    private static float Attribute(RebirthWorldCharacterRecord record, string name)
    {
        return record.Progression?.Attributes != null &&
            record.Progression.Attributes.TryGetValue(name, out var value) && value != null
            ? value.Current : 0f;
    }
}