using System;
using System.Collections.Generic;

// Correlation only. These predicates retain the exact owning originals; only a
// synchronous actual native saved release can satisfy them. No replay/effect API.
internal sealed class RebirthNpcTradeClientRetirement
{
    private readonly Func<RebirthNpcTradeOwnerExchange.Release,bool>[] originals;
    private readonly bool[] released;
    internal RebirthNpcTradeClientRetirement(params Func<RebirthNpcTradeOwnerExchange.Release,bool>[][] owners)
    {
        var values=new List<Func<RebirthNpcTradeOwnerExchange.Release,bool>>();
        foreach(var owner in owners){if(owner==null)throw new InvalidOperationException("Trade original owner missing");values.AddRange(owner);}
        if(values.Count>129||values.Exists(value=>value==null))throw new InvalidOperationException("Trade original custody bound");
        originals=values.ToArray();released=new bool[originals.Length];
    }
    internal bool Complete=>Array.TrueForAll(released,value=>value);
    internal void Observe(RebirthNpcTradeOwnerExchange.Release release)
    {
        if(release==null||!release.Current())return;
        for(int index=0;index<originals.Length;index++)if(!released[index]&&originals[index](release))released[index]=true;
    }
}
