using System;
using System.Reflection;
// PREPARED, NOT RUN. Pure arithmetic boundary checks only, no fake original/publication.
internal static class mpA_logistics_snapshot_reservation_fixture
{
    internal static void Verify(Assembly production)
    {
        var type=production.GetType("RebirthLogisticsSnapshotReservation",true);
        var method=type.GetMethod("TryInflateReservationBytes",BindingFlags.Static|BindingFlags.NonPublic);
        bool Read(long raw,int payload,out long cost){object[] args={raw,payload,0L};bool accepted=(bool)method.Invoke(null,args);cost=(long)args[2];return accepted;}
        if(Read(8,9,out _)||Read(32L*1024*1024+1,9,out _)||Read(9,8,out _)||Read(9,int.MaxValue,out _))throw new Exception("Invalid actual native bounds accepted.");
        if(!Read(9,9,out var small)||small<256+1+4096+128*1024+9+255*4096-16+24)throw new Exception("Minimum workspace omitted.");
        if(!Read(32L*1024*1024,9,out var large)||large<3*(32L*1024*1024-8)+4096+128*1024+9+255*4096-16+24)throw new Exception("Geometric inflate and exact output not both charged.");
        if(!Read(1024,100,out var first)||!Read(1024,101,out var second)||second-first!=1)throw new Exception("Actual payload clone cost missing.");
    }
}
// Additional PREPARED, NOT RUN checks requiring an actual current retained
// logistics original on the native main thread. Reservation tokens allocate
// no snapshot buffers and grant no item/source effect permission.
internal static class mpA_logistics_snapshot_account_fixture
{
    internal static void Verify(System.Reflection.Assembly production,object original,System.Func<bool> current)
    {
        var type=production.GetType("RebirthLogisticsSnapshotReservation",true);
        var open=type.GetMethod("TryOpen",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var value=open.Invoke(null,new object[]{original,current});if(value==null)throw new System.Exception("Requires actual admitted current original.");
        var budget=(IRebirthStationSnapshotByteBudget)value;System.IDisposable full=null,extra=null;
        var workspace=type.GetProperty("WorkspaceBytes",BindingFlags.Instance|BindingFlags.NonPublic);
        if((long)workspace.GetValue(value)!=0){((IDisposable)value).Dispose();throw new Exception("Requires exclusive actual original account with zero existing workspace; retained observers must first be disposed by the harness.");}
        try
        {
            if(!budget.TryReserveAllocation(192L*1024*1024,out full)||budget.TryReserveAllocation(1,out extra))throw new System.Exception("Per-original workspace limit not enforced.");
            full.Dispose();full.Dispose();full=null;
            if(!budget.TryReserveAllocation(1,out extra))throw new System.Exception("Released workspace was not reusable.");
            ((System.IDisposable)value).Dispose();
            if(budget.TryReserveAllocation(1,out var forbidden)){forbidden?.Dispose();throw new System.Exception("Closed owner admitted new allocation.");}
        }
        finally{full?.Dispose();extra?.Dispose();((System.IDisposable)value).Dispose();}
    }
}
