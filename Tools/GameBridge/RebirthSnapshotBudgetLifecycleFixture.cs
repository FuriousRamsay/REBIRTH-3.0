using System;
using System.IO;
// Authored, NOT RUN. Execute only in an isolated compiled fixture context.
// Exercises production serializer observation/allocation lifecycle, not disk proof.
internal static class RebirthSnapshotBudgetLifecycleFixture
{
    private sealed class Budget:IRebirthStationSnapshotByteBudget
    {
        public int MaximumSnapshotBytes=>16*1024*1024;
        internal long Held;internal bool Refuse;
        public bool TryReserveAllocation(long bytes,out IDisposable reservation)
        {reservation=null;if(Refuse)return false;Held+=bytes;reservation=new Lease(this,bytes);return true;}
        private sealed class Lease:IDisposable
        {private Budget owner;private readonly long bytes;internal Lease(Budget owner,long bytes){this.owner=owner;this.bytes=bytes;}public void Dispose(){var value=owner;owner=null;if(value!=null)value.Held-=bytes;}}
    }
    internal static void Verify()
    {
        object world=new object(),chunk=new object(),snapshot=new object();var budget=new Budget();
        var request=RebirthStationSnapshotEvidence.Watch(world,chunk,1,2,budget:budget);
        if(request==null)throw new Exception("Fresh isolated observer unavailable.");
        try
        {
            byte[] data={116,116,99,0,47,0,0,0,1};
            var frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,2,true);
            using(var stream=new MemoryStream(data,false))
            {RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);}
            if(budget.Held!=data.Length)throw new Exception("Capture lease missing.");
            if(RebirthStationSnapshotEvidence.TryCopy(request,out _))throw new Exception("Unscoped budget copy accepted.");
            if(!RebirthStationSnapshotEvidence.TryCopy(request,out var copy,out var allocation)||copy.Length!=data.Length||budget.Held!=2*data.Length)throw new Exception("Scoped clone not charged.");
            RebirthStationSnapshotEvidence.Invalidate(snapshot);
            if(budget.Held!=data.Length)throw new Exception("Invalidation released outstanding clone or leaked capture.");
            allocation.Dispose();allocation.Dispose();if(budget.Held!=0)throw new Exception("Clone lease not idempotent.");
            budget.Refuse=true;
            frame=RebirthStationSnapshotEvidence.Begin(snapshot,chunk,world,1,2,true);
            using(var stream=new MemoryStream(data,false))
            {RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(frame,stream,null);}
            if(RebirthStationSnapshotEvidence.TryCopy(request,out _,out _)||budget.Held!=0)throw new Exception("Refused allocation retained evidence.");
        }
        finally{request.Dispose();request.Dispose();}
        if(budget.Held!=0)throw new Exception("Request disposal leaked accounting.");
    }
}
