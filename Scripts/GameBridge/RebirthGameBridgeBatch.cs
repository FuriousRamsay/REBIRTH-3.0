using System;
using Newtonsoft.Json.Linq;

// The lease and queue share an admission lock. Guard/reflexes remain active.
public static partial class RebirthGameBridge
{
    private static readonly object batchGate = new object();
    private static string batchToken;
    private static string batchRun;
    private static bool batchUncertain;

    internal static JObject BatchStatus()
    {
        lock (batchGate) return new JObject {
            ["exclusive"] = batchToken != null, ["run"] = batchRun,
            ["uncertain"] = batchUncertain, ["queued"] = Pending.Count
        };
    }

    private static void HandleBatch(BridgeRequest req)
    {
        lock (batchGate)
        {
            if (req.Method == "GET") { req.Complete(BatchStatus()); return; }
            if (req.Method != "POST") { req.Fail("Use GET or POST", 405); return; }
            string action = req.QueryString("action");
            if (action == "acquire")
            {
                string run = req.QueryString("run");
                if (string.IsNullOrWhiteSpace(run) || run.Length > 120) { req.Fail("run identifier required (max 120 characters)"); return; }
                if (batchToken != null || batchUncertain || !Pending.IsEmpty || BridgeRequest.OpenCount > 1)
                { req.Fail("Bridge busy, leased, or has an uncertain timed-out action", 409); return; }
                batchRun = run;
                batchToken = Guid.NewGuid().ToString("N");
                req.Complete(new JObject { ["run"] = run, ["runToken"] = batchToken });
                return;
            }
            if (action == "hold")
            {
                if (batchToken == null || req.RunToken != batchToken) { req.Fail("Batch owner required", 409); return; }
                batchUncertain = true;
                req.Complete(new JObject { ["retained"] = true });
                return;
            }
            if (action == "release")
            {
                if (batchToken == null || req.RunToken != batchToken) { req.Fail("Batch owner required", 409); return; }
                if (batchUncertain || !Pending.IsEmpty || BridgeRequest.OpenCount > 1)
                { req.Fail("Outstanding/uncertain action: ownership retained; inspect before release", 409); return; }
                batchToken = null;
                batchRun = null;
                req.Complete(new JObject { ["released"] = true });
                return;
            }
            req.Fail("Expected action=acquire, hold, or release");
        }
    }

    private static void AdmitBatchRequest(BridgeRequest req)
    {
        lock (batchGate)
        {
            if ((batchToken != null && req.RunToken != batchToken) ||
                (batchToken == null && !string.IsNullOrEmpty(req.RunToken)))
            { req.Fail("Exclusive batch owns this bridge; competing/stale script rejected", 409); return; }
            Pending.Enqueue(req);
        }
    }

    private static void BatchTimedOut()
    {
        lock (batchGate) batchUncertain = true;
    }
}
