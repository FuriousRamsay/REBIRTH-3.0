using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private sealed class NpcClientRead
    {
        internal PlayerDataFile Data;internal byte[] Buffs,Bag,Belt;internal Guid[] Operations,PlayerOperations;internal PooledBinaryReader Reader;internal long Start;
        internal World World;internal EntityPlayerLocal Actor;internal INetConnection Connection;internal NetPackagePlayerId Packet;
        internal int Deaths;internal bool Dead,Returned,HydrationEntered,Hydrated,Joined;internal IDisposable Memory;
    }
    private static readonly object clientNativeGate=new object();
    private static readonly List<NpcClientRead> clientNativeReads=new List<NpcClientRead>();
    [ThreadStatic]private static NpcClientRead enteredClientJoin;
    private static object EnterClientNativeRead(PlayerDataFile data,PooledBinaryReader reader,uint version,StreamModeRead mode,bool run)
    {
        NpcClientRead scope=null;
        try
        {
            if(!run||data==null||version!=uint.MaxValue||mode!=StreamModeRead.FromServer||reader?.BaseStream==null||!reader.BaseStream.CanSeek||reader.BaseStream.Length>16L*1024*1024)return null;
            lock(clientNativeGate)
            {
                if(clientNativeReads.Count>=2||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return null;
                scope=new NpcClientRead{Data=data,Reader=reader,Start=reader.BaseStream.Position,Memory=memory};clientNativeReads.Add(scope);return scope;
            }
        }
        catch{if(scope!=null)DropClientNativeRead(scope);return null;}
    }
    private static void ObserveClientNativeRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is not NpcClientRead scope)return;
        lock(clientNativeGate)
        {
            try
            {
                if(!run||!ReferenceEquals(scope.Reader,reader)||reader.BaseStream.Position<=scope.Start||scope.Data.buffData==null||scope.Data.buffData.Length>1024*1024)return;
                scope.Buffs=scope.Data.buffData.ToArray();
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryNpcConsumptionKeys(scope.Buffs,out var operations)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryPlayerConsumptionKeys(scope.Buffs,out var playerOperations)||operations.Length==0&&playerOperations.Length==0)return;
                scope.Bag=scope.Data.bagData.ToArray();scope.Belt=scope.Data.inventoryData.ToArray();
                scope.Operations=operations;scope.PlayerOperations=playerOperations;scope.Deaths=scope.Data.deaths;scope.Dead=scope.Data.bDead;scope.Returned=true;scope.Reader=null;
            }
            catch{}
            finally{if(!scope.Returned)DropClientNativeRead(scope);}
        }
    }
    private static bool SameClientNativeData(NpcClientRead scope)=>scope.Data!=null&&scope.Data.deaths==scope.Deaths&&scope.Data.bDead==scope.Dead&&scope.Data.buffData!=null&&scope.Data.buffData.Length==scope.Buffs.Length&&scope.Data.buffData.ToArray().SequenceEqual(scope.Buffs)&&
        scope.Data.bagData.ToArray().SequenceEqual(scope.Bag)&&scope.Data.inventoryData.ToArray().SequenceEqual(scope.Belt);
    internal static object EnterClientNativeJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(callbacks,GameManager.Instance)||enteredClientJoin!=null||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))return null;
        foreach(var old in ownerActions.Values.ToArray())RetireOwnerAction(old); // authenticated fresh native join: observation retirement only.
        lock(clientNativeGate)
        {
            foreach(var old in clientNativeReads.Where(value=>!ReferenceEquals(value.Data,packet.playerDataFile)).ToArray())DropClientNativeRead(old);
            var scope=clientNativeReads.SingleOrDefault(value=>ReferenceEquals(value.Data,packet.playerDataFile)&&value.Returned);
            if(scope==null||!SameClientNativeData(scope))return null;
            scope.Packet=packet;scope.World=world;scope.Connection=connection;enteredClientJoin=scope;return scope;
        }
    }
    private static void EnterClientNativeHydration(PlayerDataFile data,EntityPlayer actor,StreamModeRead mode,bool run)
    {
        var scope=enteredClientJoin;if(!run||mode!=StreamModeRead.FromServer||scope==null||scope.HydrationEntered||!ReferenceEquals(scope.Data,data)||!SameClientNativeData(scope)||actor is not EntityPlayerLocal local||
            local.entityId!=scope.Packet.id||!ReferenceEquals(local.world,scope.World))return;
        scope.HydrationEntered=true;scope.Actor=local;
    }
    private static void ObserveClientNativeHydration(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        var scope=enteredClientJoin;if(scope==null||!run||!scope.HydrationEntered||!ReferenceEquals(scope.Data,data)||!ReferenceEquals(scope.Actor,actor)||!SameClientNativeData(scope))return;
        scope.Hydrated=true;
    }
    internal static void ObserveClientNativeJoin(NetPackagePlayerId packet,World world,bool run,object state)
    {
        if(state is not NpcClientRead scope)return;
        scope.Joined=run&&scope.Hydrated&&ReferenceEquals(scope.Packet,packet)&&ReferenceEquals(scope.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),scope.Actor)&&
            ReferenceEquals(world.GetEntity(scope.Actor.entityId),scope.Actor)&&ReferenceEquals(scope.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]);
        if(scope.Joined)foreach(var operation in scope.Operations)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionRestartReady>().Setup(scope.Actor.entityId,operation),false);
        if(scope.Joined&&scope.PlayerOperations.Length>0)
            RebirthConsumptionDurableDelivery.ObserveInventoryClientNativeHydration(scope.Actor,scope.Data,scope.Connection,scope.PlayerOperations);
    }
    internal static void EndClientNativeJoin(object state,Exception error)
    {
        if(state is not NpcClientRead scope)return;
        if(ReferenceEquals(enteredClientJoin,scope))enteredClientJoin=null;scope.Packet=null;
        if(error!=null||!scope.Joined)DropClientNativeRead(scope);
    }
    private static void DropClientNativeRead(NpcClientRead scope)
    {
        lock(clientNativeGate)
        {clientNativeReads.Remove(scope);scope.Memory?.Dispose();scope.Memory=null;scope.Data=null;scope.Buffs=null;scope.Bag=null;scope.Belt=null;scope.Operations=null;scope.PlayerOperations=null;scope.Reader=null;scope.Actor=null;scope.World=null;scope.Connection=null;scope.Packet=null;}
    }
    private static void RetireClientNativeObservations()
    {
        if(!ThreadManager.IsMainThread())return;
        lock(clientNativeGate)
            foreach(var scope in clientNativeReads.Where(value=>value.World!=null&&(!ReferenceEquals(value.World,GameManager.Instance?.World)||!ReferenceEquals(value.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))).ToArray())DropClientNativeRead(scope);
    }
    private static bool LoadedClientReceipt(EntityPlayerLocal actor,Guid operation,string key,float value)
    {
        lock(clientNativeGate)
            return clientNativeReads.Any(scope=>scope.Joined&&scope.Hydrated&&ReferenceEquals(scope.Actor,actor)&&ReferenceEquals(scope.World,actor.world)&&ReferenceEquals(actor.world,GameManager.Instance?.World)&&
                ReferenceEquals(scope.World.GetPrimaryPlayer(),actor)&&ReferenceEquals(scope.World.GetEntity(actor.entityId),actor)&&ReferenceEquals(scope.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&scope.Operations.Contains(operation)&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(scope.Buffs,key,out float observed,out bool present)&&present&&observed==value&&actor.Buffs.GetCustomVar(key)==value);
    }
    private static bool LoadedClientNoOwnerEvent(EntityPlayerLocal actor,Guid operation)
    {
        lock(clientNativeGate)
            return clientNativeReads.Any(scope=>scope.Joined&&scope.Hydrated&&ReferenceEquals(scope.Actor,actor)&&ReferenceEquals(scope.World,actor.world)&&ReferenceEquals(actor.world,GameManager.Instance?.World)&&
                ReferenceEquals(scope.World.GetPrimaryPlayer(),actor)&&ReferenceEquals(scope.World.GetEntity(actor.entityId),actor)&&ReferenceEquals(scope.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&scope.Operations.Contains(operation)&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(scope.Buffs,OwnerReceipt(operation),out float observed,out bool present)&&(!present||observed==0f)&&actor.Buffs.GetCustomVar(OwnerReceipt(operation))==0f);
    }
    private static bool LoadedClientNoSecondary(EntityPlayerLocal actor,Guid operation)
    {
        lock(clientNativeGate)
            return clientNativeReads.Any(scope=>scope.Joined&&scope.Hydrated&&ReferenceEquals(scope.Actor,actor)&&ReferenceEquals(scope.World,actor.world)&&ReferenceEquals(actor.world,GameManager.Instance?.World)&&
                ReferenceEquals(scope.World.GetPrimaryPlayer(),actor)&&ReferenceEquals(scope.World.GetEntity(actor.entityId),actor)&&ReferenceEquals(scope.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])&&scope.Operations.Contains(operation)&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(scope.Buffs,SecondaryReceipt(operation),out float observed,out bool present)&&(!present||observed==0f)&&actor.Buffs.GetCustomVar(SecondaryReceipt(operation))==0f);
    }
    private static void CompleteLoadedClientOperation(Guid operation)
    {
        lock(clientNativeGate)
            foreach(var scope in clientNativeReads.Where(value=>value.Joined&&value.Operations.Contains(operation)).ToArray())
            {scope.Operations=scope.Operations.Where(value=>value!=operation).ToArray();if(scope.Operations.Length==0&&scope.PlayerOperations.Length==0)DropClientNativeRead(scope);}
    }
    internal static void CompletePlayerNativeConsumption(Guid operation)
    {
        lock(clientNativeGate)
            foreach(var scope in clientNativeReads.Where(value=>value.Joined&&value.PlayerOperations.Contains(operation)).ToArray())
            {scope.PlayerOperations=scope.PlayerOperations.Where(value=>value!=operation).ToArray();if(scope.Operations.Length==0&&scope.PlayerOperations.Length==0)DropClientNativeRead(scope);}
    }
    internal static void ReceiveNativeRestartReady(World world,int actor,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||operation==Guid.Empty)return;
        foreach(var scope in nativeOwners.Values)
            if(scope.Hydrated&&ReferenceEquals(scope.World,world)&&scope.Actor?.entityId==actor&&ReferenceEquals(world.GetEntity(actor),scope.Actor)&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(scope.Actor,out var account)&&account.CanonicalId==scope.Account&&scope.Originals.Keys.Any(path=>System.IO.Path.GetFileName(path)==operation.ToString("N"))&&scope.Ready.Count<32)
                {scope.Ready.Add(operation);return;}
    }
}

[UnityEngine.Scripting.Preserve]public sealed class NetPackageRebirthNpcConsumptionRestartReady:NetPackage
{
    private int actor;private Guid operation;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthNpcConsumptionRestartReady Setup(int id,Guid nonce){actor=id;operation=nonce;return this;}
    public override void read(PooledBinaryReader reader){actor=reader.ReadInt32();operation=new Guid(reader.ReadBytes(16));}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(actor);writer.Write(operation.ToByteArray());}
    public override void ProcessPackage(World world,GameManager callbacks){if(ValidEntityIdForSender(actor))RebirthNpcMetabolismCustody.ReceiveNativeRestartReady(world,actor,operation);}
    public int GetLength()=>0;
}
