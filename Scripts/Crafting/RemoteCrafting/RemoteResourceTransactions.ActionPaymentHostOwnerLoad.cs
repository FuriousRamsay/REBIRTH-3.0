using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

public static partial class RemoteResourceTransactions
{
    internal sealed class ActionHostSpawn
    {internal ClientInfo Peer;internal World World;internal string Account,Save;internal INetConnection Connection;internal bool Returned,Local;}
    internal sealed class ActionHostOwnerLoad
    {
        internal WeakReference<PlayerDataFile> Data;internal ActionHostSpawn Spawn;internal string Path;
        internal Stream Source;internal PooledBinaryReader Reader;internal byte[] Native,Bag,Belt,Buffs;
        internal int NativeId,Deaths;internal bool ReadEntered,ReadReturned,Loaded,HydrationEntered,Hydrated,Joined;internal volatile bool RetireRequested;
        internal EntityPlayer Actor;internal ItemStackGrid BagGrid,BeltGrid;internal ItemStack[] BagReferences,BeltReferences;internal IDisposable Memory;
        internal long TerminalScanNext;internal readonly HashSet<Guid> TerminalMetadataDone=new HashSet<Guid>();
    }
    [ThreadStatic]private static ActionHostSpawn actionHostSpawning;
    [ThreadStatic]private static ActionHostOwnerLoad actionHostLoading;
    private static readonly ConditionalWeakTable<PlayerDataFile,ActionHostOwnerLoad> actionHostOwnerData=new ConditionalWeakTable<PlayerDataFile,ActionHostOwnerLoad>();
    private static readonly List<ActionHostOwnerLoad> actionHostOwnerLoads=new List<ActionHostOwnerLoad>();
    private static ActionHostOwnerLoad[] SnapshotActionHostOwnerLoads(){lock(actionOwnerGate)return actionHostOwnerLoads.ToArray();}
    internal static object EnterActionPaymentHostSpawn(ClientInfo peer,bool ran)
    {
        var world=GameManager.Instance?.World;
        if(!ran||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actionHostSpawning!=null||peer?.netConnection?[0]==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner))return null;
        foreach(var previous in SnapshotActionHostOwnerLoads().Where(value=>ReferenceEquals(value.Spawn.Peer,peer)||!ReferenceEquals(value.Spawn.World,world)||
            !ReferenceEquals(value.Spawn.Connection,value.Spawn.Peer?.netConnection?[0])).ToArray())DropActionHostOwnerLoad(previous);
        return actionHostSpawning=new ActionHostSpawn{Peer=peer,World=world,Account=owner.CanonicalId,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Connection=peer.netConnection[0]};
    }
    internal static void ObserveActionPaymentHostSpawn(ClientInfo peer,bool ran,object state)
    {
        if(state is not ActionHostSpawn spawn||!ReferenceEquals(actionHostSpawning,spawn)||!ran||!ReferenceEquals(spawn.Peer,peer)||
            !ReferenceEquals(GameManager.Instance?.World,spawn.World)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(peer.entityId),peer)||
            !ReferenceEquals(peer.netConnection?[0],spawn.Connection))return;
        spawn.Returned=true;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>ReferenceEquals(value.Spawn,spawn)&&value.Hydrated))
            if(ReferenceEquals(spawn.World.GetEntity(peer.entityId),source.Actor)&&source.Actor.entityId==peer.entityId)source.Joined=true;
    }
    internal static void EndActionPaymentHostSpawn(object state,Exception error)
    {
        if(state is not ActionHostSpawn spawn)return;
        if(ReferenceEquals(actionHostSpawning,spawn))actionHostSpawning=null;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>ReferenceEquals(value.Spawn,spawn)))
            if(error!=null||!spawn.Returned||!source.Joined)DropActionHostOwnerLoad(source);
    }
    internal static object EnterActionPaymentHostOwnerLoad(PlayerDataFile data,string directory,string name,bool ran)
    {
        var spawn=actionHostSpawning;
        if(spawn==null&&ran&&ThreadManager.IsMainThread()&&GameManager.Instance?.World is World localWorld&&!localWorld.IsRemote()&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localOwner)&&name==localOwner.CanonicalId&&Path.GetFullPath(directory)==Path.GetFullPath(GameIO.GetPlayerDataDir()))
            spawn=new ActionHostSpawn{World=localWorld,Account=localOwner.CanonicalId,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Local=true};
        if(!ran||!ThreadManager.IsMainThread()||data==null||spawn==null||actionHostLoading!=null||name!=spawn.Account)return null;
        foreach(var old in SnapshotActionHostOwnerLoads().Where(value=>!ReferenceEquals(value.Spawn.World,GameManager.Instance?.World)||
            !ReferenceEquals(value.Spawn.Connection,value.Spawn.Peer?.netConnection?[0])).ToArray())DropActionHostOwnerLoad(old);
        string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));
        if(SnapshotActionHostOwnerLoads().Length>=4||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var memory))return null;
        var source=new ActionHostOwnerLoad{Data=new WeakReference<PlayerDataFile>(data),Spawn=spawn,Path=path,Memory=memory};
        actionHostLoading=source;return source;
    }
    internal static void ObserveActionPaymentHostOwnerOpen(string path,Stream stream,bool ran)
    {
        var source=actionHostLoading;
        if(!ran||source==null||source.Source!=null||stream==null||Path.GetFullPath(path)!=source.Path||!stream.CanRead||!stream.CanSeek||stream.Position!=0||stream.Length<6||stream.Length>16L*1024*1024)return;
        source.Source=stream;
    }
    private static object EnterActionHostOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        var source=actionHostLoading;
        if(!ran||source==null||source.ReadEntered||version!=PlayerDataFile.cFileVersion||mode!=global::StreamModeRead.Persistency||
            !source.Data.TryGetTarget(out var expected)||!ReferenceEquals(expected,data)||!ReferenceEquals(reader?.BaseStream,source.Source)||reader.BaseStream.Position!=5)return null;
        var stream=reader.BaseStream;if(stream.Length<6||stream.Length>16L*1024*1024)return null;
        source.Reader=reader;source.ReadEntered=true;return source;
    }
    private static void ObserveActionHostOwnerRead(bool ran,object state)
    {
        if(state is not ActionHostOwnerLoad source)return;
        if(!ran){source.ReadReturned=false;return;}
        if(!ReferenceEquals(actionHostLoading,source)||source.Reader?.BaseStream.Position!=source.Reader?.BaseStream.Length||!source.Data.TryGetTarget(out var data)||
            data.bDead||data.bagData==null||data.inventoryData==null||data.buffData==null||data.bagData.Length+data.inventoryData.Length>4L*1024*1024||data.buffData.Length>1048576||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(data.buffData.ToArray(),out var operations)||operations.Length==0&&!HasPendingActionOwner(source.Spawn))return;
        // Actual native read keys select retention only. Historical directories
        // cannot starve a later original, and unrelated joins allocate no full
        // native file copy. Native stream identity and completed EOF were bound
        // before the original Read; no described bytes enter the native reader.
        var stream=source.Reader.BaseStream;if(!ReferenceEquals(stream,source.Source)||stream.Length<6||stream.Length>16L*1024*1024)return;
        long position=stream.Position;byte[] native=new byte[(int)stream.Length];
        try{stream.Position=0;int offset=0;while(offset<native.Length){int count=stream.Read(native,offset,native.Length-offset);if(count<1)return;offset+=count;}}
        finally{stream.Position=position;}
        source.Native=native;
        source.Bag=data.bagData.ToArray();source.Belt=data.inventoryData.ToArray();source.Buffs=data.buffData.ToArray();source.NativeId=data.id;source.Deaths=data.deaths;source.ReadReturned=true;
    }
    internal static void ObserveActionPaymentHostOwnerLoad(PlayerDataFile data,bool ran,object state)
    {
        if(state is not ActionHostOwnerLoad source||!ran||!ReferenceEquals(source,actionHostLoading)||!source.ReadReturned||!data.bLoaded||!source.Data.TryGetTarget(out var expected)||!ReferenceEquals(data,expected)||
            !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(source.Path,source.Native))return;
        long bytes=checked(source.Native.LongLength+source.Bag.LongLength+source.Belt.LongLength+source.Buffs.LongLength+1024L*1024);
        if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,bytes))return;
        source.Loaded=true;actionHostOwnerData.Remove(data);actionHostOwnerData.Add(data,source);lock(actionOwnerGate)actionHostOwnerLoads.Add(source);
    }
    internal static void EndActionPaymentHostOwnerLoad(object state)
    {
        if(state is not ActionHostOwnerLoad source)return;
        if(ReferenceEquals(actionHostLoading,source))actionHostLoading=null;
        source.Reader=null;source.Source=null;if(!source.Loaded)DropActionHostOwnerLoad(source);
    }
    private static bool ActionHostOwnerFinancialData(ActionHostOwnerLoad source,PlayerDataFile data)
    {
        if(data?.bagData==null||data.inventoryData==null||data.buffData==null||data.deaths!=source.Deaths||data.bDead||
            data.bagData.Length!=source.Bag.Length||data.inventoryData.Length!=source.Belt.Length||data.buffData.Length!=source.Buffs.Length)return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(source.Bag.LongLength+source.Belt.LongLength+source.Buffs.LongLength+4096),out var memory))return false;
        using(memory)return source.Bag.SequenceEqual(data.bagData.ToArray())&&source.Belt.SequenceEqual(data.inventoryData.ToArray())&&source.Buffs.SequenceEqual(data.buffData.ToArray());
    }
    private static void EnterActionHostOwnerHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||mode!=global::StreamModeRead.Persistency||!actionHostOwnerData.TryGetValue(data,out var source)||source.HydrationEntered||!source.Loaded||
            !(source.Spawn.Local?actor is EntityPlayerLocal:ReferenceEquals(source.Spawn,actionHostSpawning))||!ReferenceEquals(actor?.world,source.Spawn.World)||!ActionHostOwnerFinancialData(source,data))return;
        source.Actor=actor;source.HydrationEntered=true;
    }
    private static void ObserveActionHostOwnerHydration(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||!actionHostOwnerData.TryGetValue(data,out var source)||!source.HydrationEntered||!ReferenceEquals(source.Actor,actor)||
            !(source.Spawn.Local?actor is EntityPlayerLocal:ReferenceEquals(source.Spawn,actionHostSpawning))||!ActionHostOwnerFinancialData(source,data)||!TryReserveActionOwnerProjection(data,out var memory))return;
        using(memory)
        {
            source.BagGrid=actor.bag?.ItemGrid;source.BeltGrid=actor.inventory?.ItemGrid;
            if(source.BagGrid==null||source.BeltGrid==null||!SameActionItems(source.BagGrid.items,RebirthGearPlayerDataInventory.ReadSlots(data,true))||
                !SameActionItems(source.BeltGrid.items,RebirthGearPlayerDataInventory.ReadSlots(data,false)))return;
            source.BagReferences=source.BagGrid.items.ToArray();source.BeltReferences=source.BeltGrid.items.ToArray();source.Hydrated=true;
        }
    }
    private static void DropActionHostOwnerLoad(ActionHostOwnerLoad source)
    {
        if(!ThreadManager.IsMainThread()){source.RetireRequested=true;return;}
        lock(actionOwnerGate)
        {
        source.Joined=false;source.Loaded=false;
        actionHostOwnerLoads.Remove(source);if(source.Data!=null&&source.Data.TryGetTarget(out var data))actionHostOwnerData.Remove(data);
        source.Data=null;source.Spawn=null;source.Actor=null;source.BagGrid=source.BeltGrid=null;source.BagReferences=source.BeltReferences=null;
        source.Native=source.Bag=source.Belt=source.Buffs=null;source.Reader=null;source.Source=null;source.Memory?.Dispose();source.Memory=null;
        }
    }
    private static void RetireDeferredActionHostOwnerLoads()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>value.RetireRequested))
            lock(actionOwnerGate)if(!actionRemoteHostOriginals.Values.Any(value=>ReferenceEquals(value.RestartNativeSource,source)))DropActionHostOwnerLoad(source);
    }
    private static void ObserveJoinedActionLocalOwnerLoads()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var source in SnapshotActionHostOwnerLoads().Where(value=>value.Spawn?.Local==true&&value.Loaded&&value.Hydrated&&!value.Joined))
        {
            var spawn=source.Spawn;
            if(!ReferenceEquals(GameManager.Instance?.World,spawn.World)||source.Actor is not EntityPlayerLocal actor||
                !ReferenceEquals(spawn.World.GetPrimaryPlayer(),actor)||!ReferenceEquals(spawn.World.GetEntity(actor.entityId),actor)||actor.IsDead()||!actor.IsSpawned()||actor.Died!=source.Deaths||
                !ReferenceEquals(actor.bag?.ItemGrid,source.BagGrid)||!ReferenceEquals(actor.inventory?.ItemGrid,source.BeltGrid)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=spawn.Account||spawn.Save!=Path.GetFullPath(GameIO.GetSaveGameDir()))continue;
            spawn.Returned=true;source.Joined=true;
        }
    }
    private static bool ActionHostLoadedInventoryHeld(object target)
    {
        if(target==null)return false;
        // Both callers hold actionOwnerGate; this read-only check needs no snapshot.
        foreach(var source in actionHostOwnerLoads)
        {
            if(source.Spawn?.Local!=true||!source.Loaded||!source.Hydrated||!source.Joined||source.Actor==null||source.Actor.Died!=source.Deaths||source.Actor.IsDead()||
                !(ReferenceEquals(target,source.BagGrid)||ReferenceEquals(target,source.BeltGrid))||!RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(source.Buffs,out var operations))continue;
            if(operations.Any(operation=>!source.TerminalMetadataDone.Contains(operation)&&RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)&&present&&phase!=6f))return true;
        }
        return false;
    }
}
