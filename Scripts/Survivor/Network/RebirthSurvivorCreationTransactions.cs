using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Server-only creation transaction control plane. A stable player can have at most one
/// in-flight creation operation, and completed request IDs are replayed from a bounded cache.
/// The actual character commit handler is registered by the headless creation service in Chunk 05.
/// </summary>
public static class RebirthSurvivorCreationTransactions
{
    private sealed class CachedResult
    {
        public string Fingerprint;
        public RebirthSurvivorCreationNetworkResponse Response;
    }

    private sealed class PlayerTransactions
    {
        public bool Busy;
        public ulong BusyRequestId;
        public readonly Dictionary<ulong, CachedResult> Results = new Dictionary<ulong, CachedResult>();
        public readonly Queue<ulong> ResultOrder = new Queue<ulong>();
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, PlayerTransactions> ByPlayer = new Dictionary<string, PlayerTransactions>(StringComparer.Ordinal);
    private static RebirthSurvivorCreationCommitDelegate commitHandler;

    public static bool HasCommitHandler
    {
        get { lock (Sync) return commitHandler != null; }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            ByPlayer.Clear();
            // Do not clear commitHandler here. The installed gameplay service remains installed
            // across world starts and is expected to be stateless between calls.
        }
    }


    public static void ForgetPlayer(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey)) return;
        lock (Sync) ByPlayer.Remove(storageKey);
    }

    public static string GetDebugSummary()
    {
        lock (Sync)
        {
            int busy = 0;
            int cached = 0;
            foreach (PlayerTransactions state in ByPlayer.Values)
            {
                if (state == null) continue;
                if (state.Busy) busy++;
                cached += state.Results.Count;
            }
            return "players=" + ByPlayer.Count + " busy=" + busy + " cachedResults=" + cached + " commitHandler=" + (commitHandler != null);
        }
    }

    public static void RegisterCommitHandler(RebirthSurvivorCreationCommitDelegate handler)
    {
        if (handler == null) throw new ArgumentNullException("handler");
        lock (Sync)
        {
            if (commitHandler != null && commitHandler != handler)
                throw new InvalidOperationException("A Survivor creation commit handler is already registered.");
            commitHandler = handler;
        }
    }

    public static RebirthSurvivorCreationNetworkResponse Execute(
        EntityPlayer player,
        RebirthStablePlayerIdentity identity,
        RebirthSurvivorCreationNetworkRequest request)
    {
        if (identity == null || request == null)
            return Build(request, RebirthSurvivorCreationNetworkStatus.MalformedRequest, "missing-request-or-identity", null, 0L);

        PlayerTransactions state;
        lock (Sync)
        {
            if (!ByPlayer.TryGetValue(identity.StorageKey, out state))
            {
                state = new PlayerTransactions();
                ByPlayer[identity.StorageKey] = state;
            }

            CachedResult cached;
            if (request.RequestId != 0UL && state.Results.TryGetValue(request.RequestId, out cached) && cached != null && cached.Response != null)
            {
                string fingerprint = RebirthSurvivorCreationRequestFingerprint.Compute(request);
                if (!string.Equals(cached.Fingerprint ?? string.Empty, fingerprint, StringComparison.Ordinal))
                    return Build(request, RebirthSurvivorCreationNetworkStatus.MalformedRequest, "request-id-payload-mismatch", null, GetRevision(identity));
                RebirthSurvivorCreationNetworkResponse replay = cached.Response.Clone();
                replay.WasReplay = true;
                return replay;
            }

            if (state.Busy)
                return Build(request, RebirthSurvivorCreationNetworkStatus.Busy, "creation-transaction-busy", null, GetRevision(identity));

            state.Busy = true;
            state.BusyRequestId = request.RequestId;
        }

        RebirthSurvivorCreationNetworkResponse response;
        try
        {
            response = ExecuteNoLock(player, identity, request);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Survivor] creation transaction failed key=" + identity.StorageKey + " request=" + request.RequestId + " error=" + ex.GetType().Name + ": " + ex.Message);
            response = Build(request, RebirthSurvivorCreationNetworkStatus.InternalError, "creation-internal-error", null, GetRevision(identity));
        }
        finally
        {
            lock (Sync)
            {
                state.Busy = false;
                state.BusyRequestId = 0UL;
            }
        }

        Cache(identity.StorageKey, request, response);
        return response;
    }

    private static RebirthSurvivorCreationNetworkResponse ExecuteNoLock(
        EntityPlayer player,
        RebirthStablePlayerIdentity identity,
        RebirthSurvivorCreationNetworkRequest request)
    {
        if (request.RequestId == 0UL)
            return Build(request, RebirthSurvivorCreationNetworkStatus.MalformedRequest, "request-id-zero", null, GetRevision(identity));
        if (request.ProtocolVersion != RebirthSurvivorNetworkProtocol.Version)
            return Build(request, RebirthSurvivorCreationNetworkStatus.ProtocolMismatch, "protocol-mismatch", null, GetRevision(identity));
        if (request.Operation != RebirthSurvivorCreationNetworkOperation.Validate &&
            request.Operation != RebirthSurvivorCreationNetworkOperation.Commit)
            return Build(request, RebirthSurvivorCreationNetworkStatus.MalformedRequest, "unknown-operation", null, GetRevision(identity));
        if (request.Malformed)
            return Build(request, RebirthSurvivorCreationNetworkStatus.MalformedRequest, request.MalformedReason, null, GetRevision(identity));
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return Build(request, RebirthSurvivorCreationNetworkStatus.RebirthModeDisabled, "rebirth-progression-disabled", null, GetRevision(identity));
        if (!RebirthSurvivorDefinitionRegistry.IsReady)
            return Build(request, RebirthSurvivorCreationNetworkStatus.InternalError, "definitions-unavailable", null, GetRevision(identity));

        RebirthWorldCharacterRecord existing;
        if (RebirthWorldCharacterRepository.TryGet(identity, out existing) && existing != null && existing.IsComplete)
            return Build(request, RebirthSurvivorCreationNetworkStatus.AlreadyCreated, "already-created", null, existing.Revision);

        if (!string.Equals(request.ClientDefinitionHash ?? string.Empty,
            RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty,
            StringComparison.OrdinalIgnoreCase))
        {
            return Build(request, RebirthSurvivorCreationNetworkStatus.DefinitionMismatch, "definition-hash-mismatch", null, 0L);
        }
        if (!string.Equals(request.ClientDefinitionVersion ?? string.Empty,
            RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty,
            StringComparison.Ordinal))
        {
            return Build(request, RebirthSurvivorCreationNetworkStatus.DefinitionMismatch, "definition-version-mismatch", null, 0L);
        }

        RebirthSurvivorCreationResult validation = RebirthSurvivorCreationValidator.ValidateForCommit(request.ToSelection(), true);
        if (validation == null || !validation.IsValid)
            return Build(request, RebirthSurvivorCreationNetworkStatus.ValidationFailed, "selection-invalid", validation, 0L);

        if (request.Operation == RebirthSurvivorCreationNetworkOperation.Validate)
            return Build(request, RebirthSurvivorCreationNetworkStatus.Validated, "selection-valid", validation, 0L);

        RebirthSurvivorCreationCommitDelegate handler;
        lock (Sync) handler = commitHandler;
        if (handler == null)
            return Build(request, RebirthSurvivorCreationNetworkStatus.CommitServiceUnavailable, "commit-service-unavailable", validation, 0L);

        RebirthSurvivorCommitExecutionResult commit = handler(player, identity, request.Clone(), validation);
        if (commit == null)
            return Build(request, RebirthSurvivorCreationNetworkStatus.CommitFailed, "commit-handler-returned-null", validation, GetRevision(identity));

        if (commit.Status == RebirthSurvivorCommitStatus.Created)
        {
            RebirthWorldCharacterRecord committed;
            if (!RebirthWorldCharacterRepository.TryGet(identity, out committed) || committed == null || !committed.IsComplete)
                return Build(request, RebirthSurvivorCreationNetworkStatus.CommitFailed, "commit-not-persisted", validation, 0L);
            return Build(request, RebirthSurvivorCreationNetworkStatus.Created, commit.MessageCode, validation, committed.Revision);
        }
        if (commit.Status == RebirthSurvivorCommitStatus.AlreadyCreated)
            return Build(request, RebirthSurvivorCreationNetworkStatus.AlreadyCreated, commit.MessageCode, validation, Math.Max(commit.CharacterRevision, GetRevision(identity)));
        if (commit.Status == RebirthSurvivorCommitStatus.Rejected)
            return Build(request, RebirthSurvivorCreationNetworkStatus.CommitRejected, commit.MessageCode, validation, GetRevision(identity));
        return Build(request, RebirthSurvivorCreationNetworkStatus.CommitFailed, commit.MessageCode, validation, GetRevision(identity));
    }

    private static long GetRevision(RebirthStablePlayerIdentity identity)
    {
        if (identity == null) return 0L;
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterRepository.TryGet(identity, out record) && record != null ? record.Revision : 0L;
    }

    private static RebirthSurvivorCreationNetworkResponse Build(
        RebirthSurvivorCreationNetworkRequest request,
        RebirthSurvivorCreationNetworkStatus status,
        string code,
        RebirthSurvivorCreationResult validation,
        long revision)
    {
        return new RebirthSurvivorCreationNetworkResponse
        {
            ProtocolVersion = RebirthSurvivorNetworkProtocol.Version,
            RequestId = request != null ? request.RequestId : 0UL,
            Operation = request != null ? request.Operation : RebirthSurvivorCreationNetworkOperation.Validate,
            Status = status,
            WasReplay = false,
            ServerDefinitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty,
            ServerDefinitionVersion = RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty,
            CharacterRevision = Math.Max(0L, revision),
            MessageCode = code ?? string.Empty,
            ValidationResult = RebirthSurvivorNetworkClone.CloneCreationResult(validation)
        };
    }

    private static void Cache(string storageKey, RebirthSurvivorCreationNetworkRequest request, RebirthSurvivorCreationNetworkResponse response)
    {
        ulong requestId = request != null ? request.RequestId : 0UL;
        if (string.IsNullOrEmpty(storageKey) || requestId == 0UL || response == null)
            return;
        lock (Sync)
        {
            PlayerTransactions state;
            if (!ByPlayer.TryGetValue(storageKey, out state))
            {
                state = new PlayerTransactions();
                ByPlayer[storageKey] = state;
            }
            if (!state.Results.ContainsKey(requestId))
                state.ResultOrder.Enqueue(requestId);
            // Cache the result together with the exact canonical request intent. Reusing an ID
            // with different payload is rejected instead of replaying a misleading prior result.
            state.Results[requestId] = new CachedResult
            {
                Fingerprint = RebirthSurvivorCreationRequestFingerprint.Compute(request),
                Response = response.Clone()
            };
            while (state.ResultOrder.Count > RebirthSurvivorNetworkProtocol.MaxCachedCreationResultsPerPlayer)
            {
                ulong old = state.ResultOrder.Dequeue();
                state.Results.Remove(old);
            }
        }
    }
}
