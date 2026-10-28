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
    internal string DirectoryPath=>root;
    internal static event Action<string> PublicationStarting;
    internal static event Action<string,Record,bool> PublicationFinished;
    public RebirthCamperTransferJournal(string serverOwnedDirectory):this(serverOwnedDirectory,true){}
    private RebirthCamperTransferJournal(string serverOwnedDirectory,bool createDirectory)
    {
        if (string.IsNullOrWhiteSpace(serverOwnedDirectory)) throw new ArgumentException("Server journal directory required.");
        root = Path.GetFullPath(serverOwnedDirectory);
        if(createDirectory)Directory.CreateDirectory(root);
        else if(!Directory.Exists(root))throw new DirectoryNotFoundException();
    }

    // Observation never initializes a missing journal directory. The private
    // read-only construction cannot escape as a writable journal instance.
    internal static Record ReadExisting(string directory,Guid operation)
        =>!Directory.Exists(directory)?null:new RebirthCamperTransferJournal(directory,false).Read(operation);

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
        AssertAvailableCore(vehicle,permittedOperation);
    }
    // Clearing the shared vehicle hold permits no outstanding operation,
    // including one introduced by a synchronous publication listener.
    internal static void AssertSettledExisting(string directory,Guid vehicle,bool requireDirectory)
    {
        if(vehicle==Guid.Empty)throw new ArgumentException("Vehicle identity required.");
        if(!Directory.Exists(directory))
        {if(requireDirectory)throw new DirectoryNotFoundException("Held vehicle journal unavailable.");return;}
        new RebirthCamperTransferJournal(directory,false).AssertAvailableCore(vehicle,Guid.Empty);
    }
    private void AssertAvailableCore(Guid vehicle,Guid permittedOperation)
    {
        if(RebirthCamperTransferHistoryScope.TryGet(this,out var index))
        {
            var pending=index.FindPending(vehicle);
            if(pending!=null&&pending.Operation!=permittedOperation)
                throw new InvalidOperationException("Vehicle has an unresolved transfer: "+pending.Operation.ToString("N"));
            return;
        }
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
            var current = ReadFile(path);
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
        if(RebirthCamperTransferHistoryScope.TryGet(this,out var index))return index.FindPending(vehicle);
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
    // Recovery index bootstrap only. Never called from each vehicle update.
    internal System.Collections.Generic.IEnumerable<Record> ReadForIndex()
    {
        foreach(string path in Directory.EnumerateFiles(root,"*.rcj*",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(path);
            if(name.Length!=38||name[32]!='.'||name[34]!='.'||name[33]<'0'||name[33]>'3'||
                !Guid.TryParseExact(name.Substring(0,32),"N",out var operation)||!File.Exists(FileName(operation,Phase.Prepared)))
                throw new InvalidDataException("Unresolved or unrecognized journal candidate.");
        }
        foreach(string path in Directory.EnumerateFiles(root,"*.0.rcj",SearchOption.TopDirectoryOnly))
        {
            var record=Read(Guid.ParseExact(Path.GetFileName(path).Substring(0,32),"N"));
            yield return record;
        }
    }

    // Only the pending index opts into scoped failures. Other journal consumers
    // retain strict all-record validation through the parameterless overload.
    internal System.Collections.Generic.IEnumerable<Record> ReadForIndex(
        System.Collections.Generic.IDictionary<Guid,Guid> knownVehicles,
        System.Collections.Generic.HashSet<string> unresolvedCandidates,Action<Guid> unavailable)
    {
        var groups=new System.Collections.Generic.Dictionary<Guid,System.Collections.Generic.List<string>>();
        foreach(string path in Directory.EnumerateFiles(root,"*.rcj*",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(path);
            Guid operation,temporary;
            bool final=name.Length==38;
            bool staging=name.Length==75&&name[38]=='.'&&name.EndsWith(".tmp",StringComparison.Ordinal)&&
                Guid.TryParseExact(name.Substring(39,32),"N",out temporary);
            if((!final&&!staging)||name[32]!='.'||name[34]!='.'||name.Substring(35,3)!="rcj"||
                name[33]<'0'||name[33]>'3'||!Guid.TryParseExact(name.Substring(0,32),"N",out operation))
                {unresolvedCandidates.Add(path);continue;}
            if(!groups.TryGetValue(operation,out var files))groups.Add(operation,files=new System.Collections.Generic.List<string>());
            files.Add(path);
        }
        // A malformed name may be an interrupted rename. Do not infer custody
        // from the name or forget it merely because it disappears on retry.
        if(unresolvedCandidates.Count>0)
        foreach(string path in new System.Collections.Generic.List<string>(unresolvedCandidates))
        {
            var record=ReadFile(path); // Missing/unreadable/invalid remains globally fenced.
            if(knownVehicles.TryGetValue(record.Operation,out var known)&&known!=Guid.Empty&&known!=record.Vehicle)
                throw new InvalidDataException("Journal candidate attribution conflicts; retain custody.");
            knownVehicles[record.Operation]=record.Vehicle;
            unavailable(record.Vehicle); // Valid bytes still do not prove canonical publication.
            unresolvedCandidates.Remove(path);
        }
        foreach(var pair in groups)
        {
            bool damaged=false;
            foreach(string path in pair.Value)if(Path.GetFileName(path).Length!=38){damaged=true;break;}
            // Healthy bootstrap reads each history once. Attribution recovery is
            // additional work only for a damaged or unpublished history.
            Record latest=null;
            if(!damaged)
            {
                try{latest=Read(pair.Key);}
                catch(InvalidDataException){damaged=true;}
                catch(IOException){damaged=true;}
                catch(UnauthorizedAccessException){damaged=true;}
            }
            if(!damaged&&latest!=null){yield return latest;continue;}
            Guid vehicle;
            knownVehicles.TryGetValue(pair.Key,out vehicle);
            foreach(string path in pair.Value)
            {
                Record record;
                try{record=ReadFile(path);}
                catch(InvalidDataException){continue;}
                catch(IOException){continue;}
                catch(UnauthorizedAccessException){continue;}
                string name=Path.GetFileName(path);
                if(record.Operation!=pair.Key||(int)record.State!=name[33]-'0'||vehicle!=Guid.Empty&&record.Vehicle!=vehicle)
                    throw new InvalidDataException("Journal attribution conflicts; retain custody.");
                vehicle=record.Vehicle;
            }
            if(vehicle==Guid.Empty)
            {knownVehicles[pair.Key]=Guid.Empty;throw new InvalidDataException("Journal damage has no validated vehicle identity; retain custody.");}
            knownVehicles[pair.Key]=vehicle;
            // A candidate never proves publication, even when its checksum is valid.
            unavailable(vehicle);
        }
        // Removing a damaged file is not evidence that its inventory transaction
        // settled. Keep a previously identified vehicle fenced until restored.
        foreach(var pair in knownVehicles)
            if(!groups.ContainsKey(pair.Key))
            {
                if(pair.Value==Guid.Empty)throw new InvalidDataException("Unattributed journal history was removed; retain custody.");
                unavailable(pair.Value);
            }
    }
    private string FileName(Guid operation, Phase state) { return Path.Combine(root, operation.ToString("N") + "." + (int)state + ".rcj"); }
    private static Record ReadFile(string path)
    {
        // Bound corrupt files before allocation, including during index bootstrap.
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long length=file.Length;
            if(length<32||length>MaximumFrame*2L+8192)throw new InvalidDataException("Invalid journal size.");
            var bytes=new byte[(int)length];int offset=0;
            while(offset<bytes.Length)
            {int count=file.Read(bytes,offset,bytes.Length-offset);if(count<=0)throw new EndOfStreamException("Incomplete journal receipt.");offset+=count;}
            if(file.Length!=length||file.ReadByte()!=-1)throw new InvalidDataException("Journal changed during read.");
            return Decode(bytes);
        }
    }
    // Installation recovery may correlate a retained portable carrier with an
    // already-applied removal. Enumeration never repeats or advances a transfer.
    internal System.Collections.Generic.IEnumerable<Record> FindApplied(Guid vehicle, string ownerCreation)
    {
        if(vehicle==Guid.Empty||string.IsNullOrWhiteSpace(ownerCreation))
            throw new ArgumentException("Exact source vehicle and owner required.");
        if(RebirthCamperTransferHistoryScope.TryGet(this,out var index))
        {foreach(var record in index.FindApplied(vehicle,ownerCreation))yield return record;yield break;}
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
    internal System.Collections.Generic.IEnumerable<Record> FindRejected(Guid vehicle,string ownerCreation)
    {
        if(vehicle==Guid.Empty||string.IsNullOrWhiteSpace(ownerCreation))throw new ArgumentException("Exact source and owner required.");
        if(RebirthCamperTransferHistoryScope.TryGet(this,out var index))
        {foreach(var record in index.FindRejected(vehicle,ownerCreation))yield return record;yield break;}
        foreach(string path in Directory.EnumerateFiles(root,"*.3.rcj",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(path);
            if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var operation))throw new InvalidDataException("Unrecognized rejected receipt.");
            var record=Read(operation);
            if(record!=null&&record.State==Phase.Rejected&&record.Vehicle==vehicle&&record.OwnerCreation==ownerCreation)yield return record;
        }
    }
    private void Publish(Record record)
    {
        bool published=false;
        try
        {
        PublicationStarting?.Invoke(root);
        string destination = FileName(record.Operation, record.State);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[] bytes = Encode(record, record.State);
        // Never overwrite a receipt. A leftover temporary file is evidence only,
        // never authority to replay a partially published inventory operation.
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        File.Move(temporary, destination);
        published=true;
        }
        finally{PublicationFinished?.Invoke(root,record,published);}
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
