using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

// Contents travel on the item; the server copy authorizes one restoration of that token.
public static class RebirthContainerPackUp
{
    public const string Command = "rebirthPackUp";
    public const string Metadata = "rebirth.container.pack.v1";
    internal const int MaxPayload = 131072;
    private static World pendingWorld;
    private sealed class Pending { public string Payload; public DateTime Expires; }
    private static readonly Dictionary<Vector3i, Pending> pending = new Dictionary<Vector3i, Pending>();

    public sealed class Payload
    {
        public int Version = 1;
        public string Token;
        public string Block;
        public string Items;
        public int Categories;
        public int Width;
        public int Height;
        public bool Activated;
        public string[] Exclusions;
        public string Name;
    }

    public static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Block), nameof(Block.PlaceBlock), new[] { typeof(WorldBase), typeof(BlockPlacement.Result), typeof(EntityAlive) }),
            prefix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(BeforePlacement)),
            postfix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(AfterPlacement)));
        harmony.Patch(AccessTools.Method(typeof(NetPackageSetBlock), nameof(NetPackageSetBlock.ProcessPackage)),
            prefix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(BeforeServerPlacement)),
            postfix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(AfterServerPlacement)));
        harmony.Patch(AccessTools.Method(typeof(ItemStack), nameof(ItemStack.CanStackWith)),
            prefix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(CanStack)));
        harmony.Patch(AccessTools.Method(typeof(ItemStack), nameof(ItemStack.CanStackPartlyWith)),
            prefix: new HarmonyMethod(typeof(RebirthContainerPackUp), nameof(CanStack)));
    }

    private static bool CanStack(ItemStack __instance, ItemStack __0, ref bool __result)
    {
        if (!IsPacked(__instance?.itemValue) && !IsPacked(__0?.itemValue)) return true;
        __result = false; return false;
    }
    public static bool IsPacked(ItemValue value) => value != null && value.TryGetMetadata(Metadata, out string ignored);

    private static bool Eligible(WorldBase world, Vector3i pos, EntityPlayer player, out TEFeatureStorage storage)
    {
        storage = null;
        if (!RebirthBlockPickupPatchInstaller.Active || world == null || player == null) return false;
        var tile = world.GetTileEntity(pos) as TileEntityComposite;
        if (tile == null || !tile.PlayerPlaced || tile.IsUserAccessing()) return false;
        if (!IsWithinLandClaim(world, pos)) return false;
        string reason;
        if (!RebirthSecureAccessPolicy.CanAccess(player, tile, RebirthSecureAccessPurpose.DirectInteraction, out reason)) return false;
        if (!tile.TryGetSelfOrFeature<TEFeatureStorage>(out storage) || storage?.ItemGrid?.items == null) return false;
        BlockValue block = world.GetBlock(pos);
        if (block.ischild || block.damage > 0 || !RebirthBlockPickupService.ShouldUseRebirthPickup(block.Block)) return false;
        var target = RebirthBlockPickupTargetResolver.Resolve(block.Block);
        return target.IsStorageContainer && target.Count == 1 &&
            (player.position - (pos.ToVector3() + Vector3.one * .5f)).sqrMagnitude <= 36f;
    }

    private static bool IsWithinLandClaim(WorldBase world, Vector3i pos)
    {
        if (!(world is World actual) || actual.IsWithinTraderArea(pos)) return false;
        int radius = Math.Max(0, (GameStats.GetInt(EnumGameStats.LandClaimSize) - 1) / 2);
        // Use the native indexed-chunk check: the public position-only overload
        // returns Self without checking for a claim in some game modes.
        // Any valid claim qualifies; container access is checked separately.
        for (int x = (pos.x - radius) >> 4; x <= (pos.x + radius) >> 4; ++x)
        for (int z = (pos.z - radius) >> 4; z <= (pos.z + radius) >> 4; ++z)
        {
            var chunk = actual.GetChunkFromWorldPos(new Vector3i(x << 4, pos.y, z << 4)) as Chunk;
            if (chunk != null && actual.GetLandClaimOwner(chunk, pos, null, radius, radius, false) != EnumLandClaimOwner.None)
                return true;
        }
        return false;
    }

    public static BlockActivationCommand[] Append(BlockActivationCommand[] commands, WorldBase world, Vector3i pos, EntityAlive focusing)
    {
        var tile = world?.GetTileEntity(pos) as TileEntityComposite;
        if (tile == null || !tile.PlayerPlaced || !tile.TryGetSelfOrFeature<TEFeatureStorage>(out var storage)) return commands;
        bool enabled = Eligible(world, pos, focusing as EntityPlayer, out storage);
        var source = commands ?? BlockActivationCommand.Empty;
        foreach (var command in source) if (command.text == Command) return source;
        var result = new BlockActivationCommand[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = new BlockActivationCommand(Command, "pack_mule", enabled);
        return result;
    }

    public static bool TryHandle(string command, WorldBase world, Vector3i pos, EntityPlayerLocal player)
    {
        if (command != Command) return false;
        if (!IsWithinLandClaim(world, pos))
        { if (player != null) GameManager.ShowTooltip(player, Localization.Get("xuiRebirthPackUpLandClaim")); return true; }
        if (!Eligible(world, pos, player, out var storage))
        { if (player != null) GameManager.ShowTooltip(player, Localization.Get("xuiRebirthPackUpDenied")); return true; }
        if (!world.IsRemote())
        {
            try { Pack((World)world, pos, player.entityId); }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Pack Up] pickup failed: " + ex.Message);
                GameManager.ShowTooltip(player, Localization.Get("xuiRebirthPackUpDenied"));
            }
        }
        else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthContainerPackRequest>().Setup(player.entityId, pos, ""));
        return true;
    }

    private static string RecordPath(string token)
    {
        if (!Guid.TryParseExact(token, "N", out var ignored)) throw new InvalidDataException("Invalid packed container token");
        return Path.Combine(GameIO.GetSaveGameDir(), "RebirthData", "PackedContainers", token + ".json");
    }

    private static Payload Decode(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxPayload) throw new InvalidDataException("Packed container exceeds payload limit");
        var p = JsonConvert.DeserializeObject<Payload>(json);
        if (p == null || p.Version != 1 || string.IsNullOrEmpty(p.Block) || string.IsNullOrEmpty(p.Items)) throw new InvalidDataException("Invalid packed container data");
        RecordPath(p.Token);
        return p;
    }

    private static string EncodeItems(ItemStack[] items)
    {
        using (var bytes = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(bytes);
            writer.Write(items.Length);
            foreach (var item in items) (item ?? ItemStack.Empty).Write(writer);
            writer.Flush();
            if (bytes.Length > MaxPayload / 2) throw new InvalidDataException("Packed container contents exceed payload limit");
            return Convert.ToBase64String(bytes.ToArray());
        }
    }
    private static ItemStack[] DecodeItems(string text)
    {
        using (var bytes = new MemoryStream(Convert.FromBase64String(text)))
        using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(bytes);
            int count = reader.ReadInt32();
            if (count < 1 || count > 4096) throw new InvalidDataException("Invalid packed slot count");
            var items = ItemStack.CreateArray(count);
            for (int i = 0; i < count; ++i) items[i].Read(reader);
            if (bytes.Position != bytes.Length) throw new InvalidDataException("Trailing packed content");
            return items;
        }
    }

    public static void Pack(World world, Vector3i pos, int playerId)
    {
        if (world == null || world.IsRemote()) return;
        var player = world.GetEntity(playerId) as EntityPlayer;
        if (!Eligible(world, pos, player, out var storage)) return;
        var persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (persistent?.PrimaryId == null) return;
        var current = world.GetBlock(pos);
        var tile = world.GetTileEntity(pos) as TileEntityComposite;
        var target = RebirthBlockPickupTargetResolver.Resolve(current.Block);
        string stable = RemoteResourceIdentity.Static(pos);
        var payload = new Payload { Token = Guid.NewGuid().ToString("N"), Block = target.TargetName,
            Items = EncodeItems(storage.ItemGrid.items), Width = storage.ItemGrid.ContainerSize.x, Height = storage.ItemGrid.ContainerSize.y, Categories = QuickStackAcceptedCategoryRegistry.GetMask(pos),
            Activated = RemoteResourceStateStore.IsActivated(stable), Exclusions = RemoteResourceStateStore.CapturePackedExclusions(stable),
            Name = tile.GetFeature<TEFeatureRebirthContainerName>()?.CustomName ?? RebirthContainerNameRegistry.Get(world, pos) };
        string json = JsonConvert.SerializeObject(payload);
        Decode(json); // Validate before changing the source container.
        string path = RecordPath(payload.Token);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player is EntityPlayerLocal) StorePending(world, pos, json);
        else connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthContainerPackedItem>().Setup(pos, json), _attachedToEntityId: playerId);
        // The native pickup grant/removal path is reused; empty first so removal cannot spill duplicates.
        var original = storage.ItemGrid.items;
        storage.ItemGrid.items = ItemStack.CreateArray(original.Length);
        storage.SetModified();
        try
        {
            RebirthPlacedWorkmanshipPickupBridge.SendBeforePickup(world, pos, playerId);
            RebirthElectricalPickupBridge.SendBeforePickup(world, pos, playerId);
            world.GetGameManager().PickupBlockServer(pos, current, playerId, persistent.PrimaryId);
        }
        catch
        {
            if (ReferenceEquals(world.GetTileEntity(pos), tile)) { storage.ItemGrid.items = original; storage.SetModified(); }
            throw;
        }
        RebirthPlacedWorkmanshipService.Remove(pos);
        RebirthInfrastructureWorkService.RemoveRecord(pos);
        QuickStackAcceptedCategoryRegistry.SetMask(pos, 0);
        RemoteResourceStateStore.Forget(stable);
    }

    public static void StorePending(World world, Vector3i pos, string json)
    {
        Decode(json);
        if (!ReferenceEquals(pendingWorld, world)) { pending.Clear(); pendingWorld = world; }
        pending[pos] = new Pending { Payload = json, Expires = DateTime.UtcNow.AddSeconds(30) };
    }
    public static void ApplyPending(Vector3i pos, ItemValue value)
    {
        if (!ReferenceEquals(pendingWorld, GameManager.Instance?.World)) { pending.Clear(); return; }
        if (!pending.TryGetValue(pos, out var entry)) return;
        pending.Remove(pos);
        if (entry.Expires < DateTime.UtcNow) return;
        value.SetMetadata(Metadata, entry.Payload);
    }

    private static void BeforePlacement(WorldBase __0, BlockPlacement.Result __1, EntityAlive __2, out string __state)
    {
        __state = null;
        if (__0.IsRemote() || __0.GetTileEntity(__1.blockPos) != null) return;
        __2?.inventory?.holdingItemItemValue?.TryGetMetadata(Metadata, out __state);
    }
    private static void AfterPlacement(WorldBase __0, BlockPlacement.Result __1, EntityAlive __2, string __state)
    {
        if (string.IsNullOrEmpty(__state) || !(__2 is EntityPlayer player)) return;
        try
        {
            var payload = Decode(__state);
            if (!__0.IsRemote()) Restore((World)__0, __1.blockPos, player.entityId, payload.Token);
        }
        catch (Exception ex) { Log.Warning("[REBIRTH Pack Up] placement restore failed; saved contents retained: " + ex.Message); }
    }

    public sealed class ServerPlacement
    {
        public string Token;
        public int Player;
        public readonly List<Vector3i> Positions = new List<Vector3i>();
    }
    private static void BeforeServerPlacement(NetPackageSetBlock __instance, World __0, out ServerPlacement __state)
    {
        __state = null;
        if (__0 == null || __0.IsRemote()) return;
        var player = __0.GetEntity(__instance.localPlayerThatChanged) as EntityPlayer;
        var persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(__instance.localPlayerThatChanged);
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(__instance.persistentPlayerId)) return;
        var held = player.inventory?.holdingItemItemValue;
        if (held == null || !held.TryGetMetadata(Metadata, out string json)) return;
        var payload = Decode(json);
        var snapshot = new ServerPlacement { Token = payload.Token, Player = player.entityId };
        foreach (var change in __instance.blockChanges)
        {
            var pos = change.blockValueRef.BlockPosition;
            if (change.bChangeBlockValue && !change.blockValue.isair && !change.blockValue.ischild &&
                change.blockValue.Block.GetBlockName() == payload.Block && __0.GetTileEntity(pos) == null)
                snapshot.Positions.Add(pos);
        }
        __state = snapshot;
    }
    private static void AfterServerPlacement(World __0, ServerPlacement __state)
    {
        if (__state == null) return;
        // Native ProcessPackage has authenticated the sender and performed placement.
        // Only newly created containers from that operation can redeem the held token.
        foreach (var pos in __state.Positions)
        {
            try { Restore(__0, pos, __state.Player, __state.Token); }
            catch (Exception ex) { Log.Warning("[REBIRTH Pack Up] server placement restore failed; saved contents retained: " + ex.Message); }
        }
    }

    public static void Restore(World world, Vector3i pos, int playerId, string token)
    {
        if (world == null || world.IsRemote()) return;
        string path = RecordPath(token);
        if (!File.Exists(path)) return; // Already placed, or not issued by this world.
        var player = world.GetEntity(playerId) as EntityPlayer;
        var tile = world.GetTileEntity(pos) as TileEntityComposite;
        if (player == null || tile == null || !tile.PlayerPlaced || tile.IsUserAccessing() ||
            (player.position - pos.ToVector3()).sqrMagnitude > 64f ||
            !tile.TryGetSelfOrFeature<TEFeatureStorage>(out var storage) || !storage.IsEmpty()) return;
        string reason;
        if (!RebirthSecureAccessPolicy.CanAccess(player, tile, RebirthSecureAccessPurpose.DirectInteraction, out reason)) return;
        var p = Decode(File.ReadAllText(path));
        if (world.GetBlock(pos).Block.GetBlockName() != p.Block) return;
        var items = DecodeItems(p.Items);
        if (p.Width < 1 || p.Height < 1 || p.Width > 64 || p.Height > 64 || p.Width * p.Height != items.Length) return;
        // Rename reserves this token against repeated client requests before publishing contents.
        string consumed = path + ".placed";
        if (File.Exists(consumed)) return;
        File.Move(path, consumed);
        storage.ItemGrid.Resize(new Vector2i(p.Width, p.Height));
        storage.ItemGrid.items = items;
        storage.SetModified();
        QuickStackAcceptedCategoryRegistry.SetMask(pos, p.Categories);
        RemoteResourceStateStore.RestorePackedState(RemoteResourceIdentity.Static(pos), p.Activated, p.Exclusions);
        tile.GetFeature<TEFeatureRebirthContainerName>()?.SetCustomName(p.Name);
        // Keep the server receipt for recovery rather than deleting the captured contents.
    }
}

[Preserve]
public sealed class NetPackageRebirthContainerPackRequest : NetPackage
{
    private int player; private Vector3i pos; private string token;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthContainerPackRequest Setup(int id, Vector3i p, string t) { player=id;pos=p;token=t;return this; }
    public override void read(PooledBinaryReader r) { player=r.ReadInt32();pos=StreamUtils.ReadVector3i(r);token=RebirthSurvivorNetworkCodec.ReadBoundedString(r,32); }
    public override void write(PooledBinaryWriter w) { base.write(w);w.Write(player);StreamUtils.Write(w,pos);RebirthSurvivorNetworkCodec.WriteString(w,token,32); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world==null || world.IsRemote() || !ValidEntityIdForSender(player)) return;
        try { if (string.IsNullOrEmpty(token)) RebirthContainerPackUp.Pack(world,pos,player); }
        catch(Exception e) { Log.Warning("[REBIRTH Pack Up] request failed: "+e.Message); }
    }
    public int GetLength() => 56;
}

[Preserve]
public sealed class NetPackageRebirthContainerPackedItem : NetPackage
{
    private Vector3i pos; private string json;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthContainerPackedItem Setup(Vector3i p,string payload) {pos=p;json=payload;return this;}
    public override void read(PooledBinaryReader r) { pos=StreamUtils.ReadVector3i(r);json=RebirthSurvivorNetworkCodec.ReadBoundedString(r,RebirthContainerPackUp.MaxPayload); }
    public override void write(PooledBinaryWriter w) {base.write(w);StreamUtils.Write(w,pos);RebirthSurvivorNetworkCodec.WriteString(w,json,RebirthContainerPackUp.MaxPayload);}
    public override void ProcessPackage(World world,GameManager callbacks) {if(world!=null&&world.IsRemote())RebirthContainerPackUp.StorePending(world,pos,json);}
    public int GetLength() => 16+RebirthSurvivorNetworkCodec.EstimateString(json,RebirthContainerPackUp.MaxPayload);
}
