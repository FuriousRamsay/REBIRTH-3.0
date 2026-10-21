using System;
using System.Collections.Generic;

public static partial class RebirthNpcTradeService
{
    private static long nextClosedSocialTick;
    private static void TickClosedSocial()
    {
        if(!ThreadManager.IsMainThread()||!TradeScope(false))return;
        long now=DateTime.UtcNow.Ticks;
        if(now<nextClosedSocialTick)return;
        nextClosedSocialTick=DateTime.UtcNow.AddSeconds(1).Ticks;
        var pending=new List<HostClosed>(4);
        lock(Sync)
        {
            // Rotate the existing bounded quotation index. Neither a full
            // per-frame scan nor a disconnected owner is needed for progress.
            int visits=Math.Min(QuoteOrder.Count,32);
            for(int index=0;index<visits&&pending.Count<4;index++)
            {
                if(!QuoteOrder.TryTake(out var operation))break;
                QuoteOrder.Return(operation);
                if(Quotes.TryGetValue(operation,out var record)&&record?.OwnerPreparation?.ClosedRecord is HostClosed closed&&
                    !closed.HasObservedSavedSocialCompletion)pending.Add(closed);
            }
        }
        // This producer independently rechecks its canonical closed chain and
        // current authoritative atomic social backing. No owner/NPC leg runs.
        foreach(var closed in pending)closed.TrySaveSocialCompletion();
    }
}
