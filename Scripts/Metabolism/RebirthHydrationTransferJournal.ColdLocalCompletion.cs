internal static partial class RebirthHydrationTransferJournal
{
    private static bool completingColdLocal;
    // No financial mutation. A cold Applied original can close only from both
    // independent actual saved results and no retained warm financial custody.
    internal static bool TryCompleteSavedLocal(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!ThreadManager.IsMainThread()||completingColdLocal)return false;
        completingColdLocal=true;
        try
        {
            if(!RebirthHydrationColdOwnerWitness.TryObserveCompletedLocal(player,original,out var witness))return false;
            if(HasSavedCompletion(original))return witness.CurrentCompletedLocal();
            string path=CompletionPath(original,true);RequireSafe(path);
            var journal=new RebirthCamperTransferJournal(path);RequireSafe(path);byte[] bytes=Bytes(original);
            var record=journal.Prepare(original.Operation,Group(original.World,original.Owner,original.Creation),original.Creation,original.Revision,bytes,bytes);
            if(!Matches(record,original)||!witness.CurrentCompletedLocal())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!witness.CurrentCompletedLocal())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&Matches(record,original)&&
                HasSavedCompletion(original)&&witness.CurrentCompletedLocal();
        }
        catch{return false;}
        finally{completingColdLocal=false;}
    }
}
