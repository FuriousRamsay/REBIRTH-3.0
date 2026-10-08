using System;
using System.Collections.Generic;
using UnityEngine;

// Main-thread client projection only. Never retain a pooled network package.
internal static class RebirthNpcPendingRuntimeStates
{
    internal sealed class Snapshot
    {
        internal int EntityId;
        internal RebirthNpcStableId StableId;
        internal uint Epoch, Revision;
        internal RebirthNpcPresenceState Presence;
        internal RebirthNpcOwnershipKind Ownership;
        internal string Owner;
        internal RebirthNpcOrderState Order;
        internal RebirthNpcTravelState Travel;
        internal Vector3 GuardPosition;
        internal bool HasGuardPosition;
        internal DateTime Expires;

        internal void Apply(EntityRebirthNPC npc)
        {
            npc.ApplyReplicatedRuntimeState(Presence, Ownership, Owner, Order, Travel,
                GuardPosition, HasGuardPosition, Revision);
        }
    }

    private const int Capacity = 16384;
    private static readonly Dictionary<int, LinkedListNode<Snapshot>> ByEntity =
        new Dictionary<int, LinkedListNode<Snapshot>>();
    private static readonly LinkedList<Snapshot> Pending = new LinkedList<Snapshot>();
    private static World contextWorld;
    private static uint contextEpoch, contextConnection;

    internal static void Reset()
    {
        ByEntity.Clear();
        Pending.Clear();
        contextWorld = null;
        contextEpoch = contextConnection = 0;
    }

    private static void SetContext(World world)
    {
        uint epoch = RebirthNpcNetworkEpoch.ClientEpoch;
        uint connection = RebirthNpcNetworkEpoch.ClientConnectionEpoch;
        if (!ReferenceEquals(contextWorld, world) || contextEpoch != epoch || contextConnection != connection)
        {
            Reset();
            contextWorld = world;
            contextEpoch = epoch;
            contextConnection = connection;
        }
        DateTime now = DateTime.UtcNow;
        while (Pending.First != null && Pending.First.Value.Expires <= now)
            Remove(Pending.First);
    }

    private static void Remove(LinkedListNode<Snapshot> node)
    {
        ByEntity.Remove(node.Value.EntityId);
        Pending.Remove(node);
    }

    internal static void Receive(World world, Snapshot snapshot)
    {
        if (world == null || !world.IsRemote() || snapshot.StableId.IsEmpty ||
            !RebirthNpcNetworkEpoch.AcceptClientEpoch(snapshot.Epoch)) return;
        SetContext(world);
        Entity entity = world.GetEntity(snapshot.EntityId);
        EntityRebirthNPC npc = entity as EntityRebirthNPC;
        if (entity != null)
        {
            // Never bind a recycled entity ID to the identity claimed by a packet.
            if (npc != null && npc.RebirthRuntimeState != null &&
                npc.RebirthRuntimeState.StableId.Equals(snapshot.StableId)) snapshot.Apply(npc);
            return;
        }
        LinkedListNode<Snapshot> previous;
        if (ByEntity.TryGetValue(snapshot.EntityId, out previous))
        {
            if (previous.Value.StableId.Equals(snapshot.StableId) && previous.Value.Revision > snapshot.Revision)
                return;
            Remove(previous);
        }
        while (Pending.Count >= Capacity) Remove(Pending.First);
        snapshot.Expires = DateTime.UtcNow.AddMinutes(2);
        ByEntity.Add(snapshot.EntityId, Pending.AddLast(snapshot));
    }

    internal static void ApplyOnAdded(EntityRebirthNPC npc)
    {
        if (npc == null || npc.world == null || !npc.world.IsRemote()) return;
        SetContext(npc.world);
        LinkedListNode<Snapshot> node;
        if (!ByEntity.TryGetValue(npc.entityId, out node)) return;
        Snapshot snapshot = node.Value;
        Remove(node);
        if (npc.RebirthRuntimeState != null && npc.RebirthRuntimeState.StableId.Equals(snapshot.StableId) &&
            RebirthNpcNetworkEpoch.AcceptClientEpoch(snapshot.Epoch)) snapshot.Apply(npc);
    }
}
