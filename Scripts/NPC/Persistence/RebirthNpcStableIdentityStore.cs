using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcStableIdentityRecord
{
    public RebirthNpcStableId StableId;
    public int LastEntityId;
    public int EntityClassId;
    public string ProfileId;
    public Vector3 LastPosition;
    public int MatchAttempts;
    public string LastMatchIssue;
    public int LastMatchEntityId;
}

public static class RebirthNpcStableIdentityStore
{
    private const int FormatVersion = 3;
    private const string FileName = "RebirthNpcStableIdentities.xml";
    private const float ExactEntityMaximumDistance = 64f;
    private const float PositionalMatchRadius = 12f;
    private const float StrongPositionalDistance = 2f;
    private const float MaximumConfidentPositionalDistance = 6f;
    private const float RequiredDistanceSeparation = 2f;
    private const int MaximumConflictDetails = 24;

    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, RebirthNpcStableIdentityRecord> Pending =
        new Dictionary<RebirthNpcStableId, RebirthNpcStableIdentityRecord>();
    private static readonly Dictionary<int, string> UnresolvedByEntity = new Dictionary<int, string>();

    private static bool loaded;
    private static string loadedSaveDirectory = string.Empty;
    private static long restored, created, rejected, ambiguous, lowConfidence, exactMatches, positionalMatches, durableDogMatches, embeddedDogMatches, saves;

    private sealed class Candidate
    {
        public RebirthNpcStableIdentityRecord Record;
        public float DistanceSquared;
        public bool ExactEntityId;
    }

    /// <summary>
    /// Binds a dog to the StableId embedded directly in its entity save payload.
    /// This is stronger than entity-id/position/name heuristics and is the primary
    /// identity path for dogs saved by v231 or later. The aggregate record remains
    /// authoritative for ownership/lifecycle; the embedded value only tells us which
    /// aggregate record belongs to this live entity.
    /// </summary>
    public static bool TryBindEmbeddedDogIdentity(EntityRebirthNPC npc, RebirthNpcStableId stableId, out string issue)
    {
        issue = string.Empty;
        if (!IsServer() || npc == null || npc.RebirthRuntimeState == null || stableId.IsEmpty)
        {
            issue = "embedded identity prerequisites were not met";
            return false;
        }

        EnsureLoaded();

        // A legacy/foreign aggregate record loaded after a save switch is quarantined
        // until a physical entity from the current save proves the embedded StableId.
        // The embedded payload is stronger evidence than any sidecar position/entity id.
        RebirthNpcPersistentRecord promoted;
        RebirthNpcAggregatePersistenceStore.TryPromoteQuarantinedDogRecord(stableId, out promoted);

        RebirthNpcPersistentRecordView aggregate;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out aggregate) || aggregate == null ||
            aggregate.Profile == null ||
            !string.Equals(aggregate.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.Ordinal))
        {
            issue = "embedded StableId has no compatible dog aggregate record";
            return false;
        }
        if (aggregate.Lifecycle != null && aggregate.Lifecycle.TombstoneState)
        {
            issue = "embedded StableId points at a tombstoned dog record";
            return false;
        }
        if (aggregate.Presence != null && string.Equals(aggregate.Presence.PresenceState,
                RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            issue = "embedded StableId points at a removed dog record";
            return false;
        }

        RebirthNpcStableIdentityRecord pending = null;
        lock (Sync)
        {
            Pending.TryGetValue(stableId, out pending);
            if (pending != null)
            {
                if (pending.EntityClassId != npc.entityClass ||
                    !string.Equals(pending.ProfileId ?? string.Empty, npc.RebirthProfileId ?? string.Empty, StringComparison.Ordinal))
                {
                    issue = "embedded StableId sidecar class/profile mismatch";
                    return false;
                }
                Pending.Remove(stableId);
            }
        }

        if (!RebirthNpcRuntimeRegistry.TryRebindStableId(npc.entityId, stableId))
        {
            lock (Sync)
            {
                if (pending != null) Pending[stableId] = pending;
                UnresolvedByEntity[npc.entityId] = "embedded StableId is already bound to another loaded NPC";
            }
            issue = "embedded StableId is already bound to another loaded NPC";
            rejected++;
            return false;
        }

        lock (Sync) UnresolvedByEntity.Remove(npc.entityId);
        restored++;
        embeddedDogMatches++;
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC] Restored dog stable identity entity=" + npc.entityId +
            " stableId=" + stableId + " match=embedded-save-id."); }
        return true;
    }

    public static void TryRestore(EntityRebirthNPC npc)
    {
        if (!IsServer() || npc == null || npc.RebirthRuntimeState == null) return;
        EnsureLoaded();

        RebirthNpcStableIdentityRecord selected = null;
        string unresolvedReason = null;
        bool selectedByExactEntity = false;
        bool selectedByDurableDogIdentity = false;

        lock (Sync)
        {
            List<Candidate> candidates = BuildCandidates(npc);
            if (candidates.Count == 0)
            {
                // A normal world reload can assign the live dog a new entity id, and a dog
                // that was following its owner can legitimately be far from the sidecar's
                // last saved position. 2.6 did not key ownership solely by transient entity
                // distance; the persistent hire record remained the authority. Recover only
                // when the durable owned dog identity is unambiguous (or uniquely matches the
                // persisted display name), never by a blind nearest/first guess.
                Candidate durableDog;
                string durableIssue;
                if (TryRecoverOwnedDogDurableCandidate(npc, out durableDog, out durableIssue))
                {
                    selected = durableDog.Record;
                    selectedByDurableDogIdentity = true;
                    Pending.Remove(selected.StableId);
                    UnresolvedByEntity.Remove(npc.entityId);
                }
                else if (!string.IsNullOrEmpty(durableIssue))
                {
                    unresolvedReason = durableIssue;
                    UnresolvedByEntity[npc.entityId] = unresolvedReason;
                    ambiguous++;
                }
                else
                {
                    UnresolvedByEntity.Remove(npc.entityId);
                }
            }
            else
            {
                candidates.Sort(CompareCandidates);
                Candidate best = candidates[0];
                Candidate second = candidates.Count > 1 ? candidates[1] : null;
                float bestDistance = Mathf.Sqrt(best.DistanceSquared);

                if (best.ExactEntityId)
                {
                    if (second != null && second.ExactEntityId)
                    {
                        // Heal the specific failure mode from older dog builds: when a moved dog
                        // was rejected by the 64m sidecar-position bound, it was assigned a fresh
                        // StableId. The next save could therefore contain TWO identity records with
                        // the same persisted entity id: the original owned record and the accidental
                        // unowned replacement. A later build must prefer the one that still carries
                        // the durable player ownership record or the dog can never recover its owner.
                        Candidate recoveredDog;
                        List<RebirthNpcStableId> duplicateDogIds;
                        if (TryRecoverOwnedDogExactEntityCandidate(candidates, npc, out recoveredDog, out duplicateDogIds))
                        {
                            selected = recoveredDog.Record;
                            selectedByExactEntity = true;
                            for (int i = 0; i < duplicateDogIds.Count; i++) Pending.Remove(duplicateDogIds[i]);
                            Log.Warning("[REBIRTH NPC] Recovered dog stable identity entity=" + npc.entityId
                                + " stableId=" + selected.StableId
                                + " and discarded " + duplicateDogIds.Count
                                + " duplicate exact-entity sidecar record(s) created by an earlier failed relog restore.");
                        }
                        else
                        {
                            unresolvedReason = "multiple persisted identities claim previous entity ID " + npc.entityId;
                            ambiguous++;
                        }
                    }
                    else if (bestDistance <= ExactEntityMaximumDistance || IsDogExactEntityIdentity(best.Record, npc))
                    {
                        // Persistent EntityCreationData keeps the same entity id across a normal
                        // world save/reload. Dogs can legitimately be far from the position saved
                        // in our identity sidecar because Follow movement may continue after that
                        // sidecar snapshot. For the dog profile, a unique exact entity-id + class +
                        // profile match is therefore stronger evidence than stale position. Without
                        // this exception a moved dog can receive a new StableId after relog and the
                        // aggregate ownership record becomes unreachable, making its owner appear lost.
                        selected = best.Record;
                        selectedByExactEntity = true;
                    }
                    else
                    {
                        unresolvedReason = "exact entity-id candidate moved " + FormatDistance(bestDistance)
                            + ", beyond the " + FormatDistance(ExactEntityMaximumDistance) + " safety bound";
                        lowConfidence++;
                    }
                }
                else if (bestDistance > MaximumConfidentPositionalDistance)
                {
                    unresolvedReason = "nearest compatible identity is " + FormatDistance(bestDistance)
                        + " away, beyond the confident positional limit of "
                        + FormatDistance(MaximumConfidentPositionalDistance);
                    lowConfidence++;
                }
                else if (second != null)
                {
                    float secondDistance = Mathf.Sqrt(second.DistanceSquared);
                    float separation = secondDistance - bestDistance;
                    if (bestDistance > StrongPositionalDistance && separation < RequiredDistanceSeparation)
                    {
                        unresolvedReason = "ambiguous compatible identities at " + FormatDistance(bestDistance)
                            + " and " + FormatDistance(secondDistance)
                            + "; required separation is " + FormatDistance(RequiredDistanceSeparation);
                        ambiguous++;
                    }
                    else
                    {
                        selected = best.Record;
                    }
                }
                else
                {
                    selected = best.Record;
                }

                if (selected != null)
                {
                    Pending.Remove(selected.StableId);
                    UnresolvedByEntity.Remove(npc.entityId);
                }
                else
                {
                    MarkCandidatesAttempted(candidates, npc.entityId, unresolvedReason);
                    UnresolvedByEntity[npc.entityId] = unresolvedReason ?? "identity match was not accepted";
                }
            }
        }

        if (selected == null)
        {
            if (unresolvedReason == null) created++;
            else Log.Warning("[REBIRTH NPC] Stable identity unresolved for entity=" + npc.entityId + ": " + unresolvedReason + ".");
            return;
        }

        if (!RebirthNpcRuntimeRegistry.TryRebindStableId(npc.entityId, selected.StableId))
        {
            lock (Sync)
            {
                selected.MatchAttempts++;
                selected.LastMatchEntityId = npc.entityId;
                selected.LastMatchIssue = "stable identity is already bound to another loaded NPC";
                Pending[selected.StableId] = selected;
                UnresolvedByEntity[npc.entityId] = selected.LastMatchIssue;
            }
            rejected++;
            Log.Warning("[REBIRTH NPC] Stable identity collision for entity=" + npc.entityId + " stableId=" + selected.StableId + ".");
            return;
        }

        restored++;
        if (selectedByExactEntity) exactMatches++;
        else if (selectedByDurableDogIdentity) durableDogMatches++;
        else positionalMatches++;
        string matchKind = selectedByExactEntity ? "entity-id" : (selectedByDurableDogIdentity ? "durable-owned-dog" : "position");
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC] Restored stable identity entity=" + npc.entityId + " stableId="
            + selected.StableId + " match=" + matchKind + "."); }
    }


    private static bool IsDogExactEntityIdentity(RebirthNpcStableIdentityRecord record, EntityRebirthNPC npc)
    {
        return record != null && npc != null &&
               record.LastEntityId == npc.entityId &&
               record.EntityClassId == npc.entityClass &&
               string.Equals(record.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.Ordinal) &&
               string.Equals(npc.RebirthProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.Ordinal);
    }

    private static bool TryRecoverOwnedDogExactEntityCandidate(
        List<Candidate> candidates,
        EntityRebirthNPC npc,
        out Candidate selected,
        out List<RebirthNpcStableId> duplicateIds)
    {
        selected = null;
        duplicateIds = new List<RebirthNpcStableId>();
        if (npc == null || !string.Equals(npc.RebirthProfileId ?? string.Empty,
                RebirthDogDefinitions.ProfileId, StringComparison.Ordinal)) return false;

        Candidate ownedCandidate = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (candidate == null || !candidate.ExactEntityId || !IsDogExactEntityIdentity(candidate.Record, npc)) continue;
            if (!HasDurablePlayerOwner(candidate.Record.StableId)) continue;

            // More than one durable owned record for the exact same entity id is a real
            // conflict; do not guess between owners.
            if (ownedCandidate != null) return false;
            ownedCandidate = candidate;
        }

        if (ownedCandidate == null) return false;
        selected = ownedCandidate;

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (candidate == null || candidate == ownedCandidate || !candidate.ExactEntityId ||
                !IsDogExactEntityIdentity(candidate.Record, npc)) continue;
            // Only discard the duplicate sidecar identity if it does NOT itself carry
            // durable ownership. This keeps genuine ownership conflicts visible.
            if (!HasDurablePlayerOwner(candidate.Record.StableId))
                duplicateIds.Add(candidate.Record.StableId);
        }
        return true;
    }

    private static bool HasDurablePlayerOwner(RebirthNpcStableId stableId)
    {
        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            record.Ownership == null || string.IsNullOrWhiteSpace(record.Ownership.OwnerPlatformIdOrPersistentPlayerId))
            return false;

        if (record.Lifecycle != null && record.Lifecycle.TombstoneState) return false;
        if (record.Presence != null && string.Equals(record.Presence.PresenceState,
                RebirthNpcPresenceState.Removed.ToString(), StringComparison.OrdinalIgnoreCase)) return false;

        RebirthNpcOwnershipKind ownership;
        return Enum.TryParse(record.Ownership.OwnershipState ?? string.Empty, true, out ownership) &&
               ownership == RebirthNpcOwnershipKind.Player;
    }

    private static bool TryRecoverOwnedDogDurableCandidate(
        EntityRebirthNPC npc, out Candidate selected, out string issue)
    {
        selected = null;
        issue = string.Empty;
        if (npc == null || !string.Equals(npc.RebirthProfileId ?? string.Empty,
                RebirthDogDefinitions.ProfileId, StringComparison.Ordinal)) return false;

        List<Candidate> durable = new List<Candidate>();
        List<Candidate> nameMatches = new List<Candidate>();
        string liveName = (npc.EntityName ?? string.Empty).Trim();

        foreach (RebirthNpcStableIdentityRecord record in Pending.Values)
        {
            if (record == null || record.EntityClassId != npc.entityClass ||
                !string.Equals(record.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.Ordinal) ||
                !HasDurablePlayerOwner(record.StableId)) continue;

            Candidate candidate = new Candidate
            {
                Record = record,
                DistanceSquared = (record.LastPosition - npc.position).sqrMagnitude,
                ExactEntityId = record.LastEntityId == npc.entityId
            };
            durable.Add(candidate);

            if (!string.IsNullOrEmpty(liveName))
            {
                RebirthNpcPersistentRecordView aggregate;
                string persistedName = RebirthNpcAggregatePersistenceStore.TryGetView(record.StableId, out aggregate) && aggregate?.Identity != null
                    ? (aggregate.Identity.GeneratedOrAssignedDisplayName ?? string.Empty).Trim()
                    : string.Empty;
                if (!string.IsNullOrEmpty(persistedName) && string.Equals(persistedName, liveName, StringComparison.OrdinalIgnoreCase))
                    nameMatches.Add(candidate);
            }
        }

        if (nameMatches.Count == 1)
        {
            selected = nameMatches[0];
            return true;
        }
        if (durable.Count == 1)
        {
            selected = durable[0];
            return true;
        }
        if (durable.Count > 1)
        {
            issue = "multiple durable owned dog identities match class/profile after entity-id change";
        }
        return false;
    }

    private static List<Candidate> BuildCandidates(EntityRebirthNPC npc)
    {
        List<Candidate> result = new List<Candidate>();
        float positionalRadiusSquared = PositionalMatchRadius * PositionalMatchRadius;
        foreach (RebirthNpcStableIdentityRecord candidate in Pending.Values)
        {
            if (candidate.EntityClassId != npc.entityClass) continue;
            if (!string.Equals(candidate.ProfileId, npc.RebirthProfileId, StringComparison.Ordinal)) continue;
            float distanceSquared = (candidate.LastPosition - npc.position).sqrMagnitude;
            bool exactEntityId = candidate.LastEntityId == npc.entityId;
            if (!exactEntityId && distanceSquared > positionalRadiusSquared) continue;
            result.Add(new Candidate
            {
                Record = candidate,
                DistanceSquared = distanceSquared,
                ExactEntityId = exactEntityId
            });
        }
        return result;
    }

    private static int CompareCandidates(Candidate left, Candidate right)
    {
        if (left.ExactEntityId != right.ExactEntityId) return left.ExactEntityId ? -1 : 1;
        int distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
        if (distance != 0) return distance;
        return string.CompareOrdinal(left.Record.StableId.ToString(), right.Record.StableId.ToString());
    }

    private static void MarkCandidatesAttempted(List<Candidate> candidates, int entityId, string issue)
    {
        string resolvedIssue = issue ?? "identity match was not accepted";
        for (int i = 0; i < candidates.Count; i++)
        {
            RebirthNpcStableIdentityRecord record = candidates[i].Record;
            record.MatchAttempts++;
            record.LastMatchEntityId = entityId;
            record.LastMatchIssue = resolvedIssue;
        }
    }


    public static bool TryResolve(int entityId, RebirthNpcStableId stableId, out string message)
    {
        message = string.Empty;
        if (!IsServer())
        {
            message = "identity resolution is server-authoritative";
            return false;
        }
        EnsureLoaded();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityRebirthNPC npc = world != null ? world.GetEntity(entityId) as EntityRebirthNPC : null;
        if (npc == null || npc.RebirthRuntimeState == null)
        {
            message = "loaded REBIRTH NPC entity was not found: " + entityId;
            return false;
        }

        RebirthNpcStableIdentityRecord record;
        lock (Sync)
        {
            if (!Pending.TryGetValue(stableId, out record))
            {
                message = "pending stable identity was not found: " + stableId;
                return false;
            }
            if (record.EntityClassId != npc.entityClass)
            {
                message = "entity class mismatch; pending=" + record.EntityClassId + " loaded=" + npc.entityClass;
                return false;
            }
            if (!string.Equals(record.ProfileId, npc.RebirthProfileId, StringComparison.Ordinal))
            {
                message = "profile mismatch; pending='" + (record.ProfileId ?? string.Empty)
                    + "' loaded='" + (npc.RebirthProfileId ?? string.Empty) + "'";
                return false;
            }
            Pending.Remove(stableId);
        }

        if (!RebirthNpcRuntimeRegistry.TryRebindStableId(entityId, stableId))
        {
            lock (Sync) Pending[stableId] = record;
            message = "stable identity is already bound to another loaded NPC";
            return false;
        }

        lock (Sync) UnresolvedByEntity.Remove(entityId);
        restored++;
        Log.Warning("[REBIRTH NPC] ADMIN identity resolution entity=" + entityId + " stableId=" + stableId + ".");
        RebirthNpcExecutionPersistenceStore.TryRestore(npc);
        Save();
        message = "resolved entity=" + entityId + " stableId=" + stableId;
        return true;
    }

    public static bool TryForgetPending(RebirthNpcStableId stableId, out string message)
    {
        message = string.Empty;
        if (!IsServer())
        {
            message = "identity removal is server-authoritative";
            return false;
        }
        EnsureLoaded();
        lock (Sync)
        {
            if (!Pending.Remove(stableId))
            {
                message = "pending stable identity was not found: " + stableId;
                return false;
            }
        }
        Log.Warning("[REBIRTH NPC] ADMIN forgot pending stable identity=" + stableId + ".");
        Save();
        message = "forgot pending stable identity=" + stableId;
        return true;
    }

    public static void Save()
    {
        if (!IsServer()) return;
        RebirthNpcSaveScopeSnapshot currentScope = RebirthNpcSaveScope.ObserveCurrent();
        if (currentScope == null || string.IsNullOrEmpty(currentScope.SaveDirectory)) return;
        lock (Sync)
        {
            if (loaded && !string.IsNullOrEmpty(loadedSaveDirectory) &&
                !string.Equals(loadedSaveDirectory, currentScope.SaveDirectory, StringComparison.OrdinalIgnoreCase))
            {
                string refusal = "REFUSED cross-save stable-identity write loaded='" +
                    loadedSaveDirectory + "' current='" + currentScope.SaveDirectory + "'.";
                Log.Error("[REBIRTH NPC] " + refusal);
                throw new InvalidOperationException(refusal);
            }
        }
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Dictionary<RebirthNpcStableId, RebirthNpcStableIdentityRecord> records =
                new Dictionary<RebirthNpcStableId, RebirthNpcStableIdentityRecord>();
            lock (Sync)
            {
                foreach (KeyValuePair<RebirthNpcStableId, RebirthNpcStableIdentityRecord> pair in Pending)
                    records[pair.Key] = pair.Value;
            }

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            foreach (RebirthNpcRuntimeState state in RebirthNpcRuntimeRegistry.GetSnapshot())
            {
                int entityId;
                if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
                EntityRebirthNPC npc = world != null ? world.GetEntity(entityId) as EntityRebirthNPC : null;
                if (npc == null) continue;
                records[state.StableId] = new RebirthNpcStableIdentityRecord
                {
                    StableId = state.StableId,
                    LastEntityId = entityId,
                    EntityClassId = npc.entityClass,
                    ProfileId = state.ProfileId,
                    LastPosition = npc.position
                };
            }

            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("rebirthNpcStableIdentities");
            root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
            RebirthNpcSaveScopeSnapshot saveScope = RebirthNpcSaveScope.ObserveCurrent();
            root.SetAttribute("saveScope", saveScope != null ? (saveScope.Fingerprint ?? string.Empty) : string.Empty);
            doc.AppendChild(root);
            List<RebirthNpcStableIdentityRecord> list = new List<RebirthNpcStableIdentityRecord>(records.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.StableId.ToString(), b.StableId.ToString()));
            foreach (RebirthNpcStableIdentityRecord record in list)
            {
                XmlElement element = doc.CreateElement("npc");
                element.SetAttribute("stableId", record.StableId.ToString());
                Set(element, "entity", record.LastEntityId);
                Set(element, "class", record.EntityClassId);
                element.SetAttribute("profile", record.ProfileId ?? string.Empty);
                Set(element, "x", record.LastPosition.x);
                Set(element, "y", record.LastPosition.y);
                Set(element, "z", record.LastPosition.z);
                if (record.MatchAttempts > 0) Set(element, "attempts", record.MatchAttempts);
                if (record.LastMatchEntityId != 0) Set(element, "lastMatchEntity", record.LastMatchEntityId);
                if (!string.IsNullOrEmpty(record.LastMatchIssue)) element.SetAttribute("lastMatchIssue", record.LastMatchIssue);
                root.AppendChild(element);
            }

            RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("identities", path, FormatVersion);
            RebirthNpcPersistenceFile.SaveAtomic(path, doc);
            saves++;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC] Failed to save stable identities: " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    public static void Reset(bool save)
    {
        if (save) Save();
        lock (Sync)
        {
            Pending.Clear();
            UnresolvedByEntity.Clear();
            loaded = false;
            loadedSaveDirectory = string.Empty;
        }
    }

    public static string GetReport()
    {
        EnsureLoaded();
        lock (Sync)
        {
            return "[REBIRTH NPC] identities pending=" + Pending.Count
                + " restored=" + restored
                + " exact=" + exactMatches
                + " positional=" + positionalMatches
                + " durableDog=" + durableDogMatches
                + " embeddedDog=" + embeddedDogMatches
                + " created=" + created
                + " ambiguous=" + ambiguous
                + " lowConfidence=" + lowConfidence
                + " rejected=" + rejected
                + " unresolvedEntities=" + UnresolvedByEntity.Count
                + " saves=" + saves
                + " file=" + GetPath();
        }
    }

    public static string GetConflictReport()
    {
        EnsureLoaded();
        lock (Sync)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[REBIRTH NPC] identity conflicts unresolvedEntities=").Append(UnresolvedByEntity.Count)
                .Append(" pendingRecords=").Append(Pending.Count);
            int written = 0;
            foreach (KeyValuePair<int, string> pair in UnresolvedByEntity)
            {
                if (written++ >= MaximumConflictDetails) break;
                builder.Append("\n  entity=").Append(pair.Key).Append(" reason=").Append(pair.Value);
            }
            foreach (RebirthNpcStableIdentityRecord record in Pending.Values)
            {
                if (written >= MaximumConflictDetails) break;
                if (record.MatchAttempts <= 0 || string.IsNullOrEmpty(record.LastMatchIssue)) continue;
                written++;
                builder.Append("\n  stableId=").Append(record.StableId)
                    .Append(" lastEntity=").Append(record.LastEntityId)
                    .Append(" attemptedEntity=").Append(record.LastMatchEntityId)
                    .Append(" attempts=").Append(record.MatchAttempts)
                    .Append(" reason=").Append(record.LastMatchIssue);
            }
            if (written == 0) builder.Append("\n  none");
            else if (UnresolvedByEntity.Count + Pending.Count > written) builder.Append("\n  ... details truncated");
            return builder.ToString();
        }
    }

    public static void EnsureLoaded()
    {
        RebirthNpcSaveScopeSnapshot currentScope = RebirthNpcSaveScope.ObserveCurrent();
        string currentSaveDirectory = currentScope != null ? (currentScope.SaveDirectory ?? string.Empty) : string.Empty;
        if (string.IsNullOrEmpty(currentSaveDirectory)) return;

        lock (Sync)
        {
            if (loaded && string.Equals(loadedSaveDirectory, currentSaveDirectory, StringComparison.OrdinalIgnoreCase)) return;
            if (loaded && !string.IsNullOrEmpty(loadedSaveDirectory) &&
                !string.Equals(loadedSaveDirectory, currentSaveDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("[REBIRTH NPC] stable-identity save boundary detected; discarding cached identities from '" +
                    loadedSaveDirectory + "' before loading '" + currentSaveDirectory + "'.");
                Pending.Clear();
                UnresolvedByEntity.Clear();
            }
            loaded = true;
            loadedSaveDirectory = currentSaveDirectory;
            string path = GetPath();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                XmlDocument document; string source, loadError;
                if (!RebirthNpcPersistenceFile.TryLoad(path, ValidateDocument, out document, out source, out loadError))
                {
                    if (File.Exists(path) || File.Exists(path + ".bak"))
                        Log.Warning("[REBIRTH NPC] Failed to load stable identities: " + loadError);
                    return;
                }
                XmlElement root = document.DocumentElement;
                int format;
                if (root == null || root.Name != "rebirthNpcStableIdentities"
                    || !int.TryParse(root.GetAttribute("format"), out format)
                    || format < 1 || format > FormatVersion)
                    throw new InvalidDataException("Unsupported stable identity format.");
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("identities", path, format, FormatVersion, source);

                string fileScope = root.GetAttribute("saveScope") ?? string.Empty;
                bool hasScope = !string.IsNullOrWhiteSpace(fileScope);
                bool foreignScope = hasScope && currentScope != null &&
                    !string.Equals(fileScope, currentScope.Fingerprint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                bool unscopedSecondSave = !hasScope && currentScope != null && currentScope.IsSecondOrLaterSave;
                int skippedDogIdentities = 0;

                if (foreignScope)
                    Log.Warning("[REBIRTH NPC] Stable identity sidecar belongs to another save scope; identities will not be trusted file=" +
                        path + ".");

                foreach (XmlNode node in root.SelectNodes("npc"))
                {
                    XmlElement element = node as XmlElement;
                    RebirthNpcStableId stableId;
                    int entityId, entityClassId;
                    float x, y, z;
                    if (element == null
                        || !RebirthNpcStableId.TryParse(element.GetAttribute("stableId"), out stableId)
                        || !int.TryParse(element.GetAttribute("entity"), out entityId)
                        || !int.TryParse(element.GetAttribute("class"), out entityClassId)) continue;

                    string profileId = element.GetAttribute("profile") ?? string.Empty;
                    bool dogIdentity = string.Equals(profileId, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase);
                    if (foreignScope || (unscopedSecondSave && dogIdentity))
                    {
                        if (dogIdentity) skippedDogIdentities++;
                        continue;
                    }

                    float.TryParse(element.GetAttribute("x"), NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                    float.TryParse(element.GetAttribute("y"), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                    float.TryParse(element.GetAttribute("z"), NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                    int attempts = 0, lastMatchEntityId = 0;
                    if (format >= 2)
                    {
                        int.TryParse(element.GetAttribute("attempts"), out attempts);
                        int.TryParse(element.GetAttribute("lastMatchEntity"), out lastMatchEntityId);
                    }
                    Pending[stableId] = new RebirthNpcStableIdentityRecord
                    {
                        StableId = stableId,
                        LastEntityId = entityId,
                        EntityClassId = entityClassId,
                        ProfileId = profileId,
                        LastPosition = new Vector3(x, y, z),
                        MatchAttempts = attempts,
                        LastMatchEntityId = lastMatchEntityId,
                        LastMatchIssue = format >= 2 ? element.GetAttribute("lastMatchIssue") : string.Empty
                    };
                }

                if (unscopedSecondSave && skippedDogIdentities > 0)
                    Log.Warning("[REBIRTH NPC] Quarantined " + skippedDogIdentities +
                        " unscoped legacy dog identity record(s) after save switch. Embedded current-save identities remain authoritative.");
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("identities", path, FormatVersion, ex.GetType().Name + ": " + ex.Message);
                Pending.Clear();
                Log.Warning("[REBIRTH NPC] Failed to load stable identities: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private static bool ValidateDocument(XmlDocument document)
    {
        XmlElement root = document != null ? document.DocumentElement : null;
        int format;
        return root != null && root.Name == "rebirthNpcStableIdentities"
            && int.TryParse(root.GetAttribute("format"), out format)
            && format >= 1 && format <= FormatVersion;
    }

    private static string FormatDistance(float distance)
    {
        return distance.ToString("0.00", CultureInfo.InvariantCulture) + "m";
    }

    private static bool IsServer()
    {
        return SingletonMonoBehaviour<ConnectionManager>.Instance != null
            && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }

    private static void Set(XmlElement element, string name, object value)
    {
        element.SetAttribute(name, Convert.ToString(value, CultureInfo.InvariantCulture));
    }
}
