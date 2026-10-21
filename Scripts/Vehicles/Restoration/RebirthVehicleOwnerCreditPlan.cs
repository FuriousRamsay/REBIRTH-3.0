using System;

// Detached full-owner delivery: preserve native bag-first then owned-belt
// fallback, full item metadata and total credit without publishing a partial fit.
internal static class RebirthVehicleOwnerCreditPlan
{
    internal static RebirthVehicleInventoryPlanResult Prepare(ItemStack[] beforeBag,ItemStack[] beforeBelt,int owned,RebirthVehicleTransferItem operation,out ItemStack[] bag,out ItemStack[] belt,Func<int,bool> bagLocked=null,Func<int,bool> beltLocked=null)
    {
        bag=belt=null;
        if(beforeBag==null||beforeBelt==null||owned<0||owned>beforeBelt.Length||operation==null||operation.Debit)return RebirthVehicleInventoryPlanResult.Pending;
        if(!RebirthNativeItemCodec.TryDecode(operation.ItemData,out var value)||value?.ItemClass==null)return RebirthVehicleInventoryPlanResult.Rejected;
        int maximum=Math.Min(ushort.MaxValue,value.ItemClass.MaxCount);if(maximum<1)return RebirthVehicleInventoryPlanResult.Rejected;
        var incoming=new ItemStack(value,operation.Count);int remaining=operation.Count;
        var nextBag=RebirthVehicleInventoryWrite.Snapshot(beforeBag);var nextBelt=RebirthVehicleInventoryWrite.Snapshot(beforeBelt);
        void Fill(ItemStack[] slots,int length,XUiC_ItemStack.StackLocationTypes location,Func<int,bool> locked)
        {
            if(!incoming.CanMoveTo(location))return;
            for(int index=0;index<length&&remaining>0;index++)
            {
                var slot=slots[index];if(locked!=null&&locked(index)||slot.IsEmpty()||RebirthNativeItemCodec.Encode(slot.itemValue)!=operation.ItemData||!slot.CanStackPartlyWith(incoming,out int available))continue;
                int added=Math.Min(remaining,Math.Min(available,Math.Max(0,maximum-slot.count)));slot.count+=added;remaining-=added;
            }
            for(int index=0;index<length&&remaining>0;index++)
            {if(locked!=null&&locked(index)||!slots[index].IsEmpty())continue;int added=Math.Min(remaining,maximum);slots[index]=new ItemStack(value.Clone(),added);remaining-=added;}
        }
        Fill(nextBag,nextBag.Length,XUiC_ItemStack.StackLocationTypes.Backpack,bagLocked);Fill(nextBelt,owned,XUiC_ItemStack.StackLocationTypes.ToolBelt,beltLocked);
        if(remaining!=0)return RebirthVehicleInventoryPlanResult.Pending;
        bag=nextBag;belt=nextBelt;return RebirthVehicleInventoryPlanResult.Ready;
    }
}
