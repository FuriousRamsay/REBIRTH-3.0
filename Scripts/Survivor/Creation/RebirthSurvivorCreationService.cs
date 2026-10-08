using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Headless server-authoritative Survivor creation service. It has no XUi dependency: main-menu
/// profiles, first-world creation and debug validation all converge on the same validator and the
/// same commit path.
/// </summary>
public static class RebirthSurvivorCreationService
{
    private static bool installed;

    public static bool IsInstalled { get { return installed; } }

    public static string Install()
    {
        if (installed)
            return "[REBIRTH Survivor] creation service already installed.";

        RebirthSurvivorCreationTransactions.RegisterCommitHandler(CommitFromTransaction);
        installed = true;
        return "[REBIRTH Survivor] headless creation service installed worldSchema="
            + RebirthWorldCharacterRecord.CurrentSchemaVersion
            + " metabolismSchema=" + RebirthMetabolismState.CurrentVersion;
    }

    public static RebirthSurvivorCreationResult Validate(RebirthSurvivorCreationSelection selection)
    {
        return RebirthSurvivorCreationValidator.ValidateForCommit(selection, true);
    }

    public static bool TryBuildCandidate(
        EntityPlayer player,
        RebirthStablePlayerIdentity identity,
        RebirthSurvivorCreationNetworkRequest request,
        RebirthSurvivorCreationResult validation,
        string creationId,
        DateTime committedAtUtc,
        out RebirthWorldCharacterRecord candidate,
        out string error)
    {
        candidate = null;
        error = string.Empty;
        if (player == null) { error = "player-missing"; return false; }
        if (identity == null) { error = "identity-missing"; return false; }
        if (request == null) { error = "request-missing"; return false; }
        if (validation == null || !validation.IsValid) { error = "validation-invalid"; return false; }
        if (string.IsNullOrEmpty(creationId)) { error = "creation-id-missing"; return false; }

        RebirthStablePlayerIdentity resolved;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out resolved) || resolved == null ||
            !string.Equals(resolved.StorageKey, identity.StorageKey, StringComparison.Ordinal) ||
            !string.Equals(resolved.CanonicalId, identity.CanonicalId, StringComparison.Ordinal))
        {
            error = "server-identity-mismatch";
            return false;
        }

        RebirthWorldOriginSnapshot origin = RebirthWorldOriginSnapshot.FromAuthoritativeCreationResult(
            creationId,
            validation,
            request,
            committedAtUtc);
        if (origin == null)
        {
            error = "origin-build-failed";
            return false;
        }

        candidate = RebirthSurvivorStartingStateInitializer.BuildRecord(identity, origin, player, validation);
        if (candidate == null || !candidate.IsComplete)
        {
            candidate = null;
            error = "initial-state-build-failed";
            return false;
        }

        string invariantError;
        if (!RebirthSurvivorStartingStateInitializer.ValidateCandidateAgainstResult(candidate, identity, validation, out invariantError))
        {
            candidate = null;
            error = "initial-state-invariant-failed:" + invariantError;
            return false;
        }
        return true;
    }

    private static RebirthSurvivorCommitExecutionResult CommitFromTransaction(
        EntityPlayer player,
        RebirthStablePlayerIdentity identity,
        RebirthSurvivorCreationNetworkRequest request,
        RebirthSurvivorCreationResult transactionValidation)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority || !RebirthMetabolismService.IsServerAuthority)
            return RebirthSurvivorCommitExecutionResult.Rejected("server-authority-required");
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return RebirthSurvivorCommitExecutionResult.Rejected("rebirth-progression-disabled");
        if (player == null || identity == null || request == null)
            return RebirthSurvivorCommitExecutionResult.Rejected("creation-context-missing");

        RebirthWorldCharacterRecord existing;
        if (RebirthWorldCharacterRepository.TryGet(identity, out existing) && existing != null && existing.IsComplete)
            return RebirthSurvivorCommitExecutionResult.AlreadyCreated(existing.Revision);

        // Recompute from request IDs at the commit boundary. The transaction's validation object is
        // an internal optimization/response preview, not authority that bypasses the canonical validator.
        RebirthSurvivorCreationResult validation = RebirthSurvivorCreationValidator.ValidateForCommit(request.ToSelection(), true);
        if (validation == null || !validation.IsValid)
            return RebirthSurvivorCommitExecutionResult.Rejected("selection-invalid-at-commit");
        if (!Equivalent(validation, transactionValidation))
            return RebirthSurvivorCommitExecutionResult.Rejected("validation-result-changed");

        // Do this before touching metabolism. A corrupt/unreadable existing character file must
        // block creation without resetting the player's physical state.
        string preflightError;
        if (!RebirthWorldCharacterRepository.CanCommitNew(identity, out preflightError))
        {
            if (RebirthWorldCharacterRepository.TryGet(identity, out existing) && existing != null && existing.IsComplete)
                return RebirthSurvivorCommitExecutionResult.AlreadyCreated(existing.Revision);
            Log.Warning("[REBIRTH Survivor] creation preflight rejected key=" + identity.StorageKey + " reason=" + preflightError);
            return RebirthSurvivorCommitExecutionResult.Rejected("character-storage-occupied-or-unavailable");
        }

        // CommitFromTransaction has already proven this is the authoritative server path. Keep
        // the metabolism repository synchronized defensively as well as at GameStarting, so an
        // early install-time client authority probe can never poison first-character creation.
        RebirthMetabolismStateRepository.SetServerAuthority(true);
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("creation precommit authority worldRepo=" + RebirthWorldCharacterRepository.IsServerAuthority
            + " metabolismService=" + RebirthMetabolismService.IsServerAuthority
            + " metabolismRepo=" + RebirthMetabolismStateRepository.IsServerAuthority
            + " entity=" + player.entityId); }

        string creationId = Guid.NewGuid().ToString("N");
        DateTime committedAtUtc = DateTime.UtcNow;
        RebirthWorldCharacterRecord candidate;
        string buildError;
        if (!TryBuildCandidate(player, identity, request, validation, creationId, committedAtUtc, out candidate, out buildError))
            return RebirthSurvivorCommitExecutionResult.Failed(buildError);

        // Cross-store crash ordering:
        //  1) write a fresh, tagged metabolism generation while there is still NO world character;
        //  2) atomically publish the immutable world character;
        //  3) initialize runtime/native-facing state only after the character is durable.
        // If the process dies after (1), creation simply retries and replaces the unclaimed fresh
        // generation. If it dies after (2), the tagged metabolism save is already correct and the
        // next online initialization is idempotent.
        RebirthMetabolismState metabolism = RebirthMetabolismStateRepository.ResetForFreshSurvivorCharacter(
            player,
            creationId,
            "survivor-origin-precommit");
        if (metabolism == null)
            return RebirthSurvivorCommitExecutionResult.Failed("fresh-metabolism-reset-failed");

        string metabolismSaveError;
        if (!RebirthMetabolismStateRepository.TrySave("survivor-origin-precommit", out metabolismSaveError))
        {
            Log.Error("[REBIRTH Survivor] creation metabolism precommit save failed key=" + identity.StorageKey + " error=" + metabolismSaveError);
            return RebirthSurvivorCommitExecutionResult.Failed("fresh-metabolism-persist-failed");
        }

        RebirthWorldCharacterRecord committed;
        string commitError;
        if (!RebirthWorldCharacterRepository.TryCommitNew(identity, candidate, out committed, out commitError))
        {
            // The fresh metabolism generation is intentionally left unclaimed. No world character
            // exists, so the creation gate keeps it frozen and the next valid attempt safely resets it.
            Log.Error("[REBIRTH Survivor] world-origin commit failed key=" + identity.StorageKey + " creationId=" + creationId + " error=" + commitError);
            return RebirthSurvivorCommitExecutionResult.Failed("world-origin-persist-failed");
        }

        FinalizeFreshOnlineState(player, committed, metabolism);
        RebirthBackgroundStarterItems.Offer(player);
        RebirthCharacterCreationHoldService.OnCharacterCommitted(player, identity);
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] character created key=" + identity.StorageKey
            + " creationId=" + creationId
            + " revision=" + committed.Revision
            + " background=" + committed.Origin.BackgroundId
            + " diet=" + committed.Origin.DietId
            + " traits=" + committed.Origin.TraitIds.Count); }
        return RebirthSurvivorCommitExecutionResult.Created(committed.Revision);
    }

    private static void FinalizeFreshOnlineState(EntityPlayer player, RebirthWorldCharacterRecord committed, RebirthMetabolismState metabolism)
    {
        if (player == null || committed == null || committed.Origin == null)
            return;
        try
        {
            RebirthMetabolismService.EnsurePlayerReady(player);
            RebirthMetabolismState current = RebirthMetabolismStateRepository.GetOrCreate(player);
            if (current != null)
            {
                if (!string.Equals(current.SurvivorCreationId, committed.Origin.CreationId, StringComparison.Ordinal))
                {
                    // This should be unreachable inside the per-player transaction. Fail loudly and
                    // repair to the just-committed generation rather than silently continuing stale state.
                    Log.Error("[REBIRTH Survivor] metabolism creationId mismatch immediately after commit; repairing to committed generation.");
                    current.SurvivorCreationId = committed.Origin.CreationId;
                    current.Touch();
                }
                RebirthToolbeltCapacity.SanitizePlayerStartup(player, current, true);
            }

            string saveError;
            if (!RebirthMetabolismStateRepository.TrySaveIfDirty("survivor-origin-finalize", out saveError))
                Log.Error("[REBIRTH Survivor] post-commit metabolism finalize save failed: " + saveError);
        }
        catch (Exception ex)
        {
            // At this point both authoritative persistence records already exist. Runtime finishing
            // is restart-safe, so never pretend the durable origin failed and permit a second creation.
            Log.Error("[REBIRTH Survivor] post-commit online initialization failed creationId="
                + committed.Origin.CreationId + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool Equivalent(RebirthSurvivorCreationResult a, RebirthSurvivorCreationResult b)
    {
        if (a == null || b == null || a.IsValid != b.IsValid)
            return false;
        if (!string.Equals(a.DefinitionHash, b.DefinitionHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(a.DefinitionVersion, b.DefinitionVersion, StringComparison.Ordinal) ||
            !string.Equals(a.BackgroundId, b.BackgroundId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(a.DietId, b.DietId, StringComparison.OrdinalIgnoreCase) ||
            a.RemainingCreationPoints != b.RemainingCreationPoints ||
            Math.Abs(a.HealthPotential - b.HealthPotential) > 0.0001f ||
            a.UnencumberedSlotDelta != b.UnencumberedSlotDelta ||
            a.TraitIds.Count != b.TraitIds.Count ||
            a.Attributes.Count != b.Attributes.Count ||
            a.StartingSkills.Count != b.StartingSkills.Count ||
            a.StartingSkillKnowledge.Count != b.StartingSkillKnowledge.Count ||
            a.StartingKnowledgeIds.Count != b.StartingKnowledgeIds.Count)
            return false;

        for (int i = 0; i < a.TraitIds.Count; i++)
            if (!string.Equals(a.TraitIds[i], b.TraitIds[i], StringComparison.OrdinalIgnoreCase)) return false;
        for (int i = 0; i < a.Attributes.Count; i++)
        {
            RebirthResolvedAttributeStart x = a.Attributes[i];
            RebirthResolvedAttributeStart y = b.Attributes[i];
            if (x == null || y == null || !string.Equals(x.AttributeId, y.AttributeId, StringComparison.OrdinalIgnoreCase) ||
                Math.Abs(x.Current - y.Current) > 0.0001f || Math.Abs(x.Potential - y.Potential) > 0.0001f)
                return false;
        }
        foreach (KeyValuePair<string, float> pair in a.StartingSkills)
        {
            float v;
            if (!b.StartingSkills.TryGetValue(pair.Key, out v) || Math.Abs(pair.Value - v) > 0.0001f)
                return false;
        }
        foreach (KeyValuePair<string, float> pair in a.StartingSkillKnowledge)
        {
            float v;
            if (!b.StartingSkillKnowledge.TryGetValue(pair.Key, out v) || Math.Abs(pair.Value - v) > 0.0001f)
                return false;
        }
        for (int i = 0; i < a.StartingKnowledgeIds.Count; i++)
            if (!string.Equals(a.StartingKnowledgeIds[i], b.StartingKnowledgeIds[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
