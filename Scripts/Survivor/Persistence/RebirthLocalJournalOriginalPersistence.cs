using System;
using System.IO;
using System.Xml.Linq;

#nullable disable

// Generic exact-original DATA persistence only. No native setter, owner receipt or entitlement authority.
public static partial class RebirthWorldCharacterRepository
{
    private static object RepositoryEpoch = new object();
    private static readonly object LocalJournalIssuer = new object();

    private sealed class RepositoryWriteGate
    {
        internal readonly string Path;
        internal RepositoryWriteGate(string path) { Path = path; }
        internal object Generation = new object();
        internal bool Active;
        internal LocalJournalWriteWitness PendingJournal;
    }

    private sealed class RepositoryWriteSelection
    {
        internal string Key, Path;
        internal object Epoch, Generation;
        internal RepositoryWriteGate Gate;
        internal RebirthWorldCharacterRecord Original, Snapshot;
        internal RebirthWorldSupportState Support;
        internal XDocument Document;
    }

    internal enum LocalJournalWriteResult
    {
        DeniedBeforeWrite,
        PersistedVerified,
        UncertainAfterAttempt
    }

    internal enum LocalJournalAttemptDataInspection
    {
        ExactAttemptedImage,
        MissingFinal,
        DifferentFinal,
        InvalidFinal,
        Blocked,
        Faulted
    }

    // Issued only here, retains a private immutable XML image and exact live binding.
    // This proves DATA publication at a current check; never native execution.
    internal sealed class LocalJournalWriteWitness
    {
        private readonly RepositoryWriteSelection selected;
        private readonly RebirthStablePlayerIdentity identity;
        private readonly Func<bool> current;
        private readonly Func<RebirthWorldSupportState, bool> matches;
        private bool attempted;

        private LocalJournalWriteWitness(RepositoryWriteSelection selection, RebirthStablePlayerIdentity owner,
            Func<bool> isCurrent, Func<RebirthWorldSupportState, bool> comparator)
        { selected = selection; identity = owner; current = isCurrent; matches = comparator; }

        internal static LocalJournalWriteResult Commit(object issuer, RebirthStablePlayerIdentity owner,
            RebirthWorldCharacterRecord record, RebirthWorldSupportState support, Func<bool> isCurrent,
            Func<RebirthWorldSupportState, bool> comparator, string reason, out LocalJournalWriteWitness witness)
        {
            witness = null;
            if (!ReferenceEquals(issuer, LocalJournalIssuer) || owner == null || record == null || support == null ||
                isCurrent == null || comparator == null || !ReferenceEquals(record.Support, support)) return LocalJournalWriteResult.DeniedBeforeWrite;
            var gate = (RepositoryWriteGate)GetWriteLock(owner.StorageKey);
            lock (gate)
            {
                if (!TryEnterRepositoryOperation(gate)) return LocalJournalWriteResult.DeniedBeforeWrite;
                try
                {
                    RepositoryWriteSelection selection;
                    lock (Sync)
                    {
                        if (!serverAuthority || !Cache.TryGetValue(owner.StorageKey, out var cached) || !ReferenceEquals(cached, record))
                            return LocalJournalWriteResult.DeniedBeforeWrite;
                        selection = CaptureWriteSelectionLocked(owner.StorageKey, record);
                        if (!ReferenceEquals(selection.Gate, gate)) return LocalJournalWriteResult.DeniedBeforeWrite;
                    }
                    selection.Document = Serialize(selection.Snapshot);
                    var candidate = new LocalJournalWriteWitness(selection, owner, isCurrent, comparator);
                    if (!candidate.CheckLiveAndCallbacks()) return LocalJournalWriteResult.DeniedBeforeWrite;
                    // Serialization/comparators may call into code. Recheck before allocating attempted authority.
                    if (!IsSelectionCurrent(selection, true)) return LocalJournalWriteResult.DeniedBeforeWrite;
                    witness = candidate;
                    candidate.attempted = true;
                    gate.PendingJournal = candidate;
                    if (!TryWriteSelected(selection, out var error))
                    {
                        AddIssue(selection.Key, selection.Path, "local journal write uncertain (" + (reason ?? "unspecified") + "): " + error, false);
                        return LocalJournalWriteResult.UncertainAfterAttempt;
                    }
                    return candidate.VerifyUnderGate(true) ? LocalJournalWriteResult.PersistedVerified : LocalJournalWriteResult.UncertainAfterAttempt;
                }
                catch
                {
                    return witness != null && witness.attempted ? LocalJournalWriteResult.UncertainAfterAttempt : LocalJournalWriteResult.DeniedBeforeWrite;
                }
                finally { gate.Active = false; }
            }
        }

        internal bool Verify(object issuer)
        {
            if (!ReferenceEquals(issuer, LocalJournalIssuer) || !attempted) return false;
            lock (selected.Gate)
            {
                if (!TryEnterRepositoryOperation(selected.Gate, this)) return false;
                try { return VerifyUnderGate(false); }
                catch { return false; }
                finally { selected.Gate.Active = false; }
            }
        }

        internal LocalJournalWriteResult Retry(object issuer)
        {
            if (!ReferenceEquals(issuer, LocalJournalIssuer) || !attempted) return LocalJournalWriteResult.DeniedBeforeWrite;
            lock (selected.Gate)
            {
                if (!TryEnterRepositoryOperation(selected.Gate, this)) return LocalJournalWriteResult.UncertainAfterAttempt;
                try
                {
                    // Outcome covers the witness lifetime: refusing a NEW write never disproves its prior attempt.
                    // Reuses this receipt's exact private snapshot; cannot remint another record or journal image.
                    if (!CheckLiveAndCallbacks() || !IsSelectionCurrent(selected, true)) return LocalJournalWriteResult.UncertainAfterAttempt;
                    if (!TryWriteSelected(selected, out _)) return LocalJournalWriteResult.UncertainAfterAttempt;
                    return VerifyUnderGate(true) ? LocalJournalWriteResult.PersistedVerified : LocalJournalWriteResult.UncertainAfterAttempt;
                }
                catch { return LocalJournalWriteResult.UncertainAfterAttempt; }
                finally { selected.Gate.Active = false; }
            }
        }

        // Read-only stale-epoch DATA inspection. Does not call live scope/journal callbacks or resolve custody.
        internal LocalJournalAttemptDataInspection InspectAttemptData(object issuer, out XElement attemptedSupportData, out Exception failure)
        {
            attemptedSupportData = null; failure = null;
            if (!ReferenceEquals(issuer, LocalJournalIssuer) || !attempted) return LocalJournalAttemptDataInspection.Blocked;
            if (!System.Threading.Monitor.TryEnter(selected.Gate)) return LocalJournalAttemptDataInspection.Blocked;
            try
            {
                if (selected.Gate.Active || !InspectionContextCurrent()) return LocalJournalAttemptDataInspection.Blocked;
                XDocument document;
                try
                {
                    using (var stream = new FileStream(selected.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        document = XDocument.Load(stream, LoadOptions.None);
                }
                catch (FileNotFoundException) { return InspectionContextCurrent() ? LocalJournalAttemptDataInspection.MissingFinal : LocalJournalAttemptDataInspection.Blocked; }
                catch (DirectoryNotFoundException) { return InspectionContextCurrent() ? LocalJournalAttemptDataInspection.MissingFinal : LocalJournalAttemptDataInspection.Blocked; }
                if (!InspectionContextCurrent()) return LocalJournalAttemptDataInspection.Blocked;
                // No migration registry, repair write, backup fallback, cache insert or live capability is involved.
                if (document.Root == null ||
                    (string)document.Root.Attribute("schemaVersion") != RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    !TryDeserialize(document, out var saved, out _) || saved == null || !saved.IsComplete ||
                    saved.Support == null || saved.StablePlayerId != identity.CanonicalId || saved.StablePlayerKey != selected.Key)
                    return LocalJournalAttemptDataInspection.InvalidFinal;
                if (!InspectionContextCurrent()) return LocalJournalAttemptDataInspection.Blocked;
                if (!XNode.DeepEquals(document.Root, selected.Document.Root)) return LocalJournalAttemptDataInspection.DifferentFinal;
                XDocument trailing;
                using (var stream = new FileStream(selected.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    trailing = XDocument.Load(stream, LoadOptions.None);
                if (!InspectionContextCurrent() || selected.Gate.Active) return LocalJournalAttemptDataInspection.Blocked;
                if (!XNode.DeepEquals(trailing.Root, selected.Document.Root)) return LocalJournalAttemptDataInspection.DifferentFinal;
                var support = selected.Document.Root.Element("support");
                if (support == null) return LocalJournalAttemptDataInspection.InvalidFinal;
                attemptedSupportData = new XElement(support);
                // An exact DATA result leaves PendingJournal untouched and does NOT authorize I3/native recovery.
                return LocalJournalAttemptDataInspection.ExactAttemptedImage;
            }
            catch (System.Xml.XmlException) { return LocalJournalAttemptDataInspection.InvalidFinal; }
            catch (Exception error) { failure = error; return LocalJournalAttemptDataInspection.Faulted; }
            finally { System.Threading.Monitor.Exit(selected.Gate); }
        }

        private bool InspectionContextCurrent()
        {
            lock (Sync)
            {
                return serverAuthority && !ReferenceEquals(RepositoryEpoch, selected.Epoch) &&
                    ReferenceEquals(selected.Gate.PendingJournal, this) &&
                    ReferenceEquals(selected.Gate.Generation, selected.Generation) && selected.Gate.Path != null &&
                    KeyWriteLocks.TryGetValue(selected.Gate.Path, out var gate) && ReferenceEquals(gate, selected.Gate) &&
                    string.Equals(selected.Path, selected.Gate.Path, RepositoryPathComparison) &&
                    string.Equals(selected.Path, CanonicalRepositoryPath(GetPath(selected.Key)), RepositoryPathComparison);
            }
        }
        private bool CheckLiveAndCallbacks()
        {
            if (!IsSelectionCurrent(selected, true) ||
                selected.Original.StablePlayerId != identity.CanonicalId || selected.Key != identity.StorageKey ||
                !current() || !IsSelectionCurrent(selected, true)) return false;
            // Comparator gets a detached clone; mutation cannot alter the issued snapshot.
            var detached = selected.Snapshot.Support.Clone();
            if (!matches(detached) || !IsSelectionCurrent(selected, true) || !current()) return false;
            return IsSelectionCurrent(selected, true);
        }

        private bool VerifyUnderGate(bool markPersisted)
        {
            if (!attempted || !CheckLiveAndCallbacks()) return false;
            if (!RebirthAtomicXmlFile.TryLoad(selected.Path, out var document, out _) ||
                !XNode.DeepEquals(document.Root, selected.Document.Root)) return false;
            // Loader is final-only, validates owner/schema/origin. No repair or backup capability.
            if (!TryLoadValidatedRecord(selected.Path, identity, out var saved, out var migrated, out _, out _) ||
                migrated || saved?.Support == null || !XNode.DeepEquals(Serialize(saved).Root, selected.Document.Root)) return false;
            var savedImage = Serialize(saved);
            if (!matches(saved.Support) || !XNode.DeepEquals(Serialize(saved).Root, savedImage.Root) ||
                !CheckLiveAndCallbacks()) return false;
            // Callback could change the physical final. Trailing raw check refuses that change.
            if (!RebirthAtomicXmlFile.TryLoad(selected.Path, out var trailing, out _) ||
                !XNode.DeepEquals(trailing.Root, selected.Document.Root) || !IsSelectionCurrent(selected, true)) return false;
            if (markPersisted)
            {
                lock (Sync)
                {
                    if (!IsSelectionBindingCurrentLocked(selected)) return false;
                    selected.Original.MarkPersisted();
                }
            }
            if (ReferenceEquals(selected.Gate.PendingJournal, this)) selected.Gate.PendingJournal = null;
            return true;
        }
    }

    internal static LocalJournalWriteResult TryCommitLocalJournalOriginal(RebirthStablePlayerIdentity identity,
        RebirthWorldCharacterRecord originalRecord, RebirthWorldSupportState originalSupport,
        Func<bool> isOriginalCurrent, Func<RebirthWorldSupportState, bool> matchesSavedJournal, string reason,
        out LocalJournalWriteWitness witness)
    {
        return LocalJournalWriteWitness.Commit(LocalJournalIssuer, identity, originalRecord, originalSupport,
            isOriginalCurrent, matchesSavedJournal, reason, out witness);
    }

    internal static bool HasSavedLocalJournalOriginal(LocalJournalWriteWitness witness)
    { return witness != null && witness.Verify(LocalJournalIssuer); }

    internal static LocalJournalWriteResult TryPersistLocalJournalOriginal(LocalJournalWriteWitness witness)
    { return witness == null ? LocalJournalWriteResult.DeniedBeforeWrite : witness.Retry(LocalJournalIssuer); }

    internal static LocalJournalAttemptDataInspection InspectRetainedLocalJournalAttemptData(LocalJournalWriteWitness witness,
        out XElement attemptedSupportData, out Exception failure)
    {
        attemptedSupportData = null; failure = null;
        return witness == null ? LocalJournalAttemptDataInspection.Blocked :
            witness.InspectAttemptData(LocalJournalIssuer, out attemptedSupportData, out failure);
    }
    // All generation accesses are under Sync; all Active accesses are under the stable key gate.
    // Gate identity is the canonical FINAL path, not a player key shared by unrelated worlds.
    private static StringComparer RepositoryPathComparer => Environment.OSVersion.Platform == PlatformID.Win32NT
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static StringComparison RepositoryPathComparison => Environment.OSVersion.Platform == PlatformID.Win32NT
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static string CanonicalRepositoryPath(string path)
    {
        try { return string.IsNullOrEmpty(path) ? null : System.IO.Path.GetFullPath(path); }
        catch { return null; }
    }
    private static RepositoryWriteGate GetRepositoryGateLocked(string key)
    {
        key = key ?? string.Empty;
        string path = CanonicalRepositoryPath(GetPath(key));
        // Invalid/unavailable roots have a non-file dictionary key and can never pass a write context guard.
        string gateKey = path ?? ("\0unavailable:" + key);
        if (!KeyWriteLocks.TryGetValue(gateKey, out var gate))
            KeyWriteLocks[gateKey] = gate = new RepositoryWriteGate(path);
        return (RepositoryWriteGate)gate;
    }
    private static bool TryEnterRepositoryOperation(RepositoryWriteGate gate, LocalJournalWriteWitness retained = null)
    {
        if (gate.Active)
        {
            // Same-thread Monitor reentry must revoke the outer proof even when inner write is refused.
            lock (Sync) gate.Generation = new object();
            return false;
        }
        if (gate.PendingJournal != null && !ReferenceEquals(gate.PendingJournal, retained)) return false;
        gate.Active = true;
        return true;
    }

    private static RepositoryWriteSelection CaptureWriteSelectionLocked(string key, RebirthWorldCharacterRecord original)
    {
        var gate = GetRepositoryGateLocked(key);
        return new RepositoryWriteSelection
        {
            Key = key, Path = gate.Path, Epoch = RepositoryEpoch, Generation = gate.Generation, Gate = gate,
            Original = original, Support = original.Support, Snapshot = original.Clone()
        };
    }

    private static bool IsRepositoryContextCurrentLocked(object epoch, RepositoryWriteGate gate, object generation,
        string key, string path)
    {
        return serverAuthority && ReferenceEquals(RepositoryEpoch, epoch) && ReferenceEquals(gate.Generation, generation) &&
            gate.Path != null && KeyWriteLocks.TryGetValue(gate.Path, out var currentGate) && ReferenceEquals(currentGate, gate) &&
            !string.IsNullOrEmpty(path) && string.Equals(path, gate.Path, RepositoryPathComparison) &&
            string.Equals(path, CanonicalRepositoryPath(GetPath(key)), RepositoryPathComparison);
    }

    private static bool IsRepositoryContextCurrent(object epoch, RepositoryWriteGate gate, object generation, string key, string path)
    { lock (Sync) return IsRepositoryContextCurrentLocked(epoch, gate, generation, key, path); }

    private static bool IsSelectionBindingCurrentLocked(RepositoryWriteSelection selected)
    {
        return IsRepositoryContextCurrentLocked(selected.Epoch, selected.Gate, selected.Generation, selected.Key, selected.Path) &&
            Cache.TryGetValue(selected.Key, out var current) && ReferenceEquals(current, selected.Original) &&
            ReferenceEquals(current.Support, selected.Support) && current.IsComplete;
    }

    private static bool IsSelectionCurrent(RepositoryWriteSelection selected, bool compareContent)
    {
        lock (Sync) if (!IsSelectionBindingCurrentLocked(selected)) return false;
        if (compareContent && (selected.Document == null ||
            !XNode.DeepEquals(Serialize(selected.Original).Root, selected.Document.Root))) return false;
        lock (Sync) return IsSelectionBindingCurrentLocked(selected);
    }

    private static bool TryWriteSelected(RepositoryWriteSelection selected, out string error)
    {
        error = string.Empty;
        lock (Sync)
        {
            if (!IsSelectionBindingCurrentLocked(selected)) { error = "selected original binding changed"; return false; }
            selected.Gate.Generation = selected.Generation = new object();
        }
        return RebirthAtomicXmlFile.TryWrite(selected.Path, new XDocument(selected.Document), out error);
    }
}