using System;
using System.IO;
using System.Linq;
using System.Text;

// Owning-client descriptions only. This journal cannot issue a grant: native
// saved owner images and the authenticated host still qualify every reentry.
internal static partial class RebirthHydrationClientOriginals
{
    private sealed class Session
    {
        private readonly EntityPlayerLocal player;private readonly World nativeWorld;private readonly GameManager game;
        private readonly ConnectionManager manager;private readonly object peer;private readonly int deaths;
        private readonly Guid world;private readonly string owner,creation,path,root;
        internal Session(EntityPlayerLocal player,Guid world,string owner,string creation,string path,string root)
        {this.player=player;this.world=world;this.owner=owner;this.creation=creation;this.path=path;this.root=root;nativeWorld=player.world;
            game=GameManager.Instance;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;peer=manager.connectionToServer[0];deaths=player.Died;}
        private bool NativeCurrent()=>ReferenceEquals(player.world,nativeWorld)&&ReferenceEquals(game,GameManager.Instance)&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&
            ReferenceEquals(manager.connectionToServer[0],peer)&&player.Died==deaths;
        internal bool Current()=>NativeCurrent()&&RebirthHydrationClientOriginals.Current(player,world,owner,creation,path);
        internal bool IdleCurrent(EntityPlayerLocal candidate)=>ReferenceEquals(player,candidate)&&NativeCurrent()&&
            TryIdentify(player,out var currentWorld,out string currentOwner,out string currentCreation,out string currentRoot)&&
            world==currentWorld&&owner==currentOwner&&creation==currentCreation&&root==currentRoot;
    }
    private static bool running;
    private static Session emptyRecoveryScope;
    internal static void ClearIdleRecovery()=>emptyRecoveryScope=null;
    internal static bool Prepare(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
        =>With(player,original,false);
    internal static bool Close(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
        =>With(player,original,true);
    private static bool With(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,bool close)
    {
        if(running)return false;running=true;
        ClearIdleRecovery();
        try
        {
            if(!Scope(player,out var world,out var owner,out var creation,out var path,out var root)||original==null||
                !original.Matches(world,owner,creation,player.Died))return false;
            var session=new Session(player,world,owner,creation,path,root);
            var journal=new RebirthCamperTransferJournal(path);var bytes=Bytes(original);
            var record=journal.Read(original.Operation);
            if(record==null)
            {
                if(close||!ReadPending(journal,world,owner,out var pending)||pending!=null)return false;
                record=journal.Prepare(original.Operation,Group(original),original.Creation,original.Revision,bytes,bytes);
            }
            if(!Matches(record,original)||!session.Current())return false;
            if(close)
            {
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                    record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                if(!session.Current())return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                    record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            }
            return Matches(journal.Read(original.Operation),original)&&
                (!close||record.State==RebirthCamperTransferJournal.Phase.Applied)&&session.Current();
        }
        catch(Exception error)
        {
            RebirthDebug.Log("metabolism",$"[REBIRTH HydrationTransfer] owner journal {(close?"close":"prepare")} failed: {error.GetType().Name}");
            return false;
        }
        finally{running=false;}
    }
    // Idle recovery caches only a positively checked empty journal. Explicit
    // proposal and native reentry still read their original evidence fresh.
    internal static bool TryReadForIdleRecovery(EntityPlayerLocal player,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try { if(emptyRecoveryScope?.IdleCurrent(player)==true)return true; }
        catch { ClearIdleRecovery(); }
        return TryRead(player,out original);
    }
    internal static bool TryRead(EntityPlayerLocal player,out RebirthHydrationTransferOriginal original)
    {
        original=null;if(running)return false;running=true;
        ClearIdleRecovery();
        try
        {
            if(!Scope(player,out var world,out var owner,out var creation,out var path,out var root))return false;
            var session=new Session(player,world,owner,creation,path,root);
            if(Directory.Exists(path)&&!ReadPending(new RebirthCamperTransferJournal(path),world,owner,out original))return false;
            if(!session.Current())return false;
            if(original==null)emptyRecoveryScope=session;
            return true;
        }
        catch{original=null;return false;}
        finally{running=false;}
    }
    private static bool ReadPending(RebirthCamperTransferJournal journal,Guid world,string owner,out RebirthHydrationTransferOriginal original)
    {
        original=null;int count=0;
        foreach(var record in journal.ReadForIndex())
        {
            if(++count>16384)throw new InvalidDataException("Hydration client history bound exceeded");
            var candidate=Decode(record.Before);
            if(!Matches(record,candidate)||candidate.World!=world||candidate.Owner!=owner)throw new InvalidDataException("Hydration client namespace changed");
            if(record.State==RebirthCamperTransferJournal.Phase.Applied)continue;
            if(record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain||original!=null)
                throw new InvalidDataException("Hydration client original requires reconciliation");
            original=candidate;
        }
        return true;
    }
    private static byte[] Bytes(RebirthHydrationTransferOriginal original)=>Encoding.UTF8.GetBytes(original.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
    private static RebirthHydrationTransferOriginal Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>1024*1024)throw new InvalidDataException("Hydration client original size");
        using(var stream=new MemoryStream(bytes,false))using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{
            DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024}))
        {
            if(!RebirthHydrationTransferOriginal.TryRead(System.Xml.Linq.XElement.Load(reader),out var original))throw new InvalidDataException("Hydration client original malformed");
            return original;
        }
    }
    private static bool Matches(RebirthCamperTransferJournal.Record record,RebirthHydrationTransferOriginal original)=>
        record!=null&&original!=null&&record.Operation==original.Operation&&record.Vehicle==Group(original)&&
        record.OwnerCreation==original.Creation&&record.Revision==original.Revision&&record.Before.SequenceEqual(Bytes(original))&&record.After.SequenceEqual(Bytes(original));
    private static Guid Group(RebirthHydrationTransferOriginal original)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {var digest=hash.ComputeHash(Encoding.UTF8.GetBytes(original.World.ToString("N")+"|"+original.Owner+"|"+original.Creation));var bytes=new byte[16];Array.Copy(digest,bytes,16);return new Guid(bytes);}
    }
    private static bool TryIdentify(EntityPlayerLocal player,out Guid world,out string owner,out string creation,out string root)
    {
        world=Guid.Empty;owner=creation=root=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||manager==null||manager.IsServer||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
            manager.connectionToServer==null||manager.connectionToServer.Length==0||manager.connectionToServer[0]==null||manager.connectionToServer[0].IsDisconnected()||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out world)||world==Guid.Empty||
            !RebirthHydrationTransferScope.TryObserve(player,out creation)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        owner=identity.StorageKey;root=GameIO.GetPlayerDataDir();if(string.IsNullOrWhiteSpace(root))return false;
        root=Path.GetFullPath(root);
        return true;
    }
    private static bool Scope(EntityPlayerLocal player,out Guid world,out string owner,out string creation,out string path,out string root)
    {
        path=null;
        if(!TryIdentify(player,out world,out owner,out creation,out root))return false;
        string legacy,compact;
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {
            string account=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-",string.Empty).ToLowerInvariant();
            legacy=Path.Combine(root,"REBIRTH","HydrationClientOriginals",world.ToString("N"),account);
            var scopeHash=hash.ComputeHash(Encoding.UTF8.GetBytes(world.ToString("N")+"|"+owner));
            string scope=BitConverter.ToString(scopeHash,0,20).Replace("-",string.Empty).ToLowerInvariant();
            compact=Path.Combine(root,"RBH",scope);
        }
        if(!SafePath(legacy)||!SafePath(compact))return false;
        // Keep existing history in place. An empty legacy directory can be left
        // by a failed first write and does not own any transaction.
        bool hasLegacy=Directory.Exists(legacy)&&Directory.EnumerateFileSystemEntries(legacy).Any();
        if(hasLegacy&&Directory.Exists(compact)&&Directory.EnumerateFileSystemEntries(compact).Any())return false;
        path=hasLegacy?legacy:compact;
        return true;
    }
    private static bool SafePath(string path)
    {
        for(string next=path;!string.IsNullOrEmpty(next);next=Path.GetDirectoryName(next))
            try{if((File.GetAttributes(next)&FileAttributes.ReparsePoint)!=0)return false;}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        return true;
    }
    private static bool Current(EntityPlayerLocal player,Guid world,string owner,string creation,string path)=>
        Scope(player,out var nextWorld,out var nextOwner,out var nextCreation,out var nextPath,out _)&&world==nextWorld&&owner==nextOwner&&creation==nextCreation&&path==nextPath;
}
