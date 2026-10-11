using System;using System.Collections.Generic;using System.Linq;
class Request {public bool Kind;}
class RebirthNpcCommandRecord {public Request Request=new Request();}
static class RebirthNpcWorkReleaseGate {public static bool Hold(bool held)=>held;}
class Before{public static Queue<ulong>[] PendingByPriority; public static Dictionary<ulong,RebirthNpcCommandRecord> Records; public static int pendingCount;    public static bool TryDequeueNextLocked(out ulong id)
    {
        for(int priority=PendingByPriority.Length-1;priority>=0;priority--)
        {
            var queue=PendingByPriority[priority];int count=queue.Count; int eligible=-1,index=0;
            foreach(ulong token in queue){RebirthNpcCommandRecord held;if(!Records.TryGetValue(token,out held)||!RebirthNpcWorkReleaseGate.Hold(held.Request.Kind)){eligible=index;break;}index++;}
            if(eligible<0)continue;
            id=0;for(int i=0;i<count;i++)
            {
                ulong candidate=queue.Dequeue();if(i==eligible)id=candidate;else queue.Enqueue(candidate);
            }
            if(pendingCount>0)pendingCount--;return true;
        }
        id=0;return false;
    }

}
class After{public static Queue<ulong>[] PendingByPriority; public static Dictionary<ulong,RebirthNpcCommandRecord> Records; public static int pendingCount;    public static bool TryDequeueNextLocked(out ulong id)
    {
        for(int priority=PendingByPriority.Length-1;priority>=0;priority--)
        {
            var queue=PendingByPriority[priority];int count=queue.Count; int eligible=-1,index=0;
            foreach(ulong token in queue){RebirthNpcCommandRecord held;if(!Records.TryGetValue(token,out held)||!RebirthNpcWorkReleaseGate.Hold(held.Request.Kind)){eligible=index;break;}index++;}
            if(eligible<0)continue;
            if(eligible==0)
            {
                id=queue.Dequeue();
                if(pendingCount>0)pendingCount--;
                return true;
            }
            id=0;for(int i=0;i<count;i++)
            {
                ulong candidate=queue.Dequeue();if(i==eligible)id=candidate;else queue.Enqueue(candidate);
            }
            if(pendingCount>0)pendingCount--;return true;
        }
        id=0;return false;
    }

}
class Program{
 static void Main(){var random=new Random(731);for(int run=0;run<2000;run++){
 var queues=Enumerable.Range(0,4).Select(_=>new Queue<ulong>()).ToArray();var records=new Dictionary<ulong,RebirthNpcCommandRecord>();
 for(ulong id=1;id<100;id++){queues[random.Next(4)].Enqueue(id);if(random.Next(5)!=0)records[id]=new RebirthNpcCommandRecord{Request=new Request{Kind=random.Next(3)==0}};}
 Before.PendingByPriority=queues.Select(q=>new Queue<ulong>(q)).ToArray();After.PendingByPriority=queues.Select(q=>new Queue<ulong>(q)).ToArray();Before.Records=After.Records=records;Before.pendingCount=After.pendingCount=random.Next(110);
 for(int step=0;step<110;step++){var a=Before.TryDequeueNextLocked(out var x);var b=After.TryDequeueNextLocked(out var y);if(a!=b||x!=y||Before.pendingCount!=After.pendingCount||Enumerable.Range(0,4).Any(i=>!Before.PendingByPriority[i].SequenceEqual(After.PendingByPriority[i])))throw new Exception("Differential mismatch "+run+":"+step);if(!a)break;}
 }Console.WriteLine("PASS 2000 seeded queue populations: priorities, held prefixes, stale IDs, remaining order, and pending counts identical. Actual extracted methods; policy doubles; no Unity timing claim.");}
}