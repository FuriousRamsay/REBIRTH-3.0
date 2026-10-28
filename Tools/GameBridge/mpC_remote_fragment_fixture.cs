using System;
using System.Linq;

// Deferred fixture: compile with the ACTUAL production
// RebirthRemoteResourceFragments.cs. No networking/authority simulation is
// claimed. In particular, assembly completion is never a payment witness.
internal static class RemoteOriginalFragmentChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    private static void Main()
    {
        var bytes=Enumerable.Range(0,2097152+19001).Select(n=>(byte)(n*17)).ToArray();
        var body=new RebirthRemoteResourceFragments.Body(bytes);var op=Guid.NewGuid();byte[] complete=null;
        bytes[0]^=1;
        RebirthRemoteResourceFragments.ClearTransient();
        Require(body.Count>1,"body larger than installed native frame is split");
        for(int n=body.Count-1;n>=0;n--)
        {
            Require(body.Part(n).Length<=12000,"bounded individual native package fragment");
            Require(RebirthRemoteResourceFragments.TryAdd("authenticated-owner/request",op,body.Hash,body.Length,body.Count,n,body.Part(n),1d,out complete),"out-of-order accepted");
            if(n!=0)Require(complete==null,"partial body cannot reach domain handler");
        }
        Require(complete!=null&&complete.Length==body.Length&&complete[0]!=bytes[0],"detached original body and full hash");
        RebirthRemoteResourceFragments.ClearTransient();
        Require(RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,0,body.Part(0),2d,out complete)&&complete==null,"first part");
        Require(RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,0,body.Part(0),2d,out complete)&&complete==null,"duplicate is idempotent");
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",op,new string('a',64),body.Length,body.Count,1,body.Part(1),2d,out _),"conflicting header cannot replace retained assembly");
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,-1,body.Part(0),2d,out _),"negative index");
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count+1,0,body.Part(0),2d,out _),"incorrect count");
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,0,body.Part(0)+"\n",2d,out _),"noncanonical base64");
        for(int n=1;n<body.Count;n++)Require(RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,n,body.Part(n),3d,out complete),"retained header survives conflict");
        Require(complete!=null,"correct retained body completes");
        RebirthRemoteResourceFragments.ClearTransient();
        for(int n=0;n<8;n++)Require(RebirthRemoteResourceFragments.TryAdd("owner",Guid.NewGuid(),body.Hash,body.Length,body.Count,0,body.Part(0),4d,out _),"bounded concurrent original");
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",Guid.NewGuid(),body.Hash,body.Length,body.Count,0,body.Part(0),4d,out _),"assembly admission bound");
        Require(RebirthRemoteResourceFragments.TryAdd("owner",op,body.Hash,body.Length,body.Count,0,body.Part(0),35d,out _),"transient expiry permits retry without durable claim effects");
        RebirthRemoteResourceFragments.ClearTransient();
        var tiny=new RebirthRemoteResourceFragments.Body(new byte[]{1,2,3});
        Require(!RebirthRemoteResourceFragments.TryAdd("owner",op,new string('b',64),3,1,0,tiny.Part(0),1d,out complete)&&complete==null,"whole hash mismatch never publishes body");
        Require(RebirthRemoteResourceFragments.TryAdd("different-authenticated-owner",op,tiny.Hash,3,1,0,tiny.Part(0),1d,out complete)&&complete.SequenceEqual(new byte[]{1,2,3}),"caller-derived owner scopes remain separate");
    }
}
