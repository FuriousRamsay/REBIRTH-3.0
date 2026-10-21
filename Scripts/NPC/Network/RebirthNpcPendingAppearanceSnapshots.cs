using System;
using System.Collections.Generic;

// Detached projection mailbox. No pooled packet/native entity reference is held.
internal static partial class RebirthNpcPendingAppearanceSnapshots
{
    internal sealed class Snapshot
    {
        internal int Entity;
        internal RebirthNpcStableId Stable;
        internal uint Epoch, Revision, SemanticRevision;
        internal ulong Generation;
        internal RebirthHumanNpcAppearanceDescriptor Appearance;
        internal Equipment Equipment;
        internal DateTime Expires, Retry;
    }
    private static Dictionary<int,Snapshot> Pending=new Dictionary<int,Snapshot>();
    private static World context;
    private static uint epoch,connection;
    private static int cursor;
    internal static void Reset(){if(Pending.Count!=0)throw new InvalidOperationException("Original pending appearance views require typed retirement");Pending.Clear();context=null;epoch=connection=0;cursor=0;}
    private static void Scope(World world)
    {
        if(ReferenceEquals(context,world)&&epoch==RebirthNpcNetworkEpoch.ClientEpoch&&connection==RebirthNpcNetworkEpoch.ClientConnectionEpoch)return;
        Reset();context=world;epoch=RebirthNpcNetworkEpoch.ClientEpoch;connection=RebirthNpcNetworkEpoch.ClientConnectionEpoch;
    }
    internal static void Receive(World world,Snapshot snapshot)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||snapshot==null||snapshot.Entity<=0||snapshot.Stable.IsEmpty||!snapshot.Appearance.IsValid||
            !RebirthNpcNetworkEpoch.AcceptClientEpoch(snapshot.Epoch)||!RebirthNpcRecoveryProjectionScope.Observe(snapshot.Generation))return;
        Scope(world);
        if(Pending.TryGetValue(snapshot.Entity,out var previous)&&previous.Stable==snapshot.Stable&&
            (previous.Generation>snapshot.Generation||(previous.Generation==snapshot.Generation&&(previous.Revision>snapshot.Revision||previous.SemanticRevision>snapshot.SemanticRevision))))return;
        if(Pending.Count>=RebirthNpcNetworkProtocol.MaxBaselineActors&&!Pending.ContainsKey(snapshot.Entity))return; // server retry, never a convergence ACK
        snapshot.Expires=DateTime.UtcNow.AddMinutes(2);
        // Packet reader created this detached Equipment; no native entity owns it.
        Pending[snapshot.Entity]=snapshot;
        Apply(world,snapshot);
    }
    private static void Apply(World world,Snapshot snapshot)
    {
        if(DateTime.UtcNow>=snapshot.Expires||!RebirthNpcRecoveryProjectionScope.Observe(snapshot.Generation)){Pending.Remove(snapshot.Entity);return;}
        var entity=world.GetEntity(snapshot.Entity);
        if(entity==null)return;
        if(entity is not EntityRebirthHumanoidNPC npc||npc.RebirthRuntimeState?.StableId!=snapshot.Stable){Pending.Remove(snapshot.Entity);return;}
        snapshot.Retry=DateTime.UtcNow.AddMilliseconds(500);
        if(!npc.ApplyReplicatedHumanAppearance(snapshot.Appearance,snapshot.Revision,snapshot.Generation))return;
        // A delayed native appearance image cannot replace a later managed
        // equipment projection from the same recovery generation.
        if(snapshot.Equipment!=null&&npc.equipment!=null&&RebirthNpcNativeEquipmentBridge.CanApplyNativeRecoveryImage(snapshot.Stable,snapshot.Generation,snapshot.SemanticRevision))
        {
            int count=Utils.FastMin(snapshot.Equipment.GetSlotCount(),npc.equipment.GetSlotCount());
            for(int i=0;i<count;i++)npc.equipment.ItemGrid[i]=snapshot.Equipment.ItemGrid[i].Clone();
            int[] cosmetics=snapshot.Equipment.GetCosmeticIDs();
            for(int i=0;i<count&&i<cosmetics.Length;i++)npc.equipment.SetCosmeticSlot(i,cosmetics[i]);
            npc.equipment.m_unlockedCosmetics.Clear();npc.equipment.m_unlockedCosmetics.AddRange(snapshot.Equipment.m_unlockedCosmetics);
        }
        npc.RefreshRebirthModelEquipment();
        Pending.Remove(snapshot.Entity);
        // Whole recovery coordinator will separately acknowledge coherent runtime,
        // appearance and semantic native projection; this mailbox grants no success.
    }
    internal static void ApplyOnAdded(EntityRebirthNPC npc)
    {if(npc?.world?.IsRemote()!=true||!ReferenceEquals(npc.world,GameManager.Instance?.World))return;Scope(npc.world);if(Pending.TryGetValue(npc.entityId,out var snapshot))Apply(npc.world,snapshot);}
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;if(world?.IsRemote()!=true)return;Scope(world);
        var rows=new List<Snapshot>(Pending.Values);
        if(rows.Count==0){cursor=0;return;}
        int count=Math.Min(8,rows.Count),start=cursor%rows.Count;
        for(int i=0;i<count;i++){var snapshot=rows[(start+i)%rows.Count];if(DateTime.UtcNow>=snapshot.Retry)Apply(world,snapshot);}
        cursor=(start+count)%rows.Count;
    }
}
