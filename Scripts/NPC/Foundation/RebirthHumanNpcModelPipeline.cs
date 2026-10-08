using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;

#nullable disable

public enum RebirthHumanNpcModelPipeline : byte
{
    Auto = 0,
    Custom = 1,
    SDCS = 2
}

public enum RebirthHumanNpcModelFallbackPolicy : byte
{
    Fail = 0,
    Custom = 1
}

public readonly struct RebirthHumanNpcAppearanceDescriptor : IEquatable<RebirthHumanNpcAppearanceDescriptor>
{
    public const ushort CurrentSchemaVersion = 2;

    public readonly ushort SchemaVersion;
    public readonly RebirthHumanNpcModelPipeline Pipeline;
    public readonly int Seed;
    public readonly string Archetype;
    internal readonly RebirthNpcResolvedAppearance Resolved;

    public RebirthHumanNpcAppearanceDescriptor(
        RebirthHumanNpcModelPipeline pipeline,
        int seed,
        string archetype,
        ushort schemaVersion = 1)
    {
        if (pipeline != RebirthHumanNpcModelPipeline.Custom && pipeline != RebirthHumanNpcModelPipeline.SDCS)
            throw new ArgumentOutOfRangeException(nameof(pipeline), "A persisted appearance must use a concrete model pipeline.");
        if (schemaVersion == 0 || schemaVersion > CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        SchemaVersion = schemaVersion;
        Pipeline = pipeline;
        Seed = seed;
        Archetype = (archetype ?? string.Empty).Trim();
        Resolved = null;
    }

    public bool IsValid =>
        SchemaVersion > 0 &&
        SchemaVersion <= CurrentSchemaVersion &&
        (Pipeline == RebirthHumanNpcModelPipeline.Custom || Pipeline == RebirthHumanNpcModelPipeline.SDCS);

    public bool Equals(RebirthHumanNpcAppearanceDescriptor other) =>
        SchemaVersion == other.SchemaVersion &&
        Pipeline == other.Pipeline &&
        Seed == other.Seed &&
        string.Equals(Archetype, other.Archetype, StringComparison.Ordinal) &&
        object.Equals(Resolved, other.Resolved);

    internal RebirthHumanNpcAppearanceDescriptor(RebirthNpcResolvedAppearance resolved)
        : this(resolved?.Pipeline ?? throw new ArgumentNullException(nameof(resolved)), resolved.LegacySeed, resolved.Archetype, 2)
    { Resolved = resolved; }

    internal bool TryWithResolved(RebirthNpcResolvedAppearance candidate, out RebirthHumanNpcAppearanceDescriptor descriptor)
    {
        descriptor = this;
        if (!IsValid || !RebirthNpcResolvedAppearance.TryResolveOnce(Resolved, Pipeline, Seed, Archetype, candidate, out var selected)) return false;
        descriptor = new RebirthHumanNpcAppearanceDescriptor(selected); return true;
    }

    public override bool Equals(object obj) => obj is RebirthHumanNpcAppearanceDescriptor other && Equals(other);
    public override int GetHashCode() => unchecked(((((SchemaVersion * 397) ^ (int)Pipeline) * 397 ^ Seed) * 397 ^ (Archetype?.GetHashCode() ?? 0)) * 397 ^ (Resolved?.GetHashCode() ?? 0));

    public static RebirthHumanNpcAppearanceDescriptor CreateCustom(string archetype = "") =>
        new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.Custom, 0, archetype);

    public static RebirthHumanNpcAppearanceDescriptor CreateSdcs(RebirthNpcStableId stableId, string archetype = "") =>
        new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS, DeriveSeed(stableId), archetype);

    private static int DeriveSeed(RebirthNpcStableId stableId)
    {
        unchecked
        {
            ulong mixed = stableId.High ^ (stableId.Low + 0x9E3779B97F4A7C15UL + (stableId.High << 6) + (stableId.High >> 2));
            int seed = (int)(mixed ^ (mixed >> 32));
            return seed == 0 ? 1 : seed;
        }
    }
}

internal static class RebirthNpcAppearanceBinaryCodec
{
    internal static void Write(BinaryWriter writer, RebirthHumanNpcAppearanceDescriptor descriptor)
    {
        if (!descriptor.IsValid) throw new InvalidDataException("Invalid NPC appearance.");
        writer.Write(descriptor.SchemaVersion); writer.Write((byte)descriptor.Pipeline); writer.Write(descriptor.Seed);
        WriteString(writer, descriptor.Archetype, 1024);
        if (descriptor.SchemaVersion >= 2)
        {
            writer.Write(descriptor.Resolved != null);
            if (descriptor.Resolved != null) WriteString(writer, descriptor.Resolved.Encode(), 32768);
        }
    }
    internal static RebirthHumanNpcAppearanceDescriptor Read(BinaryReader reader, bool allowResolved = true)
    {
        ushort schema = reader.ReadUInt16();
        if (schema < 1 || schema > RebirthHumanNpcAppearanceDescriptor.CurrentSchemaVersion || (!allowResolved && schema != 1))
            throw new InvalidDataException("Unsupported NPC appearance schema.");
        var pipeline = (RebirthHumanNpcModelPipeline)reader.ReadByte(); int seed = reader.ReadInt32();
        string archetype = ReadString(reader, 1024);
        var descriptor = new RebirthHumanNpcAppearanceDescriptor(pipeline, seed, archetype, schema);
        if (schema >= 2 && reader.ReadBoolean())
        {
            if (!RebirthNpcResolvedAppearance.TryDecode(ReadString(reader, 32768), out var resolved) ||
                !descriptor.TryWithResolved(resolved, out descriptor)) throw new InvalidDataException("Invalid resolved NPC appearance binding.");
        }
        return descriptor;
    }
    internal static XmlElement WriteAggregate(XmlDocument document, RebirthHumanNpcAppearanceDescriptor descriptor)
    {
        byte[] bytes;
        using (var stream = new MemoryStream())
        { using (var writer = new BinaryWriter(stream)) { Write(writer, descriptor); writer.Flush(); bytes = stream.ToArray(); } }
        var node = document.CreateElement("humanAppearance");
        node.SetAttribute("version", "1"); node.SetAttribute("required", "1"); node.SetAttribute("payload", Convert.ToBase64String(bytes));
        return node;
    }
    internal static bool TryReadAggregate(XmlElement parent, out RebirthHumanNpcAppearanceDescriptor? descriptor)
    {
        descriptor = null;
        if (parent == null) return false;
        var nodes = parent.ChildNodes.Cast<XmlNode>().OfType<XmlElement>().Where(n => string.Equals(n.LocalName, "humanAppearance", StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (nodes.Length == 0) return true;
        var node = nodes[0];
        if (nodes.Length != 1 || node.Name != "humanAppearance" || node.NamespaceURI.Length != 0 || node.ChildNodes.Count != 0 ||
            node.Attributes.Count != 3 || node.GetAttribute("version") != "1" || node.GetAttribute("required") != "1" ||
            node.Attributes.Cast<XmlAttribute>().Any(a => a.NamespaceURI.Length != 0 || (a.Name != "version" && a.Name != "required" && a.Name != "payload"))) return false;
        string payload = node.GetAttribute("payload");
        if (payload.Length == 0 || payload.Length > 54000) return false;
        try
        {
            byte[] bytes = Convert.FromBase64String(payload);
            if (bytes.Length > 40000 || Convert.ToBase64String(bytes) != payload) return false;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            { var value = Read(reader); if (stream.Position != stream.Length) return false; descriptor = value; return true; }
        }
        catch (FormatException) { return false; }
        catch (IOException) { return false; }
        catch (InvalidDataException) { return false; }
        catch (ArgumentException) { return false; }
    }
    internal static void WriteString(BinaryWriter writer, string value, int maxBytes)
    {
        if (value == null || new System.Text.UTF8Encoding(false, true).GetByteCount(value) > maxBytes)
            throw new InvalidDataException("NPC appearance string exceeds bound.");
        writer.Write(value);
    }
    internal static string ReadString(BinaryReader reader, int maxBytes)
    {
        uint size = 0; int shift = 0;
        for (int i = 0; i < 5; i++)
        {
            byte b = reader.ReadByte();
            if (i == 4 && (b & 0xf8) != 0) throw new InvalidDataException("Invalid NPC appearance string length.");
            size |= (uint)(b & 0x7f) << shift;
            if ((b & 0x80) == 0)
            {
                if (i > 0 && (b & 0x7f) == 0) throw new InvalidDataException("Noncanonical NPC appearance string length.");
                if (size > maxBytes) throw new InvalidDataException("NPC appearance string exceeds bound.");
                byte[] bytes = reader.ReadBytes((int)size);
                if (bytes.Length != size) throw new EndOfStreamException();
                try { return new System.Text.UTF8Encoding(false, true).GetString(bytes); }
                catch (System.Text.DecoderFallbackException ex) { throw new InvalidDataException("Invalid NPC appearance UTF8.", ex); }
            }
            shift += 7;
        }
        throw new InvalidDataException("Invalid NPC appearance string length.");
    }
}
public readonly struct RebirthHumanNpcModelPipelineResolution
{
    public readonly RebirthHumanNpcModelPipeline Requested;
    public readonly RebirthHumanNpcModelPipeline Resolved;
    public readonly RebirthHumanNpcModelFallbackPolicy FallbackPolicy;
    public readonly string Archetype;
    public readonly string Diagnostic;

    public RebirthHumanNpcModelPipelineResolution(
        RebirthHumanNpcModelPipeline requested,
        RebirthHumanNpcModelPipeline resolved,
        RebirthHumanNpcModelFallbackPolicy fallbackPolicy,
        string archetype,
        string diagnostic)
    {
        Requested = requested;
        Resolved = resolved;
        FallbackPolicy = fallbackPolicy;
        Archetype = archetype ?? string.Empty;
        Diagnostic = diagnostic ?? string.Empty;
    }
}

public static class RebirthHumanNpcModelPipelineResolver
{
    public const string PipelineProperty = "ModelPipeline";
    public const string FallbackProperty = "ModelPipelineFallback";
    public const string ArchetypeProperty = "ModelArchetype";

    public static RebirthHumanNpcModelPipelineResolution Resolve(int entityClassId)
    {
        IDictionary<string, string> values = GetEntityClassValues(entityClassId);
        string rawPipeline = Get(values, PipelineProperty);
        string rawFallback = Get(values, FallbackProperty);
        string archetype = Get(values, ArchetypeProperty);

        RebirthHumanNpcModelPipeline requested;
        string diagnostic;
        if (!TryParsePipeline(rawPipeline, out requested))
        {
            requested = RebirthHumanNpcModelPipeline.Auto;
            diagnostic = string.IsNullOrWhiteSpace(rawPipeline)
                ? "ModelPipeline was not declared; inherited project default applies."
                : "Unknown ModelPipeline value '" + rawPipeline + "'; inherited project default applies.";
        }
        else
        {
            diagnostic = string.Empty;
        }

        RebirthHumanNpcModelFallbackPolicy fallback = ParseFallback(rawFallback);
        RebirthHumanNpcModelPipeline resolved = requested == RebirthHumanNpcModelPipeline.Auto
            ? RebirthHumanNpcModelPipeline.Custom
            : requested;

        return new RebirthHumanNpcModelPipelineResolution(requested, resolved, fallback, archetype, diagnostic);
    }

    public static bool TryParsePipeline(string value, out RebirthHumanNpcModelPipeline pipeline)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            pipeline = RebirthHumanNpcModelPipeline.Auto;
            return true;
        }
        if (normalized.Equals("Custom", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("CustomModel", StringComparison.OrdinalIgnoreCase))
        {
            pipeline = RebirthHumanNpcModelPipeline.Custom;
            return true;
        }
        if (normalized.Equals("SDCS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Sdcs", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ProceduralSDCS", StringComparison.OrdinalIgnoreCase))
        {
            pipeline = RebirthHumanNpcModelPipeline.SDCS;
            return true;
        }
        pipeline = RebirthHumanNpcModelPipeline.Auto;
        return false;
    }

    private static RebirthHumanNpcModelFallbackPolicy ParseFallback(string value)
    {
        return string.Equals((value ?? string.Empty).Trim(), "Fail", StringComparison.OrdinalIgnoreCase)
            ? RebirthHumanNpcModelFallbackPolicy.Fail
            : RebirthHumanNpcModelFallbackPolicy.Custom;
    }

    private static IDictionary<string, string> GetEntityClassValues(int entityClassId)
    {
        EntityClass entityClass;
        if (EntityClass.list == null ||
            !EntityClass.list.TryGetValue(entityClassId, out entityClass) ||
            entityClass == null || entityClass.Properties == null || entityClass.Properties.Values == null)
            return EmptyValues.Instance;
        return entityClass.Properties.Values;
    }

    private static string Get(IDictionary<string, string> values, string key)
    {
        string value;
        return values != null && values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private sealed class EmptyValues : Dictionary<string, string>
    {
        public static readonly EmptyValues Instance = new EmptyValues();
        private EmptyValues() : base(StringComparer.OrdinalIgnoreCase) { }
    }
}

public interface IRebirthHumanNpcModelAdapter
{
    RebirthHumanNpcModelPipeline Pipeline { get; }
    RebirthHumanNpcAppearanceDescriptor Appearance { get; }
    void Initialize(EntityRebirthHumanoidNPC entity);
    void RefreshEquipment();
    void Release();
}

public abstract class RebirthHumanNpcModelAdapterBase : IRebirthHumanNpcModelAdapter
{
    protected EntityRebirthHumanoidNPC Entity { get; private set; }
    public abstract RebirthHumanNpcModelPipeline Pipeline { get; }
    public RebirthHumanNpcAppearanceDescriptor Appearance { get; }

    protected RebirthHumanNpcModelAdapterBase(RebirthHumanNpcAppearanceDescriptor appearance)
    {
        if (!appearance.IsValid) throw new ArgumentException("A valid appearance descriptor is required.", nameof(appearance));
        Appearance = appearance;
    }

    public void Initialize(EntityRebirthHumanoidNPC entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (Entity != null) throw new InvalidOperationException("Human NPC model adapter is already initialized.");
        Entity = entity;
        OnInitialize();
    }

    public virtual void RefreshEquipment() { }

    public void Release()
    {
        if (Entity == null) return;
        OnRelease();
        Entity = null;
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnRelease() { }
}

public sealed class RebirthCustomHumanNpcModelAdapter : RebirthHumanNpcModelAdapterBase
{
    public override RebirthHumanNpcModelPipeline Pipeline => RebirthHumanNpcModelPipeline.Custom;
    public RebirthCustomHumanNpcModelAdapter(RebirthHumanNpcAppearanceDescriptor appearance) : base(appearance) { }

    protected override void OnInitialize()
    {
        if (Entity.emodel is EModelRebirthSdcsNpc)
            throw new InvalidOperationException(
                "Custom human NPC pipeline cannot use EModelRebirthSdcsNpc. " +
                "Configure the entity class with its existing custom ModelType.");
    }

    public override void RefreshEquipment()
    {
        Entity.ReassignEquipmentTransforms();
    }
}

public sealed class RebirthSdcsHumanNpcModelAdapter : RebirthHumanNpcModelAdapterBase
{
    private EModelRebirthSdcsNpc model;

    public override RebirthHumanNpcModelPipeline Pipeline => RebirthHumanNpcModelPipeline.SDCS;
    public RebirthSdcsHumanNpcModelAdapter(RebirthHumanNpcAppearanceDescriptor appearance) : base(appearance) { }

    protected override void OnInitialize()
    {
        model = Entity.emodel as EModelRebirthSdcsNpc;
        if (model == null)
            throw new InvalidOperationException(
                "SDCS human NPC pipeline requires ModelType='EModelRebirthSdcsNpc, RebirthUtils'.");

        model.ApplyAppearance(Appearance);
    }

    public override void RefreshEquipment()
    {
        Entity.ReassignEquipmentTransforms();
        model?.RefreshAppearanceMeshes();
    }

    protected override void OnRelease()
    {
        model = null;
    }
}

public static class RebirthHumanNpcModelAdapterFactory
{
    public static IRebirthHumanNpcModelAdapter Create(RebirthHumanNpcAppearanceDescriptor appearance)
    {
        switch (appearance.Pipeline)
        {
            case RebirthHumanNpcModelPipeline.Custom:
                return new RebirthCustomHumanNpcModelAdapter(appearance);
            case RebirthHumanNpcModelPipeline.SDCS:
                return new RebirthSdcsHumanNpcModelAdapter(appearance);
            default:
                throw new InvalidDataException("Unsupported concrete human NPC model pipeline: " + appearance.Pipeline);
        }
    }
}
