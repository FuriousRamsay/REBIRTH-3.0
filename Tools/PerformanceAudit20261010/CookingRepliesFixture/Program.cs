using System; using System.Collections.Generic;
class World {} class GameManager { public static GameManager Instance=new(); public World World=new(); }
static class Time {public static float realtimeSinceStartup;}
static class Log {public static void Warning(string message) { warnings++; } public static int warnings;}
class Program {
class Reply {public Action<string> Callback; public float Expires; public World World;}
static Dictionary<long,Reply> replies=new(); static Queue<KeyValuePair<long,string>> received=new();static object ReplySync=new();
    public static void PumpReplies()
    {
        KeyValuePair<long,string>[] results;
        lock (ReplySync) { results = received.Count == 0 ? null : received.ToArray(); received.Clear(); }
        if (results != null) foreach (var result in results)
        {
            Reply reply;
            if (!replies.TryGetValue(result.Key, out reply)) continue;
            replies.Remove(result.Key);
            if (!ReferenceEquals(reply.World, GameManager.Instance != null ? GameManager.Instance.World : null)) continue;
            try { reply.Callback(result.Value ?? string.Empty); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] response callback failed: " + ex.Message); }
        }
        if (replies.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        List<KeyValuePair<long, Reply>> expired = null;
        foreach (var pair in replies)
            if (now >= pair.Value.Expires)
            {
                if (expired == null) expired = new List<KeyValuePair<long, Reply>>();
                expired.Add(pair);
            }
        if (expired == null) return;
        foreach (var entry in expired)
        {
            // A previous callback can reset the session or replace a pending request.
            Reply reply;
            if (!replies.TryGetValue(entry.Key, out reply) || !ReferenceEquals(reply, entry.Value)) continue;
            replies.Remove(entry.Key);
            try { reply.Callback("Cooking request timed out; authoritative state was not assumed to change."); }
            catch (Exception ex) { Log.Warning("[REBIRTH Cooking] timeout callback failed: " + ex.Message); }
        }
    }

static void Add(long id,float deadline,Action<string> callback) {replies[id]=new Reply{Callback=callback,Expires=deadline,World=GameManager.Instance.World};}
static void Check(bool condition,string name) {if(!condition)throw new Exception(name);Console.WriteLine("PASS: "+name);}
static void Reset(){replies.Clear();received.Clear();Time.realtimeSinceStartup=10;Log.warnings=0;}
static void Main(){
Reset();int calls=0;Add(1,11,_=>calls++);PumpReplies();Check(calls==0&&replies.Count==1,"pending deadline retained");Time.realtimeSinceStartup=11;PumpReplies();Check(calls==1&&replies.Count==0,"exact deadline fires once");PumpReplies();Check(calls==1,"no repeated timeout");
Reset();calls=0;Add(1,0,_=>{calls++;replies.Clear();});Add(2,0,_=>calls++);PumpReplies();Check(calls==1&&replies.Count==0,"callback session reset skips removed entries");
Reset();calls=0;Add(1,0,_=>Add(2,0,_=>calls+=100));Add(2,0,_=>calls++);PumpReplies();Check(calls==0&&replies.Count==1,"replacement identity survives old expiry batch");PumpReplies();Check(calls==100,"replacement expires on following pump");
Reset();calls=0;Add(1,0,_=>throw new Exception("test"));Add(2,0,_=>calls++);PumpReplies();Check(Log.warnings==1&&calls==1&&replies.Count==0,"throwing callback does not block later expiry");
Reset();calls=0;Add(1,0,s=>{if(s=="reply")calls++;});received.Enqueue(new(1,"reply"));PumpReplies();Check(calls==1&&replies.Count==0,"received response precedes timeout");
Reset();calls=0;Add(1,11,_=>calls++);replies[1].World=new World();received.Enqueue(new(1,"reply"));PumpReplies();Check(calls==0&&replies.Count==0,"stale world response discarded");
Reset();Add(1,20,_=>{});for(int i=0;i<100;i++)PumpReplies();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)PumpReplies();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"10000 unexpired pumps allocate zero fixture bytes");
Console.WriteLine("Scope: actual production PumpReplies with clock/world/log doubles; not network or Unity validation.");
}}
