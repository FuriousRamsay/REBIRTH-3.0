using System;
using System.IO;

internal static partial class RebirthHydrationTransferJournal
{
    // Historical rejection description. Never reconstructs a source lease,
    // client item grant, native inventory handle or compensation permission.
    internal sealed class SavedRejection
    {
        internal readonly EntityPlayer Player;internal readonly RebirthHydrationTransferOriginal Original;
        private SavedRejection(EntityPlayer player,RebirthHydrationTransferOriginal original){Player=player;Original=original;}
        internal bool Current()=>ReadRejected(Player,Original.Operation,Original.Digest,out var current)&&current.Digest==Original.Digest;
        internal static bool TryRead(EntityPlayer player,Guid operation,string digest,out SavedRejection rejection)
        {
            rejection=null;
            if(!ReadRejected(player,operation,digest,out var original))return false;
            rejection=new SavedRejection(player,original);return true;
        }
    }
    private static bool ReadRejected(EntityPlayer player,Guid operation,string digest,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            if(operation==Guid.Empty||!Owner(player,out var world,out var owner,out var creation))return false;
            string path=DirectoryPath(world,owner,creation);RequireSafe(path);if(!Directory.Exists(path))return false;
            var journal=new RebirthCamperTransferJournal(path);foreach(var ignored in journal.ReadForIndex()){}
            var record=journal.Read(operation);if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Rejected)return false;
            var candidate=Decode(record.Before);
            if(candidate.Operation!=operation||candidate.Digest!=digest||!Matches(record,candidate)||
                !Current(player,candidate)||RebirthHydrationRemoteGrant.HasIssued(operation)||
                !SamePath(path,DirectoryPath(candidate.World,candidate.Owner,candidate.Creation)))return false;
            original=candidate;return true;
        }
        catch{return false;}
    }
}
