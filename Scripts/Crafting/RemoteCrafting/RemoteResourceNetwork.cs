using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable


public static class RemoteResourcesRuntimePolicy
{
    private static bool enabled = true;
    private static float radius = RebirthResourceDistancePolicy.RemoteResourcesDefault;

    public static bool Enabled { get { return enabled; } }
    public static float Radius { get { return radius; } }

    public static void SetEnabled(bool value)
    {
        if (enabled == value) return;
        enabled = value;
        RemoteResourceLiveSync.Reset();
        RemoteResourceSnapshotCache.InvalidateAll();
        if (enabled) RemoteResourceLiveSync.NotifyGlobal("resources enabled");
        RemoteResourceDiagnostics.Write("runtime enabled=" + enabled);
    }

    public static void SetDistance(int value)
    {
        float normalized = RebirthResourceDistancePolicy.NormalizeRemoteResources(value);
        if (Math.Abs(radius - normalized) < 0.01f) return;
        radius = normalized;
        RemoteResourceLiveSync.Reset();
        RemoteResourceSnapshotCache.InvalidateAll();
        if (enabled) RemoteResourceLiveSync.NotifyGlobal("resource radius changed");
        RemoteResourceDiagnostics.Write("runtime radius=" + radius + "m");
    }
}

public enum RemoteResourceSourceKind : byte
{
    StaticContainer = 1,
    WorkstationOutput = 2,
    VehicleStorage = 3,
    DroneStorage = 4,
    NpcStorage = 5
}

public interface IRemoteResourceSource
{
    string StableId { get; }
    string DisplayName { get; }
    RemoteResourceSourceKind Kind { get; }
    Vector3 Position { get; }
    bool IsLoaded { get; }
    bool IsBusy { get; }
    bool IsActivated { get; }
    bool IsExcluded { get; }
    ItemStack[] Slots { get; }
    PackedBoolArray SlotLocks { get; }
    int Revision { get; }
    bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason);
    void SetSlot(int slot, ItemStack value);
    void MarkModified();
}

public sealed class RemoteResourceRegistration
{
    public readonly string StableId;
    public readonly RemoteResourceSourceKind Kind;
    public readonly Vector3i Position;

    public RemoteResourceRegistration(string stableId, RemoteResourceSourceKind kind, Vector3i position)
    {
        StableId = stableId;
        Kind = kind;
        Position = position;
    }
}

public static class RemoteResourceDiagnostics
{
    public static bool Enabled;
    public static void Write(string message)
    {
        if (Enabled) Log.Out("[RemoteResources] " + message);
    }

    public static void WriteSource(EntityPlayer player, IRemoteResourceSource source, bool eligible, string reason, Dictionary<int, int> counts)
    {
        if (!Enabled || source == null) return;
        int total = 0;
        if (counts != null) foreach (KeyValuePair<int, int> pair in counts) total += pair.Value;
        float distance = player != null ? Vector3.Distance(player.position, source.Position) : -1f;
        Log.Out("[RemoteResources] source=" + source.StableId +
            " kind=" + source.Kind +
            " name='" + source.DisplayName + "'" +
            " distance=" + distance.ToString("0.0") + "m" +
            " revision=" + source.Revision +
            " eligible=" + eligible +
            " items=" + total +
            " reason='" + (reason ?? string.Empty) + "'");
    }
}

public static class RemoteResourceIdentity
{
    public static string Static(Vector3i position) { return "S:" + position.x + "," + position.y + "," + position.z; }
    public static string Workstation(Vector3i position) { return "W:" + position.x + "," + position.y + "," + position.z; }
    public static string Vehicle(int entityId) { return "V:" + entityId; }
    public static string Drone(int entityId) { return "D:" + entityId; }
}

public static class RemoteResourceStateStore
{
    private const string FileName = "RebirthRemoteResources.dat";
    private const string Magic = "RBRR2";
    private const string LegacyMagic = "RBRR1";
    private static readonly object gate = new object();
    private static readonly HashSet<string> activated = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, HashSet<string>> excludedByPlayer =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private static string loadedSaveDirectory;
    private static bool loaded;
    private static bool dirty;
    private static int generation;

    public static int Generation { get { return generation; } }

    public static bool IsActivated(string stableId)
    {
        EnsureLoaded();
        lock (gate) return activated.Contains(stableId);
    }

    // Exclusion is intentionally player-specific. Actor-neutral callers must not
    // accidentally turn one player's personal preference into a global exclusion.
    public static bool IsExcluded(string stableId)
    {
        return false;
    }

    public static bool IsExcluded(string stableId, PlatformUserIdentifierAbs userId)
    {
        if (string.IsNullOrEmpty(stableId) || userId == null) return false;
        EnsureLoaded();
        string playerKey = userId.CombinedString;
        if (string.IsNullOrEmpty(playerKey)) return false;
        lock (gate)
        {
            HashSet<string> values;
            return excludedByPlayer.TryGetValue(playerKey, out values) && values.Contains(stableId);
        }
    }

    public static bool Activate(string stableId)
    {
        if (string.IsNullOrEmpty(stableId) || !IsAuthoritativeServer()) return false;
        EnsureLoaded();
        bool changed;
        lock (gate) changed = activated.Add(stableId);
        if (changed)
        {
            generation++;
            RemoteResourceSnapshotCache.InvalidateAll();
            Save();
            RemoteResourceDiagnostics.Write("activated " + stableId);
        }
        return changed;
    }

    public static bool SetExcluded(
        string stableId,
        PlatformUserIdentifierAbs userId,
        bool value)
    {
        if (string.IsNullOrEmpty(stableId) || userId == null || !IsAuthoritativeServer()) return false;
        EnsureLoaded();
        string playerKey = userId.CombinedString;
        if (string.IsNullOrEmpty(playerKey)) return false;

        bool changed;
        lock (gate)
        {
            HashSet<string> values;
            if (!excludedByPlayer.TryGetValue(playerKey, out values))
            {
                if (!value) return false;
                values = new HashSet<string>(StringComparer.Ordinal);
                excludedByPlayer[playerKey] = values;
            }

            changed = value ? values.Add(stableId) : values.Remove(stableId);
            if (!value && values.Count == 0) excludedByPlayer.Remove(playerKey);
        }
        if (changed)
        {
            generation++;
            RemoteResourceSnapshotCache.InvalidateAll();
            Save();
            RemoteResourceDiagnostics.Write(
                (value ? "excluded " : "included ") + stableId + " player=" + playerKey);
        }
        return changed;
    }

    // Preserve every player's broadcast preference when a container changes position.
    public static string[] CapturePackedExclusions(string stableId)
    {
        EnsureLoaded();
        lock (gate)
        {
            var result = new List<string>();
            foreach (var pair in excludedByPlayer) if (pair.Value.Contains(stableId)) result.Add(pair.Key);
            return result.ToArray();
        }
    }

    public static void RestorePackedState(string stableId, bool wasActivated, string[] exclusions)
    {
        if (!IsAuthoritativeServer()) return;
        EnsureLoaded();
        lock (gate)
        {
            if (wasActivated) activated.Add(stableId); else activated.Remove(stableId);
            foreach (var values in excludedByPlayer.Values) values.Remove(stableId);
            foreach (string key in exclusions ?? new string[0])
            {
                HashSet<string> values;
                if (!excludedByPlayer.TryGetValue(key, out values))
                    excludedByPlayer[key] = values = new HashSet<string>(StringComparer.Ordinal);
                values.Add(stableId);
            }
            generation++;
        }
        RemoteResourceSnapshotCache.InvalidateAll();
        Save();
    }

    public static void Forget(string stableId)
    {
        if (string.IsNullOrEmpty(stableId) || !IsAuthoritativeServer()) return;
        EnsureLoaded();
        bool changed = false;
        lock (gate)
        {
            changed = activated.Remove(stableId);
            List<string> emptyPlayers = null;
            foreach (KeyValuePair<string, HashSet<string>> pair in excludedByPlayer)
            {
                if (!pair.Value.Remove(stableId)) continue;
                changed = true;
                if (pair.Value.Count == 0)
                {
                    if (emptyPlayers == null) emptyPlayers = new List<string>();
                    emptyPlayers.Add(pair.Key);
                }
            }
            if (emptyPlayers != null)
                for (int i = 0; i < emptyPlayers.Count; i++) excludedByPlayer.Remove(emptyPlayers[i]);
        }
        if (changed)
        {
            generation++;
            RemoteResourceSnapshotCache.InvalidateAll();
            Save();
        }
    }

    public static void ResetForWorldUnload()
    {
        lock (gate)
        {
            activated.Clear();
            excludedByPlayer.Clear();
            loadedSaveDirectory = null;
            loaded = false;
            dirty = false;
            generation++;
        }
    }

    private static bool IsAuthoritativeServer()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer;
    }

    private static void EnsureLoaded()
    {
        string saveDirectory = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(saveDirectory)) return;
        lock (gate)
        {
            if (loaded && string.Equals(loadedSaveDirectory, saveDirectory, StringComparison.Ordinal)) return;
            loadedSaveDirectory = saveDirectory;
            loaded = true;
            dirty = false;
            generation++;

            string path = Path.Combine(saveDirectory, FileName);
            HashSet<string> stagedActivated;
            Dictionary<string, HashSet<string>> stagedExcluded;
            string error;
            bool recovered = false;
            if (!TryLoadFile(path, out stagedActivated, out stagedExcluded, out error))
            {
                string primaryError = error;
                if (!TryLoadFile(path + ".bak", out stagedActivated, out stagedExcluded, out error))
                {
                    activated.Clear();
                    excludedByPlayer.Clear();
                    if (File.Exists(path) || File.Exists(path + ".bak"))
                        Log.Error("[RemoteResources] Failed to load primary/backup persistence: primary=" + primaryError + " backup=" + error);
                    return;
                }
                recovered = true;
            }

            activated.Clear();
            foreach (string id in stagedActivated) activated.Add(id);
            excludedByPlayer.Clear();
            foreach (KeyValuePair<string, HashSet<string>> pair in stagedExcluded)
                excludedByPlayer[pair.Key] = pair.Value;
            int exclusionTotal = 0;
            foreach (HashSet<string> values in excludedByPlayer.Values) exclusionTotal += values.Count;
            RemoteResourceDiagnostics.Write("loaded activation=" + activated.Count +
                " perPlayerExclusion=" + exclusionTotal + (recovered ? " recovered=backup" : string.Empty));
        }
    }

    private static bool TryLoadFile(string path, out HashSet<string> stagedActivated,
        out Dictionary<string, HashSet<string>> stagedExcluded, out string error)
    {
        stagedActivated = new HashSet<string>(StringComparer.Ordinal);
        stagedExcluded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        error = string.Empty;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = "missing"; return false; }
        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                string magic = reader.ReadString();
                if (!string.Equals(magic, Magic, StringComparison.Ordinal) &&
                    !string.Equals(magic, LegacyMagic, StringComparison.Ordinal))
                    throw new InvalidDataException("unsupported persistence version");

                int activationCount = reader.ReadInt32();
                if (activationCount < 0 || activationCount > 1000000) throw new InvalidDataException("invalid activation count");
                for (int i = 0; i < activationCount; i++) stagedActivated.Add(reader.ReadString());

                int exclusionCount = reader.ReadInt32();
                if (exclusionCount < 0 || exclusionCount > 1000000) throw new InvalidDataException("invalid exclusion count");
                if (string.Equals(magic, LegacyMagic, StringComparison.Ordinal))
                {
                    for (int i = 0; i < exclusionCount; i++) reader.ReadString();
                    if (exclusionCount > 0)
                        Log.Warning("[RemoteResources] Migrated RBRR1: cleared " + exclusionCount +
                            " legacy global exclusions for per-player state.");
                }
                else
                {
                    for (int i = 0; i < exclusionCount; i++)
                    {
                        string playerKey = reader.ReadString();
                        string stableId = reader.ReadString();
                        if (string.IsNullOrEmpty(playerKey) || string.IsNullOrEmpty(stableId)) continue;
                        HashSet<string> values;
                        if (!stagedExcluded.TryGetValue(playerKey, out values))
                            stagedExcluded[playerKey] = values = new HashSet<string>(StringComparer.Ordinal);
                        values.Add(stableId);
                    }
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("trailing persistence bytes");
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            stagedActivated.Clear();
            stagedExcluded.Clear();
            return false;
        }
    }

    private static bool Save()
    {
        string saveDirectory = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(saveDirectory)) return false;
        try
        {
            Directory.CreateDirectory(saveDirectory);
            string path = Path.Combine(saveDirectory, FileName);
            string tempPath = path + ".tmp";
            lock (gate)
            {
                dirty = true;
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(activated.Count);
                    foreach (string id in activated) writer.Write(id);
                    int exclusionCount = 0;
                    foreach (HashSet<string> values in excludedByPlayer.Values) exclusionCount += values.Count;
                    writer.Write(exclusionCount);
                    foreach (KeyValuePair<string, HashSet<string>> pair in excludedByPlayer)
                        foreach (string id in pair.Value) { writer.Write(pair.Key); writer.Write(id); }
                    writer.Flush();
                    stream.Flush(true);
                }
                string publishError;
                if (!RebirthDurableFileCommit.TryPublish(tempPath, path, out publishError))
                    throw new IOException("durable publication failed: " + publishError);
                dirty = false;
                return true;
            }
        }
        catch (Exception e)
        {
            Log.Error("[RemoteResources] Failed to save persistence; state remains dirty for retry: " + e);
            return false;
        }
    }

    public static void FlushPending()
    {
        EnsureLoaded();
        lock (gate) { if (!dirty) return; }
        Save();
    }

}

public static class RemoteResourceRegistry
{
    public const int BucketSize = 32;
    private static readonly Dictionary<string, RemoteResourceRegistration> byId = new Dictionary<string, RemoteResourceRegistration>(StringComparer.Ordinal);
    private static readonly Dictionary<long, HashSet<string>> buckets = new Dictionary<long, HashSet<string>>();
    private static int generation;

    public static int Generation { get { return generation; } }

    public static void Clear()
    {
        byId.Clear();
        buckets.Clear();
        generation++;
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static void Register(TileEntity tileEntity)
    {
        if (tileEntity == null) return;
        Vector3i position = tileEntity.ToWorldPos();
        string id;
        RemoteResourceSourceKind kind;
        if (tileEntity is TileEntityWorkstation)
        {
            id = RemoteResourceIdentity.Workstation(position);
            kind = RemoteResourceSourceKind.WorkstationOutput;
        }
        else
        {
            TEFeatureStorage loot;
            if (!tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out loot) ||
                !RemoteResourceSourcePolicy.IsSupportedStatic(tileEntity, loot)) return;
            id = RemoteResourceIdentity.Static(position);
            kind = RemoteResourceSourceKind.StaticContainer;
        }

        RemoteResourceRegistration old;
        if (byId.TryGetValue(id, out old))
        {
            if (old.Kind == kind && old.Position == position) return;
            RemoveFromBucket(old);
        }
        RemoteResourceRegistration registration = new RemoteResourceRegistration(id, kind, position);
        byId[id] = registration;
        AddToBucket(registration);
        generation++;
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static void Unregister(TileEntity tileEntity)
    {
        if (tileEntity == null) return;
        Vector3i position = tileEntity.ToWorldPos();
        string id = tileEntity is TileEntityWorkstation ? RemoteResourceIdentity.Workstation(position) : RemoteResourceIdentity.Static(position);
        RemoteResourceRegistration registration;
        if (!byId.TryGetValue(id, out registration)) return;
        byId.Remove(id);
        RemoveFromBucket(registration);
        generation++;
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static List<RemoteResourceRegistration> Query(Vector3 center, float radius, RemoteResourceSourceKind kind)
    {
        List<RemoteResourceRegistration> result = new List<RemoteResourceRegistration>();
        float radiusSquared = radius * radius;
        int minX = Mathf.FloorToInt((center.x - radius) / BucketSize);
        int maxX = Mathf.FloorToInt((center.x + radius) / BucketSize);
        int minZ = Mathf.FloorToInt((center.z - radius) / BucketSize);
        int maxZ = Mathf.FloorToInt((center.z + radius) / BucketSize);
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int bucketX = minX; bucketX <= maxX; bucketX++)
        {
            for (int bucketZ = minZ; bucketZ <= maxZ; bucketZ++)
            {
                HashSet<string> ids;
                if (!buckets.TryGetValue(BucketKey(bucketX, bucketZ), out ids)) continue;
                foreach (string id in ids)
                {
                    if (!seen.Add(id)) continue;
                    RemoteResourceRegistration registration;
                    if (!byId.TryGetValue(id, out registration) || registration.Kind != kind) continue;
                    if ((registration.Position.ToVector3() - center).sqrMagnitude <= radiusSquared) result.Add(registration);
                }
            }
        }
        result.Sort(delegate(RemoteResourceRegistration a, RemoteResourceRegistration b)
        {
            int distance = (a.Position.ToVector3() - center).sqrMagnitude.CompareTo((b.Position.ToVector3() - center).sqrMagnitude);
            if (distance != 0) return distance;
            int x = a.Position.x.CompareTo(b.Position.x); if (x != 0) return x;
            int y = a.Position.y.CompareTo(b.Position.y); if (y != 0) return y;
            return a.Position.z.CompareTo(b.Position.z);
        });
        return result;
    }

    public static List<IRemoteResourceSource> ResolveNearby(World world, EntityPlayer player, float radius, bool includeStatic, bool includeWorkstations, bool includeMobile)
    {
        List<IRemoteResourceSource> result = new List<IRemoteResourceSource>();
        if (world == null || player == null) return result;
        if (includeStatic)
        {
            List<RemoteResourceRegistration> registrations = Query(player.position, radius, RemoteResourceSourceKind.StaticContainer);
            for (int i = 0; i < registrations.Count; i++) result.Add(new StaticContainerResourceSource(world, registrations[i].Position));
        }
        if (includeWorkstations)
        {
            List<RemoteResourceRegistration> registrations = Query(player.position, radius, RemoteResourceSourceKind.WorkstationOutput);
            for (int i = 0; i < registrations.Count; i++) result.Add(new WorkstationOutputResourceSource(world, registrations[i].Position));
        }
        if (includeMobile)
        {
            Bounds bounds = new Bounds(player.position, Vector3.one * radius * 2f);
            List<Entity> entities = world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
            for (int i = 0; i < entities.Count; i++)
            {
                EntityVehicle vehicle = entities[i] as EntityVehicle;
                if (vehicle != null && vehicle.bag != null && (vehicle.position - player.position).sqrMagnitude <= radius * radius)
                {
                    result.Add(new VehicleResourceSource(vehicle));
                    continue;
                }
                EntityDrone drone = entities[i] as EntityDrone;
                if (drone != null && drone.bag != null && (drone.position - player.position).sqrMagnitude <= radius * radius)
                    result.Add(new DroneResourceSource(drone));
            }
        }
        result.Sort(delegate(IRemoteResourceSource a, IRemoteResourceSource b)
        {
            int distance = (a.Position - player.position).sqrMagnitude.CompareTo((b.Position - player.position).sqrMagnitude);
            if (distance != 0) return distance;
            return string.CompareOrdinal(a.StableId, b.StableId);
        });
        return result;
    }

    private static void AddToBucket(RemoteResourceRegistration registration)
    {
        long key = BucketKey(Mathf.FloorToInt((float)registration.Position.x / BucketSize), Mathf.FloorToInt((float)registration.Position.z / BucketSize));
        HashSet<string> ids;
        if (!buckets.TryGetValue(key, out ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            buckets[key] = ids;
        }
        ids.Add(registration.StableId);
    }

    private static void RemoveFromBucket(RemoteResourceRegistration registration)
    {
        long key = BucketKey(Mathf.FloorToInt((float)registration.Position.x / BucketSize), Mathf.FloorToInt((float)registration.Position.z / BucketSize));
        HashSet<string> ids;
        if (!buckets.TryGetValue(key, out ids)) return;
        ids.Remove(registration.StableId);
        if (ids.Count == 0) buckets.Remove(key);
    }

    private static long BucketKey(int x, int z) { return ((long)x << 32) ^ (uint)z; }
}

public static class RemoteResourceSourcePolicy
{
    public static bool IsSupportedStatic(TileEntity tileEntity, TEFeatureStorage loot)
    {
        if (!(tileEntity is TileEntityComposite) || loot == null) return false;
        try
        {
            // Temporary/event loot containers that destroy themselves on close are not
            // stable resource-network sources. Player storage and persistent POI storage
            // become eligible after a legitimate player deposit or an explicit Quick Stack
            // category assignment.
            if (loot.ShouldDestroyOnClose()) return false;
        }
        catch
        {
            return false;
        }
        return true;
    }
}

public static class RemoteResourceAccess
{
    public static bool IsServerBusy(ILockTarget target)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer && target != null && LockManager.Instance.IsLockedServer(target);
    }

    public static bool TryGetPersistentId(EntityPlayer player, out PlatformUserIdentifierAbs userId)
    {
        userId = null;
        if (player == null || GameManager.Instance == null) return false;
        PersistentPlayerData data = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(player.entityId);
        if (data == null || data.PrimaryId == null) return false;
        userId = data.PrimaryId;
        return true;
    }

    public static bool AuthorizeComposite(TileEntityComposite composite, EntityPlayer player, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        PlatformUserIdentifierAbs userId;
        if (composite == null || !TryGetPersistentId(player, out userId))
        {
            reason = "missing persistent player or composite";
            return false;
        }
        bool owner = composite.Owner != null && composite.Owner.Equals(userId);
        if (exactOwner && !owner)
        {
            reason = "exact owner required";
            return false;
        }
        TEFeatureLockable lockable = composite.GetFeature<TEFeatureLockable>();
        if (lockable != null && lockable.IsLocked() && !RebirthSecureAccessPolicy.CanAccess(player, lockable, RebirthSecureAccessPurpose.RemoteResourceRead, out reason)) return false;
        return true;
    }

    public static bool AuthorizeLockable(ILockable lockable, EntityPlayer player, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        PlatformUserIdentifierAbs userId;
        if (lockable == null || !TryGetPersistentId(player, out userId))
        {
            reason = "missing persistent player or lockable";
            return false;
        }
        if (exactOwner && !lockable.IsOwner(userId))
        {
            reason = "exact owner required";
            return false;
        }
        if (!RebirthSecureAccessPolicy.CanAccess(player, lockable, RebirthSecureAccessPurpose.RemoteResourceRead, out reason)) return false;
        return true;
    }

    public static int ComputeRevision(ItemStack[] slots)
    {
        unchecked
        {
            int hash = 17;
            if (slots == null) return hash;
            hash = hash * 31 + slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.IsEmpty()) { hash = hash * 31; continue; }
                hash = hash * 31 + stack.itemValue.type;
                hash = hash * 31 + stack.count;
                hash = hash * 31 + stack.itemValue.Quality;
                hash = hash * 31 + Mathf.RoundToInt(stack.itemValue.UseTimes * 1000f);
            }
            return hash;
        }
    }

    public static bool HasUsableItem(ItemStack[] slots)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++) if (slots[i] != null && !slots[i].IsEmpty()) return true;
        return false;
    }
}

public sealed class StaticContainerResourceSource : IRemoteResourceSource
{
    private readonly World world;
    private readonly Vector3i position;
    public StaticContainerResourceSource(World value, Vector3i pos) { world = value; position = pos; }
    private TileEntity TileEntity { get { return world != null ? world.GetTileEntity(position) : null; } }
    private TEFeatureStorage Loot { get { TEFeatureStorage value; TileEntity te = TileEntity; return te != null && te.TryGetSelfOrFeature<TEFeatureStorage>(out value) ? value : null; } }
    public string StableId { get { return RemoteResourceIdentity.Static(position); } }
    public string DisplayName
    {
        get
        {
            TileEntity te = TileEntity;
            TileEntityComposite composite = te as TileEntityComposite;
            string customName = RebirthContainerRenameService.GetCustomName(
                world, position, composite);
            if (!string.IsNullOrWhiteSpace(customName))
                return customName.Trim();

            BlockValue blockValue = world != null ? world.GetBlock(position) : BlockValue.Air;
            string localized = blockValue.Block != null ? blockValue.Block.GetLocalizedBlockName() : string.Empty;
            return string.IsNullOrWhiteSpace(localized)
                ? Localization.Get("xuiRebirthContainerStorage")
                : localized;
        }
    }
    public RemoteResourceSourceKind Kind { get { return RemoteResourceSourceKind.StaticContainer; } }
    public Vector3 Position { get { return position.ToVector3(); } }
    public bool IsLoaded { get { TileEntity te = TileEntity; TEFeatureStorage loot = Loot; return te != null && RemoteResourceSourcePolicy.IsSupportedStatic(te, loot); } }
    public bool IsBusy { get { TileEntity te = TileEntity; TEFeatureStorage loot = Loot; return te == null || loot == null || te.IsUserAccessing() || RemoteResourceAccess.IsServerBusy(loot); } }
    public bool IsActivated { get { return RemoteResourceStateStore.IsActivated(StableId); } }
    public bool IsExcluded { get { return RemoteResourceStateStore.IsExcluded(StableId); } }
    public ItemStack[] Slots { get { TEFeatureStorage loot = Loot; return loot != null ? loot.ItemGrid.items : null; } }
    public PackedBoolArray SlotLocks { get { TEFeatureStorage loot = Loot; return loot != null ? loot.ItemGrid.SlotLocks : null; } }
    public int Revision { get { return RemoteResourceAccess.ComputeRevision(Slots); } }
    public bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason)
    {
        TileEntity te = TileEntity;
        if (te == null) { reason = "unloaded"; return false; }
        TileEntityComposite composite = te as TileEntityComposite;
        if (composite == null) { reason = "unsupported static source"; return false; }
        return RemoteResourceAccess.AuthorizeComposite(composite, player, exactOwner, out reason);
    }
    public void SetSlot(int slot, ItemStack value) { TEFeatureStorage loot = Loot; if (loot != null) loot.UpdateSlot(slot, value); }
    public void MarkModified()
    {
        TEFeatureStorage loot = Loot;
        if (loot != null) loot.SetModified();
        RemoteResourceLiveSync.NotifySourceChanged(StableId, Position, "static source modified");
    }
}

public sealed class WorkstationOutputResourceSource : IRemoteResourceSource
{
    private readonly World world;
    private readonly Vector3i position;
    public WorkstationOutputResourceSource(World value, Vector3i pos) { world = value; position = pos; }
    private TileEntityWorkstation Workstation { get { return world != null ? world.GetTileEntity(position) as TileEntityWorkstation : null; } }
    public string StableId { get { return RemoteResourceIdentity.Workstation(position); } }
    public string DisplayName
    {
        get
        {
            BlockValue blockValue = world != null ? world.GetBlock(position) : BlockValue.Air;
            string localized = blockValue.Block != null ? blockValue.Block.GetLocalizedBlockName() : string.Empty;
            return string.IsNullOrWhiteSpace(localized)
                ? Localization.Get("xuiRebirthWorkstationOutput")
                : localized;
        }
    }
    public RemoteResourceSourceKind Kind { get { return RemoteResourceSourceKind.WorkstationOutput; } }
    public Vector3 Position { get { return position.ToVector3(); } }
    public bool IsLoaded { get { return Workstation != null; } }
    public bool IsBusy { get { TileEntityWorkstation ws = Workstation; return ws == null || ws.IsUserAccessing() || RemoteResourceAccess.IsServerBusy(ws); } }
    public bool IsActivated { get { return true; } }
    public bool IsExcluded { get { return RemoteResourceStateStore.IsExcluded(StableId); } }
    public ItemStack[] Slots { get { TileEntityWorkstation ws = Workstation; return ws != null ? ws.Output : null; } }
    public PackedBoolArray SlotLocks { get { return null; } }
    public int Revision { get { return RemoteResourceAccess.ComputeRevision(Slots); } }
    public bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason)
    {
        PlatformUserIdentifierAbs userId;
        if (!RemoteResourceAccess.TryGetPersistentId(player, out userId))
        { reason = "missing persistent requester"; return false; }
        return RebirthWorkstationSecurityService.CanAccessWorkstation(world, position, player,
            userId, exactOwner, RebirthSecureAccessPurpose.RemoteResourceRead, out reason);
    }
    public void SetSlot(int slot, ItemStack value)
    {
        TileEntityWorkstation ws = Workstation;
        if (ws == null || ws.Output == null || slot < 0 || slot >= ws.Output.Length) return;
        ItemStack[] output = ItemStack.Clone((IList<ItemStack>)ws.Output);
        output[slot] = value.Clone();
        ws.Output = output;
    }
    public void MarkModified() { TileEntityWorkstation ws = Workstation; if (ws != null) ws.setModified(); RemoteResourceSnapshotCache.InvalidateSource(StableId); }
}

public sealed class VehicleResourceSource : IRemoteResourceSource
{
    private readonly EntityVehicle vehicle;
    public VehicleResourceSource(EntityVehicle value) { vehicle = value; }
    public EntityVehicle Vehicle { get { return vehicle; } }
    public string StableId { get { return RemoteResourceIdentity.Vehicle(vehicle != null ? vehicle.entityId : -1); } }
    public string DisplayName
    {
        get
        {
            string value = vehicle != null ? vehicle.EntityName : string.Empty;
            return string.IsNullOrWhiteSpace(value) ? Localization.Get("xuiRebirthVehicleStorage") : value;
        }
    }
    public RemoteResourceSourceKind Kind { get { return RemoteResourceSourceKind.VehicleStorage; } }
    public Vector3 Position { get { return vehicle != null ? vehicle.position : Vector3.zero; } }
    public bool IsLoaded { get { return vehicle != null && !vehicle.IsDead() && vehicle.bag != null; } }
    public bool IsBusy { get { return vehicle == null || RemoteResourceAccess.IsServerBusy(vehicle); } }
    public bool IsActivated { get { return RemoteResourceStateStore.IsActivated(StableId); } }
    public bool IsExcluded { get { return RemoteResourceStateStore.IsExcluded(StableId); } }
    public ItemStack[] Slots { get { return vehicle != null && vehicle.bag != null ? vehicle.bag.ItemGrid.items : null; } }
    public PackedBoolArray SlotLocks { get { return vehicle != null && vehicle.bag != null ? vehicle.bag.LockedSlots : null; } }
    public int Revision { get { return RemoteResourceAccess.ComputeRevision(Slots); } }
    public bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason) { return RemoteResourceAccess.AuthorizeLockable(vehicle, player, exactOwner, out reason); }
    public void SetSlot(int slot, ItemStack value) { if (vehicle != null && vehicle.bag != null) vehicle.bag.SetSlot(slot, value); }
    public void MarkModified()
    {
        if (vehicle != null) vehicle.SetBagModified();
        RemoteResourceLiveSync.NotifySourceChanged(StableId, Position, "vehicle storage modified");
    }
}

public sealed class DroneResourceSource : IRemoteResourceSource
{
    private readonly EntityDrone drone;
    public DroneResourceSource(EntityDrone value) { drone = value; }
    public EntityDrone Drone { get { return drone; } }
    public string StableId { get { return RemoteResourceIdentity.Drone(drone != null ? drone.entityId : -1); } }
    public string DisplayName
    {
        get
        {
            string value = drone != null ? drone.EntityName : string.Empty;
            return string.IsNullOrWhiteSpace(value) ? Localization.Get("xuiRebirthDroneStorage") : value;
        }
    }
    public RemoteResourceSourceKind Kind { get { return RemoteResourceSourceKind.DroneStorage; } }
    public Vector3 Position { get { return drone != null ? drone.position : Vector3.zero; } }
    public bool IsLoaded { get { return drone != null && !drone.IsDead() && drone.bag != null; } }
    public bool IsBusy { get { return drone == null || RemoteResourceAccess.IsServerBusy(drone); } }
    public bool IsActivated { get { return RemoteResourceStateStore.IsActivated(StableId); } }
    public bool IsExcluded { get { return RemoteResourceStateStore.IsExcluded(StableId); } }
    public ItemStack[] Slots { get { return drone != null && drone.bag != null ? drone.bag.ItemGrid.items : null; } }
    public PackedBoolArray SlotLocks { get { return drone != null && drone.bag != null ? drone.bag.LockedSlots : null; } }
    public int Revision { get { return RemoteResourceAccess.ComputeRevision(Slots); } }
    public bool IsAuthorized(EntityPlayer player, bool exactOwner, out string reason) { return RemoteResourceAccess.AuthorizeLockable(drone, player, exactOwner, out reason); }
    public void SetSlot(int slot, ItemStack value) { if (drone != null && drone.bag != null) drone.bag.SetSlot(slot, value); }
    public void MarkModified()
    {
        if (drone != null) drone.SendSyncData((ushort)8);
        RemoteResourceLiveSync.NotifySourceChanged(StableId, Position, "drone storage modified");
    }
}

public static class RemoteResourceEligibility
{
    public static bool IsActivatedForUse(IRemoteResourceSource source, EntityPlayer player)
    {
        if (source == null) return false;
        if (source.IsActivated) return true;

        // Workstation outputs and Rebirth companion inventories are intrinsic sources.
        if (source.Kind == RemoteResourceSourceKind.WorkstationOutput ||
            source.Kind == RemoteResourceSourceKind.NpcStorage) return true;

        // Owned mobile storage should work immediately; it must not require the owner
        // to perform a dummy deposit just to register it with Remote Resources.
        if (source.Kind == RemoteResourceSourceKind.VehicleStorage ||
            source.Kind == RemoteResourceSourceKind.DroneStorage)
        {
            string ownerReason;
            if (source.IsAuthorized(player, true, out ownerReason)) return true;
        }

        // A configured category is an explicit routing decision by the player/admin.
        if (source.Kind == RemoteResourceSourceKind.StaticContainer)
        {
            Vector3 position = source.Position;
            Vector3i blockPosition = new Vector3i(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z));
            if (QuickStackAcceptedCategoryRegistry.HasAny(blockPosition)) return true;
        }

        return false;
    }

    public static bool CanUse(IRemoteResourceSource source, EntityPlayer player, bool requireItems, bool exactOwner, out string reason)
    {
        reason = string.Empty;
        if (source == null || !source.IsLoaded) { reason = "unloaded"; return false; }
        if (!IsActivatedForUse(source, player)) { reason = "not activated, owned, or category-enabled"; return false; }

        PlatformUserIdentifierAbs userId;
        if (!RemoteResourceAccess.TryGetPersistentId(player, out userId))
        {
            reason = "missing persistent player";
            return false;
        }
        if (RemoteResourceStateStore.IsExcluded(source.StableId, userId))
        {
            reason = "excluded for this player";
            return false;
        }

        if (source.IsBusy) { reason = "open, reserved, or being edited"; return false; }
        if (!source.IsAuthorized(player, exactOwner, out reason)) return false;
        if (requireItems && !RemoteResourceAccess.HasUsableItem(source.Slots)) { reason = "empty"; return false; }
        return true;
    }
}

public static class RemoteResourcePatchInstaller
{
    private static bool installed;
    public static void Install()
    {
        if (installed) return;
        installed = true;
        Harmony harmony = new Harmony("rebirth.remote.resources.3.1");
        // Patch the typed 3.1 lifecycle methods once. Composite overrides call their base
        // implementation, while TileEntityWorkstation inherits it directly.
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceTileEntityLoadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceTileEntityUnloadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceTileEntityRemovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceStorageUpdateSlotPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceStorageUnlockedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceWorldUnloadPatch));
    }

    internal static void AfterTileEntityLoad(TileEntity __instance) { RemoteResourceRegistry.Register(__instance); }
    internal static void BeforeTileEntityUnload(TileEntity __instance) { RemoteResourceRegistry.Unregister(__instance); }
    internal static void BeforeTileEntityRemove(TileEntity __instance)
    {
        if (__instance == null) return;
        RemoteResourceRegistry.Unregister(__instance);
        string id = __instance is TileEntityWorkstation ? RemoteResourceIdentity.Workstation(__instance.ToWorldPos()) : RemoteResourceIdentity.Static(__instance.ToWorldPos());
        RemoteResourceStateStore.Forget(id);
    }

    internal static void BeforeStorageUpdateSlot(TEFeatureStorage __instance, int _idx, out ItemStack __state)
    {
        __state = ItemStack.Empty.Clone();
        if (__instance?.ItemGrid?.items == null || _idx < 0 || _idx >= __instance.ItemGrid.items.Length) return;
        __state = __instance.ItemGrid.items[_idx]?.Clone() ?? ItemStack.Empty.Clone();
    }

    internal static void AfterStorageUpdateSlot(TEFeatureStorage __instance, int _idx, ItemStack _item, ItemStack __state)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || __instance == null || __instance.Parent == null) return;

        // Newly placed composite storage is not guaranteed to pass through OnLoad before
        // its first player deposit. Register it here so activation and discovery happen
        // in the same authoritative update path.
        RemoteResourceRegistry.Register(__instance.Parent);
        // TEFeatureStorage.Parent is TileEntityComposite in 3.1. Workstations are a
        // separate TileEntity type and their output inventory is handled by the
        // dedicated WorkstationOutputResourceSource path, so this callback can only
        // invalidate the composite/static source that owns this storage feature.
        string changedId = RemoteResourceIdentity.Static(__instance.Parent.ToWorldPos());
        // Slot edits can occur many times during one transfer. Invalidate provenance here,
        // but push only from the source's final SetModified/access-close event so clients do
        // not receive intermediate snapshots for every slot in a multi-item transfer.
        RemoteResourceSnapshotCache.InvalidateSource(changedId);

        if (!__instance.Parent.IsUserAccessing()) return;
        int oldCount = __state == null || __state.IsEmpty() ? 0 : __state.count;
        int newCount = _item == null || _item.IsEmpty() ? 0 : _item.count;
        bool deposit = newCount > oldCount ||
            (oldCount > 0 && newCount > 0 && __state.itemValue.type != _item.itemValue.type);
        if (deposit)
            RemoteResourceStateStore.Activate(RemoteResourceIdentity.Static(__instance.Parent.ToWorldPos()));
    }

    internal static void BeforeStorageUnlocked(TEFeatureStorage storage)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || storage == null || storage.Parent == null) return;

        // LockManager removes the server lock before invoking OnUnlockedServer. This is the
        // first point where a freshly edited source is no longer Busy, so rebuild/push the
        // availability snapshot here. Previously UpdateSlot only invalidated the cache and
        // a host/client could remain stale until the Wi-Fi toggle forced a global rebuild.
        RemoteResourceRegistry.Register(storage.Parent);
        Vector3i position = storage.Parent.ToWorldPos();
        string stableId = RemoteResourceIdentity.Static(position);
        bool explicitlyRouted = RemoteResourceStateStore.IsActivated(stableId) ||
                                QuickStackAcceptedCategoryRegistry.HasAny(position);
        if (explicitlyRouted)
            RemoteResourceLiveSync.NotifySourceChanged(stableId, position.ToVector3(), "static storage closed after edit");
        else
            RemoteResourceSnapshotCache.InvalidateSource(stableId);
    }

    internal static void AfterWorldUnload()
    {
        RemoteResourceStateStore.FlushPending();
        QuickStackAcceptedCategoryRegistry.FlushPending();
        RemoteResourceRegistry.Clear();
        RemoteResourceStateStore.ResetForWorldUnload();
        RemoteResourceClientAvailability.Clear();
        RemoteResourceClientTransactionCoordinator.Clear();
        RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
        LogisticsTransferOutcomeJournal.ResetForWorldUnload();
        LogisticsTransferService.ResetClientRequestScope();
        QuickStackAcceptedCategoryRegistry.ClearWorld();
    }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.OnLoad))]
internal static class RemoteResourceTileEntityLoadPatch
{
    private static void Postfix(TileEntity __instance)
    { RemoteResourcePatchInstaller.AfterTileEntityLoad(__instance); }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.OnUnload))]
internal static class RemoteResourceTileEntityUnloadPatch
{
    private static void Prefix(TileEntity __instance)
    { RemoteResourcePatchInstaller.BeforeTileEntityUnload(__instance); }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.OnRemove))]
internal static class RemoteResourceTileEntityRemovePatch
{
    private static void Prefix(TileEntity __instance)
    { RemoteResourcePatchInstaller.BeforeTileEntityRemove(__instance); }
}

[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.UpdateSlot))]
internal static class RemoteResourceStorageUpdateSlotPatch
{
    private static void Prefix(TEFeatureStorage __instance, int _idx, out ItemStack __state)
    { RemoteResourcePatchInstaller.BeforeStorageUpdateSlot(__instance, _idx, out __state); }

    private static void Postfix(TEFeatureStorage __instance, int _idx, ItemStack _item, ItemStack __state)
    { RemoteResourcePatchInstaller.AfterStorageUpdateSlot(__instance, _idx, _item, __state); }
}

[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.OnUnlockedServer))]
internal static class RemoteResourceStorageUnlockedPatch
{
    private static void Prefix(TEFeatureStorage __instance)
    { RemoteResourcePatchInstaller.BeforeStorageUnlocked(__instance); }
}

[HarmonyPatch(typeof(World), nameof(World.UnloadWorld))]
internal static class RemoteResourceWorldUnloadPatch
{
    private static void Postfix()
    { RemoteResourcePatchInstaller.AfterWorldUnload(); }
}

