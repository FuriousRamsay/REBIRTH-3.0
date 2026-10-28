// NOT RUN. Actual offer DTO and registry are injected by parent offline runner.
using System;
using System.Collections.Generic;
using System.Linq;
static class RebirthTradeOfferRegistryFixture
{
    // PRODUCTION_TRADE_REGISTRY
    static RebirthNpcTradeOffer Offer(string profile,string item,int buy,int sell)=>new RebirthNpcTradeOffer
    {ProfileId=profile,ItemKey=item,CurrencyItemKey="coin",BuyUnitPrice=buy,SellUnitPrice=sell,MaximumQuantity=20};
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        RebirthNpcTradeOfferRegistry.Reset();
        var global=Offer("*","wood",4,2);var specific=Offer("ranger","WOOD",8,3);
        var onlyGlobal=Offer("*","stone",6,1);var foreign=Offer("other","iron",10,5);
        RebirthNpcTradeOfferRegistry.Register(global);RebirthNpcTradeOfferRegistry.Register(specific);
        RebirthNpcTradeOfferRegistry.Register(onlyGlobal);RebirthNpcTradeOfferRegistry.Register(foreign);
        Need(RebirthNpcTradeOfferRegistry.TryResolve(" ranger "," wood ",out var found)&&ReferenceEquals(found,specific)&&found.BuyUnitPrice==8&&found.SellUnitPrice==3,"Exact override must quote both directions");
        Need(RebirthNpcTradeOfferRegistry.TryResolve("visitor","wood",out found)&&ReferenceEquals(found,global),"Wildcard display cannot quote");
        var displayed=RebirthNpcTradeOfferRegistry.GetForProfile(" RANGER ");
        Need(displayed.Length==2&&displayed.Select(x=>x.ItemKey).Distinct(StringComparer.OrdinalIgnoreCase).Count()==2,"Duplicate/global/foreign catalogue entry");
        foreach(var offer in displayed)
            Need(RebirthNpcTradeOfferRegistry.TryResolve("ranger",offer.ItemKey,out found)&&ReferenceEquals(found,offer),"Display/quote disagreement");
        Need(displayed[0].ItemKey.Equals("stone",StringComparison.OrdinalIgnoreCase),"Catalogue order changed");
        Need(!RebirthNpcTradeOfferRegistry.TryResolve("ranger","iron",out _)&&!RebirthNpcTradeOfferRegistry.TryResolve(" ","wood",out _),"Invalid/foreign profile exposed offer");
        Need(RebirthNpcTradeOfferRegistry.GetForProfile(null).Length==0,"Missing profile exposed catalogue");
        RebirthNpcTradeOfferRegistry.Reset();
    }
}
