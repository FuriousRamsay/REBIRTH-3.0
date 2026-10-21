using System;

internal static partial class RebirthHydrationTransferJournal
{
    // Warm capability created only by the actual joint saved-terminal producer.
    // Parsing an original or observing one terminal receipt cannot construct it.
    internal sealed class LocalTerminal
    {
        internal readonly RebirthHydrationTransferGrant Grant;
        private LocalTerminal(RebirthHydrationTransferGrant grant){Grant=grant;}
        internal bool CurrentFor(RebirthHydrationTransferGrant grant)
        {
            try{return ReferenceEquals(Grant,grant)&&HasCurrentLocalTerminal(grant)&&
                HasSavedCompletion(grant.Original)&&HasCurrentLocalTerminal(grant);}
            catch{return false;}
        }
        internal static bool TryPublish(RebirthHydrationTransferGrant grant,out LocalTerminal terminal)
        {
            terminal=null;
            try
            {
                if(grant==null||!grant.Current()||!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant))return false;
                var original=grant.Original;var journal=Open(original,false);if(journal==null)return false;
                journal.AssertAvailable(Group(original.World,original.Owner,original.Creation),original.Operation);
                var record=journal.Read(original.Operation);
                if(!Matches(record,original))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                    journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                else if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
                if(!SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))||!HasCurrentLocalTerminal(grant))return false;
                if(!TryPersistLocalCompletion(grant)||!HasCurrentLocalTerminal(grant))return false;
                terminal=new LocalTerminal(grant);return true;
            }
            catch{return false;}
        }
    }
    private static bool TryPersistLocalCompletion(RebirthHydrationTransferGrant grant)
    {
        if(!HasCurrentLocalTerminal(grant))return false;
        var original=grant.Original;
        if(HasSavedCompletion(original))return HasCurrentLocalTerminal(grant);
        string path=CompletionPath(original,true);RequireSafe(path);
        var journal=new RebirthCamperTransferJournal(path);RequireSafe(path);
        byte[] bytes=Bytes(original);
        var record=journal.Prepare(original.Operation,Group(original.World,original.Owner,original.Creation),original.Creation,original.Revision,bytes,bytes);
        if(!Matches(record,original)||!HasCurrentLocalTerminal(grant))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
            record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(!HasCurrentLocalTerminal(grant))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
            record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied&&Matches(record,original)&&
            HasCurrentLocalTerminal(grant)&&HasSavedCompletion(original);
    }
    private static bool HasCurrentLocalTerminal(RebirthHydrationTransferGrant grant)
    {
        try
        {
            if(grant==null||!grant.Current()||!RebirthMetabolismStateRepository.HasSavedLocalHydration(grant))return false;
            var original=grant.Original;var journal=Open(original,false);if(journal==null)return false;
            var record=journal.Read(original.Operation);
            return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Applied&&
                SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))&&grant.Current();
        }
        catch{return false;}
    }
}
