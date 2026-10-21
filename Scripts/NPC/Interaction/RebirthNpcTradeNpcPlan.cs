using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Detached two-leg NPC plan for one owner-native exchange. No reservation,
// mutation, payment or saved receipt is issued by constructing this plan.
internal sealed class RebirthNpcTradeNpcPlan
{
    private readonly RebirthNpcInventorySnapshot before,after;
    private RebirthNpcTradeNpcPlan(RebirthNpcInventorySnapshot before,RebirthNpcInventorySnapshot after)
    {this.before=before;this.after=after;}
    internal static bool TryCreate(RebirthNpcTradeOriginal original,RebirthNpcInventorySnapshot snapshot,out RebirthNpcTradeNpcPlan plan)
    {
        plan=null;
        try
        {
            if(original==null||snapshot==null)return false;
            var quote=original.CopyQuote();
            if(snapshot.NpcId.ToString()!=quote.NpcId.ToString()||snapshot.Revision!=quote.ExpectedNpcInventoryRevision||snapshot.Revision==uint.MaxValue||
                !Valid(snapshot.Quantities)||!Valid(snapshot.Reservations)||snapshot.Reservations.Any(pair=>
                    !snapshot.Quantities.TryGetValue(pair.Key,out var count)||pair.Value>count)||
                snapshot.NativeStacks!=null&&(snapshot.NativeStacks.Owner!=snapshot.NpcId.ToString()||snapshot.NativeStacks.Revision!=snapshot.Revision||
                    !snapshot.NativeStacks.FitsQuantities(snapshot.Quantities)))return false;
            var frozen=Copy(snapshot);var proposed=Copy(snapshot);
            bool buy=quote.Direction==RebirthNpcTradeDirection.BuyFromNpc;
            string debit=buy?quote.ItemKey:quote.CurrencyItemKey,credit=buy?quote.CurrencyItemKey:quote.ItemKey;
            int debitCount=buy?quote.Quantity:quote.TotalPrice,creditCount=buy?quote.TotalPrice:quote.Quantity;
            if(string.Equals(debit,credit,StringComparison.OrdinalIgnoreCase))return false;
            proposed.Quantities.TryGetValue(debit,out int available);proposed.Reservations.TryGetValue(debit,out int reserved);
            long remaining=(long)available-debitCount;
            if(remaining<(long)reserved+(proposed.NativeStacks?.GetQuantity(debit)??0))return false;
            proposed.Quantities.TryGetValue(credit,out int priorCredit);long credited=(long)priorCredit+creditCount;
            if(credited>int.MaxValue)return false;
            if(remaining==0)proposed.Quantities.Remove(debit);else proposed.Quantities[debit]=(int)remaining;
            proposed.Quantities[credit]=(int)credited;proposed.Revision++;
            if(!Valid(proposed.Quantities))return false;
            if(proposed.NativeStacks!=null)proposed.NativeStacks=proposed.NativeStacks.WithRevision(proposed.Revision);
            plan=new RebirthNpcTradeNpcPlan(frozen,proposed);return true;
        }
        catch{return false;}
    }
    private static bool Valid(Dictionary<string,int> values)=>values!=null&&values.Count<=4096&&values.All(pair=>
        !string.IsNullOrWhiteSpace(pair.Key)&&pair.Key.Length<=512&&pair.Value>0);
    private static RebirthNpcInventorySnapshot Copy(RebirthNpcInventorySnapshot source)=>new RebirthNpcInventorySnapshot
    {
        NpcId=source.NpcId,Revision=source.Revision,
        Quantities=new Dictionary<string,int>(source.Quantities,StringComparer.OrdinalIgnoreCase),
        Reservations=new Dictionary<string,int>(source.Reservations,StringComparer.OrdinalIgnoreCase),
        NativeStacks=source.NativeStacks,
        LogisticsEffects=source.LogisticsEffects==null?null:new Dictionary<Guid,string>(source.LogisticsEffects)
    };
    internal RebirthNpcInventorySnapshot CopySnapshot(bool completed)=>Copy(completed?after:before);
    internal bool Matches(RebirthNpcInventorySnapshot actual,bool completed)
    {
        var expected=completed?after:before;
        return actual!=null&&actual.NpcId.ToString()==expected.NpcId.ToString()&&actual.Revision==expected.Revision&&
            Same(expected.Quantities,actual.Quantities)&&Same(expected.Reservations,actual.Reservations)&&
            XNode.DeepEquals(expected.NativeStacks?.Write(),actual.NativeStacks?.Write())&&
            (expected.LogisticsEffects==null?actual.LogisticsEffects==null:actual.LogisticsEffects!=null&&expected.LogisticsEffects.Count==actual.LogisticsEffects.Count&&
                expected.LogisticsEffects.All(pair=>actual.LogisticsEffects.TryGetValue(pair.Key,out var value)&&value==pair.Value));
    }
    private static bool Same(Dictionary<string,int> expected,Dictionary<string,int> actual)=>actual!=null&&expected.Count==actual.Count&&
        expected.All(pair=>actual.TryGetValue(pair.Key,out var value)&&value==pair.Value);
}
