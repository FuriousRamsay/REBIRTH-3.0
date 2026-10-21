using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionClientNativeRead
    {
        private WeakReference<PlayerDataFile> data;
        internal PlayerDataFile Data{get=>data!=null&&data.TryGetTarget(out var value)?value:null;set=>data=value==null?null:new WeakReference<PlayerDataFile>(value);}
        internal byte[] Bag,Belt,Buffs;internal Guid[] Operations;
        internal readonly System.Collections.Generic.HashSet<Guid> MetadataDone=new System.Collections.Generic.HashSet<Guid>();
        internal int Deaths,HydratedDeaths,NativeId;internal long ScanNext;internal bool Returned,HydrationEntered,Hydrated,Joined,Held,NativeLifeEnded,Dead;
        internal NetPackagePlayerId Packet;internal World World;internal INetConnection Connection;internal EntityPlayerLocal Actor;
        internal ItemStackGrid BagGrid,BeltGrid;internal ItemStack[] BagReferences,BeltReferences;
    }
    private static readonly object actionClientReadGate=new object();
    // At most one pending and one joined native read, each <=5 MiB of raw
    // fields plus bounded row-reference arrays. No worker allocates a decoder
    // or obtains native mutation authority from this retained observation.
    private static IDisposable actionClientReadAllocation;
    private static ActionClientNativeRead actionClientNextRead,actionClientLoadedRead;
    [ThreadStatic]private static ActionClientNativeRead actionClientJoining;
    internal static object EnterActionPaymentNativeOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        var host=EnterActionHostOwnerRead(data,reader,version,mode,ran);if(host!=null)return host;
        if(!ran||actionClientReadAllocation==null||data==null||reader==null||version!=uint.MaxValue||mode!=global::StreamModeRead.FromServer)return null;
        return new ActionClientNativeRead{Data=data};
    }
    internal static void ObserveActionPaymentNativeOwnerRead(bool ran,object state)
    {
        if(state is ActionHostOwnerLoad){ObserveActionHostOwnerRead(ran,state);return;}
        if(state is not ActionClientNativeRead source)return;
        if(!ran){lock(actionClientReadGate){if(ReferenceEquals(actionClientNextRead,source))actionClientNextRead=null;}return;}
        var data=source.Data;
        if(data==null||data.bagData==null||data.inventoryData==null||data.buffData==null||data.bagData.Length+data.inventoryData.Length>4L*1024*1024||data.buffData.Length>1048576)return;
        lock(actionClientReadGate)
        {
            // Drop only superseded observation fields before allocating a new
            // bounded read image. A pending native original is never settled.
            if(actionClientNextRead!=null){actionClientNextRead.Data=null;actionClientNextRead.Bag=null;actionClientNextRead.Belt=null;actionClientNextRead.Buffs=null;actionClientNextRead=null;}
            source.Bag=data.bagData.ToArray();source.Belt=data.inventoryData.ToArray();source.Buffs=data.buffData.ToArray();source.Deaths=data.deaths;source.NativeId=data.id;source.Dead=data.bDead;source.NativeLifeEnded=data.bDead;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(source.Buffs,out source.Operations))return;
            source.Held=!source.Dead&&RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,"rbActionPaymentHeld",out float held,out bool present)&&present&&held==1f;
            source.Returned=true;actionClientNextRead=source;
        }
    }
    private static bool ActionClientLoadedInventoryHeld(object target)
    {
        var source=actionClientJoining?.Hydrated==true?actionClientJoining:actionClientLoadedRead;
        if(source?.Held!=true||!source.Hydrated||!source.Joined&&!ReferenceEquals(source,actionClientJoining))return false;
        if(ThreadManager.IsMainThread()&&(source.Actor?.IsDead()==true||source.Actor!=null&&source.Actor.Died!=source.HydratedDeaths))source.NativeLifeEnded=true;
        return !source.NativeLifeEnded&&(ReferenceEquals(target,source.BagGrid)||ReferenceEquals(target,source.BeltGrid));
    }
    private static void ReleaseActionClientLoadedHold(ActionRemoteClick click)
    {
        var source=click.RestartOwnerSource;if(source==null||!ReferenceEquals(source.Actor,click.Actor))return;
        source.MetadataDone.Add(click.Operation);
        foreach(var operation in source.Operations)
            if(operation!=click.Operation&&!source.MetadataDone.Contains(operation)&&RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)&&present&&phase!=6f)return;
        source.Held=false;
    }
    private static bool SameActionClientNativeRead(ActionClientNativeRead source)
    {
        // EndJoin drops the full native PDS graph only after the exact original
        // read/hydration/final join checks succeeded. Immutable raw fields and
        // actual native actor/grid identities retain that private observation.
        if(source?.Joined==true&&source.Data==null)return true;
        if(source?.Data==null||source.Data.bDead!=source.Dead||source.Data.deaths!=source.Deaths||source.Data.bagData==null||source.Data.inventoryData==null||source.Data.buffData==null||
            source.Data.bagData.Length!=source.Bag.Length||source.Data.inventoryData.Length!=source.Belt.Length||source.Data.buffData.Length!=source.Buffs.Length)return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(source.Bag.LongLength+source.Belt.LongLength+source.Buffs.LongLength+4096),out var scratch))return false;
        using(scratch)return source.Bag.SequenceEqual(source.Data.bagData.ToArray())&&source.Belt.SequenceEqual(source.Data.inventoryData.ToArray())&&source.Buffs.SequenceEqual(source.Data.buffData.ToArray());
    }
    internal static object EnterActionPaymentNativeJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||actionClientJoining!=null||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(callbacks,GameManager.Instance)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))return null;
        // This actual authenticated join supersedes the prior native hydration,
        // even if World and connection objects were reused by vanilla.
        RetireActionRemoteObservationForNativeJoin();
        lock(actionClientReadGate)
        {
            var source=actionClientNextRead;
            if(source?.Returned!=true||!ReferenceEquals(source.Data,packet.playerDataFile)||!SameActionClientNativeRead(source))return null;
            actionClientLoadedRead=null;actionClientNextRead=null;
            source.Packet=packet;source.World=world;source.Connection=connection;actionClientJoining=source;return source;
        }
    }
    internal static void EnterActionPaymentNativeHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        EnterActionHostOwnerHydration(data,actor,mode,ran);
        var source=actionClientJoining;
        if(!ran||source==null||source.HydrationEntered||mode!=global::StreamModeRead.FromServer||!ReferenceEquals(source.Data,data)||
            actor is not EntityPlayerLocal local||local.entityId!=source.Packet.id||!ReferenceEquals(local.world,source.World)||!SameActionClientNativeRead(source))return;
        source.HydrationEntered=true;source.Actor=local;
    }
    internal static void ObserveActionPaymentNativeHydration(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        ObserveActionHostOwnerHydration(data,actor,ran);
        var source=actionClientJoining;
        if(!ran||source?.HydrationEntered!=true||!ReferenceEquals(source.Data,data)||!ReferenceEquals(source.Actor,actor)||!SameActionClientNativeRead(source)||!TryReserveActionOwnerProjection(data,out var projection))return;
        using(projection)
        {
            var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
            source.BagGrid=actor.bag?.ItemGrid;source.BeltGrid=actor.inventory?.ItemGrid;
            if(source.BagGrid==null||source.BeltGrid==null||!SameActionItems(source.BagGrid.items,bag)||!SameActionItems(source.BeltGrid.items,belt))return;
            source.BagReferences=source.BagGrid.items.ToArray();source.BeltReferences=source.BeltGrid.items.ToArray();source.Hydrated=true;
        }
    }
    internal static void ObserveActionPaymentNativeJoin(NetPackagePlayerId packet,World world,bool ran,object state)
    {
        if(state is not ActionClientNativeRead source)return;
        if(ran&&source.Hydrated&&ReferenceEquals(actionClientJoining,source)&&ReferenceEquals(source.Packet,packet)&&ReferenceEquals(source.World,world)&&
            ReferenceEquals(world.GetPrimaryPlayer(),source.Actor)&&ReferenceEquals(world.GetEntity(source.Actor.entityId),source.Actor)&&
            ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&SameActionClientNativeRead(source))
        {source.HydratedDeaths=source.Actor.Died;if(source.HydratedDeaths!=source.Deaths)source.NativeLifeEnded=true;source.Joined=true;lock(actionClientReadGate)actionClientLoadedRead=source;}
    }
    internal static void EndActionPaymentNativeJoin(object state,Exception error)
    {
        if(state is not ActionClientNativeRead source)return;
        if(error!=null)source.Joined=false;
        if(ReferenceEquals(actionClientJoining,source))actionClientJoining=null;
        source.Packet=null;
        if(source.Joined)source.Data=null;
        if(!source.Joined){lock(actionClientReadGate)if(ReferenceEquals(actionClientLoadedRead,source))actionClientLoadedRead=null;source.Data=null;source.Bag=null;source.Belt=null;source.Buffs=null;}
    }
}
