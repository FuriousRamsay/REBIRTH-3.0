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
