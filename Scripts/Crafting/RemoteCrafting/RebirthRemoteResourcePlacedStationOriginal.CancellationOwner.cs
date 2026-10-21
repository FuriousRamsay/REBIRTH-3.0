using System;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal static bool IsPreparedCancellationOwner(CancellationClient original)
    {
        if(original==null||!original.NativeLease()||!cancellationClients.TryGetValue(original.Operation,out var actual)||!ReferenceEquals(actual,original))return false;
        var record=CancellationJournal(original.Operation,"Client").Read(original.Operation);var bytes=Encoding.UTF8.GetBytes(original.Header);
        return record?.State==RebirthCamperTransferJournal.Phase.Uncertain&&record.Vehicle==original.Source&&record.OwnerCreation==original.Original.Creation&&record.Revision==0&&
            record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    }
    internal sealed class CancellationOwnerPermission
    {
        internal readonly CancellationClient Original;
        internal Guid Operation=>Original.Operation;
        private CancellationOwnerPermission(CancellationClient original){Original=original;}
        internal bool Current=>Original.RestartSavedAfterOwner==null&&Original.NativeLease()&&Original.PermissionSaved&&Original.After!=null&&Original.Permission!=null&&
            cancellationClients.TryGetValue(Original.Operation,out var actual)&&ReferenceEquals(actual,Original)&&
            Original.Actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==1f;
        internal static bool TryCreate(CancellationClient original,out CancellationOwnerPermission permission)
        {
            permission=null;
            if(original==null||original.RestartSavedAfterOwner!=null||!original.NativeLease()||!original.PermissionSaved||original.Permission==null||original.After==null||
                !cancellationClients.TryGetValue(original.Operation,out var actual)||!ReferenceEquals(actual,original)||
                original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=1f||
                !original.OwnerAttempted&&original.RestartBeforeOwner?.PrefixCurrent!=true&&!original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
            var record=CancellationJournal(original.Operation,"Permission").Read(original.Operation);var bytes=Encoding.UTF8.GetBytes(original.Permission);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Uncertain||record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||
                !record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            permission=new CancellationOwnerPermission(original);original.OwnerAttempted=true;return true;
        }
    }
    internal static bool HasCancellationClientTerminal(CancellationClient original)
    {
        if(original==null||!original.Current()||!original.HostTerminal||original.Permission==null||original.TerminalEvidence==null)return false;
        var record=CancellationJournal(original.Operation,"Terminal").Read(original.Operation);
        var bytes=CancellationClientTerminalBytes(original);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Source&&record.OwnerCreation==original.Original.Creation&&record.Revision==0&&
            record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    }
    internal static bool HasCancellationClientFinalAck(CancellationClient original)
    {
        if(original==null||!original.HostReleased||!HasCancellationClientTerminal(original)||original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=3f||
            !original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
        var record=CancellationJournal(original.Operation,"FinalAck",MaximumApplicationCommand).Read(original.Operation);var bytes=CancellationClientFinalAckBytes(original);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Source&&record.OwnerCreation==original.Original.Creation&&record.Revision==0&&record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes);
    }
}
