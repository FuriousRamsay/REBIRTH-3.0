using System;
using System.Collections.Generic;
using System.Linq;

// Read-only client projection mailbox. Detached snapshots only; no pooled packet
// survives ProcessPackage and an unloaded/recycled entity is never adopted.
internal static partial class RebirthNpcPendingEquipmentSnapshots
{
    private sealed class Snapshot
    {internal int Entity;internal RebirthNpcStableId Id;internal uint Epoch,Revision;internal ulong Generation;internal Dictionary<RebirthNpcEquipmentSlot,string> Slots;internal DateTime Expires;internal float Next;}
    private static Dictionary<int,Snapshot> pending=new Dictionary<int,Snapshot>();
    private static World context;private static uint epoch,connection;
    internal static void Reset(){if(pending.Count!=0)throw new InvalidOperationException("Original pending equipment views require typed retirement");pending.Clear();context=null;epoch=connection=0;}
    private static void Scope(World world)
    {if(!ReferenceEquals(context,world)||epoch!=RebirthNpcNetworkEpoch.ClientEpoch||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch){Reset();context=world;epoch=RebirthNpcNetworkEpoch.ClientEpoch;connection=RebirthNpcNetworkEpoch.ClientConnectionEpoch;}}
    internal static void Receive(World world,int entity,string stable,uint receivedEpoch,ulong generation,uint revision,IDictionary<RebirthNpcEquipmentSlot,string> slots)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!RebirthNpcNetworkEpoch.AcceptClientEpoch(receivedEpoch)||!RebirthNpcStableId.TryParse(stable,out var id)||slots==null)return;
        Scope(world);if(!RebirthNpcNativeEquipmentBridge.ObserveRecoveryGeneration(generation))return;
        if(pending.TryGetValue(entity,out var prior)&&prior.Id==id&&(prior.Generation>generation||prior.Generation==generation&&prior.Revision>revision))return;
        if(pending.Count>=RebirthNpcNetworkProtocol.MaxBaselineActors&&!pending.ContainsKey(entity))
        {foreach(var stale in pending.Values.Where(value=>value.Expires<=DateTime.UtcNow).ToArray())pending.Remove(stale.Entity);if(pending.Count>=RebirthNpcNetworkProtocol.MaxBaselineActors)return;}
        var snapshot=new Snapshot{Entity=entity,Id=id,Epoch=receivedEpoch,Generation=generation,Revision=revision,Slots=new Dictionary<RebirthNpcEquipmentSlot,string>(slots),Expires=DateTime.UtcNow.AddMinutes(2)};
        pending[entity]=snapshot;TryApply(world,snapshot);
    }
    private static void TryApply(World world,Snapshot snapshot)
    {
        if(!RebirthNpcNetworkEpoch.AcceptClientEpoch(snapshot.Epoch)){pending.Remove(snapshot.Entity);return;}
        var entity=world.GetEntity(snapshot.Entity);if(entity==null)return;
        if(entity is not EntityRebirthHumanoidNPC npc||npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId!=snapshot.Id){pending.Remove(snapshot.Entity);return;}
        var result=RebirthNpcNativeEquipmentBridge.ApplyRecoverySnapshot(npc,snapshot.Id,snapshot.Generation,snapshot.Revision,snapshot.Slots);
        if(result==RebirthNpcNativeEquipmentApplyResult.StaleRevision){pending.Remove(snapshot.Entity);return;}
        if(result!=RebirthNpcNativeEquipmentApplyResult.Applied)return;
        string digest=NetPackageRebirthNpcSemanticEquipmentV2.Digest(snapshot.Entity,snapshot.Id,snapshot.Generation,snapshot.Revision,snapshot.Slots);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcEquipmentProjectionAck>().Setup(snapshot.Entity,snapshot.Id,snapshot.Generation,snapshot.Revision,digest,snapshot.Epoch,RebirthNpcNetworkEpoch.ClientConnectionEpoch));
        pending.Remove(snapshot.Entity);
    }
    internal static void ApplyOnAdded(EntityRebirthNPC npc)
    {if(npc?.world?.IsRemote()!=true||!ReferenceEquals(npc.world,GameManager.Instance?.World))return;Scope(npc.world);if(pending.TryGetValue(npc.entityId,out var snapshot))TryApply(npc.world,snapshot);}
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true)return;Scope(world);float now=UnityEngine.Time.realtimeSinceStartup;int count=0;
        foreach(var snapshot in pending.Values.ToArray())
        {if(snapshot.Expires<=DateTime.UtcNow){pending.Remove(snapshot.Entity);continue;}if(now<snapshot.Next)continue;if(count++>=8)break;snapshot.Next=now+.5f;TryApply(world,snapshot);}
    }
}
