using System;

internal static partial class RebirthHydrationTransferJournal
{
    // Actual warm local no-effect capability. No Uncertain/issued original can
    // enter it, regardless of timeout, absent reply or current item appearance.
    internal sealed class LocalPreparedRejection
    {
        internal readonly EntityPlayerLocal Player;
        internal readonly RebirthHydrationTransferOriginal Original;
        private LocalPreparedRejection(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
        {Player=player;Original=original;}
        private bool Untouched()=>Original!=null&&!RebirthHydrationTransferGrant.HasIssued(Original.Operation)&&
            RebirthHydrationOwnerHold.Matches(Player,Original)&&
            RebirthHydrationOwnerCheckpoint.HasSavedBefore(Player,Original)&&
            RebirthMetabolismStateRepository.HasSavedLocalHydrationBefore(Player,Original)&&
            RebirthHydrationOwnerCheckpoint.HasLiveBefore(Player,Original)&&
            RebirthMetabolismStateRepository.HasCurrentLocalHydrationBefore(Player,Original)&&
            !RebirthHydrationTransferGrant.HasIssued(Original.Operation)&&RebirthHydrationOwnerHold.Matches(Player,Original);
        internal bool Current()
        {
            try
            {
                if(!Untouched())return false;
                var journal=Open(Original,false);if(journal==null)return false;
                var record=journal.Read(Original.Operation);
                return Matches(record,Original)&&record.State==RebirthCamperTransferJournal.Phase.Rejected&&
                    SamePath(journal.DirectoryPath,DirectoryPath(Original.World,Original.Owner,Original.Creation))&&Untouched();
            }
            catch{return false;}
        }
        internal static bool TryPublish(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out LocalPreparedRejection rejection)
        {
            rejection=null;
            try
            {
                var next=new LocalPreparedRejection(player,original);if(!next.Untouched())return false;
                var journal=Open(original,false);if(journal==null)return false;
                var record=journal.Read(original.Operation);if(!Matches(record,original))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                    journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
                else if(record.State!=RebirthCamperTransferJournal.Phase.Rejected)return false;
                if(!next.Current())return false;rejection=next;return true;
            }
            catch{return false;}
        }
    }
}
