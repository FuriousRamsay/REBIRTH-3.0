#if DEBUG
using System;
using System.Collections.Generic;

public static partial class RebirthNpcGuardDutyWorkPackageService
{
    internal static void VerifyActualHeldPackage(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync)
        {
            var records=Active;var original=packageScope;string before=GetReport();var refs=new Dictionary<Guid,ActivePackage>(Active);
            if(CreateAndStart(null).Detail!=RebirthNpcWorkReleaseGate.Detail)throw new InvalidOperationException("Held guard admission changed");Tick(1);
            bool expectedRefusal=!CanRetireEmptyGuardPackage||!RebirthNpcGuardPostWorkAdapter.CanRetireEmptyNative;bool refused=false;try{ResetForWorldChange();}catch(InvalidOperationException){refused=true;}if(refused!=expectedRefusal)throw new InvalidOperationException("Opaque held reset contract changed");
            if(!ReferenceEquals(records,Active)||!ReferenceEquals(original,packageScope)||before!=GetReport()||records.Count!=refs.Count)throw new InvalidOperationException("Held original package changed");foreach(var row in refs)if(!records.TryGetValue(row.Key,out var current)||!ReferenceEquals(row.Value,current))throw new InvalidOperationException("Held native row replaced");
        }
    }
}
public static partial class RebirthNpcMedicalTreatmentWorkPackageService
{
    internal static void VerifyActualHeldPackage(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync)
        {
            var records=Active;var original=packageScope;string before=GetReport();var refs=new Dictionary<Guid,ActivePackage>(Active);
            if(CreateAndStart(null).Detail!=RebirthNpcWorkReleaseGate.Detail)throw new InvalidOperationException("Held medical admission changed");Tick(1);
            bool expectedRefusal=!CanRetireEmptyMedicalPackage||!RebirthNpcMedicalTreatmentAdapter.CanRetireEmptyNative;bool refused=false;try{ResetForWorldChange();}catch(InvalidOperationException){refused=true;}if(refused!=expectedRefusal)throw new InvalidOperationException("Opaque held reset contract changed");
            if(!ReferenceEquals(records,Active)||!ReferenceEquals(original,packageScope)||before!=GetReport()||records.Count!=refs.Count)throw new InvalidOperationException("Held original package changed");foreach(var row in refs)if(!records.TryGetValue(row.Key,out var current)||!ReferenceEquals(row.Value,current))throw new InvalidOperationException("Held native row replaced");
        }
    }
}
public sealed partial class RebirthNpcGuardPostWorkAdapter
{
    internal static void VerifyActualHeldPackage(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync)
        {
            var records=States;var original=nativeScope;string before=GetReport();var refs=new Dictionary<ulong,DutyState>(States);
            var adapter=new RebirthNpcGuardPostWorkAdapter();if(adapter.BeginGuardDuty(null).Detail!=RebirthNpcWorkReleaseGate.Detail||adapter.TickGuardDuty(null).Detail!=RebirthNpcWorkReleaseGate.Detail)throw new InvalidOperationException("Held native guard ran");adapter.CancelGuardDuty(null,"fixture");TickPendingRestores(1);
            bool expectedRefusal=!CanRetireEmptyNative;bool refused=false;try{ResetForWorldChange();}catch(InvalidOperationException){refused=true;}if(refused!=expectedRefusal)throw new InvalidOperationException("Opaque held reset contract changed");
            if(!ReferenceEquals(records,States)||!ReferenceEquals(original,nativeScope)||before!=GetReport()||records.Count!=refs.Count)throw new InvalidOperationException("Held original package changed");foreach(var row in refs)if(!records.TryGetValue(row.Key,out var current)||!ReferenceEquals(row.Value,current))throw new InvalidOperationException("Held native row replaced");
        }
    }
}
public sealed partial class RebirthNpcMedicalTreatmentAdapter
{
    internal static void VerifyActualHeldPackage(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(sync)
        {
            var records=states;var original=nativeScope;string before=GetReport();var refs=new Dictionary<ulong,State>(states);
            var adapter=new RebirthNpcMedicalTreatmentAdapter();if(adapter.BeginMedical(null).Detail!=RebirthNpcWorkReleaseGate.Detail||adapter.TickMedical(null).Detail!=RebirthNpcWorkReleaseGate.Detail)throw new InvalidOperationException("Held native medical ran");adapter.CancelMedical(null,"fixture");
            bool expectedRefusal=!CanRetireEmptyNative;bool refused=false;try{ResetForWorldChange();}catch(InvalidOperationException){refused=true;}if(refused!=expectedRefusal)throw new InvalidOperationException("Opaque held reset contract changed");
            if(!ReferenceEquals(records,states)||!ReferenceEquals(original,nativeScope)||before!=GetReport()||records.Count!=refs.Count)throw new InvalidOperationException("Held original package changed");foreach(var row in refs)if(!records.TryGetValue(row.Key,out var current)||!ReferenceEquals(row.Value,current))throw new InvalidOperationException("Held native row replaced");
        }
    }
}
internal static partial class RebirthNpcEquipmentProjectionReceipts
{
    internal static void VerifyActualReceiptQueries(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException("Actual disposable fixture required");
        var records=receipts;var original=receiptScope;string before=ReceiptImage().OuterXml;var refs=new Dictionary<string,Receipt>(receipts,StringComparer.Ordinal);
        Observe(null,GameManager.Instance.World,0,null,0,0,null,0,0);
        foreach(var r in refs.Values)if(r.Generation<ulong.MaxValue&&Acknowledged(r.Sender,r.Entity,r.Generation+1,r.Revision,r.Digest))throw new InvalidOperationException("Wrong generation ACK cache accepted");
        if(!ReferenceEquals(records,receipts)||!ReferenceEquals(original,receiptScope)||ReceiptImage().OuterXml!=before)throw new InvalidOperationException("Read-only receipt query changed cache");
    }
}
#endif
