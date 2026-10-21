using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // This is an actual primary native Load -> Read -> ToPlayer event. It is
    // only one side of restart admission: a saved station original and the
    // immutable operation ledger must independently qualify the other side.
    internal sealed class LoadedOwner:IDisposable
    {
        internal PlayerDataFile data;private IDisposable memory;internal Stream stream;
        internal byte[] native,buffs;internal ItemStack[] bag,belt;
        internal readonly World world;internal readonly string save,path,account;
        internal EntityPlayerLocal actor;internal string owner,creation;internal int deaths;
        internal bool readEntered,readReturned,loaded,entering,hydrated,closed,registered;
        internal Guid[] Pending;
        private LoadedOwner(PlayerDataFile data,string path,string account,IDisposable memory)
        {this.data=data;this.path=path;this.account=account;this.memory=memory;world=GameManager.Instance.World;save=Path.GetFullPath(GameIO.GetSaveGameDir());}
        internal static LoadedOwner Begin(PlayerDataFile data,string path,string account,IDisposable memory)=>new LoadedOwner(data,path,account,memory);
        internal bool Current()
        {
            // Full primary bytes were checked at actual Read/Load and again
            // at actual ToPlayer. Later legitimate native saves need not keep
            // the old player position/clock file equal. The retained event is
            // historical; live full owner grids/life and exact operation stamps
            // are corroborated separately before any original can qualify.
            try{bool current=!closed&&hydrated&&loaded&&readReturned&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
                ReferenceEquals(world.GetPrimaryPlayer(),actor)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.IsSpawned()&&!actor.IsDead()&&
                actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==owner&&identity.CanonicalId==account&&
                SameOwnerGrid(actor.bag.ItemGrid.items,bag)&&SameOwnerGrid(actor.inventory.ItemGrid.items,belt);
                if(current)registered=true;return current;}
            catch{return false;}
        }
        // The caller supplies the retained full original; this method cannot
        // construct an original or interpret an absent operation as unused.
        internal bool MatchesOriginalCheckpoint(Original original,int phase,int completed)
        {
            try{return Current()&&original!=null&&original.Owner==owner&&original.CanonicalOwner==account&&original.Creation==creation&&original.Deaths==deaths&&
                ReferenceEquals(original.World,world)&&ReferenceEquals(original.Actor,actor)&&original.Directory==save&&ValidOwnerCheckpointStage(phase,completed,original.Slots.Length)&&
                PhaseBytes(buffs,original.Receipt,out float receipt)&&receipt==phase&&PhaseBytes(buffs,original.Checkpoint,out float slot)&&slot==completed&&
                actor.Buffs.GetCustomVar(original.Receipt)==phase&&actor.Buffs.GetCustomVar(original.Checkpoint)==completed&&
                original.MatchesOwnerCheckpoint(bag,belt,completed);}
            catch{return false;}
        }
        public void Dispose()
        {
            if(closed)return;closed=true;loadedOwners.Remove(this);if(data!=null)loadedOwnerFiles.Remove(data);
            data=null;actor=null;stream=null;native=buffs=null;bag=belt=null;Pending=null;memory?.Dispose();memory=null;
        }
    }
    private static readonly List<LoadedOwner> loadedOwners=new List<LoadedOwner>();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerDataFile,LoadedOwner> loadedOwnerFiles=
        new System.Runtime.CompilerServices.ConditionalWeakTable<PlayerDataFile,LoadedOwner>();
    [ThreadStatic]private static LoadedOwner loadingRecyclerOwner;
    private static bool SameOwnerGrid(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Zip(b,(x,y)=>Same(x,y)).All(value=>value);
    private static bool CompletedOwnerRead(Stream actual,Stream expected,bool run)
    {
        return run&&actual!=null&&ReferenceEquals(actual,expected)&&actual.CanSeek&&actual.Position==actual.Length;
    }
    internal static object EnterRecyclerOwnerLoad(PlayerDataFile data,string directory,string account,bool run)
    {
        if(!authorityInstalled||!run||!ThreadManager.IsMainThread()||loadingRecyclerOwner!=null||GameManager.Instance?.World is not World world||world.IsRemote())return null;
        try
        {
            foreach(var old in loadedOwners.Where(value=>!ReferenceEquals(value.world,world)).ToArray())old.Dispose();
            // Discovery selects candidates only. No file, stamp, or parsed XML
            // can issue this token without the actual native primary read.
            string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Survivor","WorkstationCompletions","recyclerPlaced");
            if(!Directory.Exists(root)||loadedOwners.Count>=4||data==null||string.IsNullOrEmpty(account)||
                Path.GetFileName(account)!=account||Path.GetFullPath(directory)!=Path.GetFullPath(GameIO.GetPlayerDataDir()))return null;
            string path=Path.GetFullPath(Path.Combine(directory,account+"."+PlayerDataFile.EXT));SafeOwnerPath(path);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return null;
            try{var source=LoadedOwner.Begin(data,path,account,memory);loadingRecyclerOwner=source;return source;}
            catch{memory.Dispose();throw;}
        }
        catch{return null;}
    }
    internal static void ObserveRecyclerOwnerOpen(string path,Stream stream,bool run)
    {
        var source=loadingRecyclerOwner;if(!run||source==null||source.stream!=null||stream==null)return;
        try{if(Path.GetFullPath(path)==source.path&&stream.CanRead&&stream.CanSeek&&stream.Position==0){SafeOwnerPath(path);source.stream=stream;}}
        catch{}
    }
    internal static object EnterRecyclerOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool run)
    {
        var source=loadingRecyclerOwner;
        if(!run||source==null||source.readEntered||!ReferenceEquals(source.data,data)||version!=PlayerDataFile.cFileVersion||mode!=global::StreamModeRead.Persistency)return null;
        try
        {
            var stream=reader?.BaseStream;if(stream==null||!ReferenceEquals(stream,source.stream)||!stream.CanSeek||stream.Position!=5||stream.Length<6||stream.Length>16*1024*1024)return null;
            long position=stream.Position;var bytes=new byte[checked((int)stream.Length)];
            try{stream.Position=0;int offset=0;while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count<1)return null;offset+=count;}}
            finally{stream.Position=position;}
            if(bytes[0]!='t'||bytes[1]!='t'||bytes[2]!='p'||bytes[3]!=0||bytes[4]!=PlayerDataFile.cFileVersion)return null;
            source.native=bytes;source.readEntered=true;return source;
        }
        catch{return null;}
    }
    internal static void ObserveRecyclerOwnerRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is not LoadedOwner source)return;
        try{source.readReturned=ReferenceEquals(loadingRecyclerOwner,source)&&CompletedOwnerRead(reader?.BaseStream,source.stream,run);}
        catch{source.readReturned=false;}
    }
    internal static void ObserveRecyclerOwnerLoad(PlayerDataFile data,bool run,object state)
    {
        if(state is not LoadedOwner source)return;
        try
        {
            if(!run||!ReferenceEquals(loadingRecyclerOwner,source)||!ReferenceEquals(source.data,data)||!source.readReturned||!data.bLoaded||data.bDead||
                !NativeOwnerImage(data).SequenceEqual(source.native)||!MatchesOwnerFile(source.path,source.native))return;
            source.bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);source.belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
            if(source.bag==null||source.belt==null||data.buffData==null||data.buffData.Length>1024*1024)return;source.buffs=data.buffData.ToArray();
            if(loadedOwnerFiles.TryGetValue(data,out var old))old.Dispose();loadedOwnerFiles.Add(data,source);loadedOwners.Add(source);source.loaded=true;
        }
        catch{}
    }
    internal static void EndRecyclerOwnerLoad(object state)
    {
        if(state is not LoadedOwner source)return;if(ReferenceEquals(loadingRecyclerOwner,source))loadingRecyclerOwner=null;
        source.stream=null;if(!source.loaded)source.Dispose();
    }
    internal static object EnterRecyclerOwnerHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool run)
    {
        var joined=EnterJoinedRecyclerOwner(data,actor,mode,run);if(joined!=null)return joined;
        try{var remote=RemoteLoadedOwner.Enter(data,actor,mode,run);if(remote!=null)return remote;}catch{/* No remote native load source. */}
        if(!run||mode!=global::StreamModeRead.Persistency||actor is not EntityPlayerLocal local||!loadedOwnerFiles.TryGetValue(data,out var source)||
            source.closed||source.hydrated||source.entering||!source.loaded||!ReferenceEquals(local.world,source.world))return null;
        try{if(!NativeOwnerImage(data).SequenceEqual(source.native)||!MatchesOwnerFile(source.path,source.native))return null;source.actor=local;source.entering=true;return source;}
        catch{return null;}
    }
    internal static void ObserveRecyclerOwnerHydration(PlayerDataFile data,EntityPlayer actor,bool run,object state)
    {
        if(state is RecyclerJoining joined){ObserveJoinedRecyclerOwner(data,actor,run,joined);return;}
        if(state is RemoteLoadedOwner remote){ObserveRemoteLoadedRecyclerOwner(data,actor,run,remote);return;}
        if(state is not LoadedOwner source)return;
        try
        {
            if(!run||!source.entering||!ReferenceEquals(source.data,data)||!ReferenceEquals(source.actor,actor)||!ReferenceEquals(GameManager.Instance?.World,source.world)||
                !ReferenceEquals(actor.world,source.world)||actor.IsDead()||actor.Died!=data.deaths||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.CanonicalId!=source.account||
                !SameOwnerGrid(source.actor.bag.ItemGrid.items,source.bag)||!SameOwnerGrid(source.actor.inventory.ItemGrid.items,source.belt)||
                !MatchesOwnerFile(source.path,source.native))return;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(string.IsNullOrEmpty(creation))return;
            source.owner=identity.StorageKey;source.creation=creation;source.deaths=actor.Died;source.hydrated=true;
            source.Pending=FindLoadedOwnerPending(source);
        }
        catch{}
    }
    internal static void EndRecyclerOwnerHydration(object state)
    {if(state is RecyclerJoining)return;if(state is RemoteLoadedOwner remote){EndRemoteLoadedRecyclerOwner(remote);return;}if(state is LoadedOwner source){source.entering=false;if(!source.hydrated)source.actor=null;}}
    internal static bool TryObserveLoadedRecyclerOwner(EntityPlayerLocal actor,out LoadedOwner source)
    {source=loadedOwners.FirstOrDefault(value=>ReferenceEquals(value.actor,actor)&&value.Current());return source!=null;}
    private static void SweepLoadedRecyclerOwners(World world)
    {
        SweepRemoteLoadedRecyclerOwners(world);
        SweepJoinedRecyclerOwners(world);
        foreach(var source in loadedOwners.Where(value=>!ReferenceEquals(value.world,world)||value.registered&&
            (!ReferenceEquals(world?.GetPrimaryPlayer(),value.actor)||!ReferenceEquals(world?.GetEntity(value.actor.entityId),value.actor))).ToArray())source.Dispose();
    }
    private static bool LoadedOwnerHeld(EntityPlayerLocal actor)=>actor!=null&&loadedOwners.Any(source=>!source.closed&&source.hydrated&&source.Pending?.Length>0&&
        ReferenceEquals(source.actor,actor)&&ReferenceEquals(source.world,GameManager.Instance?.World)&&ReferenceEquals(source.world.GetPrimaryPlayer(),actor)&&
        ReferenceEquals(source.world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==source.deaths&&
        RebirthCamperCharacterAccess.CurrentCreation(actor)==source.creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==source.owner);
}
