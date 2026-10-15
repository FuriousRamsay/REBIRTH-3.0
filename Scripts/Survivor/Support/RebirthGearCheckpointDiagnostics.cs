using System;
using System.Collections.Generic;

// Bounded failure-stage evidence only. Never changes receipts, inventory or holds.
internal static class RebirthGearCheckpointDiagnostics
{
    private static readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> order=new Queue<string>();
    internal static void Report(RebirthGearTransferState offer,string stage)
        => ReportStage(offer?.TransactionId,stage);
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void ReportStage(string transactionId,string stage)
    {
        try
        {
        string transaction=Guid.TryParseExact(transactionId,"N",out var id)?id.ToString("N"):"unbound";
        string key=transaction+":"+stage;
        if(!seen.Add(key))return;
        order.Enqueue(key);
        while(order.Count>128)seen.Remove(order.Dequeue());
        Log.Out("[GearTerminal] transaction="+transaction+" refused="+stage);
        }
        catch { } // Diagnostics must never prevent custody continuation.
    }
}
