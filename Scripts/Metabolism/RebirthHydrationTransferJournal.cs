using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Original intent custody. The LocalTerminal companion supplies the warm local
// Applied producer only after independently qualified owner/metabolism saves.
// Remote and rejected terminal producers are not supplied by this file.
internal static partial class RebirthHydrationTransferJournal
{
    private const int MaximumOriginalBytes=1024*1024;
    internal static bool HasApplied(EntityPlayer player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!Current(player,original))return false;
            var journal=Open(original,false);if(journal==null)return false;
            var record=journal.Read(original.Operation);
            return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Applied&&
                SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))&&Current(player,original);
        }
        catch{return false;}
    }
    internal static bool HasAttempted(EntityPlayer player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!Current(player,original))return false;
            var journal=Open(original,false);if(journal==null)return false;
            var record=journal.Read(original.Operation);
            return Matches(record,original)&&(record.State==RebirthCamperTransferJournal.Phase.Uncertain||record.State==RebirthCamperTransferJournal.Phase.Applied)&&
                SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))&&Current(player,original);
        }
        catch{return false;}
    }
    internal static bool HasPrepared(EntityPlayer player,RebirthHydrationTransferOriginal original)
    {
        try{if(!Current(player,original))return false;var journal=Open(original,false);if(journal==null)return false;
            var record=journal.Read(original.Operation);return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Prepared&&Current(player,original);}
        catch{return false;}
    }
    internal static bool TryPrepare(EntityPlayer player,RebirthHydrationTransferOriginal original,out string error)
    {
        error="Current authenticated hydration original required";
        try
        {
            if(!Current(player,original))return false;
            // Enforce namespace-spanning admission at the durable producer too,
            // including callers that do not enter through a UI conductor.
            if(!TryReadPending(player,out var pending,out _)||pending!=null&&pending.Digest!=original.Digest||
                !Current(player,original))return false;
            var journal=Open(original,true);Guid group=Group(original.World,original.Owner,original.Creation);
            journal.AssertAvailable(group,original.Operation);
            byte[] bytes=Bytes(original);
            var record=journal.Prepare(original.Operation,group,original.Creation,original.Revision,bytes,bytes);
            if(!Current(player,original)||!SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))||
                !Matches(record,original)||!Matches(journal.Read(original.Operation),original))return false;
            error=string.Empty;return true;
        }
        catch(Exception exception){error="Hydration original retained: "+exception.GetType().Name;return false;}
    }
    // Idempotent metadata WAL, never permission to repeat an entered native call.
    internal static bool TryMarkAttempted(EntityPlayer player,RebirthHydrationTransferOriginal original,out string error)
    {
        error="Original prepared hydration intent required";
        try
        {
            if(!Current(player,original))return false;
            var journal=Open(original,false);if(journal==null)return false;
            journal.AssertAvailable(Group(original.World,original.Owner,original.Creation),original.Operation);
            var record=journal.Read(original.Operation);
            if(!Matches(record,original))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            else if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
            record=journal.Read(original.Operation);
            if(!Current(player,original)||!SamePath(journal.DirectoryPath,DirectoryPath(original.World,original.Owner,original.Creation))||
                !Matches(record,original)||record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
            error=string.Empty;return true;
        }
        catch(Exception exception){error="Hydration attempt metadata retained: "+exception.GetType().Name;return false;}
    }
    // Read-only original lookup. An earlier life can be observed, never admitted
    // by this description lookup as a current financial owner action.
    internal static bool TryReadPending(EntityPlayer player,out RebirthHydrationTransferOriginal original,out string error)
    {
        original=null;error="Actual hydration owner scope required";
        try
        {
            if(!Owner(player,out var world,out var owner,out var creation))return false;
            string path=DirectoryPath(world,owner,creation);RequireSafe(path);
            var journal=Directory.Exists(path)?new RebirthCamperTransferJournal(path):null;
            // Validate every recognized history/candidate before FindPending.
            int activeRecords=0;
            if(journal!=null)foreach(var history in journal.ReadForIndex())
            {
                if(++activeRecords>16384)throw new InvalidDataException("Hydration active history discovery bound exceeded");
                if(history.State!=RebirthCamperTransferJournal.Phase.Applied)continue;
                var applied=Decode(history.Before);
                if(!Matches(history,applied)||applied.World!=world||applied.Owner!=owner||applied.Creation!=creation)
                    throw new InvalidDataException("Hydration applied original namespace mismatch");
                if(HasSavedCompletion(applied))continue;
                if(original!=null)throw new InvalidDataException("Multiple hydration completions require reconciliation");
                original=applied;
            }
            var record=journal?.FindPending(Group(world,owner,creation));
            if(record!=null)
            {
                var candidate=Decode(record.Before);
                if(candidate.World!=world||candidate.Owner!=owner||candidate.Creation!=creation||!Matches(record,candidate))return false;
                if(original!=null)throw new InvalidDataException("Pending hydration and unresolved completion require reconciliation");
                original=candidate;
            }
            // Namespace rotation must not hide an unresolved financial original.
            // Discovery grants no adoption/replay/release of an old generation.
            if(!DiscoverOtherGeneration(world,owner,path,ref original))return false;
            if(!Owner(player,out var finalWorld,out var finalOwner,out var finalCreation)||finalWorld!=world||finalOwner!=owner||finalCreation!=creation||
                !SamePath(path,DirectoryPath(world,owner,creation))){original=null;return false;}
            error=string.Empty;return true;
        }
        catch(Exception exception){error="Hydration pending original retained: "+exception.GetType().Name;return false;}
    }
    private static bool DiscoverOtherGeneration(Guid world,string owner,string currentPath,ref RebirthHydrationTransferOriginal pending)
    {
        string parent=Path.GetDirectoryName(currentPath);RequireSafeAncestors(parent);
        if(!Directory.Exists(parent))return true;
        int scopes=0,records=0;
        foreach(string directory in Directory.EnumerateFileSystemEntries(parent))
        {
            if(++scopes>4096)throw new InvalidDataException("Hydration scope discovery bound exceeded");
            string name=Path.GetFileName(directory);
            if(!Guid.TryParseExact(name,"N",out var group)||name!=group.ToString("N")||!Directory.Exists(directory))
                throw new InvalidDataException("Unrecognized hydration scope candidate");
            RequireSafe(directory);if(SamePath(directory,currentPath))continue;
            var journal=new RebirthCamperTransferJournal(directory);
            foreach(var record in journal.ReadForIndex())
            {
                if(++records>16384)throw new InvalidDataException("Hydration history discovery bound exceeded");
                var original=Decode(record.Before);
                if(!Matches(record,original)||!SamePath(directory,DirectoryPath(original.World,original.Owner,original.Creation)))
                    throw new InvalidDataException("Hydration original namespace mismatch");
                if(original.World!=world||original.Owner!=owner||record.State==RebirthCamperTransferJournal.Phase.Rejected||
                    record.State==RebirthCamperTransferJournal.Phase.Applied&&HasSavedCompletion(original))continue;
                if(pending!=null)throw new InvalidDataException("Multiple hydration generations require reconciliation");
                pending=original;
            }
        }
        return true;
    }
    private static void RequireSafeAncestors(string path)
    {
        for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Hydration journal reparse scope refused");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    private static bool Current(EntityPlayer player,RebirthHydrationTransferOriginal original)=>original!=null&&
        Owner(player,out var world,out var owner,out var creation)&&!player.IsDead()&&original.Matches(world,owner,creation,player.Died);
    private static bool Owner(EntityPlayer player,out Guid world,out string owner,out string creation)
    {
        world=Guid.Empty;owner=creation=null;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||connection?.IsServer!=true||player?.world==null||player.world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            !Guid.TryParse(player.world.Guid,out world)||world==Guid.Empty||
            !RebirthHydrationTransferScope.TryObserve(player,out creation))return false;
        RebirthStablePlayerIdentity identity;
        if(player is EntityPlayerLocal)
        {
            if(!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return false;
        }
        else
        {
            var peer=connection.Clients?.ForEntityId(player.entityId);
            if(peer==null||!peer.loginDone||peer.disconnecting||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out identity))return false;
        }
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var canonical)||canonical.StorageKey!=identity.StorageKey)return false;
        owner=identity.StorageKey;return true;
    }
    private static RebirthCamperTransferJournal Open(RebirthHydrationTransferOriginal original,bool create)
    {
        string path=DirectoryPath(original.World,original.Owner,original.Creation);RequireSafe(path);
        if(!create&&!Directory.Exists(path))return null;
        var journal=new RebirthCamperTransferJournal(path);RequireSafe(path);
        foreach(var ignored in journal.ReadForIndex()){}
        return journal;
    }
    private static Guid Group(Guid world,string owner,string creation)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {byte[] bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(world.ToString("N")+"|"+owner.Length+":"+owner+"|"+creation));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}
    }
    private static string DirectoryPath(Guid world,string owner,string creation)
    {
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(save))throw new InvalidDataException("Actual save path unavailable");
        return Path.Combine(Path.GetFullPath(save),"REBIRTH","HydrationTransfers",Group(world,owner,creation).ToString("N"));
    }
    private static void RequireSafe(string path)
    {
        for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
            try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Hydration journal reparse scope refused");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        int candidates=0;
        if(Directory.Exists(path))foreach(string file in Directory.EnumerateFileSystemEntries(path))
        {
            if(++candidates>65536)throw new InvalidDataException("Hydration candidate discovery bound exceeded");
            var info=new FileInfo(file);string name=Path.GetFileName(file);
            if(name.Length!=38||name.Substring(34)!=".rcj"||name[32]!='.'||name[33]<'0'||name[33]>'3'||
                !Guid.TryParseExact(name.Substring(0,32),"N",out var operation)||name.Substring(0,32)!=operation.ToString("N")||
                !File.Exists(file)||(info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length>2L*MaximumOriginalBytes+8192)
                throw new InvalidDataException("Hydration journal candidate requires reconciliation");
        }
    }
    private static bool SamePath(string first,string second)=>string.Equals(first,second,
        Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    private static byte[] Bytes(RebirthHydrationTransferOriginal original)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(original.Write().ToString(SaveOptions.DisableFormatting));
        if(bytes.Length<1||bytes.Length>MaximumOriginalBytes)throw new InvalidDataException("Hydration original frame bound");return bytes;
    }
    private static RebirthHydrationTransferOriginal Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumOriginalBytes)throw new InvalidDataException("Hydration original frame bound");
        using(var stream=new MemoryStream(bytes,false))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{
            DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumOriginalBytes}))
        {
            if(!RebirthHydrationTransferOriginal.TryRead(XElement.Load(reader),out var original)||!bytes.SequenceEqual(Bytes(original)))
                throw new InvalidDataException("Hydration journal original differs");return original;
        }
    }
    private static bool Matches(RebirthCamperTransferJournal.Record record,RebirthHydrationTransferOriginal original)=>
        record!=null&&record.Operation==original.Operation&&record.Vehicle==Group(original.World,original.Owner,original.Creation)&&
        record.OwnerCreation==original.Creation&&record.Revision==original.Revision&&record.Before.SequenceEqual(Bytes(original))&&record.After.SequenceEqual(Bytes(original));
}
