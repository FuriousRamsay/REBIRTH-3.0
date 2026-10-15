using System;
using System.Collections.Generic;
using System.Diagnostics;

// Detached presentation index. Finish a captured immutable publication before starting
// the next one, so frequent progress changes cannot starve a large world's list.
internal sealed class RebirthPurgeStatisticsPoiIndex
{
    private sealed class Entry
    {
        internal RebirthPoiMapRecord Record;
        internal string Biome,Name;
    }
    private sealed class Order:IComparer<Entry>
    {
        private readonly StringComparer names;
        internal Order(StringComparer names){this.names=names;}
        public int Compare(Entry left,Entry right)
        {
            int result=names.Compare(left.Biome,right.Biome);if(result!=0)return result;
            result=left.Record.Tier.CompareTo(right.Record.Tier);if(result!=0)return result;
            result=names.Compare(left.Name,right.Name);return result!=0?result:StringComparer.Ordinal.Compare(left.Record.Identity.Key,right.Record.Identity.Key);
        }
    }
    private Guid world,session;
    private IReadOnlyDictionary<string,RebirthPoiMapRecord> buildingSource,publishedSource;
    private long buildingRevision,publishedRevision,buildingWords,publishedWords;
    private IEnumerator<RebirthPoiMapRecord> input;
    private SortedSet<Entry> sorted;
    private IEnumerator<Entry> output;
    private RebirthPoiMapRecord[] pending;
    private int cursor;
    internal RebirthPoiMapRecord[] Published {get;private set;}=Array.Empty<RebirthPoiMapRecord>();
    internal long Generation {get;private set;}
    internal bool Known {get;private set;}
    internal void Reset()
    {
        input?.Dispose();output?.Dispose();input=null;output=null;sorted=null;pending=null;cursor=0;
        buildingSource=publishedSource=null;Known=false;Published=Array.Empty<RebirthPoiMapRecord>();world=session=Guid.Empty;
        Generation++;
    }
    internal void Pulse(IReadOnlyDictionary<string,RebirthPoiMapRecord> source,Guid currentWorld,Guid currentSession,long revision,long words,Func<string,string> biomeName,Func<string,string> poiName)
    {
        if(currentWorld!=world||currentSession!=session){Reset();world=currentWorld;session=currentSession;}
        if(source==null||currentWorld==Guid.Empty||revision<1)
        {if(Known||input!=null||output!=null)Reset();return;}
        if(input==null&&output==null)
        {
            if(ReferenceEquals(source,publishedSource)&&revision==publishedRevision&&words==publishedWords)return;
            buildingSource=source;buildingRevision=revision;buildingWords=words;
            sorted=new SortedSet<Entry>(new Order(StringComparer.CurrentCultureIgnoreCase));input=source.Values.GetEnumerator();
        }
        long deadline=Stopwatch.GetTimestamp()+Math.Max(1,(long)(Stopwatch.Frequency*.002));
        int examined=0;
        while(examined++<512&&Stopwatch.GetTimestamp()<deadline)
        {
            if(input!=null)
            {
                if(input.MoveNext())
                {var record=input.Current;sorted.Add(new Entry{Record=record,Biome=biomeName(record.Identity.Biome),Name=poiName(record.Identity.Prefab)});continue;}
                input.Dispose();input=null;pending=new RebirthPoiMapRecord[sorted.Count];cursor=0;output=sorted.GetEnumerator();
            }
            if(output.MoveNext()){pending[cursor++]=output.Current.Record;continue;}
            output.Dispose();output=null;sorted=null;Published=pending;pending=null;
            publishedSource=buildingSource;buildingSource=null;publishedRevision=buildingRevision;publishedWords=buildingWords;
            Known=true;Generation++;return;
        }
    }
}
