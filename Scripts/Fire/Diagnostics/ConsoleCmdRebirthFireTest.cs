#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthFireTest : ConsoleCmdAbstract
{
    private static RebirthFireTestRunner runner;

    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfiretest" };
    }

    public override string getDescription()
    {
        return "Runs one timed REBIRTH block-fire end-to-end diagnostic and writes a consolidated log report.";
    }

    public override string getHelp()
    {
        return "Usage: rbfiretest [seconds]\n"
             + "Example: rbfiretest 20\n"
             + "Aim at a flammable block before starting. During the run, also hit a block with a torch or use a Molotov/flaming projectile.\n"
             + "Toolbelt notifications announce start, progress, and completion.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo sender)
    {
        int durationSeconds = ParseDuration(parameters);

        if (runner != null)
        {
            try { runner.AbortAndCleanup(); } catch { }
            UnityEngine.Object.Destroy(runner.gameObject);
            runner = null;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null)
        {
            Log.Warning("[RBFireTest] Cannot start: local world/player is unavailable.");
            RebirthFireTestRunner.NotifyToolbelt("Fire diagnostic could not start: local player/world unavailable.");
            return;
        }

        Vector3i target;
        bool usedLookTarget = TryResolveLookTarget(world, player, out target);
        if (!usedLookTarget)
            target = World.worldToBlockPos(player.position) + Vector3i.down;

        GameObject go = new GameObject("RebirthFireTestRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<RebirthFireTestRunner>();
        runner.Configure(durationSeconds, target, usedLookTarget, player.entityId, OnFinished);
    }

    private static int ParseDuration(List<string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return 20;
        int value;
        if (!int.TryParse(parameters[0], out value))
            return 20;
        return Mathf.Clamp(value, 5, 120);
    }

    private static bool TryResolveLookTarget(World world, EntityPlayerLocal player, out Vector3i target)
    {
        target = Vector3i.zero;
        if (world == null || player == null)
            return false;

        try
        {
            // The console can leave Voxel.voxelRayHitInfo stale or invalid. Perform a fresh
            // raycast from the player's current look ray when the command is executed.
            Ray ray = player.GetLookRay();
            ray.origin += ray.direction.normalized * 0.5f;
            if (!Voxel.Raycast(world, ray, Constants.cDigAndBuildDistance, -538480645, 4095, 0f))
                return false;
            if (!Voxel.voxelRayHitInfo.bHitValid)
                return false;

            target = Voxel.voxelRayHitInfo.hit.blockPos;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[RBFireTest] look-target raycast failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static void OnFinished(RebirthFireTestRunner completed)
    {
        if (runner == completed)
            runner = null;
    }
}

public sealed class RebirthFireTestRunner : MonoBehaviour
{
    private int durationSeconds;
    private Vector3i target;
    private bool usedLookTarget;
    private int sourceEntityId;
    private Action<RebirthFireTestRunner> finished;
    private bool aborted;
    private bool cleanupRequested;

    private RebirthFireDiagnostics.Snapshot diagnosticsStart;
    private int activeStart;
    private int cooldownStart;
    private int revisionStart;
    private int particlesStart;
    private int particlesRemovedStart;
    private int visibleStart;
    private int targetDamageStart;
    private int targetTypeStart;
    private bool targetBurningStart;
    private long spreadCandidatesStart;
    private long spreadThrottleRejectedStart;
    private long spreadIgnitionsStart;
    private long skippedInvisibleStart;
    private long deferredSimulationCapStart;

    public void Configure(
        int seconds,
        Vector3i testTarget,
        bool targetFromLookRay,
        int sourceId,
        Action<RebirthFireTestRunner> callback)
    {
        durationSeconds = seconds;
        target = testTarget;
        usedLookTarget = targetFromLookRay;
        sourceEntityId = sourceId;
        finished = callback;
        StartCoroutine(Run());
    }

    public void AbortAndCleanup()
    {
        aborted = true;
        CleanupTestFire();
    }

    private IEnumerator Run()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Finish("Fire diagnostic stopped: world became unavailable.");
            yield break;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool isServer = connection == null || connection.IsServer;
        bool isDedicated = GameManager.IsDedicatedServer;
        BlockValue startBlock = world.GetBlock(target);
        RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(startBlock);
        MaterialBlock material = startBlock.Block != null ? startBlock.Block.blockMaterial : null;

        diagnosticsStart = RebirthFireDiagnostics.Capture();
        bool particleAssetUsableBefore = RebirthFireVisualManager.IsParticleAssetUsable(profile.FireParticle);
        bool particleAssetUsableAfterEnsure = RebirthFireVisualManager.EnsureParticleAssetAvailable(profile.FireParticle);
        RebirthFireService service = RebirthFireService.Instance;
        activeStart = service.ActiveCount;
        cooldownStart = service.CooldownCount;
        revisionStart = service.Revision;
        particlesStart = RebirthFireVisualManager.ParticlesSpawned;
        particlesRemovedStart = RebirthFireVisualManager.ParticlesRemoved;
        visibleStart = RebirthFireVisualManager.VisibleFireCount;
        targetDamageStart = startBlock.damage;
        targetTypeStart = startBlock.type;
        targetBurningStart = service.IsBurning(target) || RebirthFireVisualManager.IsFireParticleVisible(target);
        spreadCandidatesStart = service.SpreadCandidatesTotal;
        spreadThrottleRejectedStart = service.SpreadThrottleRejectedTotal;
        spreadIgnitionsStart = service.SpreadIgnitionsTotal;
        skippedInvisibleStart = service.SkippedInvisibleTotal;
        deferredSimulationCapStart = service.DeferredSimulationCapTotal;

        Log.Out("[RBFireTest] === BEGIN durationSeconds=" + durationSeconds + " ===");
        Log.Out("[RBFireTest] environment optionManager=" + RebirthSandboxOptionManager.Current.BlocksCatchFire
            + " runtimePolicy=" + RebirthFireRuntimePolicy.Enabled
            + " patchInstalled=" + RebirthFirePatchInstaller.IsInstalled
            + " patchMethods=" + RebirthFirePatchInstaller.PatchedMethods
            + " patchFailures=" + RebirthFirePatchInstaller.FailedMethods
            + " blocksLoaded=" + Block.BlocksLoaded
            + " isServer=" + isServer
            + " dedicated=" + isDedicated
            + " connectionManager=" + (connection != null)
            + " fireRenderDistance=" + RebirthFireVisualManager.FireRenderDistance
            + " fireParticleSoftCap=" + RebirthFireVisualManager.SoftFireParticleCap
            + " fireParticleCap=" + RebirthFireVisualManager.FireParticleCap
            + " reserveParticles=" + RebirthFireVisualManager.ReserveFireCount
            + " fireClusters=" + RebirthFireVisualManager.CandidateClusterCount
            + " fireLightCap=" + RebirthFireVisualManager.FireLightCap
            + " fireSoundCap=" + RebirthFireVisualManager.FireSoundCap
            + " visualBandBudgets=" + RebirthFireVisualManager.NearBandBudget
                + "/" + RebirthFireVisualManager.MidBandBudget
                + "/" + RebirthFireVisualManager.FarBandBudget
            + " visualBandDistances=" + RebirthFireVisualManager.NearBandDistance
                + "/" + RebirthFireVisualManager.MidBandDistance
                + "/" + RebirthFireVisualManager.FireRenderDistance
            + " spreadThrottleReference=" + RebirthFireDefaults.LegacySpreadThrottleReferenceFires
            + " fireAffectsHeatmap=" + RebirthFireRuntimePolicy.AffectsHeatmap
            + " fireBlockDamageSpeed=" + RebirthFireRuntimePolicy.BlockDamageSpeed
            + " fireBlockDamageMultiplier=" + RebirthFireRuntimePolicy.BlockDamageMultiplier.ToString("F1")
            + " activeGlobalCap=" + RebirthFireDefaults.MaxActiveFiresGlobal
            + " activeChunkCap=" + RebirthFireDefaults.MaxActiveFiresPerChunk
            + " simulationWindowSeconds=" + RebirthFireDefaults.ProcessIntervalSeconds
            + " simulationWindowCap=" + RebirthFireDefaults.MaxSimulatedFiresPerWindow
            + " newFrontSimulationReserve=" + RebirthFireDefaults.NewFrontSimulationReserve
            + " trimmedOnRestore=" + service.TrimmedOnRestore);
        Log.Out("[RBFireTest] target pos=" + target
            + " source=" + (usedLookTarget ? "lookRay" : "blockBelowPlayer")
            + " block=" + SafeBlockName(startBlock)
            + " type=" + startBlock.type
            + " damage=" + startBlock.damage + "/" + (startBlock.Block != null ? startBlock.Block.MaxDamage : 0)
            + " material=" + (material != null ? material.id : "<null>")
            + " damageCategory=" + (material != null ? material.DamageCategory : "<null>")
            + " surfaceCategory=" + (material != null ? material.SurfaceCategory : "<null>")
            + " trader=" + world.IsWithinTraderArea(target)
            + " flammable=" + profile.Flammable
            + " alreadyBurning=" + targetBurningStart
            + " particle='" + profile.FireParticle + "'"
            + " particleUsableBefore=" + particleAssetUsableBefore
            + " particleUsableAfterEnsure=" + particleAssetUsableAfterEnsure);
        Log.Out("[RBFireTest] instructions: during this run, ignite a flammable block, then step onto a fire particle to validate client-confirmed player ignition. The command also submits one direct network ignition request for the aimed target.");

        string startMessage = "Fire diagnostic started for " + durationSeconds + "s. Target: " + SafeBlockName(startBlock)
            + ". Use torch/Molotov now; completion will be announced.";
        NotifyToolbelt(startMessage);

        if (!profile.Flammable)
            NotifyToolbelt("Fire diagnostic warning: aimed block is not flammable. Adapter events will still be recorded.");
        if (!particleAssetUsableAfterEnsure)
            NotifyToolbelt("Fire diagnostic warning: fire particle bundle/prefab failed to load. See [RBFireTest] and [REBIRTH Fire] in the log.");
        if (!RebirthFireRuntimePolicy.Enabled)
            NotifyToolbelt("Fire diagnostic warning: runtime fire policy is OFF despite the selected option state.");
        if (service.TrimmedOnRestore > 0)
            NotifyToolbelt("Fire diagnostic notice: safety cleanup trimmed "
                + service.TrimmedOnRestore + " persisted runaway fire positions on load.");

        List<Vector3i> request = new List<Vector3i>(1) { target };
        RebirthFireNetwork.SendRequest(
            RebirthFireRequestOperation.Ignite,
            sourceEntityId,
            RebirthFireIgnitionCause.Debug,
            request,
            0f);
        Log.Out("[RBFireTest] direct ignition request submitted position=" + target
            + " sourceEntityId=" + sourceEntityId
            + " flammable=" + profile.Flammable
            + " runtimePolicy=" + RebirthFireRuntimePolicy.Enabled);

        float elapsed = 0f;
        bool midpointNotified = false;
        bool directCheckLogged = false;
        while (!aborted && elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!directCheckLogged && elapsed >= 2f)
            {
                directCheckLogged = true;
                Log.Out("[RBFireTest] direct-check elapsed=" + Format(elapsed)
                    + " serverBurning=" + service.IsBurning(target)
                    + " visualActive=" + RebirthFireVisualManager.ActiveCount
                    + " visibleAtTarget=" + RebirthFireVisualManager.IsFireParticleVisible(target)
                    + " active=" + service.ActiveCount
                    + " revision=" + service.Revision);
            }

            if (!midpointNotified && elapsed >= durationSeconds * 0.5f)
            {
                midpointNotified = true;
                NotifyToolbelt("Fire diagnostic halfway: " + (int)elapsed + "s/" + durationSeconds
                    + "s. Continue the torch/Molotov test.");
                Log.Out("[RBFireTest] progress elapsed=" + Format(elapsed)
                    + " active=" + service.ActiveCount
                    + " visibleFire=" + RebirthFireVisualManager.VisibleFireCount
                    + " targetBurning=" + service.IsBurning(target)
                    + " particlesSpawned=" + RebirthFireVisualManager.ParticlesSpawned
                    + " particlesRemoved=" + RebirthFireVisualManager.ParticlesRemoved
                    + " pendingSpawns=" + RebirthFireVisualManager.PendingSpawnCount
                    + " reserveParticles=" + RebirthFireVisualManager.ReserveFireCount
                    + " fireClusters=" + RebirthFireVisualManager.CandidateClusterCount
                    + " checkedLast=" + service.ProcessedLastUpdate
                    + " simulatedLast=" + service.SimulatedLastUpdate
                    + " simulatedWindow=" + service.SimulatedThisWindow
                        + "/" + RebirthFireDefaults.MaxSimulatedFiresPerWindow
                    + " skippedInvisibleLast=" + service.SkippedInvisibleLastUpdate
                    + " deferredWindowCapLast=" + service.DeferredSimulationCapLastUpdate
                    + " newFrontSelected=" + service.SimulationNewFrontSelectedLastWindow
                    + " candidatesNearMidFar=" + RebirthFireVisualManager.NearCandidateCount
                        + "/" + RebirthFireVisualManager.MidCandidateCount
                        + "/" + RebirthFireVisualManager.FarCandidateCount
                    + " selectedNearMidFar=" + RebirthFireVisualManager.NearSelectedCount
                        + "/" + RebirthFireVisualManager.MidSelectedCount
                        + "/" + RebirthFireVisualManager.FarSelectedCount);
            }

            yield return null;
        }

        if (aborted)
        {
            Finish("Fire diagnostic aborted and cleanup requested.");
            yield break;
        }

        PrintResult(world);
        CleanupTestFire();
        Finish("Fire diagnostic complete. Send the latest client log and the server log if dedicated.");
    }

    private void PrintResult(World world)
    {
        RebirthFireDiagnostics.Snapshot end = RebirthFireDiagnostics.Capture();
        RebirthFireService service = RebirthFireService.Instance;
        BlockValue endBlock = world.GetBlock(target);
        EntityPlayerLocal localPlayer = world.GetPrimaryPlayer();
        bool playerBurningMolotov = localPlayer != null && localPlayer.Buffs.HasBuff(RebirthFireDefaults.ContactBuff);
        bool playerIsOnFire = localPlayer != null && localPlayer.Buffs.HasBuff("buffIsOnFire");

        Log.Out("[RBFireTest] === RESULT durationSeconds=" + durationSeconds + " ===");
        Log.Out("[RBFireTest] final optionManager=" + RebirthSandboxOptionManager.Current.BlocksCatchFire
            + " runtimePolicy=" + RebirthFireRuntimePolicy.Enabled
            + " active=" + service.ActiveCount + " deltaActive=" + (service.ActiveCount - activeStart)
            + " cooldowns=" + service.CooldownCount + " deltaCooldowns=" + (service.CooldownCount - cooldownStart)
            + " revision=" + service.Revision + " deltaRevision=" + (service.Revision - revisionStart)
            + " targetServerBurning=" + service.IsBurning(target)
            + " targetVisual=" + RebirthFireVisualManager.IsFireParticleVisible(target)
            + " visibleFire=" + RebirthFireVisualManager.VisibleFireCount + " deltaVisible=" + (RebirthFireVisualManager.VisibleFireCount - visibleStart)
            + " particlesSpawned=" + RebirthFireVisualManager.ParticlesSpawned + " deltaParticles=" + (RebirthFireVisualManager.ParticlesSpawned - particlesStart)
            + " particlesRemoved=" + RebirthFireVisualManager.ParticlesRemoved + " deltaRemoved=" + (RebirthFireVisualManager.ParticlesRemoved - particlesRemovedStart)
            + " pendingSpawns=" + RebirthFireVisualManager.PendingSpawnCount
            + " reserveParticles=" + RebirthFireVisualManager.ReserveFireCount
            + " fireClusters=" + RebirthFireVisualManager.CandidateClusterCount
            + " playerBurningMolotov=" + playerBurningMolotov
            + " playerIsOnFire=" + playerIsOnFire
            + " candidatesNearMidFar=" + RebirthFireVisualManager.NearCandidateCount
                + "/" + RebirthFireVisualManager.MidCandidateCount
                + "/" + RebirthFireVisualManager.FarCandidateCount
            + " selectedNearMidFar=" + RebirthFireVisualManager.NearSelectedCount
                + "/" + RebirthFireVisualManager.MidSelectedCount
                + "/" + RebirthFireVisualManager.FarSelectedCount
            + " checkedLast=" + service.ProcessedLastUpdate
            + " simulatedLast=" + service.SimulatedLastUpdate
            + " simulatedWindow=" + service.SimulatedThisWindow
                + "/" + RebirthFireDefaults.MaxSimulatedFiresPerWindow
            + " simulationEligible=" + service.SimulationEligibleLastWindow
            + " simulationSelected=" + service.SimulationSelectedLastWindow
            + " simulationNeverRunSelected=" + service.SimulationNeverRunSelectedLastWindow
            + " simulationNewFrontSelected=" + service.SimulationNewFrontSelectedLastWindow
            + " heatmapEnabled=" + RebirthFireRuntimePolicy.AffectsHeatmap
            + " fireBlockDamageSpeed=" + RebirthFireRuntimePolicy.BlockDamageSpeed
            + " fireBlockDamageMultiplier=" + RebirthFireRuntimePolicy.BlockDamageMultiplier.ToString("F1")
            + " heatmapEventsLast=" + service.HeatMapEventsLastUpdate
            + " heatmapEventsTotal=" + service.HeatMapEventsTotal
            + " skippedInvisibleDelta=" + (service.SkippedInvisibleTotal - skippedInvisibleStart)
            + " deferredWindowCapDelta=" + (service.DeferredSimulationCapTotal - deferredSimulationCapStart)
            + " spreadThrottleFraction=" + service.SpreadThrottleFractionLastUpdate.ToString("F3")
            + " spreadCandidatesDelta=" + (service.SpreadCandidatesTotal - spreadCandidatesStart)
            + " spreadThrottleRejectedDelta=" + (service.SpreadThrottleRejectedTotal - spreadThrottleRejectedStart)
            + " spreadIgnitionsDelta=" + (service.SpreadIgnitionsTotal - spreadIgnitionsStart));
        Log.Out("[RBFireTest] target-final pos=" + target
            + " block=" + SafeBlockName(endBlock)
            + " type=" + endBlock.type + " startType=" + targetTypeStart
            + " damage=" + endBlock.damage + " startDamage=" + targetDamageStart
            + " damageDelta=" + (endBlock.damage - targetDamageStart));


        Log.Out("[RBFireTest] adapter-delta explosionCalls=" + Delta(end.ExplosionPatchCalls, diagnosticsStart.ExplosionPatchCalls)
            + " explosionDisabled=" + Delta(end.ExplosionSkippedDisabled, diagnosticsStart.ExplosionSkippedDisabled)
            + " explosionAuthority=" + Delta(end.ExplosionSkippedAuthority, diagnosticsStart.ExplosionSkippedAuthority)
            + " explosionInvalidDamageParticle=" + Delta(end.ExplosionSkippedInvalidDamageOrParticle, diagnosticsStart.ExplosionSkippedInvalidDamageOrParticle)
            + " explosionSpreadPolicy=" + Delta(end.ExplosionSkippedSpreadPolicy, diagnosticsStart.ExplosionSkippedSpreadPolicy)
            + " explosionNoChangedBlocks=" + Delta(end.ExplosionNoChangedBlocks, diagnosticsStart.ExplosionNoChangedBlocks)
            + " explosionChangedPositions=" + Delta(end.ExplosionChangedBlockPositions, diagnosticsStart.ExplosionChangedBlockPositions)
            + " explosionScheduledPositions=" + Delta(end.ExplosionScheduledPositions, diagnosticsStart.ExplosionScheduledPositions));

        Log.Out("[RBFireTest] minevent-delta calls=" + Delta(end.AddFireMinEventCalls, diagnosticsStart.AddFireMinEventCalls)
            + " disabled=" + Delta(end.AddFireMinEventSkippedDisabled, diagnosticsStart.AddFireMinEventSkippedDisabled)
            + " invalidContext=" + Delta(end.AddFireMinEventSkippedInvalidContext, diagnosticsStart.AddFireMinEventSkippedInvalidContext)
            + " range=" + Delta(end.AddFireMinEventSkippedRange, diagnosticsStart.AddFireMinEventSkippedRange)
            + " serverSchedules=" + Delta(end.AddFireMinEventServerSchedules, diagnosticsStart.AddFireMinEventServerSchedules)
            + " clientRequests=" + Delta(end.AddFireMinEventClientRequests, diagnosticsStart.AddFireMinEventClientRequests)
            + " positions=" + Delta(end.AddFireMinEventPositions, diagnosticsStart.AddFireMinEventPositions));

        Log.Out("[RBFireTest] network-delta sends=" + Delta(end.NetworkSendCalls, diagnosticsStart.NetworkSendCalls)
            + " sendPositions=" + Delta(end.NetworkSendPositions, diagnosticsStart.NetworkSendPositions)
            + " authoritativeCalls=" + Delta(end.NetworkAuthoritativeCalls, diagnosticsStart.NetworkAuthoritativeCalls)
            + " authoritativeIgnitePositions=" + Delta(end.NetworkAuthoritativeIgnitePositions, diagnosticsStart.NetworkAuthoritativeIgnitePositions)
            + " requestPackages=" + Delta(end.NetworkRequestPackages, diagnosticsStart.NetworkRequestPackages)
            + " rejectSender=" + Delta(end.NetworkRejectedSender, diagnosticsStart.NetworkRejectedSender)
            + " rejectDisabled=" + Delta(end.NetworkRejectedDisabled, diagnosticsStart.NetworkRejectedDisabled)
            + " rejectEnum=" + Delta(end.NetworkRejectedEnum, diagnosticsStart.NetworkRejectedEnum)
            + " rejectMissingSource=" + Delta(end.NetworkRejectedMissingSource, diagnosticsStart.NetworkRejectedMissingSource)
            + " rejectDistance=" + Delta(end.NetworkRejectedDistance, diagnosticsStart.NetworkRejectedDistance)
            + " validatedPositions=" + Delta(end.NetworkValidatedPositions, diagnosticsStart.NetworkValidatedPositions));

        Log.Out("[RBFireTest] service-delta attempts=" + Delta(end.ServiceIgniteAttempts, diagnosticsStart.ServiceIgniteAttempts)
            + " accepted=" + Delta(end.ServiceIgniteAccepted, diagnosticsStart.ServiceIgniteAccepted)
            + " alreadyBurning=" + Delta(end.ServiceAlreadyBurning, diagnosticsStart.ServiceAlreadyBurning)
            + " rejectDisabled=" + Delta(end.ServiceRejectedDisabled, diagnosticsStart.ServiceRejectedDisabled)
            + " rejectNoWorld=" + Delta(end.ServiceRejectedNoWorld, diagnosticsStart.ServiceRejectedNoWorld)
            + " rejectNotServer=" + Delta(end.ServiceRejectedNotServer, diagnosticsStart.ServiceRejectedNotServer)
            + " rejectGlobal=" + service.RejectedGlobalCap
            + " rejectChunk=" + service.RejectedChunkCap
            + " rejectTrader=" + service.RejectedTrader
            + " rejectCooldown=" + service.RejectedCooldown
            + " rejectWater=" + service.RejectedWater
            + " rejectNonFlammable=" + service.RejectedNonFlammable);

        Log.Out("[RBFireTest] contact-delta walkCalls=" + Delta(end.ContactWalkCalls, diagnosticsStart.ContactWalkCalls)
            + " clientRequests=" + Delta(end.ContactClientRequests, diagnosticsStart.ContactClientRequests)
            + " hostDirect=" + Delta(end.ContactHostDirect, diagnosticsStart.ContactHostDirect)
            + " localRateLimited=" + Delta(end.ContactLocalRateLimited, diagnosticsStart.ContactLocalRateLimited)
            + " serverPlayerWalkSuppressed=" + Delta(end.ContactServerPlayerWalkSuppressed, diagnosticsStart.ContactServerPlayerWalkSuppressed)
            + " requestsReceived=" + Delta(end.ContactRequestsReceived, diagnosticsStart.ContactRequestsReceived)
            + " rejectSender=" + Delta(end.ContactRejectedSender, diagnosticsStart.ContactRejectedSender)
            + " rejectDisabled=" + Delta(end.ContactRejectedDisabled, diagnosticsStart.ContactRejectedDisabled)
            + " rejectNoFire=" + Delta(end.ContactRejectedNoFire, diagnosticsStart.ContactRejectedNoFire)
            + " rejectDistance=" + Delta(end.ContactRejectedDistance, diagnosticsStart.ContactRejectedDistance)
            + " rejectInvisible=" + Delta(end.ContactRejectedInvisible, diagnosticsStart.ContactRejectedInvisible)
            + " rejectImmune=" + Delta(end.ContactRejectedImmune, diagnosticsStart.ContactRejectedImmune)
            + " rejectInvalidBuff=" + Delta(end.ContactRejectedInvalidBuff, diagnosticsStart.ContactRejectedInvalidBuff)
            + " buffApplied=" + Delta(end.ContactBuffApplied, diagnosticsStart.ContactBuffApplied));

        Log.Out("[RBFireTest] visual-delta attempts=" + Delta(end.VisualSpawnAttempts, diagnosticsStart.VisualSpawnAttempts)
            + " loadAttempts=" + Delta(end.VisualLoadAttempts, diagnosticsStart.VisualLoadAttempts)
            + " unavailable=" + Delta(end.VisualUnavailable, diagnosticsStart.VisualUnavailable)
            + " nullPrefab=" + Delta(end.VisualNullPrefab, diagnosticsStart.VisualNullPrefab)
            + " spawnUntracked=" + Delta(end.VisualSpawnUntracked, diagnosticsStart.VisualSpawnUntracked)
            + " exceptions=" + Delta(end.VisualExceptions, diagnosticsStart.VisualExceptions)
            + " success=" + Delta(end.VisualSpawnSuccess, diagnosticsStart.VisualSpawnSuccess)
            + " assetUsableNow=" + RebirthFireVisualManager.IsParticleAssetUsable(RebirthFireDefaults.FireParticle));

        string diagnosis;
        long accepted = Delta(end.ServiceIgniteAccepted, diagnosticsStart.ServiceIgniteAccepted);
        long minEvents = Delta(end.AddFireMinEventCalls, diagnosticsStart.AddFireMinEventCalls);
        long explosions = Delta(end.ExplosionPatchCalls, diagnosticsStart.ExplosionPatchCalls);
        long visualSuccess = Delta(end.VisualSpawnSuccess, diagnosticsStart.VisualSpawnSuccess);
        bool observedFire = service.IsBurning(target)
            || RebirthFireVisualManager.IsFireParticleVisible(target)
            || RebirthFireVisualManager.VisibleFireCount > visibleStart
            || service.Revision > revisionStart;
        if (!RebirthSandboxOptionManager.Current.BlocksCatchFire || !RebirthFireRuntimePolicy.Enabled)
            diagnosis = "OPTION_OR_RUNTIME_DISABLED";
        else if (!RebirthFirePatchInstaller.IsInstalled || RebirthFirePatchInstaller.FailedMethods != 0)
            diagnosis = "PATCH_INSTALL_FAILURE";
        else if (!observedFire && accepted == 0 && minEvents == 0 && explosions == 0)
            diagnosis = "NO_GAMEPLAY_ADAPTER_EVENT_AND_DIRECT_IGNITION_NOT_OBSERVED";
        else if (!observedFire && accepted == 0)
            diagnosis = "IGNITION_REQUEST_REACHED_SYSTEM_BUT_FIRE_NOT_OBSERVED";
        else if (!GameManager.IsDedicatedServer && !RebirthFireVisualManager.IsParticleAssetUsable(RebirthFireDefaults.FireParticle))
            diagnosis = "PARTICLE_BUNDLE_OR_PREFAB_UNAVAILABLE";
        else if (visualSuccess == 0 && !GameManager.IsDedicatedServer && RebirthFireVisualManager.VisibleFireCount <= visibleStart)
            diagnosis = "FIRE_STATE_OBSERVED_BUT_PARTICLE_NOT_SPAWNED";
        else
            diagnosis = "IGNITION_PATH_ACTIVE_CHECK_LOG_DETAILS";
        Log.Out("[RBFireTest] diagnosis=" + diagnosis);
        Log.Out("[RBFireTest] === END RESULT ===");
    }

    private void CleanupTestFire()
    {
        if (cleanupRequested)
            return;
        cleanupRequested = true;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
                return;
            List<Vector3i> request = new List<Vector3i>(1) { target };
            RebirthFireNetwork.SendRequest(
                RebirthFireRequestOperation.Remove,
                sourceEntityId,
                RebirthFireIgnitionCause.Debug,
                request,
                0f);
            Log.Out("[RBFireTest] cleanup remove request submitted target=" + target);
        }
        catch (Exception ex)
        {
            Log.Warning("[RBFireTest] cleanup failed error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void Finish(string message)
    {
        Log.Out("[RBFireTest] " + message);
        NotifyToolbelt(message);
        if (finished != null)
            finished(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    public static void NotifyToolbelt(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
        try
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return;
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null)
                return;
            GameManager.ShowTooltip(player, message, true, false, 4f);
        }
        catch
        {
        }
    }

    private static string SafeBlockName(BlockValue value)
    {
        try
        {
            return value.Block != null ? value.Block.GetBlockName() : "<null>";
        }
        catch
        {
            return "<error>";
        }
    }

    private static long Delta(long end, long start)
    {
        return end - start;
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif
