using System;
using System.Collections.Generic;

public static partial class RebirthNpcTradeService
{
    internal static HashSet<RebirthNpcStableId> CaptureNativeCustodyIds()
    {
        var ids=new HashSet<RebirthNpcStableId>();AppendQuarantineIds(ids);return ids;
    }
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||tradeWorld!=null&&!ReferenceEquals(tradeWorld,scope.World))return null;
            var original=Quotes;var order=QuoteOrder;var world=tradeWorld;string image=TradeImage().OuterXml;
            bool Current(){lock(Sync)return ReferenceEquals(Quotes,original)&&ReferenceEquals(QuoteOrder,order)&&ReferenceEquals(tradeWorld,world)&&TradeImage().OuterXml==image;}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(ReferenceEquals(Quotes,original)||ReferenceEquals(QuoteOrder,order))return false;
                    // Original quotations/payment owners remain in bundle
                    // custody. New quotations use a fresh native service scope;
                    // their NPCs are separately fenced until old release proof.
                    return TradeScope()&&ReferenceEquals(tradeWorld,incoming);
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

internal static partial class RebirthNpcSaveQuarantineBundle
{
    internal static bool BlocksRetainedTrade(RebirthNpcStableId npc)
    {
        if(!ThreadManager.IsMainThread())return true;
        foreach(var value in retained)
        {
            if(!value.Detached||ReferenceEquals(value.Original,GameManager.Instance?.World)||!SameDirectory(value.Directory,GameIO.GetSaveGameDir())||
                value.ServerTradeNpcIds?.Contains(npc)!=true)continue;
            if(value.RollbackIncomplete||value.ServerLocalTradeRetirement?.Complete!=true||value.ServerPreparationRetirement?.Complete!=true||
                value.ServerIssuedRetirement?.Complete!=true)return true;
        }
        return false;
    }
}
