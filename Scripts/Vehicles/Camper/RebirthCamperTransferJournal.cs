using System;
using System.IO;
using System.Security.Cryptography;

// Server-owned durable write-ahead record. Payloads must contain complete native
// cursor/player, cargo and module snapshots; this class never executes a retry.
public sealed class RebirthCamperTransferJournal
{
    public enum Phase { Prepared = 0, Uncertain = 1, Applied = 2, Rejected = 3 }
    public sealed class Record
    {
        public Guid Operation { get; private set; }
        public Guid Vehicle { get; private set; }
        public string OwnerCreation { get; private set; }
        public long Revision { get; private set; }
        public Phase State { get; private set; }
        private readonly byte[] before, after;
        public byte[] Before { get { return (byte[])before.Clone(); } }
        public byte[] After { get { return (byte[])after.Clone(); } }
        internal Record(Guid operation, Guid vehicle, string owner, long revision, Phase state, byte[] prior, byte[] next)
        { Operation=operation; Vehicle=vehicle; OwnerCreation=owner; Revision=revision; State=state;
          before=(byte[])prior.Clone(); after=(byte[])next.Clone(); }
    }
    private const uint Magic = 0x314A4352; // RCJ1
    private const int MaximumFrame = 64 * 1024 * 1024;
    private readonly string root;
    public RebirthCamperTransferJournal(string serverOwnedDirectory)
    {
        if (string.IsNullOrWhiteSpace(serverOwnedDirectory)) throw new ArgumentException("Server journal directory required.");
        root = Path.GetFullPath(serverOwnedDirectory);
        Directory.CreateDirectory(root);
    }

    public Record Prepare(Guid operation, Guid vehicle, string ownerCreation, long revision, byte[] before, byte[] after)
    {
        if (operation == Guid.Empty || vehicle == Guid.Empty || string.IsNullOrWhiteSpace(ownerCreation)
            || revision < 0 || before == null || after == null || before.Length == 0 || after.Length == 0
            || before.Length > MaximumFrame || after.Length > MaximumFrame)
            throw new ArgumentException("Complete authenticated transaction snapshots required.");
        var record = new Record(operation, vehicle, ownerCreation, revision, Phase.Prepared, before, after);
        var existing = Read(operation);
        if (existing != null)
        {
            if (!Equal(Encode(existing, Phase.Prepared), Encode(record, Phase.Prepared)))
                throw new InvalidDataException("Receipt identity reused for another transfer.");
            return existing;
        }
        Publish(record);
        return record;
    }

    public Record Advance(Guid operation, Phase expected, Phase next)
    {
        bool valid = expected == Phase.Prepared && (next == Phase.Uncertain || next == Phase.Rejected)
            || expected == Phase.Uncertain && next == Phase.Applied;
        if (!valid) throw new InvalidOperationException("Invalid transfer journal transition.");
        var record = Read(operation);
        if (record == null || record.State != expected) throw new InvalidOperationException("Transfer phase changed; reconcile first.");
        var advanced = new Record(record.Operation, record.Vehicle, record.OwnerCreation, record.Revision, next, record.Before, record.After);
        Publish(advanced);
        return advanced;
    }

    public void AssertAvailable(Guid vehicle, Guid permittedOperation)
    {
        if (vehicle == Guid.Empty || permittedOperation == Guid.Empty) throw new ArgumentException("Transfer identities required.");
        var seen = new System.Collections.Generic.HashSet<Guid>();
        foreach (string path in Directory.EnumerateFiles(root, "*.rcj", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(path);
            Guid operation;
            if (name.Length != 38 || name[32] != '.' || name[34] != '.'
                || name[33] < '0' || name[33] > '3' || !Guid.TryParseExact(name.Substring(0,32),"N",out operation))
                throw new InvalidDataException("Unrecognized journal receipt; reconcile before transferring.");
            if (!seen.Add(operation)) continue;
            var record = Read(operation);
            if (record.Vehicle == vehicle && operation != permittedOperation
                && (record.State == Phase.Prepared || record.State == Phase.Uncertain))
                throw new InvalidOperationException("Vehicle has an unresolved transfer: " + operation.ToString("N"));
        }
    }

    public Record Read(Guid operation)
    {
        if (operation == Guid.Empty) throw new ArgumentException("Receipt identity required.");
        Record latest = null;
        foreach (Phase phase in new[] { Phase.Prepared, Phase.Uncertain, Phase.Applied, Phase.Rejected })
        {
            string path = FileName(operation, phase);
            if (!File.Exists(path)) continue;
            var current = Decode(File.ReadAllBytes(path));
            if (current.Operation != operation || current.State != phase)
                throw new InvalidDataException("Journal filename and identity disagree.");
            if (latest != null && (!Equal(Encode(latest, Phase.Prepared), Encode(current, Phase.Prepared))
                || phase == Phase.Rejected && latest.State != Phase.Prepared))
                throw new InvalidDataException("Conflicting transfer records; manual reconciliation required.");
            if (latest == null && phase != Phase.Prepared || phase == Phase.Applied && (latest == null || latest.State != Phase.Uncertain))
                throw new InvalidDataException("Incomplete transfer journal history.");
            latest = current;
        }
        return latest;
    }

    // Reuse the same durable receipts on reconnect; never reconstruct or repeat
    // a cursor debit from an in-memory request cache.
    public Record FindPending(Guid vehicle)
    {
        Record pending = null;
        foreach (string path in Directory.EnumerateFiles(root, "*.0.rcj", SearchOption.TopDirectoryOnly))
        {
            Guid operation;
            if (!Guid.TryParseExact(Path.GetFileName(path).Substring(0,32), "N", out operation))
                throw new InvalidDataException("Unrecognized transfer receipt.");
            var record = Read(operation);
            if (record.Vehicle != vehicle || record.State == Phase.Applied || record.State == Phase.Rejected) continue;
            if (pending != null) throw new InvalidDataException("Multiple pending transfers require reconciliation.");
            pending = record;
        }
        return pending;
    }

    private string FileName(Guid operation, Phase state) { return Path.Combine(root, operation.ToString("N") + "." + (int)state + ".rcj"); }
    // Installation recovery may correlate a retained portable carrier with an
    // already-applied removal. Enumeration never repeats or advances a transfer.
    internal System.Collections.Generic.IEnumerable<Record> FindApplied(Guid vehicle, string ownerCreation)
    {
        if(vehicle==Guid.Empty||string.IsNullOrWhiteSpace(ownerCreation))
            throw new ArgumentException("Exact source vehicle and owner required.");
        foreach(string path in Directory.EnumerateFiles(root,"*.2.rcj",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(path);
            if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var operation))
                throw new InvalidDataException("Unrecognized applied transfer receipt.");
            var record=Read(operation);
            if(record!=null&&record.State==Phase.Applied&&record.Vehicle==vehicle&&record.OwnerCreation==ownerCreation)
                yield return record;
        }
    }
    private void Publish(Record record)
    {
        string destination = FileName(record.Operation, record.State);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[] bytes = Encode(record, record.State);
        // Never overwrite a receipt. A leftover temporary file is evidence only,
        // never authority to replay a partially published inventory operation.
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        File.Move(temporary, destination);
    }
    private static byte[] Encode(Record record, Phase phase)
    {
        using (var memory = new MemoryStream())
        using (var writer = new BinaryWriter(memory))
        {
            writer.Write(Magic); writer.Write(record.Operation.ToByteArray()); writer.Write(record.Vehicle.ToByteArray());
            writer.Write(record.OwnerCreation); writer.Write(record.Revision); writer.Write((byte)phase);
            byte[] before = record.Before, after = record.After;
            writer.Write(before.Length); writer.Write(before);
            writer.Write(after.Length); writer.Write(after); writer.Flush();
            byte[] payload = memory.ToArray();
            using (var hash = SHA256.Create()) writer.Write(hash.ComputeHash(payload));
            writer.Flush(); return memory.ToArray();
        }
    }
    private static Record Decode(byte[] bytes)
    {
        if (bytes.Length < 32 || bytes.Length > MaximumFrame * 2 + 8192) throw new InvalidDataException("Invalid journal size.");
        using (var memory = new MemoryStream(bytes, 0, bytes.Length - 32, false))
        using (var reader = new BinaryReader(memory))
        {
            using (var hash = SHA256.Create())
            { byte[] digest = hash.ComputeHash(memory); for (int i=0;i<32;i++) if (digest[i]!=bytes[bytes.Length-32+i]) throw new InvalidDataException("Journal checksum failed."); }
            memory.Position=0;
            if (reader.ReadUInt32()!=Magic) throw new InvalidDataException("Unsupported transfer journal.");
            Guid operation=new Guid(reader.ReadBytes(16)), vehicle=new Guid(reader.ReadBytes(16));
            string owner=reader.ReadString(); long revision=reader.ReadInt64(); Phase phase=(Phase)reader.ReadByte();
            byte[] before=Frame(reader), after=Frame(reader);
            if (memory.Position!=memory.Length || operation==Guid.Empty || vehicle==Guid.Empty || string.IsNullOrWhiteSpace(owner)
                || revision<0 || !Enum.IsDefined(typeof(Phase),phase)) throw new InvalidDataException("Malformed transfer journal.");
            return new Record(operation,vehicle,owner,revision,phase,before,after);
        }
    }
    private static byte[] Frame(BinaryReader reader)
    { int length=reader.ReadInt32(); if(length<=0 || length>MaximumFrame || length>reader.BaseStream.Length-reader.BaseStream.Position)
        throw new InvalidDataException("Incomplete journal frame."); return reader.ReadBytes(length); }
    private static bool Equal(byte[] left, byte[] right)
    { if(left.Length!=right.Length)return false; for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false; return true; }
}
