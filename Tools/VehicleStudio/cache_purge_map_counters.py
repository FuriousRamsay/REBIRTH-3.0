from pathlib import Path
p=Path('Scripts/Purge/Runtime/RebirthPurgeScanner.cs');s=p.read_text();anchor='    private static World owner;'
s=s.replace(anchor,'''    private sealed class Counter { internal long Epoch; internal int Areas,Entities; internal bool Approximate; }
    private static readonly Dictionary<string,Counter> counters=new Dictionary<string,Counter>(StringComparer.Ordinal);
    internal static long CounterRevision {get;private set;}
    internal static bool TryCounters(RebirthPoiClearanceRecord record,out int areas,out int entities,out bool approximate)
    {
        areas=entities=-1;approximate=true;Counter value;
        if(!ReferenceEquals(owner,GameManager.Instance?.World)||!counters.TryGetValue(record.Identity.Key,out value)||value.Epoch!=record.Epoch)return false;
        areas=value.Areas;entities=value.Entities;approximate=value.Approximate;return true;
    }
''' +anchor)
s=s.replace('ClearMarkers();projectedActors.Clear();current=null;', 'ClearMarkers();projectedActors.Clear();counters.Clear();CounterRevision++;current=null;')
needle='        RoomHistory history;'
s=s.replace(needle,'''        Counter prior;
        if(!counters.TryGetValue(identity.Key,out prior)||prior.Epoch!=record.Epoch||prior.Areas!=remaining||prior.Entities!=result.Enemies||prior.Approximate!=result.Approximate)
        {
            if(counters.Count>=RebirthPoiClearanceLedger.MaximumRecords)counters.Clear();
            counters[identity.Key]=new Counter{Epoch=record.Epoch,Areas=remaining,Entities=result.Enemies,Approximate=result.Approximate};CounterRevision++;
        }
''' +needle);p.write_text(s)
p=Path('Scripts/Purge/Network/RebirthPoiMapSync.cs');s=p.read_text();s=s.replace('private static long observedHistory=-1,originalHistory;', 'private static long observedHistory=-1,originalHistory;\n    private static long observedCounters=-1,originalCounters;')
s=s.replace('observedHistory=-1;RebirthPurgePoiHistory.Reset();', 'observedHistory=-1;observedCounters=-1;RebirthPurgePoiHistory.Reset();')
s=s.replace('observedHistory!=RebirthPurgePoiHistory.Revision)', 'observedHistory!=RebirthPurgePoiHistory.Revision||observedCounters!=RebirthPurgeScanner.CounterRevision)')
s=s.replace('originalHistory=RebirthPurgePoiHistory.Revision;', 'originalHistory=RebirthPurgePoiHistory.Revision;originalCounters=RebirthPurgeScanner.CounterRevision;')
s=s.replace('observedHistory=originalHistory;', 'observedHistory=originalHistory;observedCounters=originalCounters;')
s=s.replace('if(record.State==RebirthPoiClearanceState.ResetPending)return;', 'if(record.State==RebirthPoiClearanceState.ResetPending)return;\n        if(RebirthPurgeScanner.TryCounters(record,out areas,out entities,out approximate))return;')
p.write_text(s)
