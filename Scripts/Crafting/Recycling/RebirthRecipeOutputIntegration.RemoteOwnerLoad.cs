using System;
using System.IO;
using System.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // Independent actual remote native Read -> authenticated PlayerId -> ToPlayer
    // witness. Local sidecars and equal reconstructed player files cannot issue it.
    private sealed class RemoteOutputOwnerLoad:IDisposable
    {
        internal WeakReference<PlayerDataFile> Data;internal byte[] Bag,Belt,Equipment,Buffs,BagFrame,BeltFrame,EquipmentFrame;
        internal World World;internal EntityPlayerLocal Actor;internal NetPackagePlayerId Packet;internal INetConnection Connection;
        internal int Deaths,Entity;internal string Owner,Account,Creation;internal bool Returned,Hydrating,Hydrated,Joined,Closed;
        internal IDisposable Memory;
        internal RemoteOutputColdScan Scan;
        internal PlayerDataFile Native=>Data!=null&&Data.TryGetTarget(out var data)?data:null;
        internal bool SameData()
        {
            var data=Native;
            return data!=null&&!data.bDead&&data.deaths==Deaths&&data.bagData!=null&&data.inventoryData!=null&&data.equipmentData!=null&&data.buffData!=null&&
                data.bagData.Length==Bag.Length&&data.inventoryData.Length==Belt.Length&&data.equipmentData.Length==Equipment.Length&&data.buffData.Length==Buffs.Length&&
                Bag.SequenceEqual(data.bagData.ToArray())&&Belt.SequenceEqual(data.inventoryData.ToArray())&&
                Equipment.SequenceEqual(data.equipmentData.ToArray())&&Buffs.SequenceEqual(data.buffData.ToArray());
        }
        internal bool EpisodeCurrent()=>!Closed&&Joined&&Hydrated&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Entity),Actor)&&ReferenceEquals(Actor.world,World)&&Actor.entityId==Entity&&
            Actor.IsSpawned()&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            ReferenceEquals(Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&!Connection.IsDisconnected()&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner&&owner.CanonicalId==Account;
        internal bool Current()=>EpisodeCurrent()&&
            BagFrame.SequenceEqual(OwningGrid(Actor.bag.ItemGrid.items))&&BeltFrame.SequenceEqual(OwningGrid(Actor.inventory.ItemGrid.items))&&
            EquipmentFrame.SequenceEqual(OwningEquipmentNative(Actor));
        public void Dispose(){if(Closed)return;Closed=true;Scan?.Dispose();Scan=null;Data=null;Packet=null;World=null;Actor=null;Connection=null;Bag=Belt=Equipment=Buffs=BagFrame=BeltFrame=EquipmentFrame=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly object remoteOutputOwnerGate=new object();
    private static RemoteOutputOwnerLoad nextRemoteOutputOwner,loadedRemoteOutputOwner;
    [ThreadStatic]private static RemoteOutputOwnerLoad joiningRemoteOutputOwner;
    internal static object EnterRemoteOutputOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,global::StreamModeRead mode,bool ran)
    {
        if(!installed||!ran||data==null||reader?.BaseStream==null||version!=uint.MaxValue||mode!=global::StreamModeRead.FromServer||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024,out var memory))return null;
        return new RemoteOutputOwnerLoad{Data=new WeakReference<PlayerDataFile>(data),Memory=memory};
    }
    internal static void ObserveRemoteOutputOwnerRead(bool ran,object state)
    {
        if(state is not RemoteOutputOwnerLoad source)return;
        try
        {
            var data=source.Native;
            if(!ran||data==null||data.bDead||data.bagData==null||data.inventoryData==null||data.equipmentData==null||data.buffData==null||
                data.bagData.Length+data.inventoryData.Length>4L*1024*1024||data.equipmentData.Length>1024*1024||data.buffData.Length>1024*1024)return;
            source.Bag=data.bagData.ToArray();source.Belt=data.inventoryData.ToArray();source.Equipment=data.equipmentData.ToArray();source.Buffs=data.buffData.ToArray();source.Deaths=data.deaths;
            source.Returned=true;
            lock(remoteOutputOwnerGate){nextRemoteOutputOwner?.Dispose();nextRemoteOutputOwner=source;}
        }
        catch{source.Returned=false;}
        finally{if(!source.Returned)source.Dispose();}
    }
    internal static object EnterRemoteOutputOwnerJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||joiningRemoteOutputOwner!=null||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||
            !ReferenceEquals(callbacks,GameManager.Instance)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||
            !ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||connection.IsDisconnected())return null;
        lock(remoteOutputOwnerGate)
        {
            // Every actual authenticated join invalidates the old native episode,
            // including a reused World/connection. No old setter is replayed.
            loadedRemoteOutputOwner?.Dispose();loadedRemoteOutputOwner=null;
            var source=nextRemoteOutputOwner;
            if(source?.Returned!=true||!ReferenceEquals(source.Native,packet.playerDataFile)||!source.SameData())return null;
            nextRemoteOutputOwner=null;source.Packet=packet;source.World=world;source.Connection=connection;joiningRemoteOutputOwner=source;return source;
        }
    }
    internal static void EnterRemoteOutputOwnerHydration(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        var source=joiningRemoteOutputOwner;
        if(!ran||source==null||source.Hydrating||source.Hydrated||mode!=global::StreamModeRead.FromServer||!ReferenceEquals(source.Native,data)||
            actor is not EntityPlayerLocal local||local.entityId!=source.Packet.id||!ReferenceEquals(local.world,source.World)||!source.SameData())return;
        source.Actor=local;source.Entity=local.entityId;source.Hydrating=true;
    }
    internal static void ObserveRemoteOutputOwnerHydration(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        var source=joiningRemoteOutputOwner;
        if(!ran&&source!=null&&ReferenceEquals(source.Native,data)){source.Hydrated=false;source.Hydrating=false;return;}
        if(!ran||source?.Hydrating!=true||!ReferenceEquals(source.Native,data)||!ReferenceEquals(source.Actor,actor)||!source.SameData())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return;
            var bag=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true));var belt=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false));
            if(!bag.SequenceEqual(OwningGrid(actor.bag.ItemGrid.items))||!belt.SequenceEqual(OwningGrid(actor.inventory.ItemGrid.items))||
                !source.Equipment.SequenceEqual(OwningEquipmentNative(source.Actor)))return;
            source.BagFrame=bag;source.BeltFrame=belt;source.EquipmentFrame=source.Equipment.ToArray();source.Hydrated=true;
        }
    }
    internal static void ObserveRemoteOutputOwnerJoin(NetPackagePlayerId packet,World world,bool ran,object state)
    {
        if(state is not RemoteOutputOwnerLoad source)return;
        if(!ran||source.Hydrated!=true||!ReferenceEquals(joiningRemoteOutputOwner,source)||!ReferenceEquals(source.Packet,packet)||!ReferenceEquals(source.World,world)||
            !ReferenceEquals(world.GetPrimaryPlayer(),source.Actor)||!ReferenceEquals(world.GetEntity(source.Entity),source.Actor)||
            source.Actor.Died!=source.Deaths||!source.SameData()||
            !ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
        source.Owner=owner.StorageKey;source.Account=owner.CanonicalId;source.Creation=RebirthCamperCharacterAccess.CurrentCreation(source.Actor);
        if(string.IsNullOrEmpty(source.Creation))return;
        source.Joined=true;lock(remoteOutputOwnerGate)loadedRemoteOutputOwner=source;
    }
    internal static void EndRemoteOutputOwnerJoin(object state,Exception error)
    {
        if(state is not RemoteOutputOwnerLoad source)return;
        if(error!=null)source.Joined=false;
        if(ReferenceEquals(joiningRemoteOutputOwner,source))joiningRemoteOutputOwner=null;
        source.Packet=null;source.Data=null;
        if(!source.Joined){lock(remoteOutputOwnerGate)if(ReferenceEquals(loadedRemoteOutputOwner,source))loadedRemoteOutputOwner=null;source.Dispose();}
    }
}
