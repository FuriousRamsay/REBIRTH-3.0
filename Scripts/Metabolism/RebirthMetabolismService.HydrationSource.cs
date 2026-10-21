public static partial class RebirthMetabolismService
{
    // The overview carries the selected full native item. Type/seed alone can
    // identify more than one container with different liquid or metadata.
    public static bool EquipMatchingHydrationContainer(EntityPlayer player,ItemValue selected,out string message)
    {
        message=string.Empty;
        if(selected==null||selected.IsEmpty()||!(player is EntityPlayerLocal)||!ThreadManager.IsMainThread()||
            player.world==null||player.world.IsRemote()||!IsServerAuthority||player.bag==null||player.inventory==null)return false;
        bool Same(ItemStack item)=>item!=null&&!item.IsEmpty()&&
            RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(item.itemValue,1),new ItemStack(selected,1));
        var bag=player.bag.ItemGrid.items;var belt=player.inventory.ItemGrid.items;
        for(int index=0;index<bag.Length;index++)
            if(!(player.bag.LockedSlots!=null&&index<player.bag.LockedSlots.Length&&player.bag.LockedSlots[index])&&Same(bag[index]))
                return TryEquipHydrationAt(player,true,index,out message);
        int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
        for(int index=0;index<owned;index++)if(Same(belt[index]))return TryEquipHydrationAt(player,false,index,out message);
        return false;
    }
}
