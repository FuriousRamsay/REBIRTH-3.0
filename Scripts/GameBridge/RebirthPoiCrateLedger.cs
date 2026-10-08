using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#nullable disable

internal sealed class RebirthPoiCrateExpectation
{
    public readonly string Owner;
    public readonly int PoiId;
    public readonly Vector3i PoiOrigin, PoiSize, Position;
    public readonly Guid Placement;
    public RebirthPoiCrateExpectation(string owner, int poiId, Vector3i origin, Vector3i size, Vector3i position, Guid placement)
    { Owner = owner; PoiId = poiId; PoiOrigin = origin; PoiSize = size; Position = position; Placement = placement; }
    public bool MatchesScope(string owner, int poiId, Vector3i origin, Vector3i size)
        => string.Equals(Owner, owner, StringComparison.Ordinal) && PoiId == poiId && PoiOrigin.Equals(origin) && PoiSize.Equals(size);
    public bool Same(RebirthPoiCrateExpectation other)
        => other != null && Placement == other.Placement && Position.Equals(other.Position)
            && MatchesScope(other.Owner, other.PoiId, other.PoiOrigin, other.PoiSize);
    public bool Valid => !string.IsNullOrWhiteSpace(Owner) && Owner.Length <= 256 && Placement != Guid.Empty
        && PoiId >= 0 && PoiSize.x > 0 && PoiSize.y > 0 && PoiSize.z > 0
        && PoiSize.x <= 2048 && PoiSize.y <= 2048 && PoiSize.z <= 2048
        && (long)Position.x >= (long)PoiOrigin.x - 12 && (long)Position.x <= (long)PoiOrigin.x + PoiSize.x + 12
        && (long)Position.z >= (long)PoiOrigin.z - 12 && (long)Position.z <= (long)PoiOrigin.z + PoiSize.z + 12;
}

/// <summary>Append-only expected placements. Absence of a world crate never erases its expectation.</summary>
internal sealed class RebirthPoiCrateLedger
{
    public const int MaxPerScope = 128;
    private const int MaxRecords = 4096;
    private const long MaxBytes = 4 * 1024 * 1024;
    private readonly string path;
    private readonly object gate = new object();
    private Dictionary<Guid, RebirthPoiCrateExpectation> records = new Dictionary<Guid, RebirthPoiCrateExpectation>();
    private bool loaded, dirty;
    private string loadFailure;

    public RebirthPoiCrateLedger(string file) { path = file; }

    public bool TryReserve(RebirthPoiCrateExpectation record, out string error)
    {
        lock (gate)
        {
            if (!EnsureLoaded(out error) || record == null || !record.Valid)
            { if (error == null) error = "invalid expected crate"; return false; }
            RebirthPoiCrateExpectation prior;
            if (records.TryGetValue(record.Placement, out prior))
            {
                if (!prior.Same(record)) { error = "placement identity conflicts with durable expectation"; return false; }
                return !dirty || Save(out error);
            }
            if (records.Count >= MaxRecords) { error = "expected crate ledger is full"; return false; }
            int count = 0;
            foreach (var entry in records.Values)
                if (entry.MatchesScope(record.Owner, record.PoiId, record.PoiOrigin, record.PoiSize))
                {
                    count++;
                    if (entry.Position.Equals(record.Position)) { error = "crate position has an unresolved prior placement"; return false; }
                }
            if (count >= MaxPerScope) { error = "POI expected crate limit reached"; return false; }
            // Retain intent in memory even when publication fails. No native binding is
            // authorized until this same immutable expectation has been published.
            records.Add(record.Placement, record);
            dirty = true;
            return Save(out error);
        }
    }

    public bool TrySnapshot(string owner, int poiId, Vector3i origin, Vector3i size,
        out List<RebirthPoiCrateExpectation> result, out string error)
    {
        lock (gate)
        {
            result = null;
            if (!EnsureLoaded(out error) || (dirty && !Save(out error))) return false;
            result = records.Values.Where(r => r.MatchesScope(owner, poiId, origin, size))
                .OrderBy(r => r.Placement).ToList();
            return true;
        }
    }

    private bool EnsureLoaded(out string error)
    {
        error = loadFailure;
        if (loaded) return error == null;
        loaded = true;
        try
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("ledger path unavailable");
            bool finalExists = File.Exists(path);
            if (!finalExists && File.Exists(path + ".bak"))
                throw new InvalidDataException("primary ledger missing; backup cannot prove latest expectations");
            var staged = new Dictionary<Guid, RebirthPoiCrateExpectation>();
            if (finalExists) MergeFile(path, staged);
            int finalCount = staged.Count;
            // The ledger only grows. Union of valid older backup and interrupted temp
            // preserves expectations; conflicts/corruption refuse all custody operations.
            if (File.Exists(path + ".bak")) MergeFile(path + ".bak", staged);
            if (staged.Count != finalCount) dirty = true;
            if (File.Exists(path + ".tmp")) { MergeFile(path + ".tmp", staged); dirty = true; }
            records = staged;
            return true;
        }
        catch (Exception ex)
        {
            loadFailure = error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static void MergeFile(string file, Dictionary<Guid, RebirthPoiCrateExpectation> target)
    {
        if (new FileInfo(file).Length > MaxBytes) throw new InvalidDataException("oversized expected crate ledger");
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes };
        XDocument doc;
        using (var reader = XmlReader.Create(file, settings)) doc = XDocument.Load(reader);
        if (doc.Root == null || doc.Root.Name != "poiCrates" || (string)doc.Root.Attribute("version") != "1")
            throw new InvalidDataException("unsupported expected crate ledger schema");
        var inFile = new HashSet<Guid>();
        foreach (XElement node in doc.Root.Elements())
        {
            Guid id;
            string raw = (string)node.Attribute("id");
            if (node.Name != "crate" || raw == null || !Guid.TryParseExact(raw, "N", out id) || id.ToString("N") != raw)
                throw new InvalidDataException("invalid expected crate identity");
            var entry = new RebirthPoiCrateExpectation((string)node.Attribute("owner"), Number(node, "poi"),
                new Vector3i(Number(node, "px"), Number(node, "py"), Number(node, "pz")),
                new Vector3i(Number(node, "sx"), Number(node, "sy"), Number(node, "sz")),
                new Vector3i(Number(node, "x"), Number(node, "y"), Number(node, "z")), id);
            if (!entry.Valid || !inFile.Add(id)) throw new InvalidDataException("invalid or duplicate expected crate scope");
            RebirthPoiCrateExpectation prior;
            if (target.TryGetValue(id, out prior))
            { if (!prior.Same(entry)) throw new InvalidDataException("conflicting expected crate identity"); continue; }
            int scoped = 0;
            foreach (var other in target.Values)
                if (other.MatchesScope(entry.Owner, entry.PoiId, entry.PoiOrigin, entry.PoiSize))
                {
                    scoped++;
                    if (other.Position.Equals(entry.Position)) throw new InvalidDataException("conflicting expected crate position");
                }
            if (target.Count >= MaxRecords || scoped >= MaxPerScope) throw new InvalidDataException("expected crate ledger bounds exceeded");
            target.Add(id, entry);
        }
    }

    private static int Number(XElement node, string name)
    {
        int result;
        if (!int.TryParse((string)node.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            throw new InvalidDataException("invalid expected crate coordinate");
        return result;
    }

    private bool Save(out string error)
    {
        error = null;
        string temp = path + ".tmp";
        try
        {
            var root = new XElement("poiCrates", new XAttribute("version", "1"));
            foreach (var r in records.Values.OrderBy(r => r.Placement))
                root.Add(new XElement("crate", new XAttribute("id", r.Placement.ToString("N")), new XAttribute("owner", r.Owner),
                    new XAttribute("poi", r.PoiId), new XAttribute("px", r.PoiOrigin.x), new XAttribute("py", r.PoiOrigin.y), new XAttribute("pz", r.PoiOrigin.z),
                    new XAttribute("sx", r.PoiSize.x), new XAttribute("sy", r.PoiSize.y), new XAttribute("sz", r.PoiSize.z),
                    new XAttribute("x", r.Position.x), new XAttribute("y", r.Position.y), new XAttribute("z", r.Position.z)));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }))
                { new XDocument(root).Save(writer); writer.Flush(); }
                stream.Flush(true);
            }
            if (!RebirthDurableFileCommit.TryPublish(temp, path, out error)) return false;
            var verified = new Dictionary<Guid, RebirthPoiCrateExpectation>();
            MergeFile(path, verified);
            if (verified.Count != records.Count || records.Any(p => !verified.ContainsKey(p.Key) || !p.Value.Same(verified[p.Key])))
                throw new InvalidDataException("expected crate publication readback mismatch");
            dirty = false;
            return true;
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
    }
}