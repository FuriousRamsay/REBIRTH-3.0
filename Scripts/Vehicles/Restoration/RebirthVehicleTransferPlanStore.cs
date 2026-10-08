using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Immutable write-ahead plans. A separate coordinator owns receipts and completion;
// presence of a plan proves preparation only, never that an inventory leg ran.
public sealed class RebirthVehicleTransferPlanStore
{
    private static readonly object Sync = new object();
    private readonly string saveDirectory;
    private readonly string directory;
    private readonly Func<string> currentSaveDirectory;
    private readonly Func<bool> isCurrentSession;
    private const long MaximumFileBytes = 2097152;

    public RebirthVehicleTransferPlanStore(string saveDirectory, Func<string> currentSaveDirectory, Func<bool> isCurrentSession)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory)) throw new ArgumentException("Save directory is required.");
        this.currentSaveDirectory = currentSaveDirectory ?? throw new ArgumentNullException(nameof(currentSaveDirectory));
        this.isCurrentSession = isCurrentSession ?? throw new ArgumentNullException(nameof(isCurrentSession));
        this.saveDirectory = Path.GetFullPath(saveDirectory);
        directory = Path.Combine(this.saveDirectory, "RebirthVehicleTransfers");
    }

    public void Prepare(RebirthVehicleTransferPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        lock (Sync)
        {
            // All existing records retain custody until a separate durable completion
            // protocol proves release. Validate the entire catalogue even for retries.
            var catalogue = Discover();
            string path = PlanPath(plan.TransactionId);
            string xml = plan.ToXml().ToString(SaveOptions.DisableFormatting);
            if (File.Exists(path))
            {
                if (ReadFile(path).ToXml().ToString(SaveOptions.DisableFormatting) != xml)
                    throw new InvalidDataException("Vehicle transfer ID already belongs to a different plan.");
                AssertScope();
                return;
            }
            Guid assemblyId = plan.ReadBefore().AssemblyId;
            var incomingReceipts = new HashSet<Guid>(plan.Items.Select(i => i.ReceiptId));
            foreach (var existing in catalogue)
            {
                if (string.Equals(existing.ActorId, plan.ActorId, StringComparison.Ordinal)
                    || existing.ReadBefore().AssemblyId == assemblyId)
                    throw new InvalidDataException("An unresolved vehicle transfer retains actor or assembly custody.");
                if (incomingReceipts.Contains(existing.TransactionId)
                    || existing.Items.Any(i => i.ReceiptId == plan.TransactionId || incomingReceipts.Contains(i.ReceiptId)))
                    throw new InvalidDataException("Vehicle transfer identity is already in custody.");
            }
            AssertScope();
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            // An interrupted temporary file is retained for diagnosis. Never replace
            // it, infer rejection, or issue an owner offer after a write exception.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                new XDocument(plan.ToXml()).Save(stream, SaveOptions.DisableFormatting);
                if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Vehicle transfer file exceeds limit.");
                stream.Flush(true);
            }
            AssertScope();
            File.Move(temporary, path);
        }
    }

    public RebirthVehicleTransferPlan Read(Guid transactionId)
    {
        lock (Sync)
        {
            AssertAvailable();
            string path = PlanPath(transactionId);
            var plan = File.Exists(path) ? ReadFile(path) : null;
            AssertScope();
            return plan;
        }
    }

    // Load once when constructing the session recovery index, not on every tick.
    // Validate every file before exposing any plans; a partial catalogue could hide
    // custody and permit a second operation against the same actor or assembly.
    public IReadOnlyList<RebirthVehicleTransferPlan> Discover()
    {
        lock (Sync)
        {
            AssertAvailable();
            var plans = new List<RebirthVehicleTransferPlan>();
            var identities = new HashSet<Guid>();
            var receipts = new HashSet<Guid>();
            var actors = new HashSet<string>(StringComparer.Ordinal);
            var assemblies = new HashSet<Guid>();
            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*.xml").OrderBy(p => p, StringComparer.Ordinal))
                {
                    AssertScope();
                    var plan = ReadFile(path);
                    if (!identities.Add(plan.TransactionId) || receipts.Contains(plan.TransactionId)) throw new InvalidDataException("Duplicate vehicle transfer identity.");
                    if (!actors.Add(plan.ActorId) || !assemblies.Add(plan.ReadBefore().AssemblyId))
                        throw new InvalidDataException("Conflicting unresolved vehicle custody records.");
                    foreach (var item in plan.Items)
                        if (!receipts.Add(item.ReceiptId) || identities.Contains(item.ReceiptId)) throw new InvalidDataException("Vehicle item receipt belongs to multiple plans.");
                    plans.Add(plan);
                }
            }
            AssertScope();
            return plans.AsReadOnly();
        }
    }

    private string PlanPath(Guid transactionId)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction identity is required.");
        return Path.Combine(directory, transactionId.ToString("N") + ".xml");
    }

    private void AssertAvailable()
    {
        AssertScope();
        if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.tmp").Any())
            throw new InvalidDataException("Vehicle transfer preparation was interrupted; recovery is required.");
    }

    private void AssertScope()
    {
        string current = currentSaveDirectory();
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!isCurrentSession() || string.IsNullOrWhiteSpace(current) || !string.Equals(saveDirectory, Path.GetFullPath(current), comparison))
            throw new InvalidOperationException("Vehicle transfer store belongs to a different save session.");
    }

    private static RebirthVehicleTransferPlan ReadFile(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Vehicle transfer file exceeds limit.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaximumFileBytes };
            using (var reader = XmlReader.Create(stream, settings))
            {
                var plan = RebirthVehicleTransferPlan.Read(XDocument.Load(reader).Root);
                if (Path.GetFileNameWithoutExtension(path) != plan.TransactionId.ToString("N"))
                    throw new InvalidDataException("Vehicle transfer file identity mismatch.");
                return plan;
            }
        }
    }
}
