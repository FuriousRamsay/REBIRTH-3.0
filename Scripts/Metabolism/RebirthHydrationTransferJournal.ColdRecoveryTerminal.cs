internal static partial class RebirthHydrationTransferJournal
{
    private static bool publishingColdRecovery;
    internal sealed class ColdRecoveryTerminal
    {
        internal readonly RebirthHydrationColdRecoveryLease Lease;
        private ColdRecoveryTerminal(RebirthHydrationColdRecoveryLease lease){Lease=lease;}
        internal bool CurrentFor(RebirthHydrationColdRecoveryLease lease)
        {
            try{return ReferenceEquals(Lease,lease)&&JointCurrent(lease)&&HasSavedCompletion(lease.Original)&&JointCurrent(lease);}
            catch{return false;}
        }
        internal static bool TryPublish(RebirthHydrationColdRecoveryLease lease,out ColdRecoveryTerminal terminal)
        {
            terminal=null;
            if(!ThreadManager.IsMainThread()||publishingColdRecovery)return false;
            publishingColdRecovery=true;
            try
            {
                if(lease==null||!lease.Current()||!RebirthMetabolismStateRepository.TrySettleColdLocalHydration(lease))return false;
                var original=lease.Original;var financial=Open(original,false);if(financial==null)return false;
                var record=financial.Read(original.Operation);
                if(!Matches(record,original)||!lease.Current()||!RebirthMetabolismStateRepository.HasSavedColdRecoveryHydrationAfter(lease))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                    record=financial.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!Matches(record,original)||!JointCurrent(lease))return false;
                if(!HasSavedCompletion(original))
                {
                    string path=CompletionPath(original,true);RequireSafe(path);
                    var completion=new RebirthCamperTransferJournal(path);RequireSafe(path);byte[] bytes=Bytes(original);
                    var saved=completion.Prepare(original.Operation,Group(original.World,original.Owner,original.Creation),original.Creation,original.Revision,bytes,bytes);
                    if(!Matches(saved,original)||!JointCurrent(lease))return false;
                    if(saved.State==RebirthCamperTransferJournal.Phase.Prepared)
                        saved=completion.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                    if(!JointCurrent(lease))return false;
                    if(saved.State==RebirthCamperTransferJournal.Phase.Uncertain)
                        saved=completion.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                    if(saved.State!=RebirthCamperTransferJournal.Phase.Applied||!Matches(saved,original))return false;
                }
                var next=new ColdRecoveryTerminal(lease);if(!next.CurrentFor(lease))return false;terminal=next;return true;
            }
            catch{return false;}
            finally{publishingColdRecovery=false;}
        }
        private static bool JointCurrent(RebirthHydrationColdRecoveryLease lease)=>lease!=null&&lease.Current()&&
            HasApplied(lease.Player,lease.Original)&&RebirthMetabolismStateRepository.HasSavedColdRecoveryHydrationAfter(lease)&&lease.Current();
    }
}
