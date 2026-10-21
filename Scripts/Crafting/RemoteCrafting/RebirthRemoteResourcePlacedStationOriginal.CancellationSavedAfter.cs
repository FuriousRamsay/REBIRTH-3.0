using System;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // One concrete attempted case: the financial/native effects were already
    // positively saved, and only the exact terminal metadata remains Uncertain.
    // This cannot authorize an item setter or replay the native queue attempt.
    private sealed class CancellationSavedAfterContinuation
    {
        private readonly CancellationServer original;private readonly string before,after,permission;private readonly byte[] terminal,mutation;
        private readonly long readFloor;
        private CancellationSavedAfterContinuation(CancellationServer value,string before,string after)
        {original=value;this.before=before;this.after=after;permission=value.Permission;terminal=CancellationServerTerminalBytes(value);mutation=CancellationMutation(value);readFloor=value.SavedAfterReadFloor;}
        internal static CancellationSavedAfterContinuation Capture(CancellationServer original)
        {
            if(!original.Current()||!original.BeforeSaved||!original.AfterSaved||!original.QueueAttempted||!original.QueueCompleted||
                !original.RefundQualified||!SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,false,out var before)||
                !ReadCancellationSnapshot(original,true,out var after)||original.Permission==null)return null;
            var terminal=CancellationJournal(original.Operation,"Terminal",MaximumApplicationCommand).Read(original.Operation);
            var mutation=CancellationJournal(original.Operation,"Mutation",MaximumApplicationCommand).Read(original.Operation);
            byte[] expected=CancellationServerTerminalBytes(original),command=CancellationMutation(original);
            if(terminal?.State!=RebirthCamperTransferJournal.Phase.Uncertain||mutation?.State!=RebirthCamperTransferJournal.Phase.Applied||
                !Exact(original,terminal,expected)||!Exact(original,mutation,command)||before.Canonical==null||after.Canonical==null)return null;
            long workspace=checked(12L*(Encoding.UTF8.GetByteCount(before.Canonical)+Encoding.UTF8.GetByteCount(after.Canonical)+Encoding.UTF8.GetByteCount(original.Permission))+65536);
            if(workspace>original.SavedAfterWorkspace)
            {if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace-original.SavedAfterWorkspace)))return null;original.SavedAfterWorkspace=workspace;}
            if(original.SavedAfterReadFloor==0)original.SavedAfterReadFloor=original.Memory.ReservedBytes;
            var result=new CancellationSavedAfterContinuation(original,before.Canonical,after.Canonical);return result.Current?result:null;
        }
        private static bool Exact(CancellationServer original,RebirthCamperTransferJournal.Record record,byte[] bytes)=>record!=null&&record.Operation==original.Operation&&record.Vehicle==original.Source&&
            record.OwnerCreation==original.Original.Creation&&record.Revision==0&&record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
        internal bool Current
        {
            get
            {
                if(original.Memory?.Current!=true||original.Origin?.Current!=true||!ServerScope(original.Actor,original.Original)||original.Permission!=permission||original.AfterEvidence!=after||
                    !original.BeforeSaved||!original.AfterSaved||!original.QueueAttempted||!original.QueueCompleted||!original.RefundQualified||
                    !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Actor.entityId),original.Peer)||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(original.Actor,out var owner)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var native)||
                    native.id!=original.Actor.entityId||native.deaths!=original.Original.Deaths||!original.AfterActor.Matches(image)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
                foreach(string purpose in new[]{"Before","After","Permission","Mutation","Terminal"})
                {
                    var record=CancellationJournal(original.Operation,purpose,MaximumResultBytes,original.Memory,readFloor).Read(original.Operation);
                    byte[] expected=purpose=="Before"?Encoding.UTF8.GetBytes(before):purpose=="After"?Encoding.UTF8.GetBytes(after):purpose=="Permission"?Encoding.UTF8.GetBytes(permission):purpose=="Mutation"?mutation:terminal;
                    if(!Exact(original,record,expected))return false;
                    var required=purpose=="Permission"?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied;
                    if(purpose=="Terminal")
                    {if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;}
                    else if(record.State!=required)return false;
                }
                return ReadCancellationAccepted(original,original.Accepted);
            }
        }
    }
    private static bool ArmCancellationSavedAfter(CancellationServer original)
    {
        if(original.LoadOriginal?.SavedAfter!=null)return original.LoadOriginal.SavedAfter.Current;
        var continuation=CancellationSavedAfterContinuation.Capture(original);
        if(continuation==null||!ArmCancellationReentry(original))return false;
        original.LoadOriginal.SavedAfter=continuation;return true;
    }
}
