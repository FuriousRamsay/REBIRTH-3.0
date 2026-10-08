using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

internal static class RebirthPoiCrateLedgerService
{
    private static World loadedWorld;
    private static string loadedPath;
    private static RebirthPoiCrateLedger ledger;

    private static bool Get(EntityPlayer player, out RebirthPoiCrateLedger store, out string owner, out string error)
    {
        store = null; owner = null; error = null;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player?.world == null || player.world.IsRemote() || connection == null || !connection.IsServer)
        { error = "expected crate ledger requires server authority"; return false; }
        var identity = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if (identity == null && player is EntityPlayerLocal) identity = PlatformManager.InternalLocalUserIdentifier;
        owner = identity?.CombinedString;
        if (string.IsNullOrWhiteSpace(owner)) { error = "native placement owner is unavailable"; return false; }
        string root = GameIO.GetSaveGameDir();
        if (string.IsNullOrWhiteSpace(root)) { error = "world save path is unavailable"; return false; }
        string path = Path.Combine(root, "RebirthData", "GameBridge", "PoiCrates.xml");
        if (!ReferenceEquals(loadedWorld, player.world) || !string.Equals(loadedPath, path, StringComparison.OrdinalIgnoreCase))
        { loadedWorld = player.world; loadedPath = path; ledger = new RebirthPoiCrateLedger(path); }
        store = ledger;
        return true;
    }

    public static bool Reserve(EntityPlayer player, PrefabInstance poi, Vector3i position, Guid placement)
    {
        RebirthPoiCrateLedger store; string owner, error;
        if (poi == null || !Get(player, out store, out owner, out error)) return false;
        return store.TryReserve(new RebirthPoiCrateExpectation(owner, poi.id, poi.boundingBoxPosition,
            poi.boundingBoxSize, position, placement), out error);
    }

    public static bool Snapshot(EntityPlayer player, PrefabInstance poi, out List<RebirthPoiCrateExpectation> expected, out string error)
    {
        expected = null;
        RebirthPoiCrateLedger store; string owner;
        if (!Get(player, out store, out owner, out error)) return false;
        if (poi == null) { error = "POI scope is unavailable"; return false; }
        return store.TrySnapshot(owner, poi.id, poi.boundingBoxPosition, poi.boundingBoxSize, out expected, out error);
    }
}

internal static class RebirthPoiCrateRecovery
{
    private sealed class Pending
    {
        public World World;
        public EntityPlayerLocal Player;
        public int PoiId;
        public Vector3i Origin, Size;
        public bool Done, Success;
        public List<RebirthPoiCrateExpectation> Expected;
    }
    private static readonly Dictionary<Guid, Pending> pending = new Dictionary<Guid, Pending>();

    public static IEnumerator Request(EntityPlayerLocal player, PrefabInstance poi,
        Action<List<RebirthPoiCrateExpectation>, string> done)
    {
        if (player?.world == null || poi == null) { done(null, "POI recovery context unavailable"); yield break; }
        if (!player.world.IsRemote())
        {
            List<RebirthPoiCrateExpectation> expected; string error;
            bool success = RebirthPoiCrateLedgerService.Snapshot(player, poi, out expected, out error);
            done(success ? expected : null, success ? null : error);
            yield break;
        }
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || pending.Count >= 16) { done(null, "POI recovery connection unavailable"); yield break; }
        Guid request = Guid.NewGuid();
        var state = new Pending { World = player.world, Player = player, PoiId = poi.id, Origin = poi.boundingBoxPosition, Size = poi.boundingBoxSize };
        pending.Add(request, state);
        try
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPoiCrateRecoveryRequest>()
                .Setup(player.entityId, request, poi));
            float until = Time.realtimeSinceStartup + 5f;
            while (!state.Done && Time.realtimeSinceStartup < until && !player.IsDead()
                && ReferenceEquals(player.world, state.World)) yield return null;
            done(state.Done && state.Success ? state.Expected : null,
                state.Done && state.Success ? null : "server expected-crate recovery unavailable or not acknowledged");
        }
        finally { pending.Remove(request); }
    }

    public static void Receive(World world, int ownerId, Guid request, int poiId, Vector3i origin, Vector3i size,
        bool success, List<RebirthPoiCrateExpectation> expected)
    {
        Pending state;
        if (world == null || !world.IsRemote() || !pending.TryGetValue(request, out state) || state.Done
            || !ReferenceEquals(world, state.World) || !ReferenceEquals(state.Player.world, world)
            || !ReferenceEquals(world.GetEntity(ownerId), state.Player) || state.Player.entityId != ownerId
            || state.PoiId != poiId || !state.Origin.Equals(origin) || !state.Size.Equals(size)) return;
        state.Expected = expected; state.Success = success && expected != null; state.Done = true;
    }
}

[Preserve]
public sealed class NetPackageRebirthPoiCrateRecoveryRequest : NetPackage
{
    private int playerId, poiId;
    private Guid request;
    private Vector3i origin, size;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthPoiCrateRecoveryRequest Setup(int player, Guid id, PrefabInstance poi)
    { playerId = player; request = id; poiId = poi.id; origin = poi.boundingBoxPosition; size = poi.boundingBoxSize; return this; }
    public override void read(PooledBinaryReader reader)
    {
        request = Guid.Empty;
        if (reader.ReadByte() != 1) throw new InvalidDataException("Unknown POI recovery request.");
        playerId = reader.ReadInt32(); byte[] bytes = reader.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException(); request = new Guid(bytes);
        poiId = reader.ReadInt32(); origin = ReadPosition(reader); size = ReadPosition(reader);
    }
    internal static Vector3i ReadPosition(PooledBinaryReader reader) => new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    internal static void WritePosition(PooledBinaryWriter writer, Vector3i value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); writer.Write((byte)1); writer.Write(playerId); writer.Write(request.ToByteArray()); writer.Write(poiId);
        WritePosition(writer, origin); WritePosition(writer, size);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || Sender == null || request == Guid.Empty || !ValidEntityIdForSender(playerId)) return;
        var player = world.GetEntity(playerId) as EntityPlayer;
        var prefabs = callbacks?.GetDynamicPrefabDecorator()?.allPrefabs;
        if (player == null || !ReferenceEquals(player.world, world) || prefabs == null) return;
        foreach (PrefabInstance poi in prefabs)
            if (poi != null && poi.id == poiId && poi.boundingBoxPosition.Equals(origin) && poi.boundingBoxSize.Equals(size))
            {
                List<RebirthPoiCrateExpectation> expected; string error;
                bool success = RebirthPoiCrateLedgerService.Snapshot(player, poi, out expected, out error);
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                    NetPackageManager.GetPackage<NetPackageRebirthPoiCrateRecoveryResult>()
                        .Setup(playerId, request, poi, success, expected), _attachedToEntityId: playerId);
                return;
            }
    }
}

[Preserve]
public sealed class NetPackageRebirthPoiCrateRecoveryResult : NetPackage
{
    private int playerId, poiId;
    private Guid request;
    private Vector3i origin, size;
    private bool success;
    private List<RebirthPoiCrateExpectation> expected;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    internal NetPackageRebirthPoiCrateRecoveryResult Setup(int player, Guid id, PrefabInstance poi,
        bool ok, List<RebirthPoiCrateExpectation> records)
    {
        playerId = player; request = id; poiId = poi.id; origin = poi.boundingBoxPosition; size = poi.boundingBoxSize;
        success = ok && records != null && records.Count <= RebirthPoiCrateLedger.MaxPerScope;
        expected = success ? new List<RebirthPoiCrateExpectation>(records) : new List<RebirthPoiCrateExpectation>();
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        success = false; expected = null; request = Guid.Empty;
        if (reader.ReadByte() != 1) throw new InvalidDataException("Unknown POI recovery result.");
        playerId = reader.ReadInt32(); byte[] bytes = reader.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException(); request = new Guid(bytes);
        poiId = reader.ReadInt32(); origin = NetPackageRebirthPoiCrateRecoveryRequest.ReadPosition(reader); size = NetPackageRebirthPoiCrateRecoveryRequest.ReadPosition(reader);
        byte status = reader.ReadByte(); int count = reader.ReadUInt16();
        if (status > 1 || count > RebirthPoiCrateLedger.MaxPerScope || (status == 0 && count != 0)) throw new InvalidDataException("Invalid POI recovery result bounds.");
        var records = new List<RebirthPoiCrateExpectation>(); var ids = new HashSet<Guid>(); var positions = new HashSet<Vector3i>();
        for (int i = 0; i < count; i++)
        {
            bytes = reader.ReadBytes(16); if (bytes.Length != 16) throw new EndOfStreamException();
            Guid placement = new Guid(bytes); Vector3i position = NetPackageRebirthPoiCrateRecoveryRequest.ReadPosition(reader);
            var entry = new RebirthPoiCrateExpectation("server-owner-snapshot", poiId, origin, size, position, placement);
            if (!entry.Valid || !ids.Add(placement) || !positions.Add(position)) throw new InvalidDataException("Invalid POI recovery expectation.");
            records.Add(entry);
        }
        expected = records; success = status == 1;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if (expected == null) throw new InvalidOperationException("POI recovery result is unset.");
        base.write(writer); writer.Write((byte)1); writer.Write(playerId); writer.Write(request.ToByteArray()); writer.Write(poiId);
        NetPackageRebirthPoiCrateRecoveryRequest.WritePosition(writer, origin); NetPackageRebirthPoiCrateRecoveryRequest.WritePosition(writer, size);
        writer.Write((byte)(success ? 1 : 0)); writer.Write((ushort)expected.Count);
        foreach (var entry in expected)
        { writer.Write(entry.Placement.ToByteArray()); NetPackageRebirthPoiCrateRecoveryRequest.WritePosition(writer, entry.Position); }
    }
    public override void ProcessPackage(World world, GameManager callbacks)
        => RebirthPoiCrateRecovery.Receive(world, playerId, request, poiId, origin, size, success, expected);
}