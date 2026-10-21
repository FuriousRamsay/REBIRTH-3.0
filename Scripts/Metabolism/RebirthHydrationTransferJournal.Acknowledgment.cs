using System;
using System.IO;

internal static partial class RebirthHydrationTransferJournal
{
    // Separate metadata WAL reuses the existing immutable/fsync writer. Its
    // Applied means acknowledgment recorded, never another financial action.
    internal static bool TryPersistAcknowledgment(RemoteTerminal terminal)
    {
        try
        {
            var lease=terminal?.Lease;
            if(lease==null||!terminal.CurrentForAcknowledgment(lease))return false;
            var original=lease.Original;string path=AcknowledgmentPath(original.World,original.Owner,original.Creation);RequireSafe(path);
            var journal=new RebirthCamperTransferJournal(path);RequireSafe(path);foreach(var ignored in journal.ReadForIndex()){}
            byte[] bytes=Bytes(original);var record=journal.Prepare(original.Operation,Group(original.World,original.Owner,original.Creation),original.Creation,original.Revision,bytes,bytes);
            if(!Matches(record,original)||!terminal.CurrentForAcknowledgment(lease))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!terminal.CurrentForAcknowledgment(lease))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!Matches(record,original)||!terminal.CurrentForAcknowledgment(lease))return false;
            return TryReadAcknowledged(lease.Player,original.Operation,original.Digest,out var saved)&&saved.Digest==original.Digest&&
                SamePath(path,AcknowledgmentPath(original.World,original.Owner,original.Creation));
        }
        catch{return false;}
    }
    internal static bool TryReadAcknowledged(EntityPlayer player,Guid operation,string digest,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            if(operation==Guid.Empty||!Owner(player,out var world,out var owner,out var creation))return false;
            string path=AcknowledgmentPath(world,owner,creation);RequireSafe(path);if(!Directory.Exists(path))return false;
            var journal=new RebirthCamperTransferJournal(path);foreach(var ignored in journal.ReadForIndex()){}
            var record=journal.Read(operation);if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            var candidate=Decode(record.Before);
            if(candidate.Operation!=operation||candidate.Digest!=digest||!Current(player,candidate)||!Matches(record,candidate)||!HasApplied(player,candidate)||
                !SamePath(path,AcknowledgmentPath(candidate.World,candidate.Owner,candidate.Creation)))return false;
            original=candidate;return true;
        }
        catch{return false;}
    }
    private static string AcknowledgmentPath(Guid world,string owner,string creation)
    {
        string original=DirectoryPath(world,owner,creation);
        return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(original)),"HydrationAcknowledgments",Group(world,owner,creation).ToString("N"));
    }
}
