using System.Text.Json;

// Production journal/index, disposable directory, no game-save access.
string root=Path.Combine(Path.GetTempPath(),"rebirth-journal-audit-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var results=new List<object>();
try
{
    foreach(string kind in new[]{"unknown-checksum","known-checksum","staging-valid","staging-partial","missing-prepared","unknown-name"})
    {
        string dir=Path.Combine(root,kind);
        var journal=new RebirthCamperTransferJournal(dir);
        Guid vehicle=Guid.NewGuid(),other=Guid.NewGuid(),op=Guid.NewGuid();
        journal.Prepare(op,vehicle,"owner",0,new byte[]{1},new byte[]{2});
        string prepared=Path.Combine(dir,op.ToString("N")+".0.rcj");
        byte[] valid=File.ReadAllBytes(prepared);
        string extra=null;
        if(kind=="known-checksum"||kind=="missing-prepared")journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(kind.EndsWith("checksum")){var bad=(byte[])valid.Clone();bad[0]^=1;File.WriteAllBytes(prepared,bad);}
        else if(kind=="missing-prepared")File.Delete(prepared);
        else {extra=kind=="unknown-name"?Path.Combine(dir,"unknown.rcj"):prepared+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(extra,kind=="staging-partial"?new byte[]{1}:valid);}
        DateTime now=DateTime.UtcNow;
        using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        bool global=kind=="unknown-checksum";
        string unrelated=Refusal(()=>index.FindPending(other)); Require((unrelated!=null)==global,kind+" unrelated scope: "+unrelated);
        Require(Refusal(()=>index.FindPending(vehicle))!=null,kind+" affected held");
        Require(Refusal(()=>journal.ReadForIndex().ToArray())!=null,kind+" strict consumers unchanged");
        if(extra!=null)File.Delete(extra);else File.WriteAllBytes(prepared,valid);
        Require(Refusal(()=>index.FindPending(vehicle))!=null,kind+" bounded retry");
        now=now.AddSeconds(31);
        Require(index.FindPending(other)==null,kind+" other recovered");
        Require(index.FindPending(vehicle)?.Operation==op,kind+" same index recovers pending");
        results.Add(new{kind,unrelatedBlocked=global,sameIndexRecovered=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"deleted-unknown"));Guid op=Guid.NewGuid(),v=Guid.NewGuid(),other=Guid.NewGuid();journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2});
        string path=Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj");byte[] good=File.ReadAllBytes(path),bad=(byte[])good.Clone();bad[0]^=1;File.WriteAllBytes(path,bad);
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        Require(Refusal(()=>index.FindPending(other))!=null,"unknown initial fence");File.Delete(path);now=now.AddSeconds(31);
        Require(Refusal(()=>index.FindPending(other))!=null,"deleting unknown evidence cannot unblock");File.WriteAllBytes(path,good);now=now.AddSeconds(31);
        Require(index.FindPending(other)==null&&index.FindPending(v)?.Operation==op,"restored unknown same index");results.Add(new{kind="deleted-unknown",deletionDoesNotSettle=true});
    }
    foreach(string phase in new[]{"pending","applied","rejected"})
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"loaded-"+phase));
        Guid vehicle=Guid.NewGuid(),other=Guid.NewGuid(),op=Guid.NewGuid();
        journal.Prepare(op,vehicle,"owner",0,new byte[]{1},new byte[]{2});
        if(phase=="applied") {journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);journal.Advance(op,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);}
        if(phase=="rejected")journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
        DateTime now=DateTime.UtcNow;
        using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        Require(index.FindPending(other)==null,"loaded bootstrap");
        string path=Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj");
        byte[] valid=File.ReadAllBytes(path),bad=(byte[])valid.Clone();bad[0]^=1;File.WriteAllBytes(path,bad);
        Action query=()=>{if(phase=="pending")index.FindPending(vehicle);else if(phase=="applied")index.FindApplied(vehicle,"owner").ToArray();else index.FindRejected(vehicle,"owner").ToArray();};
        Require(Refusal(query)!=null,"loaded affected held");
        Require(index.FindPending(other)==null,"loaded other clear");
        // A retry with still-corrupt content keeps the validated mapping local.
        now=now.AddSeconds(31);Require(Refusal(query)!=null,"known mapping retry held");Require(index.FindPending(other)==null,"known mapping retry unrelated clear");
        File.WriteAllBytes(path,valid);now=now.AddSeconds(31);Require(Refusal(query)==null,"loaded same index repair");
        Guid otherOp=Guid.NewGuid();journal.Prepare(otherOp,other,"other",0,new byte[]{3},new byte[]{4});
        Require(index.FindPending(other)?.Operation==otherOp,"new publication indexed");
        journal.Advance(otherOp,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
        Require(index.FindPending(other)==null&&index.FindRejected(other,"other").Single().Operation==otherOp,"new terminal indexed");
        results.Add(new{kind="loaded-"+phase,unrelatedClear=true,repair=true,publication=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"removed-known"));
        Guid v=Guid.NewGuid(),other=Guid.NewGuid(),op=Guid.NewGuid();journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2});
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        Require(index.FindPending(v)!=null,"known receipt indexed");string path=Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj");byte[] valid=File.ReadAllBytes(path);File.Delete(path);
        Require(Refusal(()=>index.FindPending(v))!=null,"missing known held");now=now.AddSeconds(31);
        Require(index.FindPending(other)==null,"missing known other clear after revalidation");Require(Refusal(()=>index.FindPending(v))!=null,"deletion not settlement");
        File.WriteAllBytes(path,valid);now=now.AddSeconds(31);Require(index.FindPending(v)!=null,"restored known receipt");results.Add(new{kind="removed-known",deletionDoesNotSettle=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"publication-failure"));
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);Guid v=Guid.NewGuid(),other=Guid.NewGuid(),op=Guid.NewGuid();Require(index.FindPending(other)==null,"initial empty");
        string destination=Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj");Directory.CreateDirectory(destination);
        Require(Refusal(()=>journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2}))!=null,"publish refused");
        Require(index.FindPending(other)==null,"publish failure other clear");Require(Refusal(()=>index.FindPending(v))!=null,"publish failure affected held");
        Directory.Delete(destination);now=now.AddSeconds(31);Require(Refusal(()=>index.FindPending(v))!=null,"staging not published");Require(index.FindPending(other)==null,"staging other clear");
        foreach(string tmp in Directory.GetFiles(journal.DirectoryPath,"*.tmp"))File.Delete(tmp);
        now=now.AddSeconds(31);Require(Refusal(()=>index.FindPending(v))!=null,"deleted candidate not settlement");
        journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2});now=now.AddSeconds(31);Require(index.FindPending(v)?.Operation==op,"successful retry publication");
        results.Add(new{kind="publication-failure",unrelatedClear=true,stagingNotReplayed=true});
    }
    {
        var first=new RebirthCamperTransferJournal(Path.Combine(root,"conflict"));var second=new RebirthCamperTransferJournal(Path.Combine(root,"conflict-source"));Guid op=Guid.NewGuid(),a=Guid.NewGuid(),b=Guid.NewGuid();
        first.Prepare(op,a,"owner",0,new byte[]{1},new byte[]{2});second.Prepare(op,b,"owner",0,new byte[]{1},new byte[]{2});second.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        string name=op.ToString("N")+".1.rcj";File.Copy(Path.Combine(second.DirectoryPath,name),Path.Combine(first.DirectoryPath,name));using var index=new RebirthCamperTransferPendingIndex(first);
        Require(Refusal(()=>index.FindPending(Guid.NewGuid()))!=null,"conflicting identities globally held");results.Add(new{kind="conflicting-valid-identities",globalHold=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"locked-history"));Guid op=Guid.NewGuid(),v=Guid.NewGuid(),other=Guid.NewGuid();journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2});journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        using(var locked=new FileStream(Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {Require(index.FindPending(other)==null,"read lock unrelated clear");Require(Refusal(()=>index.FindPending(v))!=null,"read lock affected held");}
        now=now.AddSeconds(31);Require(index.FindPending(v)?.Operation==op,"read lock retry");results.Add(new{kind="locked-history",unrelatedClear=true,recovered=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"partial-bootstrap"));Guid healthy=Guid.NewGuid(),badVehicle=Guid.NewGuid(),goodOp=Guid.NewGuid(),badOp=Guid.NewGuid();journal.Prepare(goodOp,healthy,"owner",0,new byte[]{1},new byte[]{2});journal.Prepare(badOp,badVehicle,"owner",0,new byte[]{3},new byte[]{4});
        string path=Path.Combine(journal.DirectoryPath,badOp.ToString("N")+".0.rcj");byte[] good=File.ReadAllBytes(path),bad=(byte[])good.Clone();bad[0]^=1;File.WriteAllBytes(path,bad);
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        Require(Refusal(()=>index.FindPending(healthy))!=null,"partial bootstrap never admitted");Require(Refusal(()=>index.FindPending(healthy))!=null,"partial remains unavailable during backoff");
        File.WriteAllBytes(path,good);now=now.AddSeconds(31);Require(index.FindPending(healthy)?.Operation==goodOp,"partial repair healthy pending");Require(index.FindPending(badVehicle)?.Operation==badOp,"partial repair affected pending");
        // Healthy no-pending queries must not periodically rescan all history.
        File.WriteAllBytes(path,bad);now=now.AddHours(1);Require(index.FindPending(Guid.NewGuid())==null,"healthy loaded query does not rescan");results.Add(new{kind="partial-bootstrap",noPartialAdmission=true,noHealthyPeriodicScan=true});
    }
    {
        var journal=new RebirthCamperTransferJournal(Path.Combine(root,"malformed-name-removed"));Guid op=Guid.NewGuid(),v=Guid.NewGuid(),other=Guid.NewGuid();journal.Prepare(op,v,"owner",0,new byte[]{1},new byte[]{2});
        byte[] valid=File.ReadAllBytes(Path.Combine(journal.DirectoryPath,op.ToString("N")+".0.rcj"));string path=Path.Combine(journal.DirectoryPath,"unattributed.rcj.tmp");File.WriteAllBytes(path,new byte[]{1});
        DateTime now=DateTime.UtcNow;using var index=new RebirthCamperTransferPendingIndex(journal,()=>now);
        Require(Refusal(()=>index.FindPending(other))!=null,"malformed unknown held");File.Delete(path);now=now.AddSeconds(31);Require(Refusal(()=>index.FindPending(other))!=null,"malformed removal remains held");
        File.WriteAllBytes(path,valid);now=now.AddSeconds(31);Require(index.FindPending(other)==null,"restored malformed bytes attribute vehicle");Require(Refusal(()=>index.FindPending(v))!=null,"noncanonical file still holds vehicle");
        File.Delete(path);now=now.AddSeconds(31);Require(index.FindPending(v)?.Operation==op,"canonical repaired history readmitted");results.Add(new{kind="malformed-name-removed",deletionDoesNotSettle=true,validatedRepairRecovers=true});
    }
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,actualProductionClasses=true,cases=results.Count,results},new JsonSerializerOptions{WriteIndented=true}));
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
finally
{
    if(Path.GetDirectoryName(Path.GetFullPath(root))!=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(root).StartsWith("rebirth-journal-audit-",StringComparison.Ordinal)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new Exception("Unsafe cleanup path");
    Directory.Delete(root,true);
}
static string Refusal(Action action){try{action();return null;}catch(Exception ex){return ex.GetType().Name+": "+ex.Message;}}
static void Require(bool condition,string test){if(!condition)throw new Exception("FAIL: "+test);}
internal static class RebirthCamperTransferHistoryScope
{
    internal static bool TryGet(RebirthCamperTransferJournal journal,out RebirthCamperTransferPendingIndex index){index=null;return false;}
}
