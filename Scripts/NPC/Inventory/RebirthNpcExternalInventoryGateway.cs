using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

#nullable disable

public enum RebirthNpcExternalTransferDirection : byte
{
    ExternalToNpc = 0,
    NpcToExternal = 1
}

public enum RebirthNpcExternalTransferResult : byte
{
    Applied = 0,
    Replayed = 1,
    InvalidRequest = 2,
    AuthorityDenied = 3,
    NpcNotFound = 4,
    CapabilityDenied = 5,
    RevisionConflict = 6,
    InsufficientQuantity = 7,
    ReservationConflict = 8,
    ExternalRejected = 9,
    ExternalCommitFailed = 10,
    RollbackFailed = 11,
    Indeterminate = 12
}

public sealed class RebirthNpcExternalTransferRequest
{
    public Guid TransactionId { get; }
    public RebirthNpcStableId NpcId { get; }
    public uint ExpectedNpcRevision { get; }
    public string EndpointId { get; }
    public string ItemKey { get; }
    public int Quantity { get; }
    public RebirthNpcExternalTransferDirection Direction { get; }
    public string AuthorityKey { get; }

    public RebirthNpcExternalTransferRequest(Guid transactionId, RebirthNpcStableId npcId,
        uint expectedNpcRevision, string endpointId, string itemKey, int quantity,
        RebirthNpcExternalTransferDirection direction, string authorityKey)
    {
        TransactionId = transactionId;
        NpcId = npcId;
        ExpectedNpcRevision = expectedNpcRevision;
        EndpointId = (endpointId ?? string.Empty).Trim();
        ItemKey = (itemKey ?? string.Empty).Trim();
        Quantity = quantity;
        Direction = direction;
        AuthorityKey = (authorityKey ?? string.Empty).Trim();
    }
}

public interface IRebirthNpcExternalInventoryReservation : IDisposable
{
    string EndpointId { get; }
    Guid TransactionId { get; }
    bool Commit(out string error);
    bool Rollback(out string error);
}

/// <summary>
/// Optional outcome surface for reservations whose native commit can fail after a partial
/// mutation. A false boolean alone is not enough to decide whether the opposite domain may
/// be compensated safely.
/// </summary>
public interface IRebirthNpcExternalInventoryReservationOutcome
{
    RebirthTransactionState State { get; }
    string Detail { get; }
}

public interface IRebirthNpcExternalInventoryEndpoint
{
    string EndpointId { get; }

    bool TryReserveDebit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error);

    bool TryReserveCredit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error);
}

public static class RebirthNpcExternalInventoryEndpointRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, IRebirthNpcExternalInventoryEndpoint> Endpoints =
        new Dictionary<string, IRebirthNpcExternalInventoryEndpoint>(StringComparer.OrdinalIgnoreCase);

    public static void Register(IRebirthNpcExternalInventoryEndpoint endpoint)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
        string endpointId = (endpoint.EndpointId ?? string.Empty).Trim();
        if (endpointId.Length == 0) throw new ArgumentException("External endpoint ID is required.", nameof(endpoint));
        lock (Sync) Endpoints[endpointId] = endpoint;
    }

    public static bool Unregister(string endpointId)
    {
        endpointId = (endpointId ?? string.Empty).Trim();
        if (endpointId.Length == 0) return false;
        lock (Sync) return Endpoints.Remove(endpointId);
    }

    public static bool TryResolve(string endpointId, out IRebirthNpcExternalInventoryEndpoint endpoint)
    {
        endpointId = (endpointId ?? string.Empty).Trim();
        lock (Sync) return Endpoints.TryGetValue(endpointId, out endpoint);
    }

    public static void Reset()
    {
        lock (Sync) Endpoints.Clear();
    }

    public static int Count
    {
        get { lock (Sync) return Endpoints.Count; }
    }

    public static int GetNegotiatedQuantity(string endpointId, string itemKey, int requested, bool debit)
    {
        if (requested <= 0) return 0;
        IRebirthNpcExternalInventoryEndpoint endpoint;
        if (!TryResolve(endpointId, out endpoint)) return 0;
        IRebirthNpcExternalInventoryQuantityEndpoint quantityEndpoint = endpoint as IRebirthNpcExternalInventoryQuantityEndpoint;
        if (quantityEndpoint == null) return requested;
        int available = debit ? quantityEndpoint.GetAvailableDebitQuantity(itemKey) :
            quantityEndpoint.GetAvailableCreditQuantity(itemKey);
        return Math.Max(0, Math.Min(requested, available));
    }
}

public static class RebirthNpcExternalInventoryTransferCoordinator
{
    private sealed class ReplayRecord
    {
        public RebirthNpcExternalTransferResult Result;
        public uint Revision;
        public string Fingerprint;
    }

    private const int MaxReplayRecords = 1024;
    public const string JournalFileName = "RebirthNpcExternalTransactions.xml";
    private static readonly object Sync = new object();
    private static string loadedJournalDirectory = string.Empty;
    private static long journalGeneration;
    private static readonly Dictionary<Guid, ReplayRecord> Replay = new Dictionary<Guid, ReplayRecord>();
    private static readonly Queue<Guid> ReplayOrder = new Queue<Guid>();
    private static readonly HashSet<Guid> InFlight = new HashSet<Guid>();
    private static long applied;
    private static long replayed;
    private static long rejected;
    private static long rollbacks;
    private static long rollbackFailures;

    public static RebirthNpcExternalTransferResult Apply(RebirthNpcExternalTransferRequest request,
        out uint resultingNpcRevision, out string error)
    {
        resultingNpcRevision = 0;
        error = string.Empty;
        if (!Validate(request, out error))
            return Reject(RebirthNpcExternalTransferResult.InvalidRequest);

        string fingerprint = Fingerprint(request);
        EnsureReplayLoaded();
        string requestDirectory = GameIO.GetSaveGameDir() ?? string.Empty;
        long requestGeneration;
        try
        {
            lock (Sync)
            {
                requestGeneration = journalGeneration;
                AssertReplayAdmission(requestDirectory, requestGeneration);
            }
        }
        catch (Exception ex)
        {
            error = "NPC transfer history is unavailable: " + ex.Message;
            return Reject(RebirthNpcExternalTransferResult.Indeterminate);
        }

        if (!RebirthNpcInventoryAuthorityService.Validate(request.AuthorityKey,
            RebirthNpcInventoryAuthorityOperations.Transfer, request.NpcId))
        {
            error = "Inventory authority lease does not authorize this NPC transfer.";
            return Reject(RebirthNpcExternalTransferResult.AuthorityDenied);
        }

        lock (Sync)
        {
            // Same-ID calls are serialized. A concurrent retry waits for the first
            // operation to publish its terminal/indeterminate outcome instead of
            // independently reserving and mutating the same two domains.
            try { WaitForReplayAdmission(request.TransactionId, requestDirectory, requestGeneration); }
            catch (Exception ex)
            {
                error = "NPC transfer history changed while waiting: " + ex.Message;
                return Reject(RebirthNpcExternalTransferResult.Indeterminate);
            }

            ReplayRecord replay;
            if (Replay.TryGetValue(request.TransactionId, out replay))
            {
                if (!string.Equals(replay.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    error = "Transaction id was already used for a different transfer payload.";
                    return Reject(RebirthNpcExternalTransferResult.InvalidRequest);
                }
                resultingNpcRevision = replay.Revision;
                Interlocked.Increment(ref replayed);
                return replay.Result;
            }
            InFlight.Add(request.TransactionId);
        }

        IRebirthNpcExternalInventoryEndpoint endpoint;
        if (!RebirthNpcExternalInventoryEndpointRegistry.TryResolve(request.EndpointId, out endpoint))
        {
            error = "External inventory endpoint is not registered: " + request.EndpointId;
            ReleaseInFlight(request.TransactionId, requestGeneration);
            return Reject(RebirthNpcExternalTransferResult.ExternalRejected);
        }

        IRebirthNpcExternalInventoryReservation reservation = null;
        try
        {
            bool reserved;
            if (request.Direction == RebirthNpcExternalTransferDirection.ExternalToNpc)
                reserved = endpoint.TryReserveDebit(request.TransactionId, request.ItemKey,
                    request.Quantity, out reservation, out error);
            else
                reserved = endpoint.TryReserveCredit(request.TransactionId, request.ItemKey,
                    request.Quantity, out reservation, out error);

            if (!reserved || reservation == null)
                return Reject(RebirthNpcExternalTransferResult.ExternalRejected);

            // Bind the prepared reservation to a still-valid server authority lease.
            // Reservation preparation is not permission to commit after revocation.
            if (!RebirthNpcInventoryAuthorityService.Validate(request.AuthorityKey,
                RebirthNpcInventoryAuthorityOperations.Transfer, request.NpcId))
            {
                error = "Inventory authority lease expired while preparing the external reservation.";
                return FinishRejectedReservation(reservation, request, RebirthNpcExternalTransferResult.AuthorityDenied,
                    resultingNpcRevision, fingerprint, requestDirectory, requestGeneration, ref error);
            }

            RebirthNpcInventoryMutation mutation = new RebirthNpcInventoryMutation(
                request.ItemKey,
                request.Direction == RebirthNpcExternalTransferDirection.ExternalToNpc
                    ? request.Quantity : -request.Quantity);
            RebirthNpcInventoryTransaction transaction = new RebirthNpcInventoryTransaction(
                request.TransactionId, request.NpcId, request.ExpectedNpcRevision,
                request.AuthorityKey, new[] { mutation });

            RebirthNpcInventoryTransactionResult npcResult =
                RebirthNpcInventoryTransactionService.Apply(transaction, out resultingNpcRevision);
            RebirthNpcExternalTransferResult mapped = Map(npcResult);
            if (npcResult != RebirthNpcInventoryTransactionResult.Applied &&
                npcResult != RebirthNpcInventoryTransactionResult.Replayed)
            {
                return FinishRejectedReservation(reservation, request, mapped,
                    resultingNpcRevision, fingerprint, requestDirectory, requestGeneration, ref error);
            }

            if (npcResult == RebirthNpcInventoryTransactionResult.Replayed)
            {
                // The NPC-side replay journal outlived this coordinator's external outcome.
                // We cannot know whether the external leg committed, so never apply or refund it.
                error = "NPC transaction was already committed but the external outcome is no longer known.";
                RollbackReservation(reservation, ref error);
                Remember(request.TransactionId, RebirthNpcExternalTransferResult.Indeterminate, resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
                return RebirthNpcExternalTransferResult.Indeterminate;
            }

            if (!RebirthNpcInventoryAuthorityService.Validate(request.AuthorityKey,
                RebirthNpcInventoryAuthorityOperations.Transfer, request.NpcId))
            {
                error = "Inventory authority lease expired before external commit.";
                uint authorityRollbackRevision;
                Guid authorityRollbackId = DeriveRollbackId(request.TransactionId);
                RebirthNpcInventoryTransactionResult authorityRollbackResult;
                using (RebirthNpcInventoryAuthorityLease rollbackAuthority =
                    RebirthNpcInventoryAuthorityService.Issue("external-transfer.rollback",
                        RebirthNpcInventoryAuthorityOperations.Mutate, TimeSpan.FromSeconds(5), request.NpcId))
                {
                    RebirthNpcInventoryTransaction authorityRollback = new RebirthNpcInventoryTransaction(
                        authorityRollbackId, request.NpcId, resultingNpcRevision, rollbackAuthority.AuthorityKey,
                        new[] { new RebirthNpcInventoryMutation(request.ItemKey, -mutation.QuantityDelta) });
                    authorityRollbackResult =
                        RebirthNpcInventoryTransactionService.Apply(authorityRollback, out authorityRollbackRevision);
                }
                if (authorityRollbackResult == RebirthNpcInventoryTransactionResult.Applied ||
                    authorityRollbackResult == RebirthNpcInventoryTransactionResult.Replayed)
                {
                    resultingNpcRevision = authorityRollbackRevision;
                    Interlocked.Increment(ref rollbacks);
                    return FinishRejectedReservation(reservation, request, RebirthNpcExternalTransferResult.AuthorityDenied,
                        resultingNpcRevision, fingerprint, requestDirectory, requestGeneration, ref error);
                }
                Interlocked.Increment(ref rollbackFailures);
                error += "; NPC rollback after authority revocation failed with " + authorityRollbackResult + ".";
                Remember(request.TransactionId, RebirthNpcExternalTransferResult.RollbackFailed,
                    resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
                return RebirthNpcExternalTransferResult.RollbackFailed;
            }

            string commitError;
            if (!reservation.Commit(out commitError))
            {
                error = "External commit failed: " + commitError;
                IRebirthNpcExternalInventoryReservationOutcome reservationOutcome =
                    reservation as IRebirthNpcExternalInventoryReservationOutcome;
                if (reservationOutcome != null && reservationOutcome.State == RebirthTransactionState.Indeterminate)
                {
                    // The external endpoint may already have committed. Rolling the NPC side
                    // back here could duplicate the item. Retain the unresolved receipt and
                    // require endpoint recovery/reconciliation instead.
                    Remember(request.TransactionId, RebirthNpcExternalTransferResult.Indeterminate,
                        resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
                    return RebirthNpcExternalTransferResult.Indeterminate;
                }
                if (npcResult == RebirthNpcInventoryTransactionResult.Applied)
                {
                    uint rollbackRevision;
                    Guid rollbackId = DeriveRollbackId(request.TransactionId);
                    RebirthNpcInventoryTransactionResult rollbackResult;
                    using (RebirthNpcInventoryAuthorityLease rollbackAuthority =
                        RebirthNpcInventoryAuthorityService.Issue("external-transfer.rollback",
                            RebirthNpcInventoryAuthorityOperations.Mutate, TimeSpan.FromSeconds(5), request.NpcId))
                    {
                        RebirthNpcInventoryTransaction rollback = new RebirthNpcInventoryTransaction(
                            rollbackId, request.NpcId, resultingNpcRevision, rollbackAuthority.AuthorityKey,
                            new[] { new RebirthNpcInventoryMutation(request.ItemKey, -mutation.QuantityDelta) });
                        rollbackResult =
                            RebirthNpcInventoryTransactionService.Apply(rollback, out rollbackRevision);
                    }
                    if (rollbackResult == RebirthNpcInventoryTransactionResult.Applied ||
                        rollbackResult == RebirthNpcInventoryTransactionResult.Replayed)
                    {
                        resultingNpcRevision = rollbackRevision;
                        Interlocked.Increment(ref rollbacks);
                    }
                    else
                    {
                        Interlocked.Increment(ref rollbackFailures);
                        error += "; NPC rollback failed with " + rollbackResult + ".";
                        Remember(request.TransactionId, RebirthNpcExternalTransferResult.RollbackFailed,
                            resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
                        return RebirthNpcExternalTransferResult.RollbackFailed;
                    }
                }
                RebirthNpcExternalTransferResult failure = RollbackReservation(reservation, ref error)
                    ? RebirthNpcExternalTransferResult.ExternalCommitFailed : RebirthNpcExternalTransferResult.RollbackFailed;
                Remember(request.TransactionId, failure, resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
                return Reject(failure);
            }

            RebirthNpcExternalTransferResult finalResult = RebirthNpcExternalTransferResult.Applied;
            Remember(request.TransactionId, finalResult, resultingNpcRevision, fingerprint, requestDirectory, requestGeneration);
            if (finalResult == RebirthNpcExternalTransferResult.Applied)
                Interlocked.Increment(ref applied);
            else
                Interlocked.Increment(ref replayed);
            return finalResult;
        }
        finally
        {
            ReleaseReservation(reservation, request.TransactionId, requestGeneration);
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            Replay.Clear();
            ReplayOrder.Clear();
            InFlight.Clear();
            loadedJournalDirectory = string.Empty;
            unchecked { journalGeneration++; }
            Monitor.PulseAll(Sync);
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            return "[REBIRTH NPC External Inventory] endpoints=" +
                RebirthNpcExternalInventoryEndpointRegistry.Count +
                " replayRecords=" + Replay.Count +
                " inFlight=" + InFlight.Count +
                " applied=" + Interlocked.Read(ref applied) +
                " replayed=" + Interlocked.Read(ref replayed) +
                " rejected=" + Interlocked.Read(ref rejected) +
                " rollbacks=" + Interlocked.Read(ref rollbacks) +
                " rollbackFailures=" + Interlocked.Read(ref rollbackFailures);
        }
    }

    private static bool Validate(RebirthNpcExternalTransferRequest request, out string error)
    {
        error = string.Empty;
        if (request == null || request.TransactionId == Guid.Empty || request.NpcId.IsEmpty ||
            request.EndpointId.Length == 0 || request.ItemKey.Length == 0 || request.Quantity <= 0 ||
            request.AuthorityKey.Length == 0 ||
            !Enum.IsDefined(typeof(RebirthNpcExternalTransferDirection), request.Direction))
        {
            error = "External inventory transfer request is incomplete.";
            return false;
        }
        return true;
    }

    private static RebirthNpcExternalTransferResult Map(RebirthNpcInventoryTransactionResult result)
    {
        switch (result)
        {
            case RebirthNpcInventoryTransactionResult.Applied: return RebirthNpcExternalTransferResult.Applied;
            case RebirthNpcInventoryTransactionResult.Replayed: return RebirthNpcExternalTransferResult.Replayed;
            case RebirthNpcInventoryTransactionResult.CapabilityDenied: return RebirthNpcExternalTransferResult.CapabilityDenied;
            case RebirthNpcInventoryTransactionResult.RevisionConflict: return RebirthNpcExternalTransferResult.RevisionConflict;
            case RebirthNpcInventoryTransactionResult.InsufficientQuantity: return RebirthNpcExternalTransferResult.InsufficientQuantity;
            case RebirthNpcInventoryTransactionResult.ReservationConflict: return RebirthNpcExternalTransferResult.ReservationConflict;
            case RebirthNpcInventoryTransactionResult.NotFound: return RebirthNpcExternalTransferResult.NpcNotFound;
            case RebirthNpcInventoryTransactionResult.AuthorityDenied: return RebirthNpcExternalTransferResult.AuthorityDenied;
            default: return RebirthNpcExternalTransferResult.InvalidRequest;
        }
    }

    private static void Remember(Guid transactionId, RebirthNpcExternalTransferResult result, uint revision, string fingerprint, string expectedDirectory, long expectedGeneration)
    {
        bool changed = false;
        lock (Sync)
        {
            AssertReplayAdmission(expectedDirectory, expectedGeneration);
            if (!Replay.ContainsKey(transactionId))
            {
                Replay[transactionId] = new ReplayRecord { Result = result, Revision = revision, Fingerprint = fingerprint ?? string.Empty };
                ReplayOrder.Enqueue(transactionId);
                while (ReplayOrder.Count > MaxReplayRecords)
                    Replay.Remove(ReplayOrder.Dequeue());
                changed = true;
            }
            // Keep reset from interleaving publication and persistence.
            if (changed) TryPersistReplayJournal(expectedDirectory);
        }
    }

    public static void SaveCheckpoint()
    {
        lock (Sync)
        {
            EnsureReplayLoaded();
            string directory = loadedJournalDirectory;
            AssertReplayAdmission(directory, journalGeneration);
            TryPersistReplayJournal(directory);
        }
    }

    // Caller holds Sync. Revalidate after every wake before waiting on a reused ID.
    private static void WaitForReplayAdmission(Guid transactionId, string expectedDirectory, long expectedGeneration)
    {
        while (true)
        {
            AssertReplayAdmission(expectedDirectory, expectedGeneration);
            if (!InFlight.Contains(transactionId)) return;
            Monitor.Wait(Sync);
        }
    }

    private static void AssertReplayAdmission(string expectedDirectory, long expectedGeneration)
    {
        string currentDirectory = GameIO.GetSaveGameDir() ?? string.Empty;
        if (expectedGeneration != journalGeneration || string.IsNullOrEmpty(expectedDirectory) ||
            !string.Equals(expectedDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(expectedDirectory, loadedJournalDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Save directory changed or journal is not loaded.");
        RebirthNpcPersistenceFile.AssertWritable(Path.Combine(expectedDirectory, JournalFileName));
    }

    private static bool ValidateReplayDocument(XmlDocument document)
    {
        XmlElement root = document?.DocumentElement;
        if (root == null || root.Name != "rebirthNpcExternalTransactions" || root.GetAttribute("format") != "1") return false;
        var seen = new HashSet<Guid>();
        foreach (XmlNode node in root.ChildNodes)
        {
            XmlElement entry = node as XmlElement;
            if (entry == null) continue;
            Guid id; byte result; uint revision;
            if (entry.Name != "transaction" || !Guid.TryParse(entry.GetAttribute("id"), out id) || id == Guid.Empty || !seen.Add(id) ||
                !byte.TryParse(entry.GetAttribute("result"), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ||
                !Enum.IsDefined(typeof(RebirthNpcExternalTransferResult), result) ||
                !uint.TryParse(entry.GetAttribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision) ||
                string.IsNullOrWhiteSpace(entry.GetAttribute("fingerprint"))) return false;
        }
        return true;
    }

    private static XmlDocument ReadReplayDocument(string path)
    {
        if (!File.Exists(path))
        {
            if (File.Exists(path + ".tmp") || !RebirthNpcPersistenceFile.CanInitializeEmpty(path))
                throw new InvalidDataException("Transaction history requires recovery; an older backup cannot prove the latest transfers.");
            return null;
        }
        var document = new XmlDocument();
        document.Load(path);
        if (!ValidateReplayDocument(document))
            throw new InvalidDataException("Invalid external transaction history; automatic backup rollback is unsafe.");
        RebirthNpcPersistenceFile.VerifiedRead(path);
        return document;
    }

    private static void EnsureReplayLoaded()
    {
        string directory = GameIO.GetSaveGameDir() ?? string.Empty;
        if (directory.Length == 0) return;
        lock (Sync)
        {
            if (string.Equals(loadedJournalDirectory, directory, StringComparison.OrdinalIgnoreCase)) return;
            Replay.Clear();
            ReplayOrder.Clear();
            InFlight.Clear();
            loadedJournalDirectory = directory;
            unchecked { journalGeneration++; }
            Monitor.PulseAll(Sync);
            string path = Path.Combine(directory, JournalFileName);
            try
            {
                XmlDocument document = ReadReplayDocument(path);
                if (document == null) return;
                XmlElement root = document.DocumentElement;
                int format;
                if (root == null || root.Name != "rebirthNpcExternalTransactions" ||
                    !int.TryParse(root.GetAttribute("format"), NumberStyles.Integer, CultureInfo.InvariantCulture, out format) ||
                    format != 1)
                    throw new InvalidDataException("unsupported external transaction journal format");

                foreach (XmlElement entry in root.SelectNodes("transaction"))
                {
                    Guid id;
                    byte resultValue;
                    uint revision;
                    if (!Guid.TryParse(entry.GetAttribute("id"), out id) || id == Guid.Empty ||
                        !byte.TryParse(entry.GetAttribute("result"), NumberStyles.Integer, CultureInfo.InvariantCulture, out resultValue) ||
                        !Enum.IsDefined(typeof(RebirthNpcExternalTransferResult), resultValue) ||
                        !uint.TryParse(entry.GetAttribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision))
                        continue;
                    string fingerprint = entry.GetAttribute("fingerprint") ?? string.Empty;
                    if (fingerprint.Length == 0 || Replay.ContainsKey(id)) continue;
                    Replay[id] = new ReplayRecord
                    {
                        Result = (RebirthNpcExternalTransferResult)resultValue,
                        Revision = revision,
                        Fingerprint = fingerprint
                    };
                    ReplayOrder.Enqueue(id);
                    while (ReplayOrder.Count > MaxReplayRecords)
                        Replay.Remove(ReplayOrder.Dequeue());
                }
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceFile.BlockWrite(path, ex.Message);
                Log.Warning("[REBIRTH NPC External Inventory] transaction journal load failed: " +
                    ex.GetType().Name + ": " + ex.Message);
                Replay.Clear();
                ReplayOrder.Clear();
            }
        }
    }

    private static void CommitJournalFile(string temp, string path)
    {
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    private static void TryPersistReplayJournal(string expectedDirectory)
    {
        string directory = expectedDirectory ?? string.Empty;
        if (directory.Length == 0) return;
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, JournalFileName);
            RebirthNpcPersistenceFile.AssertWritable(path);
            string temp = path + ".tmp";
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("rebirthNpcExternalTransactions");
            root.SetAttribute("format", "1");
            root.SetAttribute("writtenUtcTicks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            document.AppendChild(root);
            lock (Sync)
            {
                foreach (Guid id in ReplayOrder)
                {
                    ReplayRecord record;
                    if (!Replay.TryGetValue(id, out record) || record == null) continue;
                    XmlElement entry = document.CreateElement("transaction");
                    entry.SetAttribute("id", id.ToString("D"));
                    entry.SetAttribute("result", ((int)record.Result).ToString(CultureInfo.InvariantCulture));
                    entry.SetAttribute("revision", record.Revision.ToString(CultureInfo.InvariantCulture));
                    entry.SetAttribute("fingerprint", record.Fingerprint ?? string.Empty);
                    root.AppendChild(entry);
                }
            }
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                document.Save(stream);
                stream.Flush(true);
            }
            CommitJournalFile(temp, path);
        }
        catch (Exception ex)
        {
            RebirthNpcPersistenceFile.BlockWrite(Path.Combine(directory, JournalFileName), ex.Message);
            Log.Warning("[REBIRTH NPC External Inventory] transaction journal save failed; transfers blocked: " +
                ex.GetType().Name + ": " + ex.Message);
            if (RebirthNpcPersistenceCoordinator.IsCheckpointWrite) throw;
        }
    }

    private static string Fingerprint(RebirthNpcExternalTransferRequest request)
    {
        if (request == null) return string.Empty;
        return request.NpcId.ToString() + "|" + request.ExpectedNpcRevision + "|" +
            request.EndpointId + "|" + request.ItemKey + "|" + request.Quantity + "|" +
            (byte)request.Direction + "|" + request.AuthorityKey;
    }

    private static Guid DeriveRollbackId(Guid transactionId)
    {
        byte[] bytes = transactionId.ToByteArray();
        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= (byte)(0xA5 + i);
        return new Guid(bytes);
    }

    private static RebirthNpcExternalTransferResult FinishRejectedReservation(
        IRebirthNpcExternalInventoryReservation reservation, RebirthNpcExternalTransferRequest request,
        RebirthNpcExternalTransferResult rejection, uint revision, string fingerprint,
        string directory, long generation, ref string error)
    {
        if (RollbackReservation(reservation, ref error)) return Reject(rejection);
        Remember(request.TransactionId, RebirthNpcExternalTransferResult.RollbackFailed,
            revision, fingerprint, directory, generation);
        return Reject(RebirthNpcExternalTransferResult.RollbackFailed);
    }

    private static bool RollbackReservation(IRebirthNpcExternalInventoryReservation reservation,
        ref string error)
    {
        if (reservation == null) return true;
        string rollbackError;
        try
        {
            if (reservation.Rollback(out rollbackError))
            {
                Interlocked.Increment(ref rollbacks);
                return true;
            }
        }
        catch (Exception ex) { rollbackError = ex.Message; }
        Interlocked.Increment(ref rollbackFailures);
        if (string.IsNullOrEmpty(rollbackError)) rollbackError = "External reservation rollback failed.";
        error = string.IsNullOrEmpty(error) ? rollbackError : error + "; " + rollbackError;
        return false;
    }

    private static void ReleaseReservation(IRebirthNpcExternalInventoryReservation reservation, Guid transactionId, long expectedGeneration)
    {
        try { if (reservation != null) reservation.Dispose(); }
        finally { ReleaseInFlight(transactionId, expectedGeneration); }
    }

    private static void ReleaseInFlight(Guid transactionId, long expectedGeneration)
    {
        lock (Sync)
        {
            if (expectedGeneration == journalGeneration && InFlight.Remove(transactionId))
                Monitor.PulseAll(Sync);
        }
    }

    private static RebirthNpcExternalTransferResult Reject(RebirthNpcExternalTransferResult result)
    {
        Interlocked.Increment(ref rejected);
        return result;
    }
}
