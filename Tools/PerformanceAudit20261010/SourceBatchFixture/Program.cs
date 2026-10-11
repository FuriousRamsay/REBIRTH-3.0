using System;using System.Collections.Generic;using System.Linq;using System.Diagnostics;
class Snapshot {readonly HashSet<string> sourceIds; public Snapshot(IEnumerable<string> ids){sourceIds=new(ids,StringComparer.Ordinal);} public bool ContainsSource(string id)=>!string.IsNullOrEmpty(id)&&sourceIds.Contains(id);
    internal bool ContainsAnySource(ICollection<string> stableIds)
    {
        if (stableIds == null || stableIds.Count == 0) return false;
        if (stableIds.Count < sourceIds.Count)
        {
            foreach (string id in stableIds)
                if (sourceIds.Contains(id)) return true;
            return false;
        }
        // Live-sync passes Dictionary.Keys, whose Contains is a hash lookup.
        foreach (string id in sourceIds)
            if (stableIds.Contains(id)) return true;
        return false;
    }
}
class Program {class CacheEntry {public Snapshot Snapshot;}static Dictionary<int,CacheEntry> entries=new();static long projectionRevision;
    public static int InvalidateSource(string stableId)
    {
        if (string.IsNullOrEmpty(stableId) || entries.Count == 0) return 0;
        List<int> remove = null;
        foreach (KeyValuePair<int, CacheEntry> pair in entries)
        {
            if (pair.Value == null || pair.Value.Snapshot == null || !pair.Value.Snapshot.ContainsSource(stableId)) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return 0;
        for (int i = 0; i < remove.Count; i++) entries.Remove(remove[i]);
        unchecked { projectionRevision++; }
        return remove.Count;
    }    internal static int InvalidateSources(ICollection<string> stableIds)
    {
        if (stableIds == null || stableIds.Count == 0 || entries.Count == 0) return 0;
        List<int> remove = null;
        foreach (KeyValuePair<int, CacheEntry> pair in entries)
        {
            if (pair.Value == null || pair.Value.Snapshot == null ||
                !pair.Value.Snapshot.ContainsAnySource(stableIds)) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return 0;
        for (int i = 0; i < remove.Count; i++) entries.Remove(remove[i]);
        // Consumers use this as an invalidation token, not an event count.
        unchecked { projectionRevision++; }
        return remove.Count;
    }
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
static Dictionary<int,CacheEntry> Copy(Dictionary<int,CacheEntry> data)=>new(data);
static void Main(){var r=new Random(371);for(int n=0;n<2000;n++){
var data=new Dictionary<int,CacheEntry>();for(int i=0;i<r.Next(0,100);i++)data[i]=i%13==0?null:new(){Snapshot=i%17==0?null:new Snapshot(Enumerable.Range(0,r.Next(0,60)).Select(_=>"s"+r.Next(100)))};
var dirty=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<r.Next(0,120);i++)dirty["s"+r.Next(150)]=1;
entries=Copy(data);int old=0;foreach(var id in dirty.Keys)old+=InvalidateSource(id);var expected=entries.Keys.Order().ToArray();
entries=Copy(data);projectionRevision=0;int current=InvalidateSources(dirty.Keys);
if(old!=current||!expected.SequenceEqual(entries.Keys.Order())||projectionRevision!=(current>0?1:0))throw new Exception("parity "+n);
}Check(true,"2000 randomized batches match original removals, retained keys and invalidation/no-change token");
entries=new(){[1]=new(){Snapshot=new Snapshot(new[]{"ABC"})}};Check(InvalidateSources(new Dictionary<string,int>{{"abc",1}}.Keys)==0,"ordinal source identity preserved");Check(InvalidateSources(null)==0&&InvalidateSources(Array.Empty<string>())==0,"null/empty batch retains cache");
var big=new Dictionary<int,CacheEntry>();for(int i=0;i<512;i++)big[i]=new(){Snapshot=new Snapshot(Enumerable.Range(0,64).Select(x=>"source"+x))};var changes=Enumerable.Range(0,4096).ToDictionary(x=>"missing"+x,x=>x,StringComparer.Ordinal);
for(int warm=0;warm<4;warm++){entries=Copy(big);foreach(var id in changes.Keys)InvalidateSource(id);entries=Copy(big);InvalidateSources(changes.Keys);}
foreach(bool batch in new[]{false,true}){entries=Copy(big);long a=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(int k=0;k<10;k++){if(batch)InvalidateSources(changes.Keys);else foreach(var id in changes.Keys)InvalidateSource(id);}sw.Stop();Console.WriteLine((batch?"batch":"original")+" no-overlap 512players/64sources/4096changes x10: ms="+sw.Elapsed.TotalMilliseconds+" bytes="+(GC.GetAllocatedBytesForCurrentThread()-a));}
Console.WriteLine("Scope: actual invalidation methods and source-ID intersection; doubled cache entry/snapshot container, .NET9, not Unity/FPS/network qualification.");}
}
