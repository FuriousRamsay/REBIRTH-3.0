using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static partial class RebirthRecipeOutputIntegration
{
    // Actual local native Load -> Read -> ToPlayer lineage, independent of all
    // other financial purposes using the shared native observation dispatchers.
    private sealed class UiOutputLoadedOwner:IDisposable
    {
        internal PlayerDataFile Data;internal World World;internal EntityPlayerLocal Actor;
        internal string Save,Path,Account,Owner,Creation,Queue;internal int Deaths,TerminalCursor;
        internal Stream Stream;internal byte[] Native,Bag,Belt,Equipment,Buffs;internal Guid[] Operations;
        internal bool ReadEntered,ReadReturned,Loaded,Hydrating,Hydrated,Closed;internal IDisposable Memory;
        internal int StationOperations;
        internal bool Current()=>!Closed&&Hydrated&&Loaded&&ReadReturned&&ThreadManager.IsMainThread()&&
            ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&Actor.IsSpawned()&&!Actor.IsDead()&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner&&owner.CanonicalId==Account&&
            Bag.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))&&Belt.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))&&
            Equipment.SequenceEqual(OwningEquipmentNative(Actor));
        public void Dispose()
        {
            if(Closed)return;Closed=true;DisposeStationOutputNativeLoads(this);uiOutputLoadedOwners.Remove(this);if(Data!=null)uiOutputLoadedFiles.Remove(Data);
            Data=null;Actor=null;Stream=null;Native=Bag=Belt=Equipment=Buffs=null;Operations=null;Memory?.Dispose();Memory=null;
        }
    }
    private static readonly List<UiOutputLoadedOwner> uiOutputLoadedOwners=new List<UiOutputLoadedOwner>();
    private static readonly ConditionalWeakTable<PlayerDataFile,UiOutputLoadedOwner> uiOutputLoadedFiles=new ConditionalWeakTable<PlayerDataFile,UiOutputLoadedOwner>();
    [ThreadStatic]private static UiOutputLoadedOwner loadingUiOutputOwner;
    private static void SweepUiOutputLoadedOwners(World world)
    {foreach(var source in uiOutputLoadedOwners.Where(value=>!ReferenceEquals(value.World,world)).ToArray())source.Dispose();}
    internal static object EnterRecipeOutputOwnerLoad(PlayerDataFile data,string directory,string account,bool ran)
    {
        IDisposable memory=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!installed||!ran||data==null||!ThreadManager.IsMainThread()||loadingUiOutputOwner!=null||world==null||world.IsRemote())return null;
            SweepUiOutputLoadedOwners(world);
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.GetFullPath(GameIO.GetPlayerDataDir());
            if(Path.GetFullPath(directory)!=root||string.IsNullOrEmpty(account)||Path.GetFileName(account)!=account||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=account||
                !Directory.Exists(Path.Combine(save,"rebirth","recipe-outputs","ui")))return null;
            // Read-only callers may load the local file without ever invoking
            // ToPlayer. Such unconsumed observations cannot monopolize slots.
            foreach(var old in uiOutputLoadedOwners.Where(value=>value.Account==account&&!value.Hydrated&&!value.Hydrating).ToArray())old.Dispose();
            if(uiOutputLoadedOwners.Count>=4)return null;
            string path=Path.GetFullPath(Path.Combine(root,account+"."+PlayerDataFile.EXT));
            if(Path.GetDirectoryName(path)!=root||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out memory))return null;
            var source=new UiOutputLoadedOwner{Data=data,World=world,Save=save,Path=path,Account=account,Owner=owner.StorageKey,Memory=memory};memory=null;
            loadingUiOutputOwner=source;return source;
        }
        catch{return null;}
        finally{memory?.Dispose();}
    }
    internal static void ObserveRecipeOutputOwnerOpen(string path,Stream stream,bool ran)
    {
        var source=loadingUiOutputOwner;
        try{if(ran&&source!=null&&source.Stream==null&&stream!=null&&stream.CanRead&&stream.CanSeek&&stream.Position==0&&Path.GetFullPath(path)==source.Path&&
            RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))source.Stream=stream;}catch{}
    }
    internal static object EnterRecipeOutputOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        var source=loadingUiOutputOwner;
        try
        {
            var stream=reader?.BaseStream;
            if(!ran||source==null||source.ReadEntered||!ReferenceEquals(source.Data,data)||version!=PlayerDataFile.cFileVersion||mode!=global::StreamModeRead.Persistency||
                stream==null||!ReferenceEquals(source.Stream,stream)||!stream.CanSeek||stream.Position!=5||stream.Length<6||stream.Length>16*1024*1024)return null;
            long position=stream.Position;var native=new byte[checked((int)stream.Length)];
            try{stream.Position=0;int offset=0;while(offset<native.Length){int count=stream.Read(native,offset,native.Length-offset);if(count<1)return null;offset+=count;}}
            finally{stream.Position=position;}
            if(native[0]!='t'||native[1]!='t'||native[2]!='p'||native[3]!=0||native[4]!=PlayerDataFile.cFileVersion)return null;
            source.Native=native;source.ReadEntered=true;return source;
        }
        catch{return null;}
    }
    internal static void ObserveRecipeOutputOwnerRead(PooledBinaryReader reader,bool ran,object state)
    {
        if(state is not UiOutputLoadedOwner source)return;
        try{source.ReadReturned=ran&&ReferenceEquals(loadingUiOutputOwner,source)&&ReferenceEquals(reader?.BaseStream,source.Stream)&&source.Stream.Position==source.Stream.Length;}
        catch{source.ReadReturned=false;}
    }
    internal static void ObserveRecipeOutputOwnerLoad(PlayerDataFile data,bool ran,object state)
    {
        if(state is not UiOutputLoadedOwner source)return;
        try
        {
            if(!ran||!ReferenceEquals(source,loadingUiOutputOwner)||!ReferenceEquals(source.Data,data)||!source.ReadReturned||!data.bLoaded||data.bDead||
                !source.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data))||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(source.Path,source.Native)||data.buffData==null||data.buffData.Length>1024*1024)return;
            var buffs=data.buffData.ToArray();if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(buffs,out var operations))return;
            source.Bag=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true));source.Belt=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false));
            source.Equipment=data.equipmentData?.ToArray();if(source.Equipment==null)return;
            source.Buffs=buffs;source.Operations=operations;source.Queue=RebirthRemoteResourceClientOriginal.QueueImage(data.craftingData);source.Deaths=data.deaths;
            source.StationOperations=PrepareStationOutputNativeLoads(source);
            if(operations.Length==0&&source.StationOperations==0)return;
            if(uiOutputLoadedFiles.TryGetValue(data,out var old))old.Dispose();uiOutputLoadedFiles.Add(data,source);uiOutputLoadedOwners.Add(source);source.Loaded=true;
        }
        catch{/* Neither a parsed marker nor a sidecar file can substitute for actual Load. */}
    }
    internal static void EndRecipeOutputOwnerLoad(object state)
    {
        if(state is not UiOutputLoadedOwner source)return;
        if(ReferenceEquals(loadingUiOutputOwner,source))loadingUiOutputOwner=null;source.Stream=null;if(!source.Loaded)source.Dispose();
    }
    internal static object EnterRecipeOutputOwnerHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        ObservePersonalOutputRecoveryInput(data,actor,mode,ran);
        try
        {
            if(!ran||!ThreadManager.IsMainThread()||mode!=global::StreamModeRead.Persistency||actor is not EntityPlayerLocal local||
                !uiOutputLoadedFiles.TryGetValue(data,out var source)||source.Closed||source.Hydrated||source.Hydrating||!source.Loaded||
                !ReferenceEquals(source.World,actor.world)||!source.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data))||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(source.Path,source.Native))return null;
            source.Actor=local;source.Hydrating=true;return source;
        }
        catch{return null;}
    }
    internal static void ObserveRecipeOutputOwnerHydration(PlayerDataFile data,EntityPlayer actor,bool ran,object state)
    {
        if(state is not UiOutputLoadedOwner source)return;
        try
        {
            if(!ran||!source.Hydrating||!ReferenceEquals(source.Data,data)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(GameManager.Instance?.World,source.World)||
                !ReferenceEquals(actor.world,source.World)||actor.IsDead()||actor.Died!=source.Deaths||
                !source.Bag.SequenceEqual(OwningGrid(source.Actor.bag.ItemGrid.items))||!source.Belt.SequenceEqual(OwningGrid(source.Actor.inventory.ItemGrid.items))||
                !source.Equipment.SequenceEqual(OwningEquipmentNative(source.Actor))||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                owner.StorageKey!=source.Owner||owner.CanonicalId!=source.Account||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(source.Path,source.Native))return;
            source.Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(string.IsNullOrEmpty(source.Creation))return;
            foreach(var operation in source.Operations)
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,PersonalOutputReceipt(operation),out float phase,out bool present)||!present||
                    source.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))!=phase)return;
            source.Hydrated=true;
            // The new actual hydration supersedes only an older observation
            // for this same native account. Original sidecars and unresolved effects are
            // untouched; an old load event never becomes a fresh action.
            foreach(var old in uiOutputLoadedOwners.Where(value=>!ReferenceEquals(value,source)&&ReferenceEquals(value.World,source.World)&&value.Account==source.Account&&value.Owner==source.Owner).ToArray())old.Dispose();
        }
        catch{/* No output setter or receipt rewrite is performed by native hydration observation. */}
    }
    internal static void EndRecipeOutputOwnerHydration(object state)
    {if(state is UiOutputLoadedOwner source){source.Hydrating=false;if(!source.Hydrated)source.Dispose();}}
}
