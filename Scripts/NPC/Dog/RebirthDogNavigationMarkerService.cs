using System;
using System.Collections.Generic;
using System.IO;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Owner-local dog navigation projection.
///
/// The persistent dog aggregate is the existence/position authority when a dog's entity
/// chunk is not loaded. A position-tracked NavObject is deliberately independent from the
/// live Entity so chunk unload does not remove the owner's map/compass marker.
///
/// No chunk observer is required. Loaded dogs continuously refresh the same marker; an
/// authoritative server baseline supplies markers for dogs that are already unloaded when
/// the owner joins.
/// </summary>
public static class RebirthDogNavigationMarkerService
{
    private const string NavClass = "FuriousRamsayCompanionNav";
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, NavObject> Markers =
        new Dictionary<string, NavObject>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> WarnedFailures =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public static void EnsureInitialized()
    {
        // Lifecycle registration is centralized in RebirthNpcLifecycle. This service does
        // not install a second PlayerSpawnedInWorld handler; capacity sync calls NotifyOwner
        // after owner projection has been reconciled.
    }

    /// <summary>
    /// Called from the live dog lifecycle. The native entity marker is replaced with a
    /// position marker so it survives entity/chunk deactivation.
    /// </summary>
    public static void SynchronizeLoadedDog(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.RebirthRuntimeState.StableId.IsEmpty)
            return;

        EnsureInitialized();
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        string stableId = state.StableId.ToString();

        if (!IsOwnedByLocalPlayer(state))
        {
            RemoveNativeEntityMarker(dog);
            Remove(stableId);
            return;
        }

        RemoveNativeEntityMarker(dog);
        string displayName = string.IsNullOrWhiteSpace(dog.EntityName)
            ? Localization.Get("xuiRebirthAttackDogType")
            : dog.EntityName.Trim();
        Upsert(stableId, displayName, dog.position);
    }

    /// <summary>
    /// Projects the authoritative owned-dog snapshot received from the server. Active dogs
    /// remain represented even when EntityId == -1; that means unloaded, not deleted.
    /// </summary>
    public static void SynchronizeSnapshot(IList<RebirthCompanionListEntry> companions)
    {
        EnsureInitialized();
        HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (companions != null)
        {
            for (int i = 0; i < companions.Count; i++)
            {
                RebirthCompanionListEntry row = companions[i];
                if (row == null || !row.IsDog || row.DogLifecycle != RebirthDogLifecycleKind.Active ||
                    string.IsNullOrWhiteSpace(row.StableId))
                    continue;

                string stableId = row.StableId.Trim();
                keep.Add(stableId);
                string displayName = string.IsNullOrWhiteSpace(row.Name)
                    ? Localization.Get("xuiRebirthAttackDogType")
                    : row.Name.Trim();
                Upsert(stableId, displayName, row.WorldPosition);
            }
        }

        List<string> remove = new List<string>();
        lock (Sync)
        {
            foreach (KeyValuePair<string, NavObject> pair in Markers)
                if (!keep.Contains(pair.Key)) remove.Add(pair.Key);
        }
        for (int i = 0; i < remove.Count; i++) Remove(remove[i]);
    }

    internal static void ReceiveNetworkSnapshot(List<RebirthDogNavigationMarkerRow> rows, bool complete)
    {
        List<RebirthCompanionListEntry> projected = new List<RebirthCompanionListEntry>();
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                RebirthDogNavigationMarkerRow row = rows[i];
                if (row == null) continue;
                projected.Add(new RebirthCompanionListEntry
                {
                    IsDog = true,
                    StableId = row.StableId ?? string.Empty,
                    Name = row.Name ?? string.Empty,
                    WorldPosition = row.Position,
                    DogLifecycle = row.Lifecycle
                });
            }
        }
        if (complete) SynchronizeSnapshot(projected);
        else
        {
            for (int i = 0; i < projected.Count; i++)
            {
                RebirthCompanionListEntry row = projected[i];
                if (row == null || string.IsNullOrWhiteSpace(row.StableId)) continue;
                string displayName = string.IsNullOrWhiteSpace(row.Name) ? Localization.Get("xuiRebirthAttackDogType") : row.Name.Trim();
                Upsert(row.StableId.Trim(), displayName, row.WorldPosition);
            }
        }
    }

    public static void Remove(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return;
        NavObject marker = null;
        lock (Sync)
        {
            if (!Markers.TryGetValue(stableId, out marker)) return;
            Markers.Remove(stableId);
        }
        if (marker != null && NavObjectManager.HasInstance)
        {
            try { NavObjectManager.Instance.UnRegisterNavObject(marker); }
            catch (Exception ex) { WarnOnce(stableId, "remove", ex); }
        }
    }

    public static void ResetForWorldChange()
    {
        List<NavObject> markers;
        lock (Sync)
        {
            markers = new List<NavObject>(Markers.Values);
            Markers.Clear();
            WarnedFailures.Clear();
        }
        if (NavObjectManager.HasInstance)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                try { NavObjectManager.Instance.UnRegisterNavObject(markers[i]); }
                catch (Exception ex)
                {
                    Log.Warning("[REBIRTH Dog Marker] world-reset unregister failed " +
                                ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
    }

    private static void Upsert(string stableId, string displayName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(stableId) || GameManager.Instance?.World == null) return;
        Vector3 markerPosition = MarkerPosition(stableId, position);
        EntityPlayerLocal localPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        if (localPlayer == null) return;

        NavObject marker = null;
        lock (Sync)
        {
            Markers.TryGetValue(stableId, out marker);
            if (marker != null && (!NavObjectManager.HasInstance ||
                !NavObjectManager.Instance.NavObjectList.Contains(marker)))
            {
                Markers.Remove(stableId);
                marker = null;
            }
        }

        if (marker == null)
        {
            if (!NavObjectManager.HasInstance) return;
            try
            {
                marker = NavObjectManager.Instance.RegisterNavObject(
                    NavClass, markerPosition, string.Empty, false, MarkerEntityId(stableId), localPlayer);
                if (marker == null)
                {
                    WarnOnce(stableId, "register", null);
                    return;
                }
                lock (Sync) Markers[stableId] = marker;
            }
            catch (Exception ex)
            {
                WarnOnce(stableId, "register", ex);
                return;
            }
        }

        try
        {
            marker.name = displayName ?? string.Empty;
            marker.TrackedPosition = markerPosition;
            marker.EntityID = MarkerEntityId(stableId);
            marker.OwnerEntity = localPlayer;
            marker.IsActive = true;
            marker.ForceDisabled = false;
            marker.hiddenOnCompass = false;
            marker.hiddenOnMap = false;
            RebirthCompanionColorService.ApplyDogMarkerColor(marker, stableId);
        }
        catch (Exception ex)
        {
            WarnOnce(stableId, "update", ex);
        }
    }

    private static bool IsOwnedByLocalPlayer(RebirthNpcRuntimeState state)
    {
        if (state == null || state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
            string.IsNullOrWhiteSpace(state.OwnerId) || GameManager.Instance == null)
            return false;
        PersistentPlayerData local = GameManager.Instance.GetPersistentLocalPlayer();
        return local?.PrimaryId != null &&
            string.Equals(state.OwnerId, local.PrimaryId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveNativeEntityMarker(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.NavObject == null || !NavObjectManager.HasInstance) return;
        NavObject native = dog.NavObject;
        try { NavObjectManager.Instance.UnRegisterNavObject(native); }
        catch (Exception ex)
        {
            string stableId = dog.RebirthRuntimeState != null && !dog.RebirthRuntimeState.StableId.IsEmpty
                ? dog.RebirthRuntimeState.StableId.ToString()
                : "entity:" + dog.entityId;
            WarnOnce(stableId, "native-marker-remove", ex);
        }
        dog.NavObject = null;
    }

    private static void WarnOnce(string stableId, string operation, Exception ex)
    {
        string key = (stableId ?? string.Empty) + "|" + (operation ?? string.Empty);
        lock (Sync)
        {
            if (!WarnedFailures.Add(key)) return;
        }
        string detail = ex == null ? "returned null" : ex.GetType().Name + ": " + ex.Message;
        Log.Warning("[REBIRTH Dog Marker] " + operation + " failed stableId=" +
                    (stableId ?? string.Empty) + " " + detail);
    }

    private static Vector3 MarkerPosition(string stableId, Vector3 position)
    {
        // NavObjectManager de-duplicates position markers that share an exact position even
        // when their synthetic ids differ. Apply a sub-centimetre stable offset so two dogs
        // parked on the same block retain independent markers without changing gameplay
        // location or the distance shown in the companion UI.
        unchecked
        {
            uint hash = 2166136261u;
            string value = stableId ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            float x = ((hash & 15u) - 7.5f) * 0.0005f;
            float z = (((hash >> 4) & 15u) - 7.5f) * 0.0005f;
            return position + new Vector3(x, 0f, z);
        }
    }

    // Position NavObjects can carry an entity id without tracking the Entity itself. A stable
    // synthetic negative id prevents a live world entity id collision and lets us distinguish
    // markers at different positions without reflection or an additional game-side registry.
    private static int MarkerEntityId(string stableId)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string value = stableId ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            int id = (int)(hash | 0x80000000u);
            if (id == -1) id = int.MinValue + 17;
            return id;
        }
    }

    public static void NotifyOwner(EntityPlayer player)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance?.World;
        if (connection == null || !connection.IsServer || world == null || player == null) return;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent?.PrimaryId == null) return;
        List<RebirthDogNavigationMarkerRow> rows = BuildRows(world, player, persistent.PrimaryId);

        EntityPlayerLocal local = world.GetPrimaryPlayer();
        if (local != null && local.entityId == player.entityId) ReceiveNetworkSnapshot(rows, true);
        if (!GameManager.IsDedicatedServer && player is EntityPlayerLocal) return;

        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthDogNavigationSnapshot>().Setup(rows),
            _attachedToEntityId: player.entityId);
    }

    private static List<RebirthDogNavigationMarkerRow> BuildRows(
        World world, EntityPlayer player, PlatformUserIdentifierAbs userId)
    {
        List<RebirthDogNavigationMarkerRow> rows = new List<RebirthDogNavigationMarkerRow>();
        if (world == null || player == null || userId == null) return rows;
        List<RebirthCompanionListEntry> companions = RebirthCompanionService.GetNearbyOwned(
            world, player, userId, RebirthCompanionService.ManagementRadius);
        for (int i = 0; i < companions.Count; i++)
        {
            RebirthCompanionListEntry entry = companions[i];
            if (entry == null || !entry.IsDog || entry.DogLifecycle != RebirthDogLifecycleKind.Active ||
                string.IsNullOrWhiteSpace(entry.StableId)) continue;
            rows.Add(new RebirthDogNavigationMarkerRow
            {
                StableId = entry.StableId,
                Name = entry.Name,
                Position = entry.WorldPosition,
                Lifecycle = entry.DogLifecycle
            });
        }
        return rows;
    }

}

public sealed class RebirthDogNavigationMarkerRow
{
    public string StableId = string.Empty;
    public string Name = string.Empty;
    public Vector3 Position;
    public RebirthDogLifecycleKind Lifecycle;
}

[Preserve]
public sealed class NetPackageRebirthDogNavigationSnapshot : NetPackage
{
    private readonly List<RebirthDogNavigationMarkerRow> rows = new List<RebirthDogNavigationMarkerRow>();
    private uint worldEpoch;
    private bool complete;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthDogNavigationSnapshot Setup(IList<RebirthDogNavigationMarkerRow> source)
    {
        rows.Clear();
        worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        complete = source == null || source.Count <= RebirthNpcNetworkFraming.MaxDogMarkers;
        if (source == null) return this;
        int count = Math.Min(RebirthNpcNetworkFraming.MaxDogMarkers, source.Count);
        for (int i = 0; i < count; i++)
        {
            RebirthDogNavigationMarkerRow row = source[i];
            if (row == null) continue;
            rows.Add(new RebirthDogNavigationMarkerRow
            {
                StableId = row.StableId ?? string.Empty,
                Name = row.Name ?? string.Empty,
                Position = row.Position,
                Lifecycle = row.Lifecycle
            });
        }
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        rows.Clear();
        BinaryReader b = (BinaryReader)reader;
        worldEpoch = b.ReadUInt32();
        complete = b.ReadBoolean();
        int count = b.ReadByte();
        if (count > RebirthNpcNetworkFraming.MaxDogMarkers) throw new InvalidDataException("Dog marker snapshot exceeds configured bound.");
        for (int i = 0; i < count; i++)
        {
            rows.Add(new RebirthDogNavigationMarkerRow
            {
                StableId = RebirthNpcNetworkFraming.ReadString(b, RebirthNpcNetworkFraming.MaxId),
                Name = RebirthNpcNetworkFraming.ReadString(b, RebirthNpcNetworkFraming.MaxLabel),
                Position = new Vector3(b.ReadSingle(), b.ReadSingle(), b.ReadSingle()),
                Lifecycle = (RebirthDogLifecycleKind)b.ReadByte()
            });
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter b = (BinaryWriter)writer;
        b.Write(worldEpoch);
        b.Write(complete);
        int count = Math.Min(RebirthNpcNetworkFraming.MaxDogMarkers, rows.Count);
        b.Write((byte)count);
        for (int i = 0; i < count; i++)
        {
            RebirthDogNavigationMarkerRow row = rows[i];
            RebirthNpcNetworkFraming.WriteString(b, row.StableId, RebirthNpcNetworkFraming.MaxId);
            RebirthNpcNetworkFraming.WriteString(b, row.Name, RebirthNpcNetworkFraming.MaxLabel);
            b.Write(row.Position.x);
            b.Write(row.Position.y);
            b.Write(row.Position.z);
            b.Write((byte)row.Lifecycle);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch))
            RebirthDogNavigationMarkerService.ReceiveNetworkSnapshot(rows, complete);
    }

    public int GetLength() => 0;
}
