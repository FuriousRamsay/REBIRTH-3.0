using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Security.Cryptography;

// Receipts for legitimate writes AFTER a recovery generation has been installed.
// This is not recovery admission and never installs a backup/runtime candidate.
// A cold coordinator must resolve this chain before preparing the latest primary.
internal static class RebirthNpcRecoveryPublications
{
    private const long MaximumDocument = 64L * 1024 * 1024;
    private const int MaximumManifest = 16384;
    private static readonly object Sync = new object();
    private sealed class Installed
    {
        internal World World;internal string Directory,IntentHash;internal Guid Operation;internal ulong Generation;
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,Installed> Active=new System.Collections.Concurrent.ConcurrentDictionary<string,Installed>(StringComparer.OrdinalIgnoreCase);
    internal sealed class Receipt
    {
        internal string Path, Scope, Hash, PendingHash;
        internal Guid Operation;
        internal Guid PendingId;
        internal ulong Generation, Sequence;
        internal bool Pending;
    }
    private static string Manifest(string path) => Path.GetFullPath(path) + ".recovery-publications.xml";
    internal static object CaptureAuditState(string path)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return new {available=false};
            lock(Sync)
            {
                var receipt=Read(path);string current=Hash(path);
                return new {available=true,operation=receipt.Operation.ToString("N"),generation=receipt.Generation,sequence=receipt.Sequence,
                    pending=receipt.Pending,hash=receipt.Hash,pendingHash=receipt.PendingHash,currentHash=current,currentMatchesSaved=current==receipt.Hash,
                    currentMatchesPending=receipt.Pending&&current==receipt.PendingHash,installed=IsInstalled(path)};
            }
        }
        catch(Exception ex){return new {available=false,error=ex.GetType().Name};}
    }    internal static string[] CheckpointReceiptNames()
    {
        var names=new System.Collections.Generic.List<string>();
        foreach(var entry in Active)if(IsInstalled(entry.Key))names.Add(Path.GetFileName(Manifest(entry.Key)));
        names.Sort(StringComparer.Ordinal);return names.ToArray();
    }
    internal static bool IsInstalled(string path)
    {
        path=Path.GetFullPath(path);
        if(!Active.TryGetValue(path,out var installed))return false;
        try
        {
            return ReferenceEquals(installed.World,GameManager.Instance?.World)&&installed.Directory==GameIO.GetSaveGameDir()&&
                RebirthNpcRecoveryGeneration.RequireCurrent()==installed.Generation&&Hash(RebirthNpcPersistenceFile.RecoveryIntentPath(path))==installed.IntentHash;
        }
        catch{return false;}
    }
    // The durable installed intent is published only after local typed runtime
    // install. Cold processes intentionally have no Active entry; coordinator
    // must validate/adopt the latest publication before registering again.
    internal static bool TryRegisterInstalled(string path,Guid operation,ulong generation,out string error)
    {
        error=string.Empty;path=Path.GetFullPath(path);
        lock(Sync)try
        {
            if(!RebirthNpcPersistenceFile.HasRecoveryLease(path,operation))throw new InvalidOperationException("Installed recovery registration requires original writer fence.");
            string intent=RebirthNpcPersistenceFile.RecoveryIntentPath(path);
            if(!File.Exists(intent)||new FileInfo(intent).Length>MaximumManifest)throw new InvalidDataException("Installed recovery intent unavailable.");
            XElement root;using(var reader=XmlReader.Create(intent,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumManifest}))root=XElement.Load(reader);
            var scope=RebirthNpcSaveScope.ObserveCurrent();
            if(root.Name!="npcLiveRecovery"||(int?)root.Attribute("version")!=1||(int?)root.Attribute("phase")!=2||
                (string)root.Attribute("file")!=Path.GetFileName(path)||(string)root.Attribute("scope")!=scope?.Fingerprint||
                (string)root.Attribute("operation")!=operation.ToString("N")||(string)root.Attribute("generation")!=generation.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Installed recovery intent identity differs.");
            if(!TryResolveLatest(path,operation,generation,out _,out _,out _,out error))return false;
            Active[path]=new Installed{World=GameManager.Instance.World,Directory=GameIO.GetSaveGameDir(),Operation=operation,Generation=generation,IntentHash=Hash(intent)};
            return true;
        }
        catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;return false;}
    }
    internal static bool TryPublishInstalled(string path,XmlDocument document,out bool handled,out string error)
    {
        path=Path.GetFullPath(path);handled=false;error=string.Empty;
        lock(Sync)
        {
            if(!Active.TryGetValue(path,out var installed))return true;
            // A replaced world keeps no installed authority. A retained intent
            // still fences its path through PersistenceFile.IsRecoveryFenced.
            if(!ReferenceEquals(installed.World,GameManager.Instance?.World)||installed.Directory!=GameIO.GetSaveGameDir())return true;
            handled=true;
            if(!IsInstalled(path)){error="installed recovery intent/generation changed";return false;}
            return TryPublish(path,installed.Operation,installed.Generation,document,out error);
        }
    }
    private static string Candidate(Receipt receipt) => receipt.Path + ".recovery-save." + receipt.Operation.ToString("N") + "." + checked(receipt.Sequence + 1).ToString(CultureInfo.InvariantCulture) + "." + receipt.PendingId.ToString("N") + ".xml";
    private static bool Digest(string value) => value != null && value.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z");
    private static string Hash(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length <= 0 || stream.Length > MaximumDocument) throw new InvalidDataException("NPC recovery publication size is invalid.");
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
    private static void RequireScope(Receipt receipt)
    {
        var scope = RebirthNpcSaveScope.ObserveCurrent();
        if (!ThreadManager.IsMainThread() || GameManager.Instance?.World?.IsRemote() != false || scope == null || scope.Fingerprint != receipt.Scope ||
            !string.Equals(Path.GetDirectoryName(receipt.Path), Path.GetFullPath(scope.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
            !RebirthNpcRecoveryGeneration.TryRead(out var generation, out _, out var error) || generation != receipt.Generation)
            throw new InvalidOperationException("NPC installed recovery publication scope/generation changed.");
    }
    private static void Write(Receipt receipt)
    {
        RequireScope(receipt);
        var root = new XElement("npcRecoveryPublications", new XAttribute("version", 1), new XAttribute("scope", receipt.Scope),
            new XAttribute("file", Path.GetFileName(receipt.Path)), new XAttribute("operation", receipt.Operation.ToString("N")),
            new XAttribute("generation", receipt.Generation.ToString(CultureInfo.InvariantCulture)), new XAttribute("sequence", receipt.Sequence.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("hash", receipt.Hash), new XAttribute("pending", receipt.Pending ? 1 : 0));
        if (receipt.Pending) root.Add(new XAttribute("pendingHash", receipt.PendingHash),new XAttribute("pendingId",receipt.PendingId.ToString("N")));
        string manifest=Manifest(receipt.Path),staged=manifest+".write."+Guid.NewGuid().ToString("N");
        using(var stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false}))
        {new XDocument(root).Save(writer);writer.Flush();if(stream.Length>MaximumManifest)throw new InvalidDataException("NPC recovery publication receipt oversized.");stream.Flush(true);}
        if (!RebirthDurableFileCommit.TryPublish(staged,manifest,out var error)) throw new IOException("NPC recovery publication receipt failed: " + error);
        var actual = Read(receipt.Path);
        if (actual.Operation != receipt.Operation || actual.Generation != receipt.Generation || actual.Sequence != receipt.Sequence || actual.Hash != receipt.Hash || actual.Pending != receipt.Pending || actual.PendingHash != receipt.PendingHash || actual.PendingId != receipt.PendingId)
            throw new InvalidDataException("NPC recovery publication receipt readback differs.");
    }
    private static Receipt Read(string path,string manifestOverride=null)
    {
        path = Path.GetFullPath(path);
        string manifest = manifestOverride??Manifest(path);
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > MaximumManifest) throw new InvalidDataException("NPC recovery publication primary receipt unavailable.");
        XElement root;
        using (var reader = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumManifest })) root = XElement.Load(reader);
        if (root.Name != "npcRecoveryPublications" || (int?)root.Attribute("version") != 1 || (string)root.Attribute("file") != Path.GetFileName(path) ||
            !Guid.TryParseExact((string)root.Attribute("operation"), "N", out var operation) || operation == Guid.Empty ||
            !ulong.TryParse((string)root.Attribute("generation"), NumberStyles.None, CultureInfo.InvariantCulture, out var generation) || generation == 0 ||
            !ulong.TryParse((string)root.Attribute("sequence"), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ||
            !Digest((string)root.Attribute("hash")) || ((string)root.Attribute("pending") != "0" && (string)root.Attribute("pending") != "1"))
            throw new InvalidDataException("NPC recovery publication receipt invalid.");
        var receipt = new Receipt { Path = path, Scope = (string)root.Attribute("scope"), Operation = operation, Generation = generation, Sequence = sequence, Hash = (string)root.Attribute("hash"), Pending = (string)root.Attribute("pending") == "1", PendingHash = (string)root.Attribute("pendingHash") };
        if(receipt.Pending)
        {if(!Guid.TryParseExact((string)root.Attribute("pendingId"),"N",out receipt.PendingId)||receipt.PendingId==Guid.Empty)throw new InvalidDataException("NPC recovery pending publication identity invalid.");}
        else if(root.Attribute("pendingId")!=null)throw new InvalidDataException("NPC recovery terminal publication has pending identity.");
        if (receipt.Pending ? !Digest(receipt.PendingHash) || sequence == ulong.MaxValue : receipt.PendingHash != null) throw new InvalidDataException("NPC recovery publication pending receipt invalid.");
        RequireScope(receipt);
        return receipt;
    }
    // The coordinator first saves phase3 with the latest receipt hash/sequence
    // AFTER required projections converge. Archive housekeeping cannot turn a
    // pending phase2/unknown primary into a terminal recovery or reset history.
    internal static bool TryRetireConverged(string path,Guid operation,ulong generation,out string error)
    {
        error=string.Empty;path=Path.GetFullPath(path);
        lock(Sync)try
        {
            if(!RebirthNpcPersistenceFile.HasRecoveryLease(path,operation))throw new InvalidOperationException("Recovery terminal retirement requires original writer fence.");
            string intent=RebirthNpcPersistenceFile.RecoveryIntentPath(path);
            if(!File.Exists(intent)||new FileInfo(intent).Length>MaximumManifest)throw new InvalidDataException("Converged recovery intent unavailable.");
            XElement root;using(var reader=XmlReader.Create(intent,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumManifest}))root=XElement.Load(reader);
            if(root.Name!="npcLiveRecovery"||(int?)root.Attribute("version")!=1||(int?)root.Attribute("phase")!=3||
                (string)root.Attribute("file")!=Path.GetFileName(path)||(string)root.Attribute("operation")!=operation.ToString("N")||
                (string)root.Attribute("generation")!=generation.ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("Converged recovery identity differs.");
            string manifest=Manifest(path),archive=manifest+".completed."+operation.ToString("N");
            var receipt=Read(path,File.Exists(manifest)?manifest:archive);
            if(receipt.Operation!=operation||receipt.Generation!=generation||receipt.Scope!=(string)root.Attribute("scope")||receipt.Pending||
                (string)root.Attribute("terminalHash")!=receipt.Hash||(string)root.Attribute("terminalSequence")!=receipt.Sequence.ToString(CultureInfo.InvariantCulture)||Hash(path)!=receipt.Hash)
                throw new InvalidDataException("Converged recovery latest publication differs.");
            if(File.Exists(manifest))
            {
                if(File.Exists(archive))throw new InvalidDataException("Conflicting completed recovery publication archive retained.");
                File.Move(manifest,archive);
            }
            if(File.Exists(manifest+".bak"))
            {
                if(File.Exists(archive+".bak"))throw new InvalidDataException("Conflicting completed recovery publication backup retained.");
                File.Move(manifest+".bak",archive+".bak");
            }
            // Move the fence last. A crash before this step is retried against
            // the positive archived receipt, never against the original backup.
            string completed=path+".recovery-completed."+operation.ToString("N")+".xml";
            if(File.Exists(completed))throw new InvalidDataException("Completed recovery intent destination already exists.");
            if(File.Exists(intent+".bak"))
            {
                if(File.Exists(completed+".bak"))throw new InvalidDataException("Completed recovery intent backup destination already exists.");
                File.Move(intent+".bak",completed+".bak");
            }
            File.Move(intent,completed);Active.TryRemove(path,out _);return true;
        }
        catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;return false;}
    }
    // Called only after the coordinator has durably published AND installed the
    // same generation. expectedPrimaryHash must come from that exact publication.
    internal static bool TryInitialize(string path, Guid operation, ulong generation, string expectedPrimaryHash, out string error)
    {
        error = string.Empty;
        lock (Sync) try
        {
            if (operation == Guid.Empty || generation == 0 || !Digest(expectedPrimaryHash)) throw new InvalidDataException("Invalid installed recovery identity.");
            if (!RebirthNpcPersistenceFile.HasRecoveryLease(path, operation)) throw new InvalidOperationException("Installed publication requires the original recovery lease.");
            var receipt = new Receipt { Path = Path.GetFullPath(path), Scope = RebirthNpcSaveScope.ObserveCurrent()?.Fingerprint, Operation = operation, Generation = generation, Hash = expectedPrimaryHash };
            RequireScope(receipt);
            if (File.Exists(Manifest(path)))
            {
                var previous = Read(path);
                if (previous.Operation != operation || previous.Generation != generation || previous.Pending || previous.Hash != expectedPrimaryHash || Hash(path) != expectedPrimaryHash)
                    throw new InvalidDataException("Existing recovery publications cannot be reset to a backup.");
                return true;
            }
            if (File.Exists(Manifest(path) + ".bak") || File.Exists(Manifest(path) + ".tmp")) throw new InvalidDataException("Interrupted publication receipt requires reconciliation.");
            if (Hash(path) != expectedPrimaryHash) throw new InvalidDataException("Installed recovery primary differs.");
            Write(receipt);
            return true;
        }
        catch (Exception exception) { error = exception.GetType().Name + ": " + exception.Message; return false; }
    }
    // The caller holds the store writer lease acquired BEFORE taking its runtime
    // snapshot. A prepared save receipt is durable before replacing the primary.
    internal static bool TryPublish(string path, Guid operation, ulong generation, XmlDocument document, out string error)
    {
        error = string.Empty;
        lock (Sync) try
        {
            if (document?.DocumentElement == null) throw new InvalidDataException("NPC recovery save candidate missing.");
            if (!RebirthNpcPersistenceFile.HasWriterLease(path)) throw new InvalidOperationException("NPC recovery save snapshot has no writer lease.");
            var receipt = Read(path);
            if (receipt.Operation != operation || receipt.Generation != generation || receipt.Pending || receipt.Sequence == ulong.MaxValue || Hash(path) != receipt.Hash)
                throw new InvalidDataException("NPC recovery writer receipt is not current.");
            receipt.PendingId=Guid.NewGuid();
            string staged = Candidate(receipt);
            // CreateNew is intentional: an interrupted candidate is evidence, not
            // a disposable temp and cannot be silently overwritten on retry.
            using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false, NewLineHandling = NewLineHandling.Entitize }))
            { document.Save(writer); writer.Flush(); if (stream.Length > MaximumDocument) throw new InvalidDataException("NPC recovery save candidate oversized."); stream.Flush(true); }
            ValidateXml(staged);
            receipt.PendingHash = Hash(staged); receipt.Pending = true;
            Write(receipt);
            RebirthNpcRecoveryFaults.PublicationCheckpoint(operation,path,"save-pending");
            RequireScope(receipt);
            if (Hash(path) != receipt.Hash) throw new InvalidDataException("NPC recovery primary changed before publication.");
            PublishExact(staged, receipt.Path, receipt.PendingHash);
            RebirthNpcRecoveryFaults.PublicationCheckpoint(operation,path,"save-primary");
            Finish(receipt);
            RebirthNpcRecoveryFaults.PublicationCheckpoint(operation,path,"save-finished");
            return true;
        }
        catch (Exception exception) { error = exception.GetType().Name + ": " + exception.Message; return false; }
    }
    private static void ValidateXml(string path)
    {
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumDocument }))
        { var doc = new XmlDocument { XmlResolver = null }; doc.Load(reader); if (doc.DocumentElement == null) throw new InvalidDataException("NPC recovery save XML missing root."); }
    }
    private static void PublishExact(string staged, string path, string digest)
    {
        string temporary = path + ".recovery-publish.tmp";
        if (File.Exists(temporary)) throw new InvalidDataException("Interrupted native recovery publication temp is retained.");
        using (var input = new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { input.CopyTo(output, 65536); output.Flush(true); }
        if (Hash(temporary) != digest) throw new InvalidDataException("NPC recovery publication copy differs.");
        // Unsupported replacement retains all evidence and the writer fence;
        // never delete a valid primary to emulate an atomic replacement.
        File.Replace(temporary, path, path + ".recovery-previous", true);
        if (Hash(path) != digest) throw new InvalidDataException("NPC recovery final readback differs.");
    }
    private static void Finish(Receipt receipt)
    {
        if (!receipt.Pending || Hash(receipt.Path) != receipt.PendingHash) throw new InvalidDataException("NPC recovery pending publication not saved.");
        receipt.Hash = receipt.PendingHash; receipt.PendingHash = null; receipt.PendingId=Guid.Empty; receipt.Pending = false; receipt.Sequence = checked(receipt.Sequence + 1);
        Write(receipt);
    }
    // Positive cold authority is the latest manifest plus the exact primary (or
    // its recorded pending save), never the original recovered backup. The
    // coordinator still performs semantic/custody validation and runtime install.
    internal static bool TryResolveLatest(string path, Guid operation, ulong generation, out XmlDocument latest, out ulong sequence, out string primaryHash, out string error)
    {
        latest = null; sequence = 0; primaryHash = null; error = string.Empty;
        lock (Sync) try
        {
            var receipt = Read(path);
            if (!RebirthNpcPersistenceFile.HasRecoveryLease(path, operation)) throw new InvalidOperationException("Cold publication reconciliation requires the original recovery lease.");
            if (receipt.Operation != operation || receipt.Generation != generation) throw new InvalidDataException("NPC cold recovery publication identity differs.");
            string actual = Hash(path);
            if (receipt.Pending)
            {
                string staged = Candidate(receipt);
                if (Hash(staged) != receipt.PendingHash) throw new InvalidDataException("NPC recovery pending save evidence differs.");
                ValidateXml(staged);
                if (actual == receipt.Hash)
                {
                    // No other value is allowed to be overwritten. Retry only
                    // the already recorded save, not its original gameplay action.
                    string temp = path + ".recovery-publish.tmp";
                    if (File.Exists(temp))
                    {
                        if (Hash(temp) != receipt.PendingHash) throw new InvalidDataException("NPC recovery pending temp differs.");
                        File.Replace(temp, path, path + ".recovery-previous", true);
                    }
                    else PublishExact(staged, path, receipt.PendingHash);
                }
                else if (actual != receipt.PendingHash) throw new InvalidDataException("NPC recovery primary has unknown publication history.");
                Finish(receipt);
            }
            if (Hash(path) != receipt.Hash) throw new InvalidDataException("NPC recovery latest primary differs.");
            ValidateXml(path);
            var candidate = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumDocument })) candidate.Load(reader);
            RequireScope(receipt);
            latest = candidate; sequence = receipt.Sequence; primaryHash = receipt.Hash;
            return true;
        }
        catch (Exception exception) { error = exception.GetType().Name + ": " + exception.Message; return false; }
    }
}
