using System;
using System.IO;

internal static partial class RebirthHydrationTransferJournal
{
    // Description-only historical closure. Never recreates a native grant or
    // releases live custody. Both writers require their own positive terminal.
    private static string CompletionPath(RebirthHydrationTransferOriginal original,bool local)
    {
        string financial=DirectoryPath(original.World,original.Owner,original.Creation);
        return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(financial)),
            local?"HydrationLocalCompletions":"HydrationAcknowledgments",
            Group(original.World,original.Owner,original.Creation).ToString("N"));
    }
    private static bool HasSavedCompletion(RebirthHydrationTransferOriginal original)
    {
        foreach(bool local in new[]{true,false})
        {
            string path=CompletionPath(original,local);RequireSafe(path);
            if(!Directory.Exists(path))continue;
            var journal=new RebirthCamperTransferJournal(path);int count=0;
            foreach(var ignored in journal.ReadForIndex())
                if(++count>16384)throw new InvalidDataException("Hydration completion history bound exceeded");
            var record=journal.Read(original.Operation);
            if(record==null)continue;
            if(!Matches(record,original))throw new InvalidDataException("Hydration completion original mismatch");
            if(record.State==RebirthCamperTransferJournal.Phase.Applied)return true;
            if(record.State==RebirthCamperTransferJournal.Phase.Rejected)
                throw new InvalidDataException("Invalid hydration completion phase");
        }
        return false;
    }
}
