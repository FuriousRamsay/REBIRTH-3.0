using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine.Scripting;

// A small native ItemValue projection. Mode/epoch survive save, pickup and late join;
// energy revisions within Camp do not discard the player's manual door choice.
internal static class RebirthCamperCampDoor
{
    internal const string Key = "RebirthCamperCampDoorV1";
    private static bool installed;
    private sealed class Applied { internal UnityEngine.Transform Model; internal long Epoch = -1, Revision = -1; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Applied> applied
        = new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Applied>();
    private sealed class Observer { internal Frame Frame; internal float NextQuery; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Observer> observers
        = new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Observer>();
    private sealed class Publication { internal long Revision = -1; internal float Next; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Publication> publications
        = new System.Runtime.CompilerServices.ConditionalWeakTable<EntityVehicle, Publication>();
    private sealed class PeerQueries { internal readonly System.Collections.Generic.Dictionary<int, float> Next = new System.Collections.Generic.Dictionary<int, float>(); }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ClientInfo, PeerQueries> peerQueries
        = new System.Runtime.CompilerServices.ConditionalWeakTable<ClientInfo, PeerQueries>();
    private sealed class Frame
    {
        internal Guid Identity;
        internal RebirthCamperEnergyMode Mode;
        internal long Epoch, Revision;
        internal bool Open;
        public override string ToString() => string.Join("|", "1", Identity.ToString("N"), ((int)Mode).ToString(CultureInfo.InvariantCulture),
            Epoch.ToString(CultureInfo.InvariantCulture), Revision.ToString(CultureInfo.InvariantCulture), Open ? "1" : "0");
    }
    internal static void Install()
    {
        if (installed) return;
        var harmony = new Harmony("rebirth.camper.camp.door.v1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperCampDoorTick));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperCampDoorIngress));
        installed = true;
    }
    private static bool Current(EntityVehicle v) => ThreadManager.IsMainThread() && v?.vehicle != null && v.world != null
        && ReferenceEquals(GameManager.Instance?.World, v.world) && ReferenceEquals(v.world.GetEntity(v.entityId), v) && RebirthCamperRadial.IsCamper(v);
    private static Frame Read(EntityVehicle v, Guid identity)
    {
        if (!v.vehicle.GetUpdatedItemValue().TryGetMetadata(Key, out string text)) return null;
        var fields = text.Split('|');
        if (fields.Length != 6 || fields[0] != "1" || !Guid.TryParse(fields[1], out var id) || id != identity
            || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode)
            || mode < 0 || mode > (int)RebirthCamperEnergyMode.Packing
            || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long epoch) || epoch < 0
            || !long.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long revision) || revision < 0
            || (fields[5] != "0" && fields[5] != "1")) return null;
        return new Frame { Identity = id, Mode = (RebirthCamperEnergyMode)mode, Epoch = epoch, Revision = revision, Open = fields[5] == "1" };
    }
    private static void Persist(EntityVehicle v, Frame f)
    {
        v.vehicle.GetUpdatedItemValue().SetMetadata(Key, f.ToString());
        v.SendSyncData(4);
        // Schedule the native vehicle save writer; do not introduce a second save format.
        VehicleManager.Instance?.TriggerSave();
        Publish(v, f);
    }
    private static void Publish(EntityVehicle v, Frame f, ClientInfo recipient = null)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (f == null || connection?.IsServer != true || !Guid.TryParse(v.world.worldState?.Guid, out var worldId)) return;
        try
        {
            var packet = NetPackageManager.GetPackage<NetPackageRebirthCamperCampDoorSnapshotV1>()
                .Setup(v.entityId, worldId, f.Identity, f.Mode, f.Epoch, f.Revision, f.Open);
            if (recipient != null) recipient.SendPackage(packet);
            else connection.SendPackage(packet);
            if (recipient == null)
            {
                var sent = publications.GetValue(v, _ => new Publication());
                sent.Revision = f.Revision; sent.Next = UnityEngine.Time.realtimeSinceStartup + 5f;
            }
        }
        catch { /* Negotiation failure refuses remote presentation; native persistence remains valid. */ }
    }
    internal static bool TryObserve(EntityVehicle v, out long campEpoch, out long doorRevision, out bool open)
    {
        campEpoch = doorRevision = -1; open = false;
        if (!installed || !Current(v)) return false;
        if (v.world.IsRemote())
        {
            // Native vehicle flag4 can contain client-origin bytes. Only the
            // dedicated ToClient package supplies remote door presentation.
            var observed = observers.GetValue(v, _ => new Observer());
            Query(v, observed);
            var projection = observed.Frame;
            if (projection == null) return false;
            campEpoch = projection.Epoch; doorRevision = projection.Revision;
            open = projection.Mode == RebirthCamperEnergyMode.Camp && projection.Open; return true;
        }
        if (!RebirthCamperEnergyRuntime.TryObserveMode(v, out var mode, out _, out _)
            || !v.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string text) || !Guid.TryParse(text, out var identity)) return false;
        var f = Read(v, identity);
        if (f == null || f.Mode != mode)
        {
            if (f == null) f = new Frame { Identity = identity };
            if (f.Epoch == long.MaxValue || f.Revision == long.MaxValue) return false;
            f.Epoch++; f.Revision++; f.Mode = mode; f.Open = mode == RebirthCamperEnergyMode.Camp;
            Persist(v, f);
        }
        if (f == null || f.Mode != mode) return false;
        campEpoch = f.Epoch; doorRevision = f.Revision; open = mode == RebirthCamperEnergyMode.Camp && f.Open; return true;
    }
    internal static void Tick(EntityVehicle v)
    {
        if (!TryObserve(v, out long epoch, out long revision, out bool open)) return;
        if (!v.world.IsRemote())
        {
            var sent = publications.GetValue(v, _ => new Publication());
            if (sent.Revision != revision || UnityEngine.Time.realtimeSinceStartup >= sent.Next)
                Publish(v, Read(v, RebirthCamperModuleRuntime.For(v).Identity));
        }
        // Keep observing authoritative transitions without rerunning all renderer
        // work for an unchanged door every frame. Retry while the model is loading.
        var seen = applied.GetValue(v, _ => new Applied());
        if (seen.Epoch == epoch && seen.Revision == revision && seen.Model == v.ModelTransform
            && RebirthCamperCampStationAppearance.DoorMatches(v, open)) return;
        if (!RebirthCamperCampStationAppearance.SetDoorOpen(v, open)
            && !RebirthCamperCampStationAppearance.DoorMatches(v, open)) return;
        seen.Epoch = epoch; seen.Revision = revision; seen.Model = v.ModelTransform;
    }
    internal static void Request(EntityVehicle v, EntityPlayerLocal p)
    {
        if (!Current(v) || p == null || !RebirthCamperPhysicalTargets.Validate(v, p, "rear_door")
            || !TryObserve(v, out long epoch, out long revision, out bool open)) return;
        if (!v.world.IsRemote()) { Execute(v, p, epoch, revision, open); return; }
        string creation = RebirthCamperCharacterAccess.CurrentCreation(p);
        if (string.IsNullOrEmpty(creation) || !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out var worldId)
            || !observers.TryGetValue(v, out var observed) || observed.Frame == null) return;
        Guid id = observed.Frame.Identity;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsClient || connection.IsServer) return;
        try { connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperCampDoorV1>().Setup(p.entityId, v.entityId, id, worldId, creation, epoch, revision, open), false); }
        catch { /* Unsupported negotiated package: refuse, never change local state. */ }
    }
    internal static bool Execute(EntityVehicle v, EntityPlayer p, long expectedCampEpoch, long expectedDoorRevision, bool expectedOpen, ClientInfo sender = null)
    {
        if (!Current(v) || v.world.IsRemote() || p == null || p.IsDead() || v.IsDead()
            || !ReferenceEquals(v.world, p.world) || !ReferenceEquals(v.world.GetEntity(p.entityId), p)
            || RebirthCamperRadial.IsDriver(v, p) || !RebirthCamperPhysicalTargets.Validate(v, p, "rear_door")
            || !RebirthCamperEnergyRuntime.TryObserveMode(v, out var mode, out _, out _) || mode != RebirthCamperEnergyMode.Camp) return false;
        int lockOwner = RebirthCamperRemoteAdmission.NativeInteractionOwner(v);
        if (lockOwner >= 0 && lockOwner != p.entityId) return false;
        if (sender != null)
        {
            if (sender.InternalId == null || sender.entityId != p.entityId
                || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(p.entityId), sender)
                || (v.IsLocked() && v.hasLock() && !(v.vehicle.OwnerId != null && v.vehicle.OwnerId.Equals(sender.InternalId)) && !v.isAllowedUser(sender.InternalId))) return false;
        }
        else if (!(p is EntityPlayerLocal local) || !v.AllowActivationCommand("service", local)) return false;
        if (!TryObserve(v, out long epoch, out long revision, out bool open) || epoch != expectedCampEpoch
            || revision != expectedDoorRevision || open != expectedOpen || revision == long.MaxValue) return false;
        var f = Read(v, RebirthCamperModuleRuntime.For(v).Identity);
        if (f == null) return false;
        f.Revision++; f.Open = !open; Persist(v, f);
        RebirthCamperCampStationAppearance.SetDoorOpen(v, f.Open); return true;
    }
    internal static void Receive(World world, ClientInfo sender, int actor, int entity, Guid identity, Guid worldId, string creation, long epoch, long revision, bool open)
    {
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world)
            || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true || sender?.entityId != actor
            || !Guid.TryParse(world.worldState?.Guid, out var currentWorld) || currentWorld != worldId) return;
        var v = world.GetEntity(entity) as EntityVehicle; var p = world.GetEntity(actor) as EntityPlayer;
        if (v?.vehicle == null || p == null || string.IsNullOrEmpty(creation) || RebirthCamperCharacterAccess.CurrentCreation(p) != creation
            || !v.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string text) || !Guid.TryParse(text, out var current) || current != identity) return;
        Execute(v, p, epoch, revision, open, sender);
    }
    private static void Query(EntityVehicle v, Observer observed)
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < observed.NextQuery) return;
        observed.NextQuery = now + (observed.Frame == null ? 1f : 5f);
        var p = v.world.GetPrimaryPlayer() as EntityPlayerLocal;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        string creation = p == null ? null : RebirthCamperCharacterAccess.CurrentCreation(p);
        if (p == null || p.IsDead() || string.IsNullOrEmpty(creation) || connection == null || !connection.IsClient || connection.IsServer
            || !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out var worldId)) return;
        try { connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperCampDoorQueryV1>()
            .Setup(p.entityId, v.entityId, worldId, creation), false); }
        catch { }
    }
    internal static void ReceiveQuery(World world, ClientInfo sender, int actor, int entity, Guid worldId, string creation)
    {
        if (!installed || !ThreadManager.IsMainThread() || world == null || world.IsRemote()
            || !ReferenceEquals(GameManager.Instance?.World, world) || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true
            || sender?.InternalId == null || sender.entityId != actor
            || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor), sender)
            || !Guid.TryParse(world.worldState?.Guid, out var currentWorld) || currentWorld != worldId) return;
        var p = world.GetEntity(actor) as EntityPlayer; var v = world.GetEntity(entity) as EntityVehicle;
        if (!Current(v) || p == null || p.IsDead() || !ReferenceEquals(p.world, world) || string.IsNullOrEmpty(creation)
            || RebirthCamperCharacterAccess.CurrentCreation(p) != creation || (p.position - v.position).sqrMagnitude > 256f * 256f) return;
        var peer = peerQueries.GetValue(sender, _ => new PeerQueries()); float now = UnityEngine.Time.realtimeSinceStartup;
        if (peer.Next.TryGetValue(entity, out float next) && now < next) return;
        // Bound retained query IDs for a long-lived connection.
        if (peer.Next.Count >= 256) peer.Next.Clear();
        peer.Next[entity] = now + .5f;
        if (TryObserve(v, out _, out _, out _)) Publish(v, Read(v, RebirthCamperModuleRuntime.For(v).Identity), sender);
    }
    internal static void ReceiveSnapshot(World world, int entity, Guid worldId, Guid identity, RebirthCamperEnergyMode mode, long epoch, long revision, bool open)
    {
        if (!installed || !ThreadManager.IsMainThread() || world == null || !world.IsRemote()
            || !ReferenceEquals(GameManager.Instance?.World, world) || identity == Guid.Empty || epoch < 0 || revision < 0
            || (byte)mode > (byte)RebirthCamperEnergyMode.Packing
            || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsClient != true
            || SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer
            || !Guid.TryParse(world.worldState?.Guid, out var currentWorld) || currentWorld != worldId
            || !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient), out var connectedWorld) || connectedWorld != worldId) return;
        var v = world.GetEntity(entity) as EntityVehicle;
        if (!Current(v)) return; // A later query recovers snapshots sent before native entity spawn.
        var observed = observers.GetValue(v, _ => new Observer());
        var old = observed.Frame;
        if (old != null && (old.Identity != identity || revision < old.Revision || epoch < old.Epoch)) return;
        if (old != null && revision == old.Revision
            && (old.Epoch != epoch || old.Mode != mode || old.Open != open)) return;
        observed.Frame = new Frame { Identity = identity, Mode = mode, Epoch = epoch, Revision = revision, Open = open };
        Tick(v);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.Update))]
internal static class RebirthCamperCampDoorTick
{
    [HarmonyPriority(Priority.Last)] private static void Postfix(EntityVehicle __instance) => RebirthCamperCampDoor.Tick(__instance);
}

// Native flag4 includes client-supplied ItemValue metadata. Preserve the server
// door frame around live ingress; saved entity loading must still restore it.
[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.ReadSyncData))]
internal static class RebirthCamperCampDoorIngress
{
    internal sealed class Projection { internal string Door, Identity; }
    private static bool Prefix(EntityVehicle __instance, ushort __1, int __2, out Projection __state)
    {
        __state = null;
        if (__2 <= 0 || (__1 & 4) == 0 || __instance.world == null || __instance.world.IsRemote() || !RebirthCamperRadial.IsCamper(__instance)) return true;
        if (!RebirthCamperCampDoor.TryObserve(__instance, out _, out _, out _)) return false;
        var item = __instance.vehicle.GetUpdatedItemValue();
        if (item.TryGetMetadata(RebirthCamperCampDoor.Key, out string door)
            && item.TryGetMetadata("RebirthCamperIdentity", out string identity)) __state = new Projection { Door = door, Identity = identity };
        return __state != null;
    }
    private static void Finalizer(EntityVehicle __instance, Projection __state)
    {
        if (__state == null) return;
        var item = __instance.vehicle.GetUpdatedItemValue();
        item.SetMetadata(RebirthCamperCampDoor.Key, __state.Door);
        item.SetMetadata("RebirthCamperIdentity", __state.Identity);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperCampDoorV1 : NetPackage
{
    private int actor, entity; private Guid identity, worldId; private string creation; private long epoch, revision; private bool open;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperCampDoorV1 Setup(int a, int e, Guid i, Guid w, string c, long ep, long r, bool o)
    { actor = a; entity = e; identity = i; worldId = w; creation = c; epoch = ep; revision = r; open = o; return this; }
    public override void read(PooledBinaryReader r)
    {
        if (r.ReadByte() != 1) throw new System.IO.InvalidDataException("Camper door version changed.");
        actor = r.ReadInt32(); entity = r.ReadInt32(); identity = new Guid(r.ReadBytes(16)); worldId = new Guid(r.ReadBytes(16));
        int count = r.ReadUInt16(); if (count > 2048) throw new System.IO.InvalidDataException("Camper door character identity too large.");
        var bytes = r.ReadBytes(count); if (bytes.Length != count) throw new System.IO.EndOfStreamException();
        creation = new System.Text.UTF8Encoding(false, true).GetString(bytes); epoch = r.ReadInt64(); revision = r.ReadInt64(); open = r.ReadBoolean();
    }
    public override void write(PooledBinaryWriter w)
    {
        base.write(w); w.Write((byte)1); w.Write(actor); w.Write(entity); w.Write(identity.ToByteArray()); w.Write(worldId.ToByteArray());
        var bytes = System.Text.Encoding.UTF8.GetBytes(creation ?? string.Empty); if (bytes.Length > 2048) throw new System.IO.InvalidDataException();
        w.Write((ushort)bytes.Length); w.Write(bytes); w.Write(epoch); w.Write(revision); w.Write(open);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (ValidEntityIdForSender(actor)) RebirthCamperCampDoor.Receive(world, Sender, actor, entity, identity, worldId, creation, epoch, revision, open); }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthCamperCampDoorQueryV1 : NetPackage
{
    private int actor, entity; private Guid worldId; private string creation;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperCampDoorQueryV1 Setup(int a, int e, Guid w, string c)
    { actor = a; entity = e; worldId = w; creation = c; return this; }
    public override void read(PooledBinaryReader r)
    {
        if (r.ReadByte() != 1) throw new System.IO.InvalidDataException("Camper door query version changed.");
        actor = r.ReadInt32(); entity = r.ReadInt32(); worldId = new Guid(r.ReadBytes(16));
        int count = r.ReadUInt16(); if (count > 2048) throw new System.IO.InvalidDataException("Camper door query identity too large.");
        var bytes = r.ReadBytes(count); if (bytes.Length != count) throw new System.IO.EndOfStreamException();
        creation = new System.Text.UTF8Encoding(false, true).GetString(bytes);
    }
    public override void write(PooledBinaryWriter w)
    {
        base.write(w); w.Write((byte)1); w.Write(actor); w.Write(entity); w.Write(worldId.ToByteArray());
        var bytes = System.Text.Encoding.UTF8.GetBytes(creation ?? string.Empty);
        if (bytes.Length > 2048) throw new System.IO.InvalidDataException();
        w.Write((ushort)bytes.Length); w.Write(bytes);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (ValidEntityIdForSender(actor)) RebirthCamperCampDoor.ReceiveQuery(world, Sender, actor, entity, worldId, creation); }
    public int GetLength() => 0;
}

// The negotiated ToClient direction is the server-origin trust boundary. Native
// vehicle packets forwarded on behalf of another client never call this endpoint.
[Preserve]
public sealed class NetPackageRebirthCamperCampDoorSnapshotV1 : NetPackage
{
    private int entity; private Guid worldId, identity; private RebirthCamperEnergyMode mode; private long epoch, revision; private bool open;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperCampDoorSnapshotV1 Setup(int e, Guid w, Guid i, RebirthCamperEnergyMode m, long ep, long r, bool o)
    { entity = e; worldId = w; identity = i; mode = m; epoch = ep; revision = r; open = o; return this; }
    public override void read(PooledBinaryReader r)
    {
        if (r.ReadByte() != 1) throw new System.IO.InvalidDataException("Camper door snapshot version changed.");
        entity = r.ReadInt32(); worldId = new Guid(r.ReadBytes(16)); identity = new Guid(r.ReadBytes(16));
        mode = (RebirthCamperEnergyMode)r.ReadByte(); epoch = r.ReadInt64(); revision = r.ReadInt64(); open = r.ReadBoolean();
    }
    public override void write(PooledBinaryWriter w)
    { base.write(w); w.Write((byte)1); w.Write(entity); w.Write(worldId.ToByteArray()); w.Write(identity.ToByteArray()); w.Write((byte)mode); w.Write(epoch); w.Write(revision); w.Write(open); }
    public override void ProcessPackage(World world, GameManager callbacks)
    { if (world?.IsRemote() == true) RebirthCamperCampDoor.ReceiveSnapshot(world, entity, worldId, identity, mode, epoch, revision, open); }
    public int GetLength() => 0;
}
