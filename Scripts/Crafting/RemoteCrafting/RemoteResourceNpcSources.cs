using System;
using System.Collections.Generic;
using UnityEngine;
using Platform;

#nullable disable

/// <summary>
/// Adapter exposing Rebirth's authoritative logical companion inventory through the
/// same source contract used by Remote Resources. Mutations are staged in-memory and
/// committed as one revision-checked NPC inventory transaction.
/// </summary>
public sealed class RemoteNpcResourceSource : IRemoteResourceSource
{
    private readonly EntityRebirthNPC npc;
    private readonly RebirthNpcStableId npcId;
    private readonly uint startingRevision;
    private readonly string[] itemKeys;
    private readonly int[] startingCounts;
    private readonly ItemStack[] workingSlots;
    private readonly PackedBoolArray workingLocks;
    private readonly bool dogStorage;

    public bool LastCommitSucceeded { get; private set; }
    public string LastCommitError { get; private set; }

    public RemoteNpcResourceSource(EntityRebirthNPC value)
    {
        npc = value;
        RebirthNpcRuntimeState state = value != null ? value.RebirthRuntimeState : null;
        npcId = state != null ? state.StableId : default(RebirthNpcStableId);
        RebirthNpcProfile profile;
        dogStorage = state != null && RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) && profile.Category == RebirthNpcCategory.DogCompanion;

        if (dogStorage && !npcId.IsEmpty)
        {
            RebirthDogInventorySnapshot snapshot = RebirthDogInventoryService.GetSnapshot(npcId);
            startingRevision = snapshot.Revision;
            workingSlots = new ItemStack[RebirthDogInventoryService.Capacity];
            workingLocks = new PackedBoolArray(RebirthDogInventoryService.Capacity);
            for (int i = 0; i < workingSlots.Length; i++)
            {
                workingSlots[i] = snapshot.Slots[i] != null ? snapshot.Slots[i].Clone() : ItemStack.Empty.Clone();
                workingLocks[i] = snapshot.Locks != null && i < snapshot.Locks.Length && snapshot.Locks[i];
            }
            itemKeys = new string[0]; startingCounts = new int[0];
        }
        else
        {
            RebirthNpcInventorySnapshot snapshot = !npcId.IsEmpty ? RebirthNpcInventoryTransactionService.GetSnapshot(npcId) : null;
            startingRevision = snapshot != null ? snapshot.Revision : 0u;
            List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>();
            if (snapshot != null && snapshot.Quantities != null)
            {
                foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
                {
                    int reserved = 0; if (snapshot.Reservations != null) snapshot.Reservations.TryGetValue(pair.Key, out reserved);
                    int available = Math.Max(0, pair.Value - reserved);
                    if (available > 0) pairs.Add(new KeyValuePair<string, int>(pair.Key, available));
                }
            }
            pairs.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase); });
            itemKeys = new string[pairs.Count]; startingCounts = new int[pairs.Count]; workingSlots = new ItemStack[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                itemKeys[i] = pairs[i].Key; startingCounts[i] = pairs[i].Value;
                ItemValue itemValue = ItemClass.GetItem(pairs[i].Key, false);
                workingSlots[i] = itemValue == null || itemValue.IsEmpty() ? ItemStack.Empty.Clone() : new ItemStack(itemValue, pairs[i].Value);
            }
            workingLocks = null;
        }
        LastCommitSucceeded = true; LastCommitError = string.Empty;
    }

    public string StableId { get { return "N:" + npcId.ToString(); } }
    public string DisplayName { get { string name = npc != null ? npc.EntityName : string.Empty; return string.IsNullOrWhiteSpace(name) ? "Companion" : name; } }
    public RemoteResourceSourceKind Kind { get { return RemoteResourceSourceKind.NpcStorage; } }
    public Vector3 Position { get { return npc != null ? npc.position : Vector3.zero; } }
    public bool IsLoaded { get { return npc != null && !npc.IsDead() && !npcId.IsEmpty; } }
    public bool IsBusy
    {
        get
        {
            if (RebirthNpcInteractionSessionService.IsInventoryAccessing(npcId)) return true;
            EntityRebirthDogCompanion dog = dogStorage ? npc as EntityRebirthDogCompanion : null;
            return dog != null && RebirthDogStorageService.IsOpenServer(dog.entityId);
        }
    }
    public bool IsActivated { get { return true; } }
    public bool IsExcluded { get { return false; } }
    public ItemStack[] Slots { get { return workingSlots; } }
    public PackedBoolArray SlotLocks { get { return workingLocks; } }
    public int Revision { get { return unchecked((int)startingRevision); } }

    public bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        RebirthNpcRuntimeState state = npc != null ? npc.RebirthRuntimeState : null;
        if (state == null || !LogisticsPreviewService.CanAccessNpcCompanion(player, state)) { reason = "companion ownership or party access denied"; return false; }
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(state.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Inventory) ||
            (profile.Category != RebirthNpcCategory.Survivor && profile.Category != RebirthNpcCategory.DogCompanion && profile.Category != RebirthNpcCategory.PantherCompanion))
        { reason = "companion has no eligible inventory"; return false; }
        if (exactOwner)
        {
            PlatformUserIdentifierAbs userId;
            if (!RemoteResourceAccess.TryGetPersistentId(player, out userId) || userId == null ||
                !string.Equals(userId.CombinedString, state.OwnerId, StringComparison.OrdinalIgnoreCase))
            { reason = "not exact companion owner"; return false; }
        }
        return true;
    }

    public void SetSlot(int slot, ItemStack value)
    {
        if (slot < 0 || slot >= workingSlots.Length) return;
        workingSlots[slot] = value != null ? value.Clone() : ItemStack.Empty.Clone();
    }

    public bool IsSnapshotCurrent()
    {
        if (npcId.IsEmpty) return false;
        if (dogStorage)
        {
            RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dog;
            if (!RebirthDogStateService.TryGetView(npcId, out record, out dog)) return false;
            uint revision = record.Inventory != null ? record.Inventory.InventoryRevision : 0U;
            return revision == startingRevision;
        }
        return RebirthNpcInventoryTransactionService.GetRevision(npcId) == startingRevision;
    }

    public void MarkModified()
    {
        LastCommitSucceeded = true; LastCommitError = string.Empty;
        if (npcId.IsEmpty) return;
        if (dogStorage)
        {
            string error;
            LastCommitSucceeded = RebirthDogInventoryService.CommitWorkingSlots(npcId, startingRevision, workingSlots, workingLocks, out error);
            LastCommitError = error ?? string.Empty;
            if (!LastCommitSucceeded) RemoteResourceDiagnostics.Write("Dog commit failed source=" + StableId + " reason=" + LastCommitError);
            return;
        }

        List<RebirthNpcInventoryMutation> mutations = new List<RebirthNpcInventoryMutation>();
        for (int i = 0; i < itemKeys.Length; i++)
        {
            int next = workingSlots[i] != null && !workingSlots[i].IsEmpty() ? workingSlots[i].count : 0;
            int delta = next - startingCounts[i]; if (delta != 0) mutations.Add(new RebirthNpcInventoryMutation(itemKeys[i], delta));
        }
        if (mutations.Count == 0) return;
        try
        {
            using (RebirthNpcInventoryAuthorityLease lease = RebirthNpcInventoryAuthorityService.Issue("remote.resources", RebirthNpcInventoryAuthorityOperations.Mutate, TimeSpan.FromSeconds(10), npcId))
            {
                uint revision;
                RebirthNpcInventoryTransactionResult result = RebirthNpcInventoryTransactionService.Apply(
                    new RebirthNpcInventoryTransaction(Guid.NewGuid(), npcId, startingRevision, lease.AuthorityKey, mutations), out revision);
                LastCommitSucceeded = result == RebirthNpcInventoryTransactionResult.Applied || result == RebirthNpcInventoryTransactionResult.Replayed;
                if (!LastCommitSucceeded) LastCommitError = result.ToString();
            }
        }
        catch (Exception ex) { LastCommitSucceeded = false; LastCommitError = ex.GetType().Name + ": " + ex.Message; }
        if (!LastCommitSucceeded) RemoteResourceDiagnostics.Write("NPC commit failed source=" + StableId + " reason=" + LastCommitError);
        else RemoteResourceSnapshotCache.InvalidateSource(StableId);
    }
}

public static class RemoteResourceSourceDiscovery
{
    public static List<IRemoteResourceSource> ResolveNearby(World world, EntityPlayer player, float radius)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || world == null || player == null || radius <= 0f)
            return new List<IRemoteResourceSource>();
        List<IRemoteResourceSource> result = RemoteResourceRegistry.ResolveNearby(
            world, player, radius, true, true, true);
        if (world == null || player == null || radius <= 0f) return result;

        float radiusSq = radius * radius;
        Bounds bounds = new Bounds(player.position, Vector3.one * radius * 2f);
        List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
        for (int i = 0; i < entities.Count; i++)
        {
            EntityRebirthNPC npc = entities[i] as EntityRebirthNPC;
            if (npc == null || npc.IsDead() || (npc.position - player.position).sqrMagnitude > radiusSq) continue;
            RemoteNpcResourceSource source = new RemoteNpcResourceSource(npc);
            string reason;
            if (!source.IsLoaded || !source.IsAuthorized(player, false, out reason)) continue;
            result.Add(source);
        }
        result.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        {
            float da = (a.Position - player.position).sqrMagnitude;
            float db = (b.Position - player.position).sqrMagnitude;
            int distance = da.CompareTo(db);
            return distance != 0 ? distance : string.Compare(a.StableId, b.StableId, StringComparison.Ordinal);
        });
        return result;
    }
}
