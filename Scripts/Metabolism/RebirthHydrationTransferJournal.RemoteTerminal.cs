using System;

internal static partial class RebirthHydrationTransferJournal
{
    // Warm server source-release capability, never client inventory permission.
    // Transport must separately bind the saved terminal to the owner's original.
    internal sealed class RemoteTerminal
    {
        internal readonly RebirthHydrationServerSourceHold.Lease Lease;
        private RemoteTerminal(RebirthHydrationServerSourceHold.Lease lease){Lease=lease;}
        internal bool CurrentFor(RebirthHydrationServerSourceHold.Lease lease)
            =>ReferenceEquals(Lease,lease)&&HasCurrentRemoteTerminal(lease);
        internal bool CurrentForAcknowledgment(RebirthHydrationServerSourceHold.Lease lease)
            =>ReferenceEquals(Lease,lease)&&lease.Current()&&HasApplied(lease.Player,lease.Original)&&
                RebirthMetabolismStateRepository.HasSavedRemoteHydrationAfterForAcknowledgment(lease)&&lease.Current();
        internal static bool TryPublish(RebirthHydrationServerSourceHold.Lease lease,out RemoteTerminal terminal)
        {
            terminal=null;
            try
            {
                if(lease==null||!lease.Current()||!RebirthMetabolismStateRepository.TrySettleRemoteHydration(lease))return false;
                var original=lease.Original;var journal=Open(original,false);if(journal==null)return false;
                journal.AssertAvailable(Group(original.World,original.Owner,original.Creation),original.Operation);
                var record=journal.Read(original.Operation);
                if(!Matches(record,original))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                    journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                else if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
                if(!SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))||!HasCurrentRemoteTerminal(lease))return false;
                terminal=new RemoteTerminal(lease);return true;
            }
            catch{return false;}
        }
    }
    private static bool HasCurrentRemoteTerminal(RebirthHydrationServerSourceHold.Lease lease)
    {
        try
        {
            if(lease==null||!lease.Current()||!RebirthMetabolismStateRepository.HasSavedRemoteHydrationAfter(lease))return false;
            var original=lease.Original;var journal=Open(original,false);if(journal==null)return false;
            var record=journal.Read(original.Operation);
            return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Applied&&
                SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))&&lease.Current();
        }
        catch{return false;}
    }
}
