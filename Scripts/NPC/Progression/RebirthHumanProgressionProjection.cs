using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Scripting;

#nullable disable

// Read-only UI projection. Authoritative NPC progression continues to live only
// in the canonical aggregate; clients never send skill values back to the host.
public sealed class RebirthHumanProgressionProjection
{
    public RebirthNpcStableId NpcId;
    public long Revision;
    public RebirthNpcOwnershipKind OwnershipKind;
    public string OwnerId;
    public string BackgroundId;
    public string[] TraitIds = new string[0];
    public Dictionary<string,float> Skills = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,float> Theory = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,float> Attributes = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,float> AttributePotentials = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
}

internal static class RebirthHumanProgressionProjectionService
{
    internal static bool TryCapture(RebirthNpcStableId id, out RebirthHumanProgressionProjection projection)
    {
        projection = null;
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id, out var entityId) ||
           !RebirthNpcRuntimeRegistry.TryGet(entityId, out var runtime) ||
           runtime == null || runtime.StableId != id ||
           // A connection baseline must only read an already initialized
           // aggregate. Creating origins here could synchronously save every
           // nearby NPC while a peer is joining or recovering.
           !RebirthNpcAggregatePersistenceStore.TryGet(id,out var person) ||
           !(person.UnknownOptionalElements ?? new string[0]).Any(raw =>
           {
               var node = RebirthCompanionDeskStore.Parse(raw);
               return node.Name == "sharedHumanProgression" && (string)node.Attribute("legacyMapped") == "1";
           }) ||
           !RebirthHumanProgression.TryGet(id, out var record)) return false;
        projection = new RebirthHumanProgressionProjection
        {
            NpcId = id, Revision = record.Revision,
            OwnershipKind = runtime.OwnershipKind, OwnerId = runtime.OwnerId ?? string.Empty,
            BackgroundId = record.Origin.BackgroundId,
            TraitIds = record.Origin.TraitIds.ToArray(),
            Skills = record.Progression.Skills.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.OrdinalIgnoreCase),
            Theory = record.Progression.SkillKnowledge.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.OrdinalIgnoreCase),
            Attributes = record.Progression.Attributes.ToDictionary(p => p.Key, p => p.Value.Current, StringComparer.OrdinalIgnoreCase),
            AttributePotentials = record.Progression.Attributes.ToDictionary(p => p.Key, p => p.Value.Potential, StringComparer.OrdinalIgnoreCase)
        };
        return true;
    }

    internal static void Publish(RebirthNpcStableId id)
    {
        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager == null || !manager.IsServer || !TryCapture(id, out var projection)) return;
        ulong generation = RebirthNpcRecoveryGeneration.RequireCurrent();
        if(!GameManager.IsDedicatedServer) RebirthHumanProgressionClientCache.Apply(projection, generation);
        foreach(var peer in manager.Clients.List)
            if(peer != null && peer.loginDone && !peer.disconnecting &&
               RebirthNpcNetworkEpoch.TryGetServerConnectionEpoch(peer.entityId, out var connection))
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHumanProgression>().Setup(projection, connection, generation),
                    _attachedToEntityId:peer.entityId);
    }

    internal static void SendBaseline(ClientInfo peer)
    {
        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager == null || !manager.IsServer || peer == null ||
           !RebirthNpcNetworkEpoch.TryGetServerConnectionEpoch(peer.entityId, out var connection)) return;
        ulong generation = RebirthNpcRecoveryGeneration.RequireCurrent();
        foreach(var runtime in RebirthNpcRuntimeRegistry.GetSnapshot())
            if(TryCapture(runtime.StableId, out var projection))
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHumanProgression>().Setup(projection, connection, generation),
                    _attachedToEntityId:peer.entityId);
    }
}

public static class RebirthHumanProgressionClientCache
{
    private sealed class Entry
    {
        internal World World;
        internal uint Epoch;
        internal uint Connection;
        internal ulong Generation;
        internal RebirthHumanProgressionProjection Value;
    }
    private static readonly Dictionary<RebirthNpcStableId,Entry> Values = new Dictionary<RebirthNpcStableId,Entry>();
    public static bool TryGet(RebirthNpcStableId id, out RebirthHumanProgressionProjection value)
    {
        value = null;
        if(!ThreadManager.IsMainThread()) return false;
        var world = GameManager.Instance?.World;
        if(world == null || !Values.TryGetValue(id, out var entry) || !ReferenceEquals(entry.World, world)) return false;
        uint epoch = world.IsRemote() ? RebirthNpcNetworkEpoch.ClientEpoch : RebirthNpcNetworkEpoch.GetServerEpoch();
        uint connection = world.IsRemote() ? RebirthNpcNetworkEpoch.ClientConnectionEpoch : 0;
        if(entry.Epoch != epoch || entry.Connection != connection || entry.Generation != RebirthNpcRecoveryProjectionScope.Current ||
           !RebirthNpcRuntimeRegistry.TryGetEntityId(id, out var entityId) ||
           !RebirthNpcRuntimeRegistry.TryGet(entityId, out var runtime) || runtime == null ||
           runtime.OwnershipKind != entry.Value.OwnershipKind ||
           !string.Equals(runtime.OwnerId ?? string.Empty, entry.Value.OwnerId, StringComparison.Ordinal)) return false;
        value = Clone(entry.Value);
        return true;
    }
    internal static bool Apply(RebirthHumanProgressionProjection value, ulong generation)
    {
        if(!ThreadManager.IsMainThread() || value == null || value.NpcId.IsEmpty) return false;
        var world = GameManager.Instance?.World;
        if(world == null) return false;
        uint epoch = world.IsRemote() ? RebirthNpcNetworkEpoch.ClientEpoch : RebirthNpcNetworkEpoch.GetServerEpoch();
        uint connection = world.IsRemote() ? RebirthNpcNetworkEpoch.ClientConnectionEpoch : 0;
        if(world.IsRemote() && !RebirthNpcRecoveryProjectionScope.Observe(generation)) return false;
        if(Values.TryGetValue(value.NpcId, out var prior) && ReferenceEquals(prior.World, world) &&
           prior.Epoch == epoch && prior.Connection == connection &&
           (prior.Generation > generation || prior.Generation == generation && prior.Value.Revision > value.Revision)) return false;
        Values[value.NpcId] = new Entry { World = world, Epoch = epoch, Connection = connection, Generation = generation, Value = Clone(value) };
        return true;
    }
    private static RebirthHumanProgressionProjection Clone(RebirthHumanProgressionProjection source) => new RebirthHumanProgressionProjection
    {
        NpcId = source.NpcId, Revision = source.Revision, OwnershipKind = source.OwnershipKind,
        OwnerId = source.OwnerId, BackgroundId = source.BackgroundId,
        TraitIds = (string[])(source.TraitIds ?? new string[0]).Clone(),
        Skills = new Dictionary<string,float>(source.Skills,StringComparer.OrdinalIgnoreCase),
        Theory = new Dictionary<string,float>(source.Theory,StringComparer.OrdinalIgnoreCase),
        Attributes = new Dictionary<string,float>(source.Attributes ?? new Dictionary<string,float>(),StringComparer.OrdinalIgnoreCase),
        AttributePotentials = new Dictionary<string,float>(source.AttributePotentials ?? new Dictionary<string,float>(),StringComparer.OrdinalIgnoreCase)
    };
}

[Preserve]
public sealed class NetPackageRebirthHumanProgression : NetPackage
{
    private uint epoch, connection;
    private ulong generation;
    private RebirthHumanProgressionProjection value;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthHumanProgression Setup(RebirthHumanProgressionProjection projection, uint recipientConnection, ulong recoveryGeneration)
    { value = projection; epoch = RebirthNpcNetworkEpoch.GetServerEpoch(); connection = recipientConnection; generation = recoveryGeneration; return this; }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        var w = (BinaryWriter)writer;
        if(value == null || value.NpcId.IsEmpty || value.Skills.Count > 128 || value.Theory.Count > 128 ||
           value.Attributes == null || value.AttributePotentials == null || value.Attributes.Count > 128 ||
           value.AttributePotentials.Count > 128 || (value.TraitIds?.Length ?? 0) > 32) throw new InvalidDataException("Invalid human progression projection");
        w.Write((ushort)2); w.Write(epoch); w.Write(connection); w.Write(generation);
        w.Write(value.NpcId.High); w.Write(value.NpcId.Low); w.Write(value.Revision);
        w.Write((byte)value.OwnershipKind); WriteBounded(w,value.OwnerId,128); WriteBounded(w,value.BackgroundId,128);
        w.Write((byte)value.TraitIds.Length); foreach(var trait in value.TraitIds) WriteBounded(w,trait,128);
        WriteValues(w,value.Skills); WriteValues(w,value.Theory);
        WriteValues(w,value.Attributes); WriteValues(w,value.AttributePotentials);
    }
    public override void read(PooledBinaryReader reader)
    {
        ushort version = reader.ReadUInt16();
        if(version != 1 && version != 2) throw new InvalidDataException("Unsupported human progression projection");
        epoch = reader.ReadUInt32(); connection = reader.ReadUInt32(); generation = reader.ReadUInt64();
        value = new RebirthHumanProgressionProjection
        {
            NpcId = new RebirthNpcStableId(reader.ReadUInt64(),reader.ReadUInt64()), Revision = reader.ReadInt64(),
            OwnershipKind = (RebirthNpcOwnershipKind)reader.ReadByte(), OwnerId = ReadBounded(reader,128),
            BackgroundId = ReadBounded(reader,128)
        };
        int traitCount = reader.ReadByte(); if(traitCount > 32) throw new InvalidDataException("Human trait count exceeds bound");
        value.TraitIds = new string[traitCount]; for(int i = 0; i < traitCount; i++) value.TraitIds[i] = ReadBounded(reader,128);
        value.Skills = ReadValues(reader); value.Theory = ReadValues(reader);
        if(version >= 2)
        {
            value.Attributes = ReadValues(reader);
            value.AttributePotentials = ReadValues(reader);
            if(value.Attributes.Count != value.AttributePotentials.Count ||
               value.Attributes.Keys.Any(key => !value.AttributePotentials.ContainsKey(key)))
                throw new InvalidDataException("Incomplete human attributes");
        }
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread() || world?.IsRemote() != true || !ReferenceEquals(world,GameManager.Instance?.World) ||
           connection == 0 || connection != RebirthNpcNetworkEpoch.ClientConnectionEpoch ||
           !RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch) || !RebirthNpcRecoveryProjectionScope.Observe(generation)) return;
        RebirthHumanProgressionClientCache.Apply(value,generation);
    }
    public int GetLength() => 0;
    private static void WriteBounded(BinaryWriter writer,string text,int maximum)
    { if((text?.Length ?? 0) > maximum) throw new InvalidDataException("Human progression label exceeds bound"); writer.Write(text ?? string.Empty); }
    private static string ReadBounded(PooledBinaryReader reader,int maximum)
    { string text = reader.ReadString(); if(text.Length > maximum) throw new InvalidDataException("Human progression label exceeds bound"); return text; }
    private static void WriteValues(BinaryWriter writer,Dictionary<string,float> values)
    {
        writer.Write((byte)values.Count);
        foreach(var pair in values.OrderBy(p => p.Key,StringComparer.Ordinal))
        { WriteBounded(writer,pair.Key,128); if(float.IsNaN(pair.Value) || float.IsInfinity(pair.Value)) throw new InvalidDataException("Nonfinite human progression value"); writer.Write(pair.Value); }
    }
    private static Dictionary<string,float> ReadValues(PooledBinaryReader reader)
    {
        int count = reader.ReadByte(); if(count > 128) throw new InvalidDataException("Human progression value count exceeds bound");
        var values = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        for(int i = 0; i < count; i++)
        { string key = ReadBounded(reader,128); float number = reader.ReadSingle(); if(string.IsNullOrEmpty(key) || float.IsNaN(number) || float.IsInfinity(number) || values.ContainsKey(key)) throw new InvalidDataException("Invalid human progression value"); values.Add(key,number); }
        return values;
    }
}
