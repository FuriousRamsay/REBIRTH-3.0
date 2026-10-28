using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json.Linq { public sealed class JObject : Dictionary<string,object> {} }

public sealed class BridgeRequest
{
    public string Method = "POST", Path = "/batch", RunToken;
    public static int OpenCount = 1;
    public int Status;
    public JObject Reply;
    public Dictionary<string,string> Query = new Dictionary<string,string>();
    public string QueryString(string key) { string value; return Query.TryGetValue(key,out value)?value:null; }
    public void Complete(JObject value) { Status=200; Reply=value; }
    public void Fail(string message,int status=400) { Status=status; Reply=new JObject { ["error"]=message }; }
}
public static partial class RebirthGameBridge
{
    internal static readonly ConcurrentQueue<BridgeRequest> Pending = new ConcurrentQueue<BridgeRequest>();
    private static int checks;
    private static void Check(bool condition,string name) { if(!condition)throw new Exception(name); checks++; }
    private static BridgeRequest Lease(string action,string owner=null)
    {
        var req=new BridgeRequest { RunToken=owner }; req.Query["action"]=action; req.Query["run"]="fixture";
        HandleBatch(req); return req;
    }
    private static void ClearQueue() { BridgeRequest r; while(Pending.TryDequeue(out r)){} }
    public static void Main()
    {
        var first=Lease("acquire"); Check(first.Status==200,"acquire idle");
        string owner=(string)first.Reply["runToken"];
        Check(Lease("acquire").Status==409,"overlap denied");
        Check(Lease("release","wrong").Status==409,"foreign release denied");
        var foreign=new BridgeRequest(); AdmitBatchRequest(foreign);
        Check(foreign.Status==409&&Pending.IsEmpty,"foreign direct HTTP client cannot enqueue");
        var own=new BridgeRequest {RunToken=owner}; AdmitBatchRequest(own);
        Check(Pending.Count==1,"owner admitted");
        Check(Lease("release",owner).Status==409,"queued release denied"); ClearQueue();
        BridgeRequest.OpenCount=2; Check(Lease("release",owner).Status==409,"active release denied");
        BridgeRequest.OpenCount=1; Check(Lease("release",owner).Status==200,"idle owner release");
        var stale=new BridgeRequest {RunToken=owner}; AdmitBatchRequest(stale); Check(stale.Status==409,"old owner denied after release");
        BridgeRequest.OpenCount=2; Check(Lease("acquire").Status==409,"running action prevents acquisition");
        BridgeRequest.OpenCount=1;
        // Many clients racing to acquire: actual admission lock chooses exactly one.
        var attempts=new ConcurrentBag<BridgeRequest>(); Parallel.For(0,64,i=>attempts.Add(Lease("acquire")));
        Check(attempts.Count(r=>r.Status==200)==1,"one race winner");
        owner=(string)attempts.Single(r=>r.Status==200).Reply["runToken"];
        Parallel.For(0,64,i=> {var r=new BridgeRequest(); AdmitBatchRequest(r); if(r.Status!=409)throw new Exception("foreign race admitted");});
        Check(Pending.IsEmpty,"all foreign racing clients refused");
        BatchTimedOut(); Check((bool)BatchStatus()["uncertain"],"timeout uncertainty latched");
        Check(Lease("release",owner).Status==409,"timeout does not silently release late action");
        Check(Lease("acquire").Status==409,"no timeout takeover");
        Console.WriteLine("{\"passed\":true,\"actualLeaseChecks\":"+checks+"}");
    }
}
