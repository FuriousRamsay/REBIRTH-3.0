using System;
using System.Linq;

// Issued-quote metadata only. Positive no-permission disposition cannot release
// an owner's native Before/hold, a prepared trade or any enclosing domain.
internal sealed class RebirthNpcTradeIssuedRetirement
{
    private readonly RebirthNpcTradeService.HostQuote[] quotes;private readonly bool[] completed;
    internal RebirthNpcTradeIssuedRetirement(RebirthNpcTradeService.HostQuote[] originals)
    {
        if(originals==null||originals.Length>512||originals.Any(value=>value==null))throw new InvalidOperationException("Exact retained issued quotes required");
        quotes=(RebirthNpcTradeService.HostQuote[])originals.Clone();completed=quotes.Select(value=>value.HasSavedUnusedState).ToArray();
    }
    internal bool Complete=>Array.TrueForAll(completed,value=>value);
    internal void Observe(RebirthNpcTradeService.HostUnused unused)
    {
        if(!ThreadManager.IsMainThread()||unused==null)return;
        for(int index=0;index<quotes.Length;index++)if(!completed[index]&&quotes[index].MatchesSavedUnused(unused))completed[index]=true;
    }
}
