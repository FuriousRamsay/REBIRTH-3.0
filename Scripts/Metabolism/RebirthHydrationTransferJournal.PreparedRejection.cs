using System;

internal static partial class RebirthHydrationTransferJournal
{
    // Positive untouched server-source outcome only. Uncertain/issued work can
    // never enter this producer merely because a callback timed out.
    internal sealed class PreparedRejection
    {
        internal readonly RebirthHydrationServerSourceHold.Lease Lease;
        private PreparedRejection(RebirthHydrationServerSourceHold.Lease lease){Lease=lease;}
        internal bool CurrentFor(RebirthHydrationServerSourceHold.Lease lease)
        {
            try
            {
                if(!ReferenceEquals(Lease,lease)||!Untouched(lease))return false;
                var journal=Open(lease.Original,false);if(journal==null)return false;
                var record=journal.Read(lease.Original.Operation);
                return Matches(record,lease.Original)&&record.State==RebirthCamperTransferJournal.Phase.Rejected&&Untouched(lease)&&
                    SamePath(journal.DirectoryPath,DirectoryPath(lease.Original.World,lease.Original.Owner,lease.Original.Creation));
            }
            catch{return false;}
        }
        internal static bool TryPublish(RebirthHydrationServerSourceHold.Lease lease,out PreparedRejection rejection)
        {
            rejection=null;
            try
            {
                if(!Untouched(lease))return false;
                var original=lease.Original;var journal=Open(original,false);if(journal==null)return false;
                var record=journal.Read(original.Operation);
                if(!Matches(record,original))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                    journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
                else if(record.State!=RebirthCamperTransferJournal.Phase.Rejected)return false;
                var next=new PreparedRejection(lease);if(!next.CurrentFor(lease))return false;rejection=next;return true;
            }
            catch{return false;}
        }
        private static bool Untouched(RebirthHydrationServerSourceHold.Lease lease)=>lease!=null&&lease.Current()&&
            !RebirthHydrationRemoteGrant.HasIssued(lease.Original.Operation)&&
            lease.State.HydrationTransferReceipt?.Digest!=lease.Original.Digest&&
            RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)&&lease.Current();
    }
}
