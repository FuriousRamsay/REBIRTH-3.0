using System;
using System.Collections.Generic;
using System.Linq;

// Player destination retained. Native camper actor-payment receipts couple the
// owner image to the grouped station image before either can settle.
internal static class RebirthRecyclerOwnerDelivery
{
    internal static bool Plan(ItemStack[] originalBag,ItemStack[] originalBelt,bool[] bagLocks,bool[] beltLocks,int beltCount,
        IList<ItemStack> items,out ItemStack[] bag,out ItemStack[] belt)
    {
        bag=ItemStack.Clone(originalBag);belt=ItemStack.Clone(originalBelt);
        foreach(var item in items)
        {
            if(item==null||item.IsEmpty()||item.count<1)return false;
            var remaining=item.Clone();
            if(!Insert(bag,bagLocks,bag.Length,remaining)&&!Insert(belt,beltLocks,Math.Min(beltCount,belt.Length),remaining))return false;
        }
        return true;
    }
    private static bool Insert(ItemStack[] cells,bool[] locks,int count,ItemStack incoming)
    {
        // Work on clones; a refused full-inventory request has no native setters.
        for(int pass=0;pass<2;pass++)for(int slot=0;slot<count&&incoming.count>0;slot++)
        {
            if(locks!=null&&slot<locks.Length&&locks[slot])continue;
            var stack=cells[slot];if(stack==null)return false;
            if(pass==0&&stack.IsEmpty()||pass==1&&!stack.IsEmpty())continue;
            int maximum=incoming.itemValue.ItemClass?.MaxCount??0;if(maximum<1)return false;
            if(!stack.IsEmpty())
            {
                if(!stack.CanStackWith(incoming))continue;
            }
            int available=maximum-stack.count;if(available<=0)continue;
            int take=Math.Min(incoming.count,available);
            cells[slot]=new ItemStack((stack.IsEmpty()?incoming.itemValue:stack.itemValue).Clone(),stack.count+take);incoming.count-=take;
        }
        return incoming.count==0;
    }
    internal static bool Begin(EntityPlayer actor,RebirthCamperStationState station,Guid operation,ItemStack[] input,
        RecipeQueueItem[] queue,IList<ItemStack> returns,out string reason,Guid cancellationGroup=default(Guid),ItemStack[] output=null,ItemStack[] tools=null)
    {
        reason="xuiRebirthRecyclingOwnerUnavailable";
        RebirthStablePlayerIdentity identity=null;bool[] bagLocks,beltLocks=null;ItemStack[] beforeBag,beforeBelt;
        bool remote=!(actor is EntityPlayerLocal);
        if(remote)
        {
            if(!RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out identity,out _)
                ||!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out beforeBag,out beforeBelt,out bagLocks))return false;
        }
        else
        {
            beforeBag=ItemStack.Clone(actor.bag.ItemGrid.items);beforeBelt=ItemStack.Clone(actor.inventory.ItemGrid.items);
            bagLocks=RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots);
            beltLocks=RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks);
        }
        if(!Plan(beforeBag,beforeBelt,bagLocks,beltLocks,actor.inventory.Length,returns,out var afterBag,out var afterBelt))
        {reason="xuiRebirthRecyclingOwnerFull";return false;}
        var address=new RebirthCamperStationAddress(RebirthCamperModuleRuntime.For(station.BoundVehicle).Identity,station.InstallationId);
        bool accepted=RebirthCamperStationNativeActorPayment.Begin(actor,station,address,RebirthCamperCharacterAccess.CurrentCreation(actor),operation,
            beforeBag,afterBag,beforeBelt,afterBelt,queue,identity,remote?bagLocks:null,input,cancellationGroup==Guid.Empty?operation:cancellationGroup,output,tools);
        if(accepted)reason=null;return accepted;
    }
    internal static bool TryDeliverPlaced(EntityPlayer actor,TileEntityWorkstation station,IList<ItemStack> returns,out string reason)
    {
        reason="xuiRebirthRecyclingOwnerUnavailable";
        if(returns.Count==0){reason=null;return true;}
        if(!(actor is EntityPlayerLocal local)||local.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true)return false;
        if(RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(actor.inventory)
            ||RebirthBackpackLibraryReservation.IsHeld(local)||RebirthGearOwnerReservation.IsHeld(local)
            ||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(local))return false;
        var beforeBag=ItemStack.Clone(actor.bag.ItemGrid.items);var beforeBelt=ItemStack.Clone(actor.inventory.ItemGrid.items);
        if(!Plan(beforeBag,beforeBelt,RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots),
            RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks),actor.inventory.Length,returns,out var bag,out var belt))
        {reason="xuiRebirthRecyclingOwnerFull";return false;}
        for(int i=0;i<bag.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(beforeBag[i],bag[i]))actor.bag.SetSlot(i,bag[i]);
        for(int i=0;i<belt.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(beforeBelt[i],belt[i]))actor.inventory.SetItem(i,belt[i].itemValue,belt[i].count);
        for(int i=0;i<bag.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actor.bag.ItemGrid.items[i],bag[i]))
            throw new InvalidOperationException("Recycler native owner return did not settle; preserve native station custody.");
        for(int i=0;i<belt.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actor.inventory.ItemGrid.items[i],belt[i]))
            throw new InvalidOperationException("Recycler native owner return did not settle; preserve native station custody.");
        RebirthRecyclerTransport.OwnerChanged(actor,station);
        reason=null;return true;
    }
}
