using System;

internal static class RebirthVehicleOwnerCreditApply
{
    private static int recoveryCreditTraceBudget;
    private static void Trace(RebirthVehicleTransferPlan plan,string stage)
    {if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryCreditTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] credit id="+plan.TransactionId+" "+stage);}
    internal static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem item,Func<bool> current)
    {
        try
        {
            bool scope=current!=null&&current(), custody=RebirthVehicleOwnerCustody.Matches(player,plan);
            bool bagEnter=RebirthVehicleOwnerCustody.NativeCanEnter(player,true),beltEnter=RebirthVehicleOwnerCustody.NativeCanEnter(player,false);
            if(item==null||item.Debit||!scope||!custody||!bagEnter||!beltEnter)
            {Trace(plan,"entry scope="+scope+" custody="+custody+" bag="+bagEnter+" belt="+beltEnter);return RebirthVehicleOwnerTransferResult.Pending;}
            string marker="rbVehicle_"+item.ReceiptId.ToString("N");float stage=player.Buffs.GetCustomVar(marker);
            if(stage==1f)return RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,new[]{item},out var completedRetry)&&!completedRetry?
                RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
            if(stage==2f)
            {
                if(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,new[]{item},out var retry))return RebirthVehicleOwnerTransferResult.Indeterminate;
                if(!retry)return RebirthVehicleOwnerTransferResult.Applied;
            }
            else if(stage!=0f)return RebirthVehicleOwnerTransferResult.Indeterminate;
            var beforeBag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items);var beforeBelt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items);
            var result=RebirthVehicleOwnerCreditPlan.Prepare(beforeBag,beforeBelt,RebirthToolbeltCapacity.GetOwnedSlotCount(player,beforeBelt.Length),item,out var bag,out var belt,
                index=>RebirthVehicleNativePolicy.IsLocked(player,true,index),index=>RebirthVehicleNativePolicy.IsLocked(player,false,index));
            if(result!=RebirthVehicleInventoryPlanResult.Ready)
            {Trace(plan,"plan="+result+" count="+item.Count);return RebirthVehicleOwnerTransferResult.Pending;}
            var expectedBag=RebirthVehicleInventoryWrite.Snapshot(bag);var expectedBelt=RebirthVehicleInventoryWrite.Snapshot(belt);
            if(!current()||!RebirthVehicleOwnerOriginal.PrepareAll(player,plan,new[]{item},bag,belt))
            {Trace(plan,"original prepare refused");return RebirthVehicleOwnerTransferResult.Pending;}
            player.Buffs.SetCustomVar(marker,2f,true);
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{item.ReceiptId},2f,current)||!current()||
                !RebirthVehicleInventoryWrite.SameSlots(beforeBag,player.bag.ItemGrid.items)||!RebirthVehicleInventoryWrite.SameSlots(beforeBelt,player.inventory.ItemGrid.items))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!RebirthVehicleInventoryWrite.SameSlots(beforeBag,bag))RebirthVehicleOwnerCustody.WriteSlots(player,true,bag,new[]{item},current);
            if(!current())return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!RebirthVehicleInventoryWrite.SameSlots(beforeBelt,belt))RebirthVehicleOwnerCustody.WriteSlots(player,false,belt,new[]{item},current);
            if(!current()||!RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag.ItemGrid.items)||!RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory.ItemGrid.items))return RebirthVehicleOwnerTransferResult.Indeterminate;
            player.Buffs.SetCustomVar(marker,1f,true);
            return RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory.ItemGrid.items)&&
                RebirthVehicleOwnerCheckpoint.Persist(player,new[]{item.ReceiptId},1f,current)?RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        }
        catch(Exception error){Trace(plan,"threw "+error.GetType().Name+": "+error.Message);return RebirthVehicleOwnerTransferResult.Indeterminate;}
    }
}
