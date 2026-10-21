using System;

// Native setter success is a postcondition, not a void return. Unknown outcomes
// propagate to existing vehicle request Indeterminate handling without a refund.
internal static class RebirthVehicleInventoryWrite
{
    internal static ItemStack[] Snapshot(ItemStack[] slots)
    {
        if(slots==null)throw new ArgumentNullException(nameof(slots));
        var image=new ItemStack[slots.Length];
        for(int i=0;i<slots.Length;i++)image[i]=slots[i]?.Clone()??ItemStack.Empty.Clone();
        return image;
    }
    internal static bool CanMutate(EntityPlayer player,bool bag)
    {
        if(!ThreadManager.IsMainThread()||player?.world==null||player.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetEntity(player.entityId),player))return false;
        object inventory=bag?(object)player.bag:player.inventory;
        return inventory!=null&&!RebirthCamperStationNativeActorPayment.Blocks(inventory)
            &&(!RebirthGearOwnerReservation.BlocksInventory(inventory,bag)||RebirthVehicleOwnerCustody.NativeCanEnter(player,bag))
            &&!(player is EntityPlayerLocal local&&RebirthRemoteResourceOwnerAction.IsHeld(local));
    }
    internal static bool SameSlots(ItemStack[] expected,ItemStack[] actual)
    {
        if(expected==null||actual==null||expected.Length!=actual.Length)return false;
        for(int index=0;index<expected.Length;index++)
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected[index],actual[index]))return false;
        return true;
    }
    internal static bool CreditMatches(ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,ItemStack incoming,int credited)
    {
        if(incoming==null||incoming.IsEmpty()||credited<0||credited>incoming.count)return false;
        long added=0;
        return CreditArea(beforeBag,afterBag,incoming,ref added)&&CreditArea(beforeBelt,afterBelt,incoming,ref added)&&added==credited;
    }
    // Verify the complete native fuel debit, including cells unrelated to fuel.
    // Counts alone cannot distinguish consumption from an unrelated replacement.
    internal static bool TryMeasureDebit(ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,int itemType,int maximum,out int consumed)
    {
        consumed=0;long removed=0;
        if(itemType<=0||maximum<0||!DebitArea(beforeBag,afterBag,itemType,ref removed)||
            !DebitArea(beforeBelt,afterBelt,itemType,ref removed)||removed>maximum)return false;
        consumed=(int)removed;return true;
    }
    internal static bool FuelMatches(float before,float maximum,float after,int consumed,bool nativeResult)
    {
        if(float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(maximum)||float.IsInfinity(maximum)||
            float.IsNaN(after)||float.IsInfinity(after)||before<0f||maximum<=before||consumed<0||(!nativeResult&&consumed!=0))return false;
        float expected=Math.Min(maximum,before+consumed/25f);
        return Math.Abs(after-expected)<=0.0001f;
    }
    private static bool DebitArea(ItemStack[] before,ItemStack[] after,int itemType,ref long removed)
    {
        if(before==null||after==null||before.Length!=after.Length)return false;
        for(int index=0;index<before.Length;index++)
        {
            var prior=before[index];var current=after[index];
            if(prior==null||current==null)return false;
            if(RebirthStationGridIngredients.IsSameStackSnapshot(prior,current))continue;
            if(prior.IsEmpty()||prior.itemValue.type!=itemType)return false;
            if(current.IsEmpty()){removed+=prior.count;continue;}
            if(current.count<=0||current.count>=prior.count)return false;
            var expected=prior.Clone();expected.count=current.count;
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected,current))return false;
            removed+=(long)prior.count-current.count;
        }
        return true;
    }
    private static bool CreditArea(ItemStack[] before,ItemStack[] after,ItemStack incoming,ref long added)
    {
        if(before==null||after==null||before.Length!=after.Length)return false;
        for(int index=0;index<before.Length;index++)
        {
            var prior=before[index];var current=after[index];
            if(prior==null||current==null)return false;
            if(RebirthStationGridIngredients.IsSameStackSnapshot(prior,current))continue;
            if(current.IsEmpty())return false;
            if(prior.IsEmpty())
            {
                var expected=incoming.Clone();expected.count=current.count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected,current))return false;
                added+=current.count;
            }
            else
            {
                if(current.count<=prior.count||!prior.CanStackWith(incoming,true)||
                    !RebirthCookingItemStats.Compatible(prior.itemValue,incoming.itemValue))return false;
                var expected=prior.Clone();expected.count=current.count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected,current))return false;
                added+=(long)current.count-prior.count;
            }
        }
        return true;
    }
    internal static bool TryDebit(EntityPlayer player,bool bag,int slot,ItemStack before,ItemStack after)
    {
        if(!CanMutate(player,bag))return false;
        int length=bag?player.bag.ItemGrid.items.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.Length);
        if(slot<0||slot>=length||before==null||after==null||
            bag&&player.bag.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot])return false;
        ItemStack current=bag?player.bag.ItemGrid.items[slot]:player.inventory.GetItemStack(slot);
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(current,before))return false;
        var original=before.Clone();var expected=after.Clone();
        var beforeBag=Snapshot(player.bag?.ItemGrid.items??new ItemStack[0]);
        var beforeBelt=Snapshot(player.inventory?.ItemGrid.items??new ItemStack[0]);
        var expectedBag=Snapshot(beforeBag);var expectedBelt=Snapshot(beforeBelt);
        (bag?expectedBag:expectedBelt)[slot]=expected.Clone();
        if(bag)player.bag.SetSlot(slot,expected.Clone());else player.inventory.SetItem(slot,expected.Clone());
        current=bag?player.bag.ItemGrid.items[slot]:player.inventory.GetItemStack(slot);
        var actualBag=player.bag?.ItemGrid.items??new ItemStack[0];
        var actualBelt=player.inventory?.ItemGrid.items??new ItemStack[0];
        if(CanMutate(player,bag)&&SameSlots(expectedBag,actualBag)&&SameSlots(expectedBelt,actualBelt))return true;
        if(SameSlots(beforeBag,actualBag)&&SameSlots(beforeBelt,actualBelt)&&RebirthStationGridIngredients.IsSameStackSnapshot(current,original))return false;
        throw new InvalidOperationException("Vehicle inventory debit has an unknown native postimage; retain original custody.");
    }
}
