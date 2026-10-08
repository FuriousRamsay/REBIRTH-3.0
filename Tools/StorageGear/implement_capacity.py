from pathlib import Path
r=Path.cwd()
def edit(f,a,b):
 p=r/f;s=p.read_text(encoding='utf-8-sig');assert a in s,(f,a[:70]);p.write_text(s.replace(a,b),encoding='utf-8')
edit('Scripts/Survivor/Definitions/RebirthSurvivorDefinitionLoader.cs','ReadIdChildren(e,"supported_traits","trait"),items,effects));','ReadIdChildren(e,"supported_traits","trait"),items,effects,IntAttr(e,"toolbelt_slot_bonus",0)));')
edit('Scripts/Survivor/Domain/RebirthSurvivorDefinitionVersion.cs',".Append(s.GearBagSlotBonus).Append('|')",".Append(s.GearBagSlotBonus).Append('|').Append(s.GearToolbeltSlotBonus).Append('|')")
edit('Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs','s.Kind=="survivor_gear" && s.GearBagSlotBonus>0','s.Kind=="survivor_gear" && (s.GearBagSlotBonus>0 || s.GearToolbeltSlotBonus>0)')
edit('Scripts/Survivor/Definitions/RebirthSurvivorAuthoringValidator.cs','if(s.GearMinStrength>100f', 'if(s.GearToolbeltSlotBonus>6 || (s.GearToolbeltSlotBonus>0 && s.GearSlotId!="belt")) r.Errors.Add(s.Id+" has invalid toolbelt slot bonus.");\n                if(s.GearMinStrength>100f')
f='Scripts/Survivor/Support/RebirthSurvivorGearService.cs'
edit(f,'ForceHundredSlotBackpackForPersonalCraftingTest = true','ForceHundredSlotBackpackForPersonalCraftingTest = false')
# Spill only the disappearing tail, after incoming item removal but before changing entitlement.
a='''        if (string.Equals(profile.GearSlotId, BackpackSlotId, StringComparison.OrdinalIgnoreCase) && !CanShrinkTo(player, targetSlots, inBackpack ? slot : -1))
        {
            message = "Move items out of the backpack slots that would be removed before changing to this pack.";
            return false;
        }
'''
edit(f,a,'')
a='''        if (inBackpack) player.bag.SetSlot(slot, next); else player.inventory.SetItem(slot, next);'''
edit(f,a,a+'''
        if (!RebirthGearOverflow.TryRelease(player, profile.GearSlotId, targetSlots,
            Math.Min(RebirthToolbeltCapacity.MaximumSlots, RebirthToolbeltCapacity.GetSlotsForLevel(player.Progression.Level) + profile.GearToolbeltSlotBonus)))
        {
            if (inBackpack) player.bag.SetSlot(slot, stack); else player.inventory.SetItem(slot, stack);
            message = Localization.Get("rebirthGearRecoveryFailed"); return false;
        }
''')
a='''        if (string.Equals(slotId, BackpackSlotId, StringComparison.OrdinalIgnoreCase) && !CanShrinkTo(player, targetSlots, -1))
        {
            message = "Move items out of the backpack slots that would be removed before unequipping this pack.";
            return false;
        }'''
edit(f,a,'''        if (!RebirthGearOverflow.TryRelease(player, slotId, targetSlots, RebirthToolbeltCapacity.GetSlotsForLevel(player.Progression.Level)))
        { message = Localization.Get("rebirthGearRecoveryFailed"); return false; }''')
# Existing persisted GearSlots projection supplies remote bonus without protocol changes.
f='Scripts/UI/RebirthToolbeltCapacity.cs'
edit(f,'return GetSlotsForLevel(level);','return Math.Min(MaximumSlots, GetSlotsForLevel(level) + RebirthSurvivorGearService.GetToolbeltBonus(player));')
f='Scripts/Survivor/Support/RebirthSurvivorGearService.cs'
edit(f,'    public static bool IsEquipped(','''    public static int GetToolbeltBonus(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return 0;
        string item = null;
        if (player.world != null && player.world.IsRemote())
            return RebirthSurvivorClientState.GetProjectedToolbeltBonus(player);
        RebirthWorldCharacterRecord record;
        if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete && record.Support != null)
            record.Support.EquippedGearBySlot.TryGetValue(BeltSlotId, out item);
        return ToolbeltBonusForItem(item);
    }
    public static int ToolbeltBonusForItem(string item)
    {
        RebirthTraitSupportProfileDefinition profile;
        return !string.IsNullOrEmpty(item) && RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(item, out profile)
            && profile != null && profile.GearSlotId == BeltSlotId ? profile.GearToolbeltSlotBonus : 0;
    }

    public static bool IsEquipped(''')
f='Scripts/Survivor/Network/RebirthSurvivorClientState.cs'
edit(f,'    public static int GetProjectedPhysicalBagSlots()', '''    public static int GetProjectedToolbeltBonus(EntityPlayer player)
    {
        lock (Sync)
        {
            if (player == null || player.world == null || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
                || ownerState == null || !ScopeMatches(player.world, player) || !ownerState.DefinitionsCompatible
                || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter) return 0;
            foreach (var gear in ownerState.GearSlots)
                if (gear.SlotId == RebirthSurvivorGearService.BeltSlotId)
                    return RebirthSurvivorGearService.ToolbeltBonusForItem(gear.ItemId);
            return 0;
        }
    }

    public static int GetProjectedPhysicalBagSlots()''')
