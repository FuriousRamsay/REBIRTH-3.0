using System;
using System.Collections.Generic;

// Pure bounded scheduling. Dropping an obsolete transport job never drops its
// durable original; the domain retries the same retained operation after bind.
internal sealed class RebirthRemoteResourceRepairSendQueue
{
    private sealed class Job
    {
        internal string Key;internal RebirthRemoteResourceFragments.Body Body;internal int Next;
        internal Func<bool> Current,Ready;internal Action<int> Send;
        internal Action<bool> Completed;
    }
    private readonly List<Job> jobs=new List<Job>();private long bytes;private int offset;
    internal int Count=>jobs.Count;
    internal long RetainedBytes=>bytes;
    internal bool IsQueued(string key)=>jobs.Exists(job=>job.Key==key);
    internal bool Enqueue(string key,RebirthRemoteResourceFragments.Body body,Func<bool> current,Func<bool> ready,Action<int> send)
    {
        if(string.IsNullOrEmpty(key)||key.Length>2048||body==null||current==null||ready==null||send==null)return false;
        foreach(var existing in jobs)if(existing.Key==key)return existing.Body.Hash==body.Hash&&existing.Body.Length==body.Length;
        if(jobs.Count>=8||bytes+body.Length>64L*1024*1024)return false;
        jobs.Add(new Job{Key=key,Body=body,Current=current,Ready=ready,Send=send});bytes+=body.Length;return true;
    }
    // Observation only: true means every Send callback ran, never delivery,
    // admission, native acceptance or financial publication. Default callers
    // retain their existing dedup/scheduling semantics above.
    internal bool EnqueueObserved(string key,RebirthRemoteResourceFragments.Body body,Func<bool> current,Func<bool> ready,Action<int> send,Action<bool> completed)
    {
        if(completed==null||IsQueued(key)||!Enqueue(key,body,current,ready,send))return false;
        jobs[jobs.Count-1].Completed=completed;return true;
    }
    private static void Complete(Job job,bool sent)
    {var callback=job.Completed;job.Body=null;job.Current=null;job.Ready=null;job.Send=null;job.Completed=null;try{callback?.Invoke(sent);}catch{/* Original owner retains its durable unknown. */}}
    internal void Clear(){var old=jobs.ToArray();jobs.Clear();bytes=0;offset=0;foreach(var job in old)Complete(job,false);}
    // Resource-only cancellation of one caller-owned courier. It conveys no
    // delivery, admission, refusal or financial outcome to that caller.
    internal void Cancel(string key){for(int i=jobs.Count-1;i>=0;i--)if(jobs[i].Key==key)Remove(i);}
    private void Remove(int index,bool sent=false){var job=jobs[index];bytes-=job.Body.Length;jobs.RemoveAt(index);if(offset>=jobs.Count)offset=0;Complete(job,sent);}
    internal void Pump(int maximum)
    {
        if(maximum<1||maximum>16)throw new ArgumentOutOfRangeException(nameof(maximum));
        int blocked=0;
        while(jobs.Count>0&&maximum>0&&blocked<jobs.Count)
        {
            if(offset>=jobs.Count)offset=0;var job=jobs[offset];
            bool current;try{current=job.Current();}catch{current=false;}
            if(!current){Remove(offset);blocked=0;continue;}
            bool ready;try{ready=job.Ready();}catch{ready=false;}
            if(!ready){offset=(offset+1)%jobs.Count;blocked++;continue;}
            // Exceptions retain the same unsent index. The caller's journal
            // remains authority; this queue never assumes delivery or payment.
            try{job.Send(job.Next);}catch{offset=(offset+1)%jobs.Count;blocked++;continue;}
            job.Next++;maximum--;blocked=0;
            if(job.Next==job.Body.Count)Remove(offset,true);else offset=(offset+1)%jobs.Count;
        }
    }
}
