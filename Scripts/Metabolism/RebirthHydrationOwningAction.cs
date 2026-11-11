using System;

// Actual remote UI actions retain one authenticated original. The server never
// moves a remote player's inventory through its EntityPlayer mirror.
internal static class RebirthHydrationOwningAction
{
    internal static bool Begin(EntityPlayerLocal player,bool bag,int index,bool returning)
    {
        RebirthDebug.Log("metabolism", $"[REBIRTH HydrationTransfer] client-request action={(returning?"return":"equip")} actor={player?.entityId.ToString()??"none"} source={(bag?"bag":"belt")} index={index} remote={player?.world?.IsRemote().ToString()??"none"}");
        if(player?.world?.IsRemote()!=true||!ThreadManager.IsMainThread())return false;
        if (!RebirthHydrationClientOriginals.TryReadForIdleRecovery(player, out var saved)) return false;
        if (RebirthHydrationProposalClient.TryGetCurrent(player, out var intent) ||
            saved != null && RebirthHydrationProposalClient.Intent.TryRecover(player, saved, out intent))
        {
            intent.Advance(System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency);
            return intent.Current();
        }
        if (saved != null || RebirthHydrationOwnerHold.HoldsMetabolismOwner(player)) return false;
        return RebirthMetabolismLiveAction.Begin(player,
            returning ? RebirthMetabolismLiveAction.Kind.Return : RebirthMetabolismLiveAction.Kind.Equip,
            bag ? RebirthMetabolismSourceKind.Backpack : RebirthMetabolismSourceKind.HeldToolbelt, index);
    }
    internal static bool BeginMatching(EntityPlayerLocal player,ItemValue item,bool returning)
    {
        if(returning)return Begin(player,true,-1,true);
        if(player?.world?.IsRemote()!=true||item==null||item.IsEmpty())return false;
        var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;
        if(bag==null||belt==null)return false;
        bool Same(ItemStack value)=>value!=null&&!value.IsEmpty()&&
            RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(value.itemValue,1),new ItemStack(item,1));
        for(int index=0;index<bag.Length;index++)
            if(!(player.bag.LockedSlots!=null&&index<player.bag.LockedSlots.Length&&player.bag.LockedSlots[index])&&Same(bag[index]))
                return Begin(player,true,index,false);
        int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
        for(int index=0;index<owned;index++)if(Same(belt[index]))return Begin(player,false,index,false);
        return false;
    }
}
