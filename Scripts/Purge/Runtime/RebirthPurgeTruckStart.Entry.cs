using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal static partial class RebirthPurgeTruckStart
{
    internal sealed class Entry
    {
        internal World World;
        internal string Owner, Save;
        internal ClientInfo Peer;
        internal INetConnection Connection;
        internal EntityPlayer Player;
        internal RebirthPurgeStarterClaims Store;
        internal RebirthPurgeStarterClaim Claim;
    }

    [ThreadStatic] internal static Entry RemoteEntry;
    [ThreadStatic] internal static bool RemoteRequestActive;
    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static Entry localEntry;

    internal static Entry EnterRemote(GameManager manager, ClientInfo peer)
    {
        if (!Enabled || !ThreadManager.IsMainThread() || manager?.World == null ||
            !ReferenceEquals(GameManager.Instance, manager) || manager.World.IsRemote() || !CurrentPeer(peer, peer?.netConnection?[0]))
            return null;
        var native = manager.getPersistentPlayerID(peer);
        if (native == null || !native.Equals(peer.InternalId)) return null;
        return OpenEntry(manager, native, peer);
    }

    private static bool CurrentPeer(ClientInfo peer, INetConnection connection)
    {
        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return manager?.IsServer == true && peer?.InternalId != null && peer.loginDone && connection != null && !connection.IsDisconnected() &&
            ReferenceEquals(peer.netConnection?[0], connection) && manager.Clients?.List.Contains(peer) == true;
    }

    private static bool EntryCurrent(Entry entry)
    {
        if (entry == null || !ReferenceEquals(GameManager.Instance?.World, entry.World) || entry.World.IsRemote() ||
            !string.Equals(GameIO.GetSaveGameDir(), entry.Save, StringComparison.Ordinal)) return false;
        if (entry.Peer != null && !CurrentPeer(entry.Peer, entry.Connection)) return false;
        if (entry.Player == null) return true;
        return ReferenceEquals(entry.World.GetEntity(entry.Player.entityId), entry.Player) &&
            string.Equals(Owner(entry.Player), entry.Owner, StringComparison.Ordinal) &&
            (entry.Peer == null ? ReferenceEquals(entry.World.GetPrimaryPlayer(), entry.Player) :
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(entry.Player.entityId), entry.Peer));
    }

    private static Entry OpenEntry(GameManager manager, PlatformUserIdentifierAbs principal, ClientInfo peer, bool allowFresh = true)
    {
        var world = manager.World;
        string owner = principal?.CombinedString;
        if (string.IsNullOrWhiteSpace(owner) || !Guid.TryParseExact(world.worldState?.Guid, "N", out var worldId)) return null;
        if (entries.TryGetValue(owner, out var prior))
        {
            if (EntryCurrent(prior)) return ReferenceEquals(prior.Peer, peer) ? prior : null;
            entries.Remove(owner);
        }
        string save = GameIO.GetSaveGameDir();
        Entry entry = null;
        try
        {
            var store = new RebirthPurgeStarterClaims(save, worldId);
            var read = store.Read(owner, out var claim);
            bool firstLife = allowFresh && read == RebirthPurgeStarterRead.Missing && NativeFirstLife(manager.GetPersistentPlayerList(), principal);
            var admission = RebirthPurgeStarterClaim.Admission(read, claim, initialSpawn: true, firstNativeLife: firstLife);
            if (admission == RebirthPurgeStarterAdmission.NormalSpawn) return null;
            if (admission == RebirthPurgeStarterAdmission.FreshClaim)
            {
                claim = new RebirthPurgeStarterClaim { World = worldId, Owner = owner, Claim = Guid.NewGuid() };
                if (!store.Publish(null, claim)) return null;
            }
            entry = new Entry { World = world, Owner = owner, Save = save, Peer = peer, Connection = peer?.netConnection?[0], Store = store, Claim = claim };
            entries.Add(owner, entry);
            PrepareSite(entry);
            return entry;
        }
        catch (Exception error)
        {
            if (entry != null) RetireEntry(entry);
            Log.Warning("[PurgeTruckStart] Native entry claim refused: " + error.Message);
            return null;
        }
    }

    private static bool NativeFirstLife(PersistentPlayerList players, PlatformUserIdentifierAbs principal)
    {
        // Both native entry hooks run before the current spawn records its login.
        if (players == null) return false;
        if (players.GetPlayerData(principal) != null)
        {
            RebirthDebug.Log("Purge", "Starter admission found an existing native player record.");
            return false;
        }
        string name = principal.CombinedString + "." + PlayerDataFile.EXT;
        if (!string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal)) return false;
        string path = Path.Combine(GameIO.GetPlayerDataDir(), name);
        return Absent(path) && Absent(path + ".bak") && Absent(path + ".tmp") && Absent(path + ".meta");
    }

    private static bool Absent(string path)
    {
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }

    internal static bool TryChooseAnchor(World world, out SpawnPosition anchor)
    {
        anchor = SpawnPosition.Undef;
        var manager = GameManager.Instance;
        if (!Enabled || world == null || world.IsRemote() || !ReferenceEquals(manager?.World, world)) return false;
        var entry = RemoteEntry;
        if (entry == null)
        {
            if (RemoteRequestActive || GameManager.IsDedicatedServer || world.GetPrimaryPlayer() != null) return false;
            if (localEntry == null || !EntryCurrent(localEntry))
                localEntry = OpenEntry(manager, manager.getPersistentPlayerID(null), null);
            entry = localEntry;
        }
        return EntryCurrent(entry) && ReferenceEquals(entry.World, world) && TryAnchor(entry, out anchor);
    }

    private static Vector3 Position(RebirthPurgeStarterClaim claim) => new Vector3(claim.X, claim.Y, claim.Z);
    private static SpawnPosition Road(RebirthPurgeStarterClaim claim) => new SpawnPosition(Position(claim), claim.Heading);
    private static bool WorldPosition(World world, Vector3 position)
        => (world.ClampToValidWorldPos(position) - position).sqrMagnitude <= .01f;

    private static bool TryAnchor(Entry entry, out SpawnPosition anchor)
    {
        var claim = entry.Claim;
        anchor = SpawnPosition.Undef;
        if (claim.Stage == RebirthPurgeStarterStage.WaitingForSite || !WorldPosition(entry.World, Position(claim))) return false;
        var road = Road(claim);
        // Before native VehicleManager initialization, only loaded occupancy is known.
        // Creation rechecks the available native collections after initialization.
        if (claim.Stage == RebirthPurgeStarterStage.Prepared && !PreparedSiteAvailable(entry, road, VehicleManager.Instance)) return false;
        if (!claim.HasAnchor)
            return RebirthPurgeForestRoad.PlayerAnchor(entry.World, road, out anchor, requireRoad: false, requireForest: false)
                && WorldPosition(entry.World, anchor.position);
        anchor = new SpawnPosition(new Vector3(claim.AnchorX, claim.AnchorY, claim.AnchorZ), claim.AnchorHeading);
        return WorldPosition(entry.World, anchor.position);
    }

    private static bool SiteAvailable(Entry entry, SpawnPosition road)
    {
        foreach (var other in entries.Values)
        {
            if (ReferenceEquals(other, entry) || !ReferenceEquals(other.World, entry.World) ||
                other.Claim.Stage == RebirthPurgeStarterStage.WaitingForSite || !EntryCurrent(other)) continue;
            if (!RebirthPurgeStartPlacementPolicy.FarEnough(road.position.x - other.Claim.X, road.position.z - other.Claim.Z)) return false;
        }
        return true;
    }

    private static bool PrepareSite(Entry entry)
    {
        var claim = entry.Claim;
        if (claim.Stage >= RebirthPurgeStarterStage.PublicationAttempted) return TryAnchor(entry, out _);
        if (claim.HasAnchor && TryAnchor(entry, out _)) return true;
        return FindSite(entry);
    }

    private static IEnumerable<Vector3> OccupiedCampers(World world, VehicleManager manager)
    {
        int camperClass = EntityClass.GetId("rebirthCamper80s");
        foreach (var entity in world.Entities.list)
            if (entity is EntityVehicle vehicle && vehicle.entityClass == camperClass) yield return vehicle.position;
        var unloaded = manager?.vehiclesUnloaded;
        if (unloaded != null)
            foreach (var vehicle in unloaded)
                if (vehicle.entityClass == camperClass) yield return vehicle.pos;
    }

    private static bool FarFromCampers(SpawnPosition road, IEnumerable<Vector3> occupied)
    {
        foreach (var other in occupied)
            if (!RebirthPurgeStartPlacementPolicy.FarEnough(road.position.x - other.x, road.position.z - other.z)) return false;
        return true;
    }

    private static bool PreparedSiteAvailable(Entry entry, SpawnPosition road, VehicleManager manager)
        => SiteAvailable(entry, road) && FarFromCampers(road, OccupiedCampers(entry.World, manager));

    private static bool FindSite(Entry entry)
    {
        var occupied = new List<Vector3>(OccupiedCampers(entry.World, VehicleManager.Instance));
        bool Available(SpawnPosition candidate)
            => SiteAvailable(entry, candidate) && FarFromCampers(candidate, occupied);
        if (!RebirthPurgeForestRoad.Find(entry.World, out var road, out var anchor, Available))
        { RebirthPurgeForestRoad.RetryFailedSearch(entry.World); return false; }
        return PublishSite(entry, road, anchor);
    }

    private static bool PublishSite(Entry entry, SpawnPosition road, SpawnPosition anchor)
    {
        if (!WorldPosition(entry.World, road.position) || !WorldPosition(entry.World, anchor.position) ||
            !SiteAvailable(entry, road)) return false;
        var next = entry.Claim.Copy();
        next.Stage = RebirthPurgeStarterStage.Prepared;
        next.X = road.position.x; next.Y = road.position.y; next.Z = road.position.z; next.Heading = road.heading;
        next.AnchorX = anchor.position.x; next.AnchorY = anchor.position.y; next.AnchorZ = anchor.position.z;
        next.AnchorHeading = anchor.heading; next.HasAnchor = true;
        return Publish(entry, next);
    }

    private static bool Publish(Entry entry, RebirthPurgeStarterClaim next)
    {
        if (!EntryCurrent(entry) || !entry.Store.Publish(entry.Claim, next)) return false;
        entry.Claim = next;
        return true;
    }

    private static void RetireEntry(Entry entry)
    {
        if (entries.TryGetValue(entry.Owner, out var current) && ReferenceEquals(current, entry)) entries.Remove(entry.Owner);
        if (ReferenceEquals(localEntry, entry)) localEntry = null;
    }
}
