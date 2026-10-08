using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthPartyIdentityProtocol
{
    public const int Version = 2;
    public const int MaxRows = 128;
}

public sealed class RebirthPartyIdentityView
{
    public int EntityId;
    public string GroupId = string.Empty;
    public string Name = string.Empty;
    public int ColorId;
    public bool IsParty;
    public bool IsLeader;
    public bool CanEdit;
    public long Revision;

    public RebirthPartyIdentityView Clone()
    {
        return new RebirthPartyIdentityView { EntityId = EntityId, GroupId = GroupId ?? string.Empty, Name = Name ?? string.Empty, ColorId = ColorId, IsParty = IsParty, IsLeader = IsLeader, CanEdit = CanEdit, Revision = Revision };
    }
}

public sealed class RebirthPartyIdentitySnapshot
{
    public int ProtocolVersion = RebirthPartyIdentityProtocol.Version;
    public long Revision;
    public readonly List<RebirthPartyIdentityView> Rows = new List<RebirthPartyIdentityView>();
    public RebirthPartyIdentitySnapshot Clone()
    {
        RebirthPartyIdentitySnapshot s = new RebirthPartyIdentitySnapshot { ProtocolVersion = ProtocolVersion, Revision = Revision };
        for (int i = 0; i < Rows.Count; ++i) if (Rows[i] != null) s.Rows.Add(Rows[i].Clone());
        return s;
    }
}

public static class RebirthPartyIdentityClientState
{
    private static readonly object Sync = new object();
    private static RebirthPartyIdentitySnapshot snapshot;
    private static readonly Dictionary<int, RebirthPartyIdentityView> ByEntity = new Dictionary<int, RebirthPartyIdentityView>();
    public static event Action Changed;

    public static void Reset()
    {
        lock (Sync) { snapshot = null; ByEntity.Clear(); }
    }

    public static RebirthPartyIdentitySnapshot GetSnapshot()
    {
        lock (Sync) return snapshot != null ? snapshot.Clone() : null;
    }

    public static bool TryGet(int entityId, out RebirthPartyIdentityView view)
    {
        lock (Sync)
        {
            RebirthPartyIdentityView current;
            if (ByEntity.TryGetValue(entityId, out current) && current != null) { view = current.Clone(); return true; }
        }
        view = null;
        return false;
    }

    internal static void Receive(RebirthPartyIdentitySnapshot incoming)
    {
        if (incoming == null || incoming.ProtocolVersion != RebirthPartyIdentityProtocol.Version) return;
        bool accepted = false;
        lock (Sync)
        {
            if (snapshot != null && incoming.Revision < snapshot.Revision) return;
            snapshot = incoming.Clone();
            ByEntity.Clear();
            for (int i = 0; i < snapshot.Rows.Count; ++i)
            {
                RebirthPartyIdentityView row = snapshot.Rows[i];
                if (row != null) ByEntity[row.EntityId] = row.Clone();
            }
            accepted = true;
        }
        if (accepted) Changed?.Invoke();
    }
}

[Preserve]
public sealed class NetPackageRebirthPartyIdentityRequest : NetPackage
{
    private const byte RequestSnapshot = 0;
    private const byte EditIdentity = 1;
    private int protocol = RebirthPartyIdentityProtocol.Version;
    private int playerEntityId;
    private byte operation;
    private long knownRevision;
    private bool force;
    private string groupName = string.Empty;
    private int colorId;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthPartyIdentityRequest SetupSnapshotRequest(int entityId, long known, bool forceRefresh)
    {
        protocol = RebirthPartyIdentityProtocol.Version; playerEntityId = entityId; operation = RequestSnapshot; knownRevision = Math.Max(0L, known); force = forceRefresh; groupName = string.Empty; colorId = 0; return this;
    }

    public NetPackageRebirthPartyIdentityRequest SetupEdit(int entityId, string name, int color)
    {
        protocol = RebirthPartyIdentityProtocol.Version; playerEntityId = entityId; operation = EditIdentity; knownRevision = 0L; force = true; groupName = RebirthPartyIdentityService.NormalizeName(name); colorId = RebirthPartyIdentityService.NormalizeColor(color); return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader; protocol = b.ReadInt32(); playerEntityId = b.ReadInt32(); operation = b.ReadByte(); knownRevision = b.ReadInt64(); force = b.ReadBoolean(); groupName = RebirthPartyIdentityService.NormalizeName(b.ReadString()); colorId = RebirthPartyIdentityService.NormalizeColor(b.ReadInt32());
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; b.Write(protocol); b.Write(playerEntityId); b.Write(operation); b.Write(knownRevision); b.Write(force); b.Write(groupName ?? string.Empty); b.Write(colorId);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || protocol != RebirthPartyIdentityProtocol.Version || !ValidEntityIdForSender(playerEntityId)) return;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null) return;
        if (operation == EditIdentity)
        {
            string ignored; RebirthPartyIdentityService.ProcessServerRequest(player, groupName, colorId, out ignored);
        }
        else RebirthPartyIdentityService.SendSnapshotTo(player, force, "client-party-identity-request");
    }
    public int GetLength() { return 32 + (groupName != null ? groupName.Length * 2 : 0); }
}

[Preserve]
public sealed class NetPackageRebirthPartyIdentitySnapshot : NetPackage
{
    private RebirthPartyIdentitySnapshot snapshot = new RebirthPartyIdentitySnapshot();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthPartyIdentitySnapshot Setup(RebirthPartyIdentitySnapshot value) { snapshot = value != null ? value.Clone() : new RebirthPartyIdentitySnapshot(); return this; }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader; RebirthPartyIdentitySnapshot s = new RebirthPartyIdentitySnapshot(); s.ProtocolVersion = b.ReadInt32(); s.Revision = b.ReadInt64(); int count = Math.Min((int)b.ReadUInt16(), RebirthPartyIdentityProtocol.MaxRows);
        for (int i = 0; i < count; ++i)
        {
            RebirthPartyIdentityView v = new RebirthPartyIdentityView(); v.EntityId = b.ReadInt32(); v.GroupId = ReadString(b, 64); v.Name = ReadString(b, RebirthPartyIdentityService.MaxGroupNameLength); v.ColorId = RebirthPartyIdentityService.NormalizeColor(b.ReadInt32()); v.IsParty = b.ReadBoolean(); v.IsLeader = b.ReadBoolean(); v.CanEdit = b.ReadBoolean(); v.Revision = s.Revision; s.Rows.Add(v);
        }
        snapshot = s;
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter b = (BinaryWriter)writer; RebirthPartyIdentitySnapshot s = snapshot ?? new RebirthPartyIdentitySnapshot(); b.Write(s.ProtocolVersion); b.Write(s.Revision); int count = Math.Min(s.Rows.Count, RebirthPartyIdentityProtocol.MaxRows); b.Write((ushort)count);
        for (int i = 0; i < count; ++i)
        {
            RebirthPartyIdentityView v = s.Rows[i] ?? new RebirthPartyIdentityView(); b.Write(v.EntityId); WriteString(b, v.GroupId, 64); WriteString(b, v.Name, RebirthPartyIdentityService.MaxGroupNameLength); b.Write(RebirthPartyIdentityService.NormalizeColor(v.ColorId)); b.Write(v.IsParty); b.Write(v.IsLeader); b.Write(v.CanEdit);
        }
    }
    public override void ProcessPackage(World world, GameManager callbacks) { if (snapshot.ProtocolVersion == RebirthPartyIdentityProtocol.Version) RebirthPartyIdentityClientState.Receive(snapshot); }
    public int GetLength() { return 32 + (snapshot != null ? snapshot.Rows.Count * 140 : 0); }
    private static void WriteString(BinaryWriter b, string value, int max) { string v = value ?? string.Empty; if (v.Length > max) v = v.Substring(0, max); b.Write(v); }
    private static string ReadString(BinaryReader b, int max) { string v = b.ReadString() ?? string.Empty; if (v.Length > max) v = v.Substring(0, max); return v; }
}
