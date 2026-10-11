using System;
using System.Threading.Tasks;
class Program
{
    static int n;static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);n++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        object owner=new object(),world=new object();Guid tx=Guid.NewGuid();bool live=true;
        var gate=new RebirthPoiRegionNativeAdmissionGate(owner,world,tx,new long[]{1,2},()=>live);
        Check(gate.IsWaiting&&!gate.TryEnterNative(owner,world,tx,new long[]{1,2}),"worker cannot enter before durable whole admission");
        Check(!gate.PublishReady(owner,world,tx,()=>false)&&gate.IsWaiting,"failed admission does not publish a worker grant");
        Check(!gate.PublishReady(new object(),world,tx,()=>true),"foreign owner cannot publish original grant");
        Check(gate.PublishReady(owner,world,tx,()=>true),"complete confirmed original batch publishes grant");
        Check(!gate.TryEnterNative(owner,world,tx,new long[]{1})&&!gate.TryEnterNative(owner,world,tx,new long[]{1,1}),"partial and aliased removal cannot consume grant");
        int entered=0;Parallel.For(0,32,i=>{if(gate.TryEnterNative(owner,world,tx,new long[]{2,1}))System.Threading.Interlocked.Increment(ref entered);});
        Check(entered==1&&gate.HasNativeStarted,"concurrent worker retries enter native exactly once");
        Check(!gate.NativeReturned(owner,world,Guid.NewGuid(),true,()=>true),"another transaction cannot complete original removal");
        Check(gate.NativeReturned(owner,world,tx,true,()=>true)&&gate.HasNativeReturned,"positive original deletion return captured");
        Check(!gate.TryEnterNative(owner,world,tx,new long[]{1,2}),"finished grant cannot replay native removal");
        var failed=new RebirthPoiRegionNativeAdmissionGate(owner,world,tx,new long[]{1},()=>live);failed.PublishReady(owner,world,tx,()=>true);failed.TryEnterNative(owner,world,tx,new long[]{1});
        Check(!failed.NativeReturned(owner,world,tx,false,()=>true)&&!failed.HasNativeReturned&&failed.HasNativeStarted,"suppressed original deletion cannot finish");
        var stale=new RebirthPoiRegionNativeAdmissionGate(owner,world,tx,new long[]{1},()=>live);stale.PublishReady(owner,world,tx,()=>true);live=false;
        Check(!stale.TryEnterNative(owner,world,tx,new long[]{1}),"world replacement revokes worker admission");
        live=true;stale.Revoke();Check(!stale.TryEnterNative(owner,world,tx,new long[]{1}),"explicit shutdown revocation cannot be reactivated");
        var admissionError=new RebirthPoiRegionNativeAdmissionGate(owner,world,tx,new long[]{1},()=>true);
        Check(!admissionError.PublishReady(owner,world,tx,()=>throw new InvalidOperationException("durable witness failed"))&&admissionError.IsWaiting,
            "durable admission exception cannot grant deletion or escape caller");
        Check(!admissionError.TryEnterNative(owner,world,tx,new long[]{1}),"exceptional admission retains the unconsumed original request");
        Check(admissionError.PublishReady(owner,world,tx,()=>true)&&admissionError.TryEnterNative(owner,world,tx,new long[]{1}),
            "later positive durable admission can grant the same unstarted original");
        Check(!admissionError.NativeReturned(owner,world,tx,true,()=>throw new InvalidOperationException("deletion witness failed"))&&
            admissionError.HasNativeStarted&&!admissionError.HasNativeReturned,"deletion witness exception retains original uncertainty without completion");
        Check(!admissionError.TryEnterNative(owner,world,tx,new long[]{1}),"uncertain returned native deletion cannot be replayed after witness exception");
        var race=new RebirthPoiRegionNativeAdmissionGate(owner,world,tx,new long[]{1},()=>true);
        race.PublishReady(owner,world,tx,()=>true);race.TryEnterNative(owner,world,tx,new long[]{1});
        using(var checking=new System.Threading.ManualResetEventSlim())
        using(var release=new System.Threading.ManualResetEventSlim())
        {
            var late=Task.Run(()=>race.NativeReturned(owner,world,tx,true,()=>{checking.Set();if(!release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();return false;}));
            Check(checking.Wait(TimeSpan.FromSeconds(5))&&race.NativeReturned(owner,world,tx,true,()=>true),
                "one positive concurrent original witness can publish native return");
            release.Set();Check(late.Wait(TimeSpan.FromSeconds(5))&&!late.Result&&race.HasNativeReturned,
                "late failed witness cannot overwrite an already confirmed original native return");
        }
        Console.WriteLine("RESULT "+n+" PASS; production admission gate with actual concurrent worker attempts; durable publisher and native effects doubled; not a native route claim.");
    }
}