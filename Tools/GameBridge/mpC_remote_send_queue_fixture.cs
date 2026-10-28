using System;

// Deferred pure scheduling checks. Link ACTUAL production Fragments.cs and
// RepairSendQueue.cs; native pressure/current callbacks are explicit doubles.
internal static class RemoteSendQueueChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    private static void Main()
    {
        var queue=new RebirthRemoteResourceRepairSendQueue();var body=new RebirthRemoteResourceFragments.Body(new byte[9000*20]);int sent=0;bool ready=false,current=true;
        Require(queue.Enqueue("original",body,()=>current,()=>ready,index=>{Require(index==sent,"exact unsent index");sent++;}),"admit original");
        Require(queue.Enqueue("original",body,()=>false,()=>true,index=>{throw new Exception("replacement callback");}),"same original reuses retained job");
        queue.Pump(16);Require(sent==0&&queue.Count==1,"native backpressure retains original without queue mutation");
        ready=true;queue.Pump(16);Require(sent==16&&queue.Count==1,"per-tick sixteen fragments maximum");
        queue.Pump(16);Require(sent==20&&queue.Count==0&&queue.RetainedBytes==0,"release only transient body after all queued parts");
        int attempts=0;
        Require(queue.Enqueue("throw",body,()=>true,()=>true,index=>{Require(index==0,"failed send never advances");attempts++;throw new InvalidOperationException();}),"failed transport job");
        queue.Pump(16);queue.Pump(16);Require(attempts==2&&queue.Count==1,"one failed attempt per pump for blocked sole job");
        queue.Clear();
        var tiny=new RebirthRemoteResourceFragments.Body(new byte[]{1});
        for(int index=0;index<8;index++)Require(queue.Enqueue("small"+index,tiny,()=>true,()=>false,part=>{}),"bounded job admission");
        Require(!queue.Enqueue("ninth",tiny,()=>true,()=>true,part=>{}),"eight job cap");queue.Clear();
        var large=new RebirthRemoteResourceFragments.Body(new byte[16*1024*1024]);
        for(int index=0;index<4;index++)Require(queue.Enqueue("large"+index,large,()=>current,()=>false,part=>{}),"exact body budget");
        Require(!queue.Enqueue("overflow",tiny,()=>true,()=>true,part=>{}),"sixty-four MiB cap");
        current=false;queue.Pump(16);Require(queue.Count==0&&queue.RetainedBytes==0,"obsolete transport jobs retire without any source/owner claim API");
        Console.WriteLine("PASS actual pure send scheduler; native connections/authority require separate MP scenarios.");
    }
}
