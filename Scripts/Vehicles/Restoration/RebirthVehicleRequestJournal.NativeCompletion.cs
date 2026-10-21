using System;

public static partial class RebirthVehicleRequestJournal
{
    internal static bool HasRetainedNativeOriginal(Guid id,string actor,string fingerprint)
    {
        EnsureLoaded();lock(Sync)
        {
            if(loadFailed||!fingerprint.StartsWith("native-owner-plan-v1|",StringComparison.Ordinal))return false;
            return InFlight.TryGetValue(id,out var intent)&&intent.Actor==actor&&intent.Fingerprint==fingerprint||
                Completed.TryGetValue(id,out var result)&&result.ActorId==actor&&result.Fingerprint==fingerprint&&result.Outcome==RebirthVehicleRequestOutcome.Indeterminate;
        }
    }
    internal static bool RememberNativeCompletion(Guid id,RebirthVehicleRequestResult result,RebirthVehicleTransferCoordinator.Transfer transfer)
    {
        if(transfer==null||!transfer.CanPublishResult()||transfer.Plan.TransactionId!=id||result?.Fingerprint!=transfer.Plan.Fingerprint||
            result.Outcome!=(transfer.Rejected?RebirthVehicleRequestOutcome.Rejected:RebirthVehicleRequestOutcome.Applied)||result.Snapshot==null||
            RebirthVehicleAssemblySerializer.ToBase64(result.Snapshot)!=(transfer.Rejected?transfer.Plan.BeforePayload:transfer.Plan.AfterPayload))return false;
        bool saved=ReplaceNativeResult(id,result,()=>transfer.CanPublishResult());if(saved)RebirthVehicleTransferCoordinator.RetirePublished(transfer);return saved;
    }
    internal static bool RememberNativeRefusal(NetPackageRebirthVehicleAssemblyRequest.NativeRefusal refusal)
    {
        if(refusal==null||!refusal.CanPublish())return false;
        return ReplaceNativeResult(refusal.Operation,refusal.Result,refusal.CanPublish);
    }
    private static bool ReplaceNativeResult(Guid id,RebirthVehicleRequestResult result,Func<bool> current)
    {
        EnsureLoaded();lock(Sync)
        {
            if(loadFailed||loadedDirectory.Length==0||!string.Equals(loadedDirectory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase)||!current())return false;
            if(InFlight.TryGetValue(id,out var original)&&(original.Actor!=result.ActorId||original.Fingerprint!=result.Fingerprint))return false;
            if(Completed.TryGetValue(id,out var previous))
            {
                if(previous.ActorId!=result.ActorId||previous.Fingerprint!=result.Fingerprint)return false;
                if(previous.Outcome!=RebirthVehicleRequestOutcome.Indeterminate)return previous.Outcome==result.Outcome;
            }
            else Order.Enqueue(id);
            Completed[id]=CloneResult(result);InFlight.Remove(id);TrimResolved(MaxEntries);
            Persist(loadedDirectory);
            return !loadFailed&&current()&&Completed.TryGetValue(id,out var saved)&&saved.Outcome==result.Outcome&&saved.ActorId==result.ActorId&&saved.Fingerprint==result.Fingerprint;
        }
    }
}
