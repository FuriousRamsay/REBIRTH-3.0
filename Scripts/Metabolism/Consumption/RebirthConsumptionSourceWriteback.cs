// Compare the captured source immediately before writing. This is an in-memory
// source commit; it does not claim cross-repository crash durability.
internal static class RebirthConsumptionSourceWriteback
{
    internal static bool TryCommit(EntityPlayer player, RebirthMetabolismSourceKind kind,
        int slot, ItemStack expected, ItemStack replacement, RebirthMetabolismState state)
    {
        if(!ThreadManager.IsMainThread())return false;
        RebirthConsumptionTrace.Note("debit.attempt",player,kind:kind,slot:slot,before:expected,after:replacement);
        if (player?.world==null||player.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,player.world)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||state==null||
            !RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out var creation)||
            !RebirthMetabolismStateRepository.TryGet(player,out var originalState)||!ReferenceEquals(originalState,state)||
            expected == null || expected.IsEmpty() || expected.itemValue == null ||
            replacement!=null&&(replacement.itemValue==null||replacement.count<0||replacement.count>expected.count)||RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {
            RebirthConsumptionTrace.Note("debit.refused",player,kind:kind,slot:slot,before:expected,reason:"invalid-source-or-resource-hold",success:false);
            return false;
        }
        var world=player.world;var worldState=world.worldState;string worldGuid=worldState?.Guid;int deaths=player.Died;
        var bagOwner=player.bag;var beltOwner=player.inventory;
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
            ReferenceEquals(world.worldState,worldState)&&world.worldState?.Guid==worldGuid&&ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&player.Died==deaths&&
            ReferenceEquals(player.bag,bagOwner)&&ReferenceEquals(player.inventory,beltOwner)&&
            RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out var currentCreation)&&currentCreation==creation&&
            RebirthMetabolismStateRepository.TryGet(player,out var currentState)&&ReferenceEquals(currentState,state);
        ItemStack current;
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot:
                if (state == null||(RebirthHydrationOwnerHold.HoldsMetabolismOwner(player as EntityPlayerLocal)||RebirthHydrationServerSourceHold.HoldsPlayer(player))) return false;
                current = state.HydrationSlotItem;
                break;
            case RebirthMetabolismSourceKind.Backpack:
                var bag = player.bag != null ? player.bag.ItemGrid.items : null;
                if (bag == null || slot < 0 || slot >= bag.Length) return false;
                current = bag[slot];
                break;
            case RebirthMetabolismSourceKind.HeldToolbelt:
                var belt = player.inventory != null ? player.inventory.ItemGrid.items : null;
                if (belt == null || slot < 0 || slot >= RebirthToolbeltCapacity.GetOwnedSlotCount(player, belt.Length)) return false;
                current = belt[slot];
                break;
            default:
                return false; // New source kinds must supply an explicit adapter.
        }
        if (current == null || current.IsEmpty() || !Current()||!RebirthStationGridIngredients.IsSameStackSnapshot(current,expected))
        {
            RebirthConsumptionTrace.Note("debit.refused",player,kind:kind,slot:slot,before:expected,after:current,reason:"source-preimage-changed",success:false);
            return false;
        }

        ItemStack value = replacement != null ? replacement.Clone() : ItemStack.Empty.Clone();
        ItemStack intended = value.Clone();
        var expectedBag=RebirthVehicleInventoryWrite.Snapshot(player.bag?.ItemGrid?.items??System.Array.Empty<ItemStack>());
        var expectedBelt=RebirthVehicleInventoryWrite.Snapshot(player.inventory?.ItemGrid?.items??System.Array.Empty<ItemStack>());
        if(kind==RebirthMetabolismSourceKind.Backpack)expectedBag[slot]=intended.Clone();
        if(kind==RebirthMetabolismSourceKind.HeldToolbelt)expectedBelt[slot]=intended.Clone();
        if(!Current())return false;
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot: state.HydrationSlotItem = value; break;
            case RebirthMetabolismSourceKind.Backpack: player.bag.SetSlot(slot, value); break;
            case RebirthMetabolismSourceKind.HeldToolbelt: player.inventory.SetItem(slot, value); break;
        }
        if(!Current())return false;
        // Native setters return void and may be refused by an inventory guard.
        // Only the exact observed postimage permits the caller to add ingestion.
        ItemStack observed = null;
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot: observed = state.HydrationSlotItem; break;
            case RebirthMetabolismSourceKind.Backpack:
                var liveBag=player.bag?.ItemGrid?.items;if(liveBag==null||slot<0||slot>=liveBag.Length)return false;observed=liveBag[slot];break;
            case RebirthMetabolismSourceKind.HeldToolbelt:
                var liveBelt=player.inventory?.ItemGrid?.items;if(liveBelt==null||slot<0||slot>=liveBelt.Length)return false;observed=liveBelt[slot];break;
        }
        bool committed=Current()&&RebirthStationGridIngredients.IsSameStackSnapshot(observed,intended)&&
            RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag?.ItemGrid?.items??System.Array.Empty<ItemStack>())&&
            RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory?.ItemGrid?.items??System.Array.Empty<ItemStack>());
        RebirthConsumptionTrace.Note("debit.observed",player,kind:kind,slot:slot,before:expected,after:observed,
            reason:committed?"exact-postimage":"setter-postimage-mismatch",success:committed);
        return committed;
    }
}

