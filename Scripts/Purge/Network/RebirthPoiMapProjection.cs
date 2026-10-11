using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Map-only projection. These records cannot be submitted as world-clear or reset evidence.
internal sealed class RebirthPoiMapRecord
{
    internal readonly RebirthPoiIdentity Identity;
    internal readonly long Epoch;
    internal readonly RebirthPoiClearanceState State;
    internal RebirthPoiMapRecord(RebirthPoiIdentity identity, long epoch, RebirthPoiClearanceState state)
    {
        if (identity == null || epoch < 0 || !Enum.IsDefined(typeof(RebirthPoiClearanceState), state))
            throw new ArgumentException("Invalid map record.");
        Identity = identity; Epoch = epoch; State = state;
    }
    internal bool Same(RebirthPoiMapRecord other) => other != null && Identity.Key == other.Identity.Key
        && Identity.Biome == other.Identity.Biome && Epoch == other.Epoch && State == other.State;
}

internal sealed class RebirthPoiMapFrame
{
    internal const int PageRecords = 8, MaxRecords = 200000, MaxBytes = 16384;
    internal readonly Guid Request, World, Session, Transfer;
    internal readonly long Sequence, Previous;
    internal readonly bool Full;
    internal readonly int Page, Pages, Count;
    internal readonly RebirthPoiMapRecord[] Records;
    internal RebirthPoiMapFrame(Guid request, Guid world, Guid session, Guid transfer, long sequence, long previous,
        bool full, int page, int pages, int count, RebirthPoiMapRecord[] records)
    {
        if (request == Guid.Empty || world == Guid.Empty || session == Guid.Empty || transfer == Guid.Empty
            || sequence < 1 || previous < 0 || sequence <= previous || count < 0 || count > MaxRecords
            || pages != Math.Max(1, (count + PageRecords - 1) / PageRecords) || page < 0 || page >= pages
            || records == null || records.Length != Math.Min(PageRecords, Math.Max(0, count - page * PageRecords))
            || records.Any(r => r == null)) throw new ArgumentException("Invalid map frame.");
        Request = request; World = world; Session = session; Transfer = transfer; Sequence = sequence;
        Previous = previous; Full = full; Page = page; Pages = pages; Count = count;
        Records = (RebirthPoiMapRecord[])records.Clone();
    }
    internal static byte[] Encode(RebirthPoiMapFrame frame)
    {
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
        {
            writer.Write(0x504d5031);
            foreach (var id in new[] { frame.Request, frame.World, frame.Session, frame.Transfer }) writer.Write(id.ToByteArray());
            writer.Write(frame.Sequence); writer.Write(frame.Previous); writer.Write(frame.Full);
            writer.Write(frame.Page); writer.Write(frame.Pages); writer.Write(frame.Count); writer.Write(frame.Records.Length);
            foreach (var record in frame.Records)
            {
                var id = record.Identity; writer.Write(id.Prefab); writer.Write(id.Biome);
                writer.Write(id.X); writer.Write(id.Y); writer.Write(id.Z); writer.Write(id.Rotation);
                writer.Write(id.SizeX); writer.Write(id.SizeY); writer.Write(id.SizeZ);
                writer.Write(record.Epoch); writer.Write((byte)record.State);
            }
            writer.Flush(); if (stream.Length > MaxBytes) throw new ArgumentException("Map frame limit.");
            return stream.ToArray();
        }
    }
    internal static bool TryDecode(byte[] bytes, out RebirthPoiMapFrame frame)
    {
        frame = null;
        if (bytes == null || bytes.Length < 101 || bytes.Length > MaxBytes) return false;
        try
        {
            using (var stream = new MemoryStream(bytes, false)) using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (reader.ReadInt32() != 0x504d5031) return false;
                var request = new Guid(reader.ReadBytes(16)); var world = new Guid(reader.ReadBytes(16));
                var session = new Guid(reader.ReadBytes(16)); var transfer = new Guid(reader.ReadBytes(16));
                long sequence = reader.ReadInt64(), previous = reader.ReadInt64(); bool full = reader.ReadBoolean();
                int page = reader.ReadInt32(), pages = reader.ReadInt32(), count = reader.ReadInt32(), n = reader.ReadInt32();
                if (n < 0 || n > PageRecords) return false;
                var records = new RebirthPoiMapRecord[n];
                for (int i = 0; i < n; i++)
                {
                    // The outer 16 KiB bound also bounds string allocation; identity enforces individual limits.
                    string prefab = reader.ReadString(), biome = reader.ReadString();
                    var id = new RebirthPoiIdentity(prefab, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                        reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), biome);
                    records[i] = new RebirthPoiMapRecord(id, reader.ReadInt64(), (RebirthPoiClearanceState)reader.ReadByte());
                }
                if (stream.Position != stream.Length) return false;
                frame = new RebirthPoiMapFrame(request, world, session, transfer, sequence, previous, full, page, pages, count, records);
                return true;
            }
        }
        catch (IOException) { return false; } catch (ArgumentException) { return false; } catch (OverflowException) { return false; }
    }
}

internal enum RebirthPoiMapReceive { Accepted, Duplicate, Published, Resync, Refused }
internal sealed class RebirthPoiMapReceiver
{
    private readonly object scope;
    private readonly Guid request;
    private Guid world, session, transfer;
    private RebirthPoiMapFrame first;
    private RebirthPoiMapFrame[] pages;
    private int received;
    private Dictionary<string, RebirthPoiMapRecord> published = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, RebirthPoiMapRecord> Published => published;
    internal Guid PublishedWorld => Sequence>0?world:Guid.Empty;
    internal Guid PublishedSession => Sequence>0?session:Guid.Empty;
    internal long Sequence { get; private set; }
    internal RebirthPoiMapReceiver(object scope, Guid request)
    {
        if (scope == null || request == Guid.Empty) throw new ArgumentException("Missing original map request.");
        this.scope = scope; this.request = request;
    }
    internal RebirthPoiMapReceive Accept(object originalScope, RebirthPoiMapFrame frame)
    {
        if (!ReferenceEquals(scope, originalScope) || frame == null || frame.Request != request) return RebirthPoiMapReceive.Refused;
        if (world != Guid.Empty && (frame.World != world || frame.Session != session)) return RebirthPoiMapReceive.Refused;
        if (frame.Sequence <= Sequence) return RebirthPoiMapReceive.Duplicate;
        if (!frame.Full && (Sequence == 0 || frame.Previous != Sequence)) return RebirthPoiMapReceive.Resync;
        if (first == null || frame.Transfer != transfer)
        {
            // A newer full snapshot can replace an incomplete transfer; older/interleaved pages cannot.
            if (first != null && frame.Sequence <= first.Sequence) return RebirthPoiMapReceive.Refused;
            first = frame; transfer = frame.Transfer; pages = new RebirthPoiMapFrame[frame.Pages]; received = 0;
        }
        if (frame.World != first.World || frame.Session != first.Session || frame.Sequence != first.Sequence || frame.Previous != first.Previous || frame.Full != first.Full
            || frame.Pages != first.Pages || frame.Count != first.Count) return RebirthPoiMapReceive.Refused;
        var prior = pages[frame.Page];
        if (prior != null)
        {
            if (prior.Records.Length != frame.Records.Length) return RebirthPoiMapReceive.Refused;
            for (int i = 0; i < prior.Records.Length; i++) if (!prior.Records[i].Same(frame.Records[i])) return RebirthPoiMapReceive.Refused;
            return RebirthPoiMapReceive.Duplicate;
        }
        pages[frame.Page] = frame;
        if (++received != pages.Length) return RebirthPoiMapReceive.Accepted;
        var next = frame.Full ? new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal)
            : new Dictionary<string, RebirthPoiMapRecord>(published, StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages) foreach (var record in page.Records)
        {
            if (!keys.Add(record.Identity.Key)) return RebirthPoiMapReceive.Refused;
            RebirthPoiMapRecord old;
            if (published.TryGetValue(record.Identity.Key, out old) && (record.Epoch < old.Epoch || record.Identity.Biome != old.Identity.Biome))
                return RebirthPoiMapReceive.Refused;
            next[record.Identity.Key] = record;
        }
        if (next.Count > RebirthPoiMapFrame.MaxRecords) return RebirthPoiMapReceive.Refused;
        published = next; world = frame.World; session = frame.Session; Sequence = frame.Sequence;
        first = null; pages = null; received = 0; return RebirthPoiMapReceive.Published;
    }
}

