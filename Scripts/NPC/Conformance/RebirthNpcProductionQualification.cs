using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcPerformanceSample
{
    public long Sequence; public long UtcTicks; public int RuntimeCount, SocialMemories, AppliedEvents, InteractionReplay, TradeQuotes, Conversations;
    public long ManagedBytes; public double AuditMilliseconds;
}

public static class RebirthNpcPerformanceQualificationService
{
    private const int MaxSamples = 256;
    private static readonly object Sync = new object();
    private static readonly Queue<RebirthNpcPerformanceSample> Samples = new Queue<RebirthNpcPerformanceSample>();
    private static long sequence, samples, budgetFailures;

    public static RebirthNpcPerformanceSample Capture()
    {
        Stopwatch watch = Stopwatch.StartNew();
        RebirthNpcSocialSnapshot social = RebirthNpcSocialService.GetSnapshot();
        string router = RebirthNpcInteractionCommandRouter.GetReport();
        string trade = RebirthNpcTradeService.GetReport();
        string dialogue = RebirthNpcDialogueService.GetReport();
        int runtimeCount = RebirthNpcRuntimeRegistry.GetSnapshot().Length;
        watch.Stop();
        RebirthNpcPerformanceSample sample = new RebirthNpcPerformanceSample
        {
            Sequence = Interlocked.Increment(ref sequence), UtcTicks = DateTime.UtcNow.Ticks,
            RuntimeCount = runtimeCount, SocialMemories = social.MemoryCount, AppliedEvents = social.AppliedEventCount,
            InteractionReplay = Parse(router, "replay="), TradeQuotes = Parse(trade, "activeQuotes="),
            Conversations = Parse(dialogue, "conversations="), ManagedBytes = GC.GetTotalMemory(false), AuditMilliseconds = watch.Elapsed.TotalMilliseconds
        };
        if (sample.AppliedEvents > social.MaxAppliedEvents || sample.AuditMilliseconds > 50d) Interlocked.Increment(ref budgetFailures);
        lock (Sync) { Samples.Enqueue(sample); while (Samples.Count > MaxSamples) Samples.Dequeue(); }
        Interlocked.Increment(ref samples); return sample;
    }

    public static string GetReport()
    {
        RebirthNpcPerformanceSample last = null; int count;
        lock (Sync) { count=Samples.Count; foreach (RebirthNpcPerformanceSample s in Samples) last=s; }
        return "[REBIRTH NPC Performance] samples="+Interlocked.Read(ref samples)+" retained="+count+"/"+MaxSamples+" budgetFailures="+Interlocked.Read(ref budgetFailures)+
            (last==null?string.Empty:" lastRuntime="+last.RuntimeCount+" socialMemories="+last.SocialMemories+" appliedEvents="+last.AppliedEvents+" replay="+last.InteractionReplay+" quotes="+last.TradeQuotes+" conversations="+last.Conversations+" managedBytes="+last.ManagedBytes+" auditMs="+last.AuditMilliseconds.ToString("0.000"));
    }
    public static void ResetForWorldChange() { lock (Sync) Samples.Clear(); }
    private static int Parse(string text, string key) { int i=text.IndexOf(key,StringComparison.OrdinalIgnoreCase); if(i<0)return 0; i+=key.Length; int e=i; while(e<text.Length&&char.IsDigit(text[e]))e++; int v; return int.TryParse(text.Substring(i,e-i),out v)?v:0; }
}

public static class RebirthNpcReleaseQualificationService
{
    private static int runs,passes,failures;
    public static string RunStaticQualification()
    {
        Interlocked.Increment(ref runs);
        List<string> failed=new List<string>();StringBuilder b=new StringBuilder("[REBIRTH NPC Release Qualification]\n");
        RebirthNpcLifecycleSnapshot lifecycle=RebirthNpcLifecycle.GetSnapshot();RebirthNpcSocialSnapshot social=RebirthNpcSocialService.GetSnapshot();
        Check(b,failed,"lifecycleRegistered",lifecycle.State!=RebirthNpcLifecycleState.Cold,lifecycle.State.ToString());
        Check(b,failed,"interactionPackages",Contains(lifecycle.RegisteredPackages,"NetPackageRebirthNpcInteractionRequest")&&Contains(lifecycle.RegisteredPackages,"NetPackageRebirthNpcInteractionResponse"),"request/response");
        Check(b,failed,"interactionServices",Contains(lifecycle.RegisteredServices,"RebirthNpcInteractionPresentationService")&&Contains(lifecycle.RegisteredServices,"RebirthNpcPlayerInventoryEndpointService"),"presentation/player-endpoint");
        Check(b,failed,"socialReplayBound",social.AppliedEventCount<=social.MaxAppliedEvents,social.AppliedEventCount+"/"+social.MaxAppliedEvents);
        Check(b,failed,"socialPersistence",Contains(lifecycle.RegisteredServices,"RebirthNpcSocialPersistenceStore"),"registered");
        Check(b,failed,"tradeRegistry",RebirthNpcTradeOfferRegistry.Count>0,"offers="+RebirthNpcTradeOfferRegistry.Count);
        string activation=RebirthNpcDirectActivationBinding.GetReport();Check(b,failed,"directActivation",Parse(activation,"candidates=")>0,activation);
        RebirthNpcPerformanceSample sample=RebirthNpcPerformanceQualificationService.Capture();Check(b,failed,"qualificationAuditBudget",sample.AuditMilliseconds<=50d,sample.AuditMilliseconds.ToString("0.000")+"ms");
        b.Append("trade=").Append(RebirthNpcTradeService.GetReport()).Append('\n');b.Append("dialogue=").Append(RebirthNpcDialogueService.GetReport()).Append('\n');b.Append(RebirthNpcFactionGameplayService.GetReport()).Append('\n');b.Append(RebirthNpcSocialGameplayEventProducers.GetReport()).Append('\n');b.Append(RebirthNpcPerformanceQualificationService.GetReport()).Append('\n');
        bool pass=failed.Count==0;if(pass)Interlocked.Increment(ref passes);else Interlocked.Increment(ref failures);b.Append("structuralQualification=").Append(pass?"PASS":"FAIL").Append(" failed=").Append(failed.Count);if(failed.Count>0)b.Append(" [").Append(string.Join(",",failed.ToArray())).Append(']');
        b.Append("\nruntimeAcceptance=NotRun runtimeEvidenceRequired=exact-b259-compile,bootstrap,xui,host-client,dedicated,save-load,stress,soak,performance");return b.ToString();
    }
    private static void Check(StringBuilder b,List<string> failed,string gate,bool ok,string detail){b.Append(gate).Append('=').Append(ok?"PASS":"FAIL").Append(" (").Append(detail??string.Empty).Append(")\n");if(!ok)failed.Add(gate);}
    private static bool Contains(string[] values,string text){if(values==null)return false;for(int i=0;i<values.Length;i++)if(values[i]!=null&&values[i].IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
    private static int Parse(string text,string key){int i=text.IndexOf(key,StringComparison.OrdinalIgnoreCase);if(i<0)return 0;i+=key.Length;int e=i;while(e<text.Length&&char.IsDigit(text[e]))e++;int v;return int.TryParse(text.Substring(i,e-i),out v)?v:0;}
    public static string GetCounters(){return "runs="+Interlocked.CompareExchange(ref runs,0,0)+" passes="+Interlocked.CompareExchange(ref passes,0,0)+" failures="+Interlocked.CompareExchange(ref failures,0,0);}
}
