using System;
using System.Collections.Generic;

// Byte-allocation accounting only. Shared native observers derive every length
// from their actual native stream/state/payload, never a request packet. This
// interface supplies no file, publication, inventory or payment permission.
public interface IRebirthStationSnapshotByteBudget
{
    int MaximumSnapshotBytes { get; }
    bool TryReserveAllocation(long actualAllocationBytes,out IDisposable reservation);
}

// Scoped native observer consumers supply this original-bound allocation account.
// The shared global default8MiB remains unchanged.
internal sealed class RebirthLogisticsSnapshotReservation:IRebirthStationSnapshotByteBudget,IDisposable
{
    internal const int MaximumNativeBytes=32*1024*1024;
    internal const long MaximumWorkspaceBytes=192L*1024*1024,MaximumRetainedBytes=72L*1024*1024;
    // Existing original admission separately checks native queued/inflight
    // capacity<=120MiB. Proof allocations plus that capacity reserve<=384MiB.
    private const long MaximumAllProofBytes=MaximumWorkspaceBytes+MaximumRetainedBytes;
    private static readonly object gate=new object();
    private sealed class Account
    {internal string Binding;internal long Workspace,Retained;internal int Owners;}
    private static readonly Dictionary<string,Account> accounts=new Dictionary<string,Account>(StringComparer.Ordinal);
    private static long allProofBytes;
    private readonly string key;private readonly Account account;private bool closed;
    private RebirthLogisticsSnapshotReservation(string key,Account account){this.key=key;this.account=account;}
    public int MaximumSnapshotBytes=>MaximumNativeBytes;
    internal long WorkspaceBytes{get{lock(gate)return account.Workspace;}}
    internal long RetainedBytes{get{lock(gate)return account.Retained;}}
    internal IRebirthStationSnapshotByteBudget LimitTo(int maximum)
    {if(maximum<9||maximum>MaximumNativeBytes)throw new ArgumentOutOfRangeException(nameof(maximum));return new Limited(this,maximum);}
    private sealed class Limited:IRebirthStationSnapshotByteBudget
    {
        private readonly RebirthLogisticsSnapshotReservation owner;public int MaximumSnapshotBytes{get;}
        internal Limited(RebirthLogisticsSnapshotReservation owner,int maximum){this.owner=owner;MaximumSnapshotBytes=maximum;}
        public bool TryReserveAllocation(long bytes,out IDisposable allocation)=>owner.TryReserveAllocation(bytes,out allocation);
    }
    internal static RebirthLogisticsSnapshotReservation TryOpen(RebirthLogisticsTransferJournal.Record original,Func<bool> originalCurrent)
        =>Open(original,originalCurrent,false);
    // Accounting only for native journal codec/retirement reads. This never
    // authorizes an original action or changes the producer phase policy.
    internal static RebirthLogisticsSnapshotReservation TryOpenRetainedRead(RebirthLogisticsTransferJournal.Record original,Func<bool> originalCurrent)
        =>Open(original,originalCurrent,true);
    private static RebirthLogisticsSnapshotReservation Open(RebirthLogisticsTransferJournal.Record original,Func<bool> originalCurrent,bool terminalRead)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||original==null||original.Phase<0||original.Phase>(terminalRead?4:3)||originalCurrent?.Invoke()!=true||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)||
                saved.Phase<0||saved.Phase>(terminalRead?4:3)||RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||originalCurrent()!=true)return null;
            string key=original.World+":"+original.Owner+":"+original.Operation.ToString("N"),binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);
            lock(gate)
            {
                if(!accounts.TryGetValue(key,out var value))
                {
                    if(accounts.Count>=128)return null;value=new Account{Binding=binding};
                    var created=new RebirthLogisticsSnapshotReservation(key,value);accounts.Add(key,value);value.Owners=1;return created;
                }
                if(value.Binding!=binding)return null;var existing=new RebirthLogisticsSnapshotReservation(key,value);value.Owners++;return existing;
            }
        }
        catch{return null;}
    }
    // Pure thread-safe allocation hooks. In particular the native background
    // publication writer never invokes an actor/world callback through these.
    public bool TryReserveAllocation(long actualAllocationBytes,out IDisposable reservation)
        =>TryReserve(actualAllocationBytes,false,out reservation);
    internal bool TryReserveRetainedSnapshot(long actualNativeBytes,out IDisposable reservation)
    {
        reservation=null;if(actualNativeBytes<9||actualNativeBytes>MaximumNativeBytes)return false;
        return TryReserve(actualNativeBytes,true,out reservation);
    }
    private bool TryReserve(long bytes,bool retained,out IDisposable reservation)
    {
        reservation=null;long maximum=retained?MaximumRetainedBytes:MaximumWorkspaceBytes;
        if(bytes<1||bytes>maximum)return false;
        lock(gate)
        {
            if(closed||!accounts.TryGetValue(key,out var current)||!ReferenceEquals(current,account)||allProofBytes>MaximumAllProofBytes-bytes||
                (retained?account.Retained:account.Workspace)>maximum-bytes)return false;
            var created=new Allocation(key,account,bytes,retained);
            allProofBytes+=bytes;if(retained)account.Retained+=bytes;else account.Workspace+=bytes;
            reservation=created;return true;
        }
    }
    private sealed class Allocation:IDisposable
    {
        private readonly string key;private readonly Account account;private readonly long bytes;private readonly bool retained;private int released;
        internal Allocation(string key,Account account,long bytes,bool retained){this.key=key;this.account=account;this.bytes=bytes;this.retained=retained;}
        public void Dispose()
        {
            if(System.Threading.Interlocked.Exchange(ref released,1)!=0)return;
            lock(gate){allProofBytes-=bytes;if(retained)account.Retained-=bytes;else account.Workspace-=bytes;RemoveEmpty(key,account);}
        }
    }
    private static void RemoveEmpty(string key,Account value)
    {if(value.Owners==0&&value.Workspace==0&&value.Retained==0&&accounts.TryGetValue(key,out var current)&&ReferenceEquals(current,value))accounts.Remove(key);}
    public void Dispose()
    {lock(gate){if(closed)return;closed=true;account.Owners--;RemoveEmpty(key,account);}}
    // Existing bounded Inflate allocates a geometric MemoryStream plus its
    // exact ToArray and4KiB output buffer. Charge before payload copy/inflate;
    // caller still requires decoded.Length==actualSerializedLength-8 and exact
    // bytes, complete inflate input consumption and final region readback.
    internal static bool TryInflateReservationBytes(long actualSerializedLength,int actualNativePayloadLength,out long bytes)
    {
        bytes=0;if(actualSerializedLength<9||actualSerializedLength>MaximumNativeBytes||actualNativePayloadLength<9||actualNativePayloadLength>RebirthStationRegionPayload.MaximumPayloadBytes)return false;
        long expected=actualSerializedLength-8;
        bytes=Math.Max(256,2*expected)+expected+4096+128*1024+actualNativePayloadLength+RebirthStationRegionPayload.MaximumPayloadBytes+24L;return true;
    }
}
