#if DEBUG
using System;

// Detached actual pruning policy only. No quote is inserted, no financial leg,
// resolution publisher, payment receipt or native inventory authority is invoked.
public static partial class RebirthNpcTradeService
{
    internal static void VerifyDetachedQuoteRetention()
    {
        long now=10;var record=new QuoteRecord{Quote=new RebirthNpcTradeQuote{ExpiresClockTicks=1},Consumed=true};
        Require(!CanPruneQuote(record,now)); // unknown consumed, even expired
        record.Resolution=1;Require(CanPruneQuote(record,now));
        record.Resolution=2;Require(CanPruneQuote(record,now));
        record.Resolution=3;Require(CanPruneQuote(record,now));
        record.Resolution=0;record.Consumed=false;Require(CanPruneQuote(record,now));
        record.Quote.ExpiresClockTicks=20;Require(!CanPruneQuote(record,now));
        record.Consumed=true;Require(!CanPruneQuote(record,now));
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Detached trade metadata retention policy failed");}
}
#endif
