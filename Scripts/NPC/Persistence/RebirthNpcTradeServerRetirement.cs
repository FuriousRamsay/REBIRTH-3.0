using System;
using System.Linq;

// Correlates actual completed host sources with the frozen preparations in a
// detached server bundle. No native action, restore or World admission API.
internal sealed class RebirthNpcTradeServerRetirement
{
    private readonly RebirthNpcTradeOriginal[] originals;private readonly bool[] completed;
    internal RebirthNpcTradeServerRetirement(RebirthNpcTradeOriginal[] originals)
    {
        if(originals==null||originals.Length>512||originals.Any(value=>value==null))throw new InvalidOperationException("Complete retained server trade originals required");
        this.originals=(RebirthNpcTradeOriginal[])originals.Clone();completed=new bool[originals.Length];
    }
    internal bool Complete=>Array.TrueForAll(completed,value=>value);
    internal bool MatchesNativeReentry(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)
        =>source!=null&&source.Current()&&originals.Any(source.Matches);
    internal void ObserveCapturedCompletion(RebirthNpcTradeService.HostClosed source)
    {
        if(source?.HasObservedSavedSocialCompletion!=true)return;
        for(int index=0;index<originals.Length;index++)
            if(!completed[index]&&source.Prepared.Original.Image==originals[index].Image)completed[index]=true;
    }
    internal void Observe(RebirthNpcTradeService.HostClosed source)
    {
        if(source?.HasSavedSocialCompletion!=true)return;
        for(int index=0;index<originals.Length;index++)
            if(!completed[index]&&source.Prepared.Original.Image==originals[index].Image)completed[index]=true;
    }
    internal void Observe(RebirthNpcTradeClosedHistory source)
    {
        if(source?.HasSavedSocialCompletion!=true)return;
        for(int index=0;index<originals.Length;index++)if(!completed[index]&&source.MatchesOriginal(originals[index]))completed[index]=true;
    }
}
