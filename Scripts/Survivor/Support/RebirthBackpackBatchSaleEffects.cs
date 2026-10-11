using System;
using System.Collections.Generic;

internal static class RebirthBackpackBatchSaleEffects
{
    // Invoked only when the durable journal first advances to BackpackCommitted.
    // Retries that only finish/acknowledge that phase must not repeat native stock
    // additions or progression. The immutable before/after images supply the goods.
    internal static void Apply(EntityPlayer player,RebirthBackpackLibraryReceipt receipt)
    {
        if(!receipt.IsBatchSale||!receipt.TryGetImages(out var before,out var after,out _,out _)||
            !RebirthBackpackSellStashContents.TryRead(before,out var oldCells)||
            !RebirthBackpackSellStashContents.TryRead(after,out var newCells))return;
        var trader=player.world.GetEntity(receipt.BatchTraderId) as EntityTrader;
        ItemStack training=null;int trainingValue=0;
        for(int i=0;i<oldCells.Length;i++)
        {
            int count=oldCells[i].count-newCells[i].count;if(count<=0)continue;
            var sold=oldCells[i].Clone();sold.count=count;
            if(trader?.TraderData!=null)trader.TraderData.AddToPrimaryInventory(sold.Clone(),true);
            int value=RebirthStashBatchPrice.Price(player,trader?.TraderData,sold.itemValue,count);
            // Normal trading's global award interval also limits a whole batch.
            // Choose the highest-valued line rather than depending on slot order.
            if(value>trainingValue){training=sold;trainingValue=value;}
        }
        if(training!=null)RebirthSkillWaveAService.ReportCommittedStashSale(player,training,trainingValue);
        var local=player as EntityPlayerLocal;
        local?.PlayerUI?.xui?.Trader?.TraderWindowGroup?.RefreshTraderItems();
    }
}
