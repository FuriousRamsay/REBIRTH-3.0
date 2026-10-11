using System;

// Additional library capacity belongs to the equipped backpack tier, never background bag bonuses.
public static class RebirthBackpackLibraryPolicy
{
    public const int SlotsPerTier=10;
    public const int MaxSlots=80;
    // Admission is limited to books, readable notes, registered literature and audiobooks.
    public static int CapacityForBackpack(string itemId)
    {
        RebirthTraitSupportProfileDefinition profile;
        if(string.IsNullOrEmpty(itemId)||
            !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId,out profile)||profile==null||
            !string.Equals(profile.Kind,"survivor_gear",StringComparison.Ordinal)||
            !string.Equals(profile.GearSlotId,RebirthSurvivorGearService.BackpackSlotId,StringComparison.Ordinal)||
            profile.GearBagSlotBonus<=0||profile.GearBagSlotBonus%11!=0)return 0;
        return Math.Min(MaxSlots,profile.GearBagSlotBonus/11*SlotsPerTier);
    }

    public static bool IsLearningMaterial(ItemValue item)
    {
        if(item==null||item.ItemClass==null)return false;
        string id=item.ItemClass.GetItemName();
        RebirthLiteratureDefinition literature;
        RebirthAudiobookDefinition audio;
        // Storage eligibility is broader than progression: this leisure book teaches no skill.
        return string.Equals(item.ItemClass.ItemTypeIcon,"book",StringComparison.OrdinalIgnoreCase)||
            string.Equals(id,"rebirthSupportLeisurePuzzleBook",StringComparison.Ordinal)||
            string.Equals(id,"noteDuke01",StringComparison.Ordinal)||
            RebirthProgressionRuntimeConfig.TryGetLiterature(id,out literature)&&literature!=null||
            RebirthProgressionRuntimeConfig.TryGetAudiobook(id,out audio)&&audio!=null||
            RebirthCookingCatalogue.IsPreparationReference(id);
    }

    // Every occupied trailing slot must be recovered before reducing available capacity.
    // Caller settles recovery atomically; this policy never removes or rewrites items.
    public static bool CanReduceCapacity(ItemStack[] contents,int target)
    {
        if(target<0||target>MaxSlots)return false;
        if(contents==null)return true;
        for(int i=target;i<contents.Length;i++)
            if(contents[i]!=null&&!contents[i].IsEmpty())return false;
        return true;
    }
}