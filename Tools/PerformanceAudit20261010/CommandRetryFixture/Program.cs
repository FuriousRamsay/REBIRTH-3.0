using System;using System.Collections.Generic;using System.Linq;
enum RebirthNpcCommandStatus{RetryScheduled,Other}
class Request{public bool Kind;public int Priority;}
class RebirthNpcCommandRecord{public Request Request=new Request();public RebirthNpcCommandStatus Status;public long NextAttemptUtcTicks;}
static class RebirthNpcWorkReleaseGate{public static bool Hold(bool value)=>value;}
class Before{public static SortedDictionary<long,Queue<ulong>> RetryByDue;public static Dictionary<ulong,RebirthNpcCommandRecord> Records;public static Queue<ulong>[] PendingByPriority;public static int pendingCount; public static void MoveDueRetriesLocked(long nowTicks)
    {
        var dueKeys=new List<long>();foreach(var pair in RetryByDue)if(pair.Key<=nowTicks)dueKeys.Add(pair.Key);
        foreach(long due in dueKeys)
        {
            var ids=RetryByDue[due];var retained=new Queue<ulong>();
            while(ids.Count>0)
            {
                ulong id=ids.Dequeue();RebirthNpcCommandRecord record;
                if(Records.TryGetValue(id,out record)&&RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)){retained.Enqueue(id);continue;}
                if(record==null||record.Status!=RebirthNpcCommandStatus.RetryScheduled||record.NextAttemptUtcTicks!=due){if(pendingCount>0)pendingCount--;continue;}
                PendingByPriority[(int)record.Request.Priority].Enqueue(id);
            }
            if(retained.Count==0)RetryByDue.Remove(due);else RetryByDue[due]=retained;
        }
    }

}
class After{public static SortedDictionary<long,Queue<ulong>> RetryByDue;public static Dictionary<ulong,RebirthNpcCommandRecord> Records;public static Queue<ulong>[] PendingByPriority;public static int pendingCount; public static void MoveDueRetriesLocked(long nowTicks)
    {
        List<long> dueKeys = null;
        foreach (var pair in RetryByDue)
        {
            // SortedDictionary is ordered by deadline: later buckets cannot be due.
            if (pair.Key > nowTicks) break;
            if (dueKeys == null) dueKeys = new List<long>();
            dueKeys.Add(pair.Key);
        }
        if (dueKeys == null) return;
        foreach(long due in dueKeys)
        {
            var ids=RetryByDue[due];var retained=new Queue<ulong>();
            while(ids.Count>0)
            {
                ulong id=ids.Dequeue();RebirthNpcCommandRecord record;
                if(Records.TryGetValue(id,out record)&&RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)){retained.Enqueue(id);continue;}
                if(record==null||record.Status!=RebirthNpcCommandStatus.RetryScheduled||record.NextAttemptUtcTicks!=due){if(pendingCount>0)pendingCount--;continue;}
                PendingByPriority[(int)record.Request.Priority].Enqueue(id);
            }
            if(retained.Count==0)RetryByDue.Remove(due);else RetryByDue[due]=retained;
        }
    }

}
class Program{
 static SortedDictionary<long,Queue<ulong>> Copy(SortedDictionary<long,Queue<ulong>> src)=>new(src.ToDictionary(p=>p.Key,p=>new Queue<ulong>(p.Value)));
 static void Main(){var rng=new Random(904);for(int run=0;run<2000;run++){
 var src=new SortedDictionary<long,Queue<ulong>>();var records=new Dictionary<ulong,RebirthNpcCommandRecord>();
 for(ulong id=1;id<150;id++){long due=rng.Next(30);if(!src.TryGetValue(due,out var q))src[due]=q=new();q.Enqueue(id);if(rng.Next(5)!=0)records[id]=new(){Request=new(){Kind=rng.Next(4)==0,Priority=rng.Next(4)},Status=rng.Next(4)==0?RebirthNpcCommandStatus.Other:RebirthNpcCommandStatus.RetryScheduled,NextAttemptUtcTicks=rng.Next(4)==0?due+1:due};}
 Before.RetryByDue=Copy(src);After.RetryByDue=Copy(src);Before.Records=After.Records=records;Before.PendingByPriority=Enumerable.Range(0,4).Select(_=>new Queue<ulong>()).ToArray();After.PendingByPriority=Enumerable.Range(0,4).Select(_=>new Queue<ulong>()).ToArray();Before.pendingCount=After.pendingCount=rng.Next(200);
 foreach(long now in new long[]{-1,0,10,29,30,100}){Before.MoveDueRetriesLocked(now);After.MoveDueRetriesLocked(now);if(Before.pendingCount!=After.pendingCount||!Before.RetryByDue.Keys.SequenceEqual(After.RetryByDue.Keys)||Before.RetryByDue.Any(p=>!p.Value.SequenceEqual(After.RetryByDue[p.Key]))||Enumerable.Range(0,4).Any(i=>!Before.PendingByPriority[i].SequenceEqual(After.PendingByPriority[i])))throw new Exception("Mismatch "+run+":"+now);}
 }Console.WriteLine("PASS 2000 seeded retry populations at six deadlines: held/stale/future records, priorities, counts and order identical; extracted production methods with policy doubles.");}
}