using System;
using System.Collections.Generic;
using GamePath;
using HarmonyLib;
using UnityEngine;

#nullable disable

#if DEBUG
public struct RebirthHybridPathCounterSnapshot
{
    public long Evaluated;
    public long Activated;
    public long DirectUpdates;
    public long Blocked;
    public long Vertical;
    public long Route;
    public long SpecialMove;
    public long Cooldown;
    public long NoPath;
    public long NoTarget;
    public long Stuck;
    public long StopRange;
    public long ClearanceSkipped;
    public long VerticalPathScans;
    public long VerticalPathCacheHits;
}
#endif

/// <summary>
/// Hybrid direct-target path smoothing for eligible non-player entities.
/// Native pathfinding remains authoritative whenever a safe straight approach
/// to the current attack target cannot be proven.
/// </summary>
public static class RebirthHybridPathSmoothing
{
    private const int CollisionMask = EntityMoveHelper.cCollisionMask;

    // Behavioral values retained from the established hybrid implementation.
    private const float MotionPredictionSeconds = 0.05f;
    private const float StopRangeScale = 0.80f;
    private const float FallbackAttackRange = 1.50f;
    private const float RadiusScale = 0.90f;
    private const float RadiusMargin = 0.05f;
    private const float MaxTotalVerticalDelta = 1.25f;
    private const float MaxPathSegmentVerticalDelta = 1.25f;
    // Applied only while entering direct mode, against the native path's own
    // endpoint. It is not compared with the continuously moving player.
    private const float MaxInitialNativeRouteDeviation = 2.0f;
    private const int NoDirectCooldownTicks = 40;
    private const int ClearanceCheckIntervalTicks = 2;
    private const float VerticalPathEndChangeDistance = 0.25f;
    private const int StuckCheckIntervalTicks = 10;
    private const float StuckMinMoveDistance = 0.05f;
    private const float StuckMinRemainingDistance = 1.25f;

#if DEBUG
    private const float CaptureDefaultSeconds = 20f;
    private const float CaptureMinSeconds = 5f;
    private const float CaptureMaxSeconds = 120f;
    private const float CaptureSampleIntervalSeconds = 0.50f;
    private const float CaptureSearchRadius = 60f;
#endif

    private sealed class DirectMoveState
    {
        public EntityAlive EntityIdentity;
        public EntityAlive TargetIdentity;
        public int TargetEntityId = -1;
        public bool IsDirect;
        public ulong NoDirectUntilTick;
        public ulong LastInitialAttemptTick = ulong.MaxValue;
        public int LastPathGeometrySignature;
        public Vector3 LastPosition;
        public ulong LastStuckCheckTick;

        // The full native-path vertical scan is cached by geometry, not merely
        // by PathEntity reference. Native path objects can be mutated in place.
        public int VerticalQualifiedPathSignature;
        public int VerticalQualifiedPathIndex;
        public bool HasVerticalQualification;
    }

    private static readonly Dictionary<int, DirectMoveState> States =
        new Dictionary<int, DirectMoveState>();

    private static World stateWorld;
    private static bool enabled = true;

#if DEBUG
    private static long evaluated;
    private static long activated;
    private static long directUpdates;
    private static long rejectedBlocked;
    private static long rejectedVertical;
    private static long rejectedRoute;
    private static long rejectedSpecialMove;
    private static long rejectedCooldown;
    private static long rejectedNoPath;
    private static long rejectedNoTarget;
    private static long rejectedStuck;
    private static long stoppedInRange;
    private static long clearanceSkipped;
    private static long verticalPathScans;
    private static long verticalPathCacheHits;

    private static bool captureActive;
    private static int captureEntityId = -1;
    private static int captureRequesterPlayerId = -1;
    private static float captureEndRealtime;
    private static float captureNextSampleRealtime;
    private static string captureLastDecision = string.Empty;
    private static ulong capturePostTick = ulong.MaxValue;
    private static int captureSamples;
    private static int captureTransitions;
    private static string captureSelectedClass = string.Empty;

    public static bool DebugLogging;
#endif

    public static bool Enabled
    {
        get { return enabled; }
        set
        {
            if (enabled == value)
                return;

            enabled = value;
            if (!enabled)
                States.Clear();
        }
    }

    /// <summary>
    /// Runs before the native movement update. When a current native path and
    /// body-sized physics test both support a straight approach, this replaces
    /// only the movement destination with the moving attack target. If any guard
    /// fails, no native state is changed and the grid path executes normally.
    /// </summary>
    public static void BeforeUpdateMoveHelper(EntityMoveHelper moveHelper)
    {
        if (moveHelper == null)
            return;

        EntityAlive entity = moveHelper.entity;
        PathNavigate navigator = entity != null ? entity.getNavigator() : null;
        PathEntity path = navigator != null ? navigator.getPath() : null;
        EntityAlive target = entity != null ? entity.GetAttackTarget() : null;

        if (!Enabled)
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "disabled", null);
#endif
            return;
        }

        if (!moveHelper.IsActive)
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "inactive", null);
#endif
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "noConnectionManager", null);
#endif
            return;
        }

        if (!connection.IsServer)
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "notServer", null);
#endif
            return;
        }

        if (entity == null)
        {
#if DEBUG
            CaptureDecision(moveHelper, null, target, path, "noEntity", null);
#endif
            return;
        }

        if (entity.IsDead())
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "entityDead", null);
#endif
            RemoveState(entity);
            return;
        }

        if (entity is EntityPlayer)
        {
#if DEBUG
            CaptureDecision(moveHelper, entity, target, path, "playerExcluded", null);
#endif
            RemoveState(entity);
            return;
        }

        EnsureWorldIdentity(entity.world as World);

#if DEBUG
        evaluated++;
#endif

        if (target == null || target.IsDead())
        {
#if DEBUG
            rejectedNoTarget++;
            CaptureDecision(moveHelper, entity, target, path, "noTarget", null);
#endif
            RemoveState(entity);
            return;
        }

        if (path == null || path.points == null || path.pathLength < 2 || path.isFinished())
        {
#if DEBUG
            rejectedNoPath++;
            CaptureDecision(moveHelper, entity, target, path, "noUsablePath", null);
#endif
            RemoveDirectFlag(entity);
            return;
        }

        ulong now = GameTimer.Instance.ticks;
        int pathSignature = ComputePathGeometrySignature(path);
        DirectMoveState state;
        States.TryGetValue(entity.entityId, out state);

        // Entity IDs are recyclable. Never let a replacement entity inherit
        // direct-mode/cooldown/cache state from the previous object generation.
        if (state != null && !ReferenceEquals(state.EntityIdentity, entity))
        {
            States.Remove(entity.entityId);
            state = null;
        }

        if (state == null)
        {
            state = new DirectMoveState
            {
                EntityIdentity = entity,
                TargetIdentity = target,
                TargetEntityId = target.entityId,
                IsDirect = false,
                NoDirectUntilTick = 0UL,
                LastInitialAttemptTick = ulong.MaxValue,
                LastPathGeometrySignature = pathSignature,
                LastPosition = entity.position,
                LastStuckCheckTick = now
            };
            States[entity.entityId] = state;
        }
        else if (!ReferenceEquals(state.TargetIdentity, target)
            || state.TargetEntityId != target.entityId)
        {
            state.TargetIdentity = target;
            state.TargetEntityId = target.entityId;
            state.IsDirect = false;
            state.NoDirectUntilTick = 0UL;
            state.LastInitialAttemptTick = ulong.MaxValue;
            state.LastPathGeometrySignature = pathSignature;
            state.LastPosition = entity.position;
            state.LastStuckCheckTick = now;
            ClearVerticalQualification(state);
        }
        else if (state.LastPathGeometrySignature != pathSignature)
        {
            // A changed native route is new evidence and invalidates an earlier
            // initial-rejection cooldown, even if PathEntity itself was reused.
            state.LastPathGeometrySignature = pathSignature;
            state.NoDirectUntilTick = 0UL;
            state.LastInitialAttemptTick = ulong.MaxValue;
            ClearVerticalQualification(state);
        }

        if (state.NoDirectUntilTick > now)
        {
#if DEBUG
            rejectedCooldown++;
            CaptureDecision(
                moveHelper,
                entity,
                target,
                path,
                "cooldown",
                "untilTick=" + state.NoDirectUntilTick + " now=" + now);
#endif
            return;
        }

        // Repeated callbacks in the same game tick must not repeat route scans
        // and physics probes after an initial direct-mode acquisition failed.
        if (!state.IsDirect && state.LastInitialAttemptTick == now)
        {
#if DEBUG
            rejectedCooldown++;
            CaptureDecision(moveHelper, entity, target, path, "sameTickAttempt", null);
#endif
            return;
        }
        if (!state.IsDirect)
            state.LastInitialAttemptTick = now;

        if (IsSpecialMovementActive(entity, moveHelper))
        {
#if DEBUG
            rejectedSpecialMove++;
            CaptureDecision(moveHelper, entity, target, path, "specialMove", GetSpecialMovementFlags(entity, moveHelper));
            DebugLogFallback(entity, "specialMove");
#endif
            FallBackToNative(entity, state, now);
            return;
        }

        Vector3 directTarget = target.position
            + target.GetVelocityPerSecond() * MotionPredictionSeconds;

#if DEBUG
        string verticalReason;
        if (!HasSafeLiveVerticalDelta(entity, directTarget, out verticalReason))
#else
        if (!HasSafeLiveVerticalDelta(entity, directTarget))
#endif
        {
#if DEBUG
            rejectedVertical++;
            CaptureDecision(moveHelper, entity, target, path, "vertical", verticalReason);
            DebugLogFallback(entity, verticalReason);
#endif
            FallBackToNative(entity, state, now);
            return;
        }

        bool requiresVerticalPathScan =
            RequiresVerticalPathQualification(state, path);

        if (requiresVerticalPathScan)
        {
#if DEBUG
            verticalPathScans++;
            if (!HasSafePathVerticalSegments(entity, path, out verticalReason))
#else
            if (!HasSafePathVerticalSegments(entity, path))
#endif
            {
#if DEBUG
                rejectedVertical++;
                CaptureDecision(moveHelper, entity, target, path, "vertical", verticalReason);
                DebugLogFallback(entity, verticalReason);
#endif
                FallBackToNative(entity, state, now);
                return;
            }
        }
#if DEBUG
        else
        {
            verticalPathCacheHits++;
        }
#endif

        // Match the established hybrid lifecycle: the native route is used as
        // an entry qualification, not as a live leash to a moving target.
        // Once direct mode is active, live body clearance, vertical safety,
        // special-movement state and stuck detection decide when to fall back.
        if (state == null || !state.IsDirect)
        {
#if DEBUG
            string routeReason;
            if (!NativeRouteSupportsStraightApproach(entity, path, out routeReason))
#else
            if (!NativeRouteSupportsStraightApproach(entity, path))
#endif
            {
#if DEBUG
                rejectedRoute++;
                CaptureDecision(moveHelper, entity, target, path, "route", routeReason);
                DebugLogFallback(entity, routeReason);
#endif
                FallBackToNative(entity, state, now);
                return;
            }
        }

        bool mustCheckClearance =
            state == null
            || !state.IsDirect
            || requiresVerticalPathScan
            || ShouldRunStaggeredClearanceCheck(entity.entityId, now);

        if (mustCheckClearance)
        {
#if DEBUG
            RaycastHit hit;
            float castDistance;
            float castRadius;
            if (!HasBodyClearance(entity, target, out hit, out castDistance, out castRadius))
#else
            if (!HasBodyClearance(entity, target))
#endif
            {
#if DEBUG
                rejectedBlocked++;
                string blockedReason = FormatHitDetails(hit, castDistance, castRadius);
                CaptureDecision(moveHelper, entity, target, path, "blocked", blockedReason);
                DebugLogFallback(entity, "blocked " + blockedReason);
#endif
                FallBackToNative(entity, state, now);
                return;
            }
        }
#if DEBUG
        else
        {
            clearanceSkipped++;
        }
#endif

        if (requiresVerticalPathScan)
            MarkVerticalPathQualified(state, path, pathSignature);
        else
            state.VerticalQualifiedPathIndex = path.currentPathIndex;

        if (IsDirectMoverStuck(entity, directTarget, state, now))
        {
#if DEBUG
            rejectedStuck++;
            CaptureDecision(moveHelper, entity, target, path, "stuck", null);
            DebugLogFallback(entity, "stuck");
#endif
            ApplyCooldown(entity, state, now);
            return;
        }

        float attackRange = GetSafeAttackRange(entity);
        if ((entity.position - directTarget).magnitude < attackRange * StopRangeScale)
        {
            moveHelper.Stop();
            state.IsDirect = false;
#if DEBUG
            stoppedInRange++;
            CaptureDecision(
                moveHelper,
                entity,
                target,
                path,
                "stopRange",
                "attackRange=" + attackRange.ToString("0.00"));
#endif
            return;
        }

        float speed = moveHelper.moveSpeed > 0f
            ? moveHelper.moveSpeed
            : entity.GetMoveSpeedAggro();

        // Verified 3.1 overload. This is the defining behavior: move toward the
        // attack target itself rather than merely selecting another grid node.
        moveHelper.SetMoveTo(directTarget, moveHelper.CanBreakBlocks, speed);

        if (!state.IsDirect)
        {
            state.IsDirect = true;
            state.LastPosition = entity.position;
            state.LastStuckCheckTick = now;
#if DEBUG
            activated++;
#endif
        }

#if DEBUG
        directUpdates++;
        CaptureDecision(
            moveHelper,
            entity,
            target,
            path,
            "direct",
            "speed=" + speed.ToString("0.00"));

        if (DebugLogging)
        {
            Log.Out("[RebirthHybridPath] direct entity=" + entity.entityId
                + " target=" + target.entityId
                + " distance=" + (directTarget - entity.position).magnitude.ToString("0.00")
                + " speed=" + speed.ToString("0.00"));
        }
#endif
    }

#if DEBUG
    public static void AfterUpdateMoveHelper(EntityMoveHelper moveHelper)
    {
        if (moveHelper == null || !captureActive)
            return;

        EntityAlive entity = moveHelper.entity;
        if (entity == null || entity.entityId != captureEntityId)
            return;

        ulong tick = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        if (tick != capturePostTick)
            return;

        EntityAlive target = entity.GetAttackTarget();
        PathNavigate navigator = entity.getNavigator();
        PathEntity path = navigator != null ? navigator.getPath() : null;

        LogCaptureLine("POST", moveHelper, entity, target, path, captureLastDecision, "afterNativeUpdate");
        capturePostTick = ulong.MaxValue;
    }

    public static string StartCapture(EntityPlayer requester, int requestedEntityId, float seconds)
    {
        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        if (world == null)
            return "[RebirthHybridCapture] START FAILED world=<null>";

        EntityAlive selected = null;
        if (requestedEntityId >= 0)
            selected = world.GetEntity(requestedEntityId) as EntityAlive;

        if (selected == null)
            selected = FindNearestCaptureEntity(world, requester);

        if (selected == null)
        {
            return "[RebirthHybridCapture] START FAILED no eligible non-player EntityAlive found within "
                + CaptureSearchRadius.ToString("0")
                + "m. Let one zombie acquire you as its attack target and try again.";
        }

        seconds = Mathf.Clamp(
            seconds <= 0f ? CaptureDefaultSeconds : seconds,
            CaptureMinSeconds,
            CaptureMaxSeconds);

        captureActive = true;
        captureEntityId = selected.entityId;
        captureRequesterPlayerId = requester != null ? requester.entityId : -1;
        captureEndRealtime = Time.realtimeSinceStartup + seconds;
        captureNextSampleRealtime = 0f;
        captureLastDecision = string.Empty;
        capturePostTick = ulong.MaxValue;
        captureSamples = 0;
        captureTransitions = 0;
        captureSelectedClass = GetEntityClassName(selected);

        EntityAlive target = selected.GetAttackTarget();
        string message = "[RebirthHybridCapture] START"
            + " entity=" + selected.entityId
            + " class=" + captureSelectedClass
            + " requester=" + captureRequesterPlayerId
            + " target=" + (target != null ? target.entityId.ToString() : "<none>")
            + " durationSeconds=" + seconds.ToString("0.0")
            + " enabled=" + Enabled
            + " debug=" + DebugLogging
            + " isServer=" + IsAuthoritativeServer()
            + " note=Capture logs PRE decisions and POST movement state for this entity.";

        return message;
    }

    public static string StopCapture(string reason)
    {
        if (!captureActive)
            return "[RebirthHybridCapture] inactive";

        string message = "[RebirthHybridCapture] END"
            + " entity=" + captureEntityId
            + " class=" + captureSelectedClass
            + " reason=" + (string.IsNullOrEmpty(reason) ? "manual" : reason)
            + " samples=" + captureSamples
            + " transitions=" + captureTransitions
            + " lastDecision=" + (string.IsNullOrEmpty(captureLastDecision) ? "<none>" : captureLastDecision)
            + " enabled=" + Enabled
            + " isServer=" + IsAuthoritativeServer();

        captureActive = false;
        captureEntityId = -1;
        captureRequesterPlayerId = -1;
        captureEndRealtime = 0f;
        captureNextSampleRealtime = 0f;
        captureLastDecision = string.Empty;
        capturePostTick = ulong.MaxValue;
        captureSelectedClass = string.Empty;
        return message;
    }

    public static void UpdateCaptureLifetime()
    {
        if (!captureActive)
            return;

        if (Time.realtimeSinceStartup >= captureEndRealtime)
            Log.Out(StopCapture("durationExpired"));
    }

    public static string GetCaptureStatus()
    {
        float remaining = captureActive
            ? Mathf.Max(0f, captureEndRealtime - Time.realtimeSinceStartup)
            : 0f;

        return "[RebirthHybridCapture] active=" + captureActive
            + " entity=" + captureEntityId
            + " class=" + (string.IsNullOrEmpty(captureSelectedClass) ? "<none>" : captureSelectedClass)
            + " requester=" + captureRequesterPlayerId
            + " remainingSeconds=" + remaining.ToString("0.0")
            + " samples=" + captureSamples
            + " transitions=" + captureTransitions
            + " lastDecision=" + (string.IsNullOrEmpty(captureLastDecision) ? "<none>" : captureLastDecision)
            + " smoothingEnabled=" + Enabled
            + " isServer=" + IsAuthoritativeServer();
    }

    public static string GetCaptureCandidates(EntityPlayer requester)
    {
        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        if (world == null)
            return "[RebirthHybridCapture] candidates world=<null>";

        Vector3 origin = requester != null ? requester.position : Vector3.zero;
        List<Entity> entities = new List<Entity>();
        world.GetEntitiesInBounds(
            typeof(EntityAlive),
            new Bounds(
                origin,
                new Vector3(
                    CaptureSearchRadius * 2f,
                    CaptureSearchRadius,
                    CaptureSearchRadius * 2f)),
            entities);

        string report = "[RebirthHybridCapture] candidates requester="
            + (requester != null ? requester.entityId.ToString() : "<none>")
            + " count=" + entities.Count;

        int written = 0;
        for (int i = 0; i < entities.Count && written < 12; i++)
        {
            EntityAlive entity = entities[i] as EntityAlive;
            if (!IsEligibleCaptureEntity(entity))
                continue;

            EntityAlive target = entity.GetAttackTarget();
            float distance = requester != null
                ? (entity.position - requester.position).magnitude
                : 0f;

            report += "\n  entity=" + entity.entityId
                + " class=" + GetEntityClassName(entity)
                + " distance=" + distance.ToString("0.0")
                + " target=" + (target != null ? target.entityId.ToString() : "<none>")
                + " targetClass=" + GetEntityClassName(target)
                + " moveActive=" + (entity.moveHelper != null && entity.moveHelper.IsActive);
            written++;
        }

        if (written == 0)
            report += "\n  <no eligible entities>";

        return report;
    }

    private static EntityAlive FindNearestCaptureEntity(World world, EntityPlayer requester)
    {
        if (world == null)
            return null;

        Vector3 origin = requester != null ? requester.position : Vector3.zero;
        List<Entity> entities = new List<Entity>();
        world.GetEntitiesInBounds(
            typeof(EntityAlive),
            new Bounds(
                origin,
                new Vector3(
                    CaptureSearchRadius * 2f,
                    CaptureSearchRadius,
                    CaptureSearchRadius * 2f)),
            entities);

        EntityAlive best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < entities.Count; i++)
        {
            EntityAlive candidate = entities[i] as EntityAlive;
            if (!IsEligibleCaptureEntity(candidate))
                continue;

            float score = requester != null
                ? (candidate.position - requester.position).sqrMagnitude
                : candidate.entityId;

            EntityAlive attackTarget = candidate.GetAttackTarget();
            if (requester != null && attackTarget == requester)
                score -= 100000f;

            if (candidate is EntityZombie)
                score -= 10000f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static bool IsEligibleCaptureEntity(EntityAlive entity)
    {
        return entity != null
            && !entity.IsDead()
            && !(entity is EntityPlayer);
    }

    private static bool IsAuthoritativeServer()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer;
    }

    [System.Diagnostics.Conditional("REBIRTH_HYBRID_DETAILED_CAPTURE")]
    private static void CaptureDecision(
        EntityMoveHelper moveHelper,
        EntityAlive entity,
        EntityAlive target,
        PathEntity path,
        string decision,
        string detail)
    {
        if (!captureActive || entity == null || entity.entityId != captureEntityId)
            return;

        float now = Time.realtimeSinceStartup;
        if (now >= captureEndRealtime)
        {
            Log.Out(StopCapture("durationExpired"));
            return;
        }

        bool transition = !string.Equals(captureLastDecision, decision, StringComparison.Ordinal);
        bool sampleDue = now >= captureNextSampleRealtime;
        if (!transition && !sampleDue)
            return;

        if (transition)
            captureTransitions++;

        captureLastDecision = decision ?? "<null>";
        captureNextSampleRealtime = now + CaptureSampleIntervalSeconds;
        captureSamples++;

        ulong tick = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        capturePostTick = tick;

        LogCaptureLine("PRE", moveHelper, entity, target, path, decision, detail);
    }

    private static void LogCaptureLine(
        string phase,
        EntityMoveHelper moveHelper,
        EntityAlive entity,
        EntityAlive target,
        PathEntity path,
        string decision,
        string detail)
    {
        if (entity == null)
            return;

        ulong tick = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        Vector3 targetVelocity = target != null ? target.GetVelocityPerSecond() : Vector3.zero;
        Vector3 directTarget = target != null
            ? target.position + targetVelocity * MotionPredictionSeconds
            : Vector3.zero;

        string pathInfo = GetPathCaptureInfo(entity, directTarget, path);
        string clearanceInfo = GetClearanceCaptureInfo(entity, target);
        string stateInfo = GetDirectStateCaptureInfo(entity);
        string specialInfo = moveHelper != null
            ? GetSpecialMovementFlags(entity, moveHelper)
            : "<noMoveHelper>";

        float distance = target != null
            ? (target.position - entity.position).magnitude
            : -1f;

        Log.Out("[RebirthHybridCapture]"
            + " phase=" + phase
            + " tick=" + tick
            + " decision=" + (string.IsNullOrEmpty(decision) ? "<none>" : decision)
            + " detail=" + SanitizeCaptureValue(detail)
            + " entity=" + entity.entityId
            + " class=" + GetEntityClassName(entity)
            + " entityPos=" + FormatVector(entity.position)
            + " entityTransform=" + FormatVector(entity.transform.position)
            + " yaw=" + entity.rotation.y.ToString("0.0")
            + " target=" + (target != null ? target.entityId.ToString() : "<none>")
            + " targetClass=" + GetEntityClassName(target)
            + " targetPos=" + (target != null ? FormatVector(target.position) : "<none>")
            + " targetTransform=" + (target != null ? FormatVector(target.transform.position) : "<none>")
            + " targetVelocity=" + FormatVector(targetVelocity)
            + " directTarget=" + (target != null ? FormatVector(directTarget) : "<none>")
            + " distance=" + distance.ToString("0.00")
            + " enabled=" + Enabled
            + " isServer=" + IsAuthoritativeServer()
            + " helperActive=" + (moveHelper != null && moveHelper.IsActive)
            + " helperMoveTo=" + (moveHelper != null ? FormatVector(moveHelper.moveToPos) : "<none>")
            + " helperSpeed=" + (moveHelper != null ? moveHelper.moveSpeed.ToString("0.00") : "<none>")
            + " helperExpiry=" + (moveHelper != null ? moveHelper.expiryTicks.ToString() : "<none>")
            + " canBreak=" + (moveHelper != null && moveHelper.CanBreakBlocks)
            + " special={" + specialInfo + "}"
            + " state={" + stateInfo + "}"
            + " path={" + pathInfo + "}"
            + " clearance={" + clearanceInfo + "}");
    }

    private static string GetPathCaptureInfo(EntityAlive entity, Vector3 directTarget, PathEntity path)
    {
        if (path == null)
            return "path=<null>";

        if (path.points == null)
            return "points=<null> length=" + path.pathLength;

        int startIndex = Math.Max(0, path.currentPathIndex);
        int endIndex = Math.Min(path.pathLength - 1, path.points.Length - 1);
        Vector3 endPos = path.GetEndPos();
        Vector3 currentPoint = startIndex >= 0 && startIndex < path.points.Length
            ? path.points[startIndex].projectedLocation
            : Vector3.zero;

        float maxDeviation = -1f;
        int maxDeviationIndex = -1;
        if (entity != null && startIndex <= endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                float deviation = Mathf.Sqrt(
                    DistancePointToSegmentXZSqr(
                        path.points[i].projectedLocation,
                        entity.position,
                        directTarget));

                if (deviation > maxDeviation)
                {
                    maxDeviation = deviation;
                    maxDeviationIndex = i;
                }
            }
        }

        Vector3 endDelta = endPos - directTarget;
        endDelta.y = 0f;

        return "length=" + path.pathLength
            + " pointsLength=" + path.points.Length
            + " current=" + path.currentPathIndex
            + " finished=" + path.isFinished()
            + " currentPoint=" + FormatVector(currentPoint)
            + " end=" + FormatVector(endPos)
            + " endTargetXZ=" + Mathf.Sqrt(endDelta.sqrMagnitude).ToString("0.00")
            + " maxDeviation=" + maxDeviation.ToString("0.00")
            + " maxDeviationIndex=" + maxDeviationIndex;
    }

    private static string GetClearanceCaptureInfo(EntityAlive entity, EntityAlive target)
    {
        if (entity == null || target == null)
            return "result=notEvaluated";

        RaycastHit hit;
        float castDistance;
        float castRadius;
        bool clear = HasBodyClearance(entity, target, out hit, out castDistance, out castRadius);

        return "clear=" + clear
            + " " + FormatHitDetails(hit, castDistance, castRadius);
    }

    private static string GetDirectStateCaptureInfo(EntityAlive entity)
    {
        if (entity == null)
            return "state=<null>";

        DirectMoveState state;
        if (!States.TryGetValue(entity.entityId, out state) || state == null)
            return "tracked=false";

        ulong now = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        return "tracked=true"
            + " direct=" + state.IsDirect
            + " target=" + state.TargetEntityId
            + " cooldownRemaining="
            + (state.NoDirectUntilTick > now
                ? (state.NoDirectUntilTick - now).ToString()
                : "0");
    }

    private static string GetSpecialMovementFlags(EntityAlive entity, EntityMoveHelper moveHelper)
    {
        if (entity == null || moveHelper == null)
            return "<null>";

        return "jumping=" + entity.Jumping
            + ",swimming=" + entity.isSwimming
            + ",elevator=" + entity.IsInElevator()
            + ",climb=" + moveHelper.isClimb
            + ",tempMove=" + moveHelper.isTempMove
            + ",unreachableAbove=" + moveHelper.IsUnreachableAbove
            + ",unreachableSide=" + moveHelper.IsUnreachableSide
            + ",unreachableSideJump=" + moveHelper.IsUnreachableSideJump
            + ",sideStepAngle=" + moveHelper.SideStepAngle.ToString("0.00");
    }

    private static string GetEntityClassName(EntityAlive entity)
    {
        if (entity == null)
            return "<none>";

        return entity.EntityClass != null
            ? entity.EntityClass.entityClassName
            : entity.GetType().Name;
    }

    private static string FormatVector(Vector3 value)
    {
        return "("
            + value.x.ToString("0.000") + ","
            + value.y.ToString("0.000") + ","
            + value.z.ToString("0.000") + ")";
    }

    private static string SanitizeCaptureValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "<none>";

        return value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
    }

#endif

    private static bool IsSpecialMovementActive(EntityAlive entity, EntityMoveHelper moveHelper)
    {
        return entity.Jumping
            || entity.isSwimming
            || entity.IsInElevator()
            || moveHelper.isClimb
            || moveHelper.isTempMove
            || moveHelper.IsUnreachableAbove
            || moveHelper.IsUnreachableSide
            || moveHelper.IsUnreachableSideJump
            || Math.Abs(moveHelper.SideStepAngle) > 0.001f;
    }

    private static bool HasSafeLiveVerticalDelta(
        EntityAlive entity,
        Vector3 directTarget
#if DEBUG
        , out string reason
#endif
        )
    {
#if DEBUG
        reason = null;
#endif

        float totalDelta = directTarget.y - entity.position.y;
        if (Mathf.Abs(totalDelta) <= MaxTotalVerticalDelta)
            return true;

#if DEBUG
        reason = "totalY=" + totalDelta.ToString("0.00");
#endif
        return false;
    }

    private static bool HasSafePathVerticalSegments(
        EntityAlive entity,
        PathEntity path
#if DEBUG
        , out string reason
#endif
        )
    {
#if DEBUG
        reason = null;
#endif

        int startIndex = Math.Max(0, path.currentPathIndex);
        int endIndex = Math.Min(path.pathLength - 1, path.points.Length - 1);
        if (startIndex > endIndex)
        {
#if DEBUG
            reason = "invalidVerticalPathRange";
#endif
            return false;
        }

        Vector3 previous = entity.position;
        for (int i = startIndex; i <= endIndex; i++)
        {
            Vector3 next = path.points[i].projectedLocation;
            float segmentDelta = next.y - previous.y;
            if (Mathf.Abs(segmentDelta) > MaxPathSegmentVerticalDelta)
            {
#if DEBUG
                reason = "pathY index=" + i
                    + " delta=" + segmentDelta.ToString("0.00");
#endif
                return false;
            }
            previous = next;
        }

        return true;
    }

    private static bool RequiresVerticalPathQualification(
        DirectMoveState state,
        PathEntity path)
    {
        if (state == null || path == null || !state.HasVerticalQualification)
            return true;

        int signature = ComputePathGeometrySignature(path);
        return state.VerticalQualifiedPathSignature != signature;
    }

    private static void MarkVerticalPathQualified(
        DirectMoveState state,
        PathEntity path,
        int pathSignature)
    {
        state.VerticalQualifiedPathSignature = pathSignature;
        state.VerticalQualifiedPathIndex = path.currentPathIndex;
        state.HasVerticalQualification = true;
    }

    private static void ClearVerticalQualification(DirectMoveState state)
    {
        if (state == null)
            return;

        state.VerticalQualifiedPathSignature = 0;
        state.VerticalQualifiedPathIndex = 0;
        state.HasVerticalQualification = false;
    }

    private static int ComputePathGeometrySignature(PathEntity path)
    {
        if (path == null || path.points == null)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + path.pathLength;
            hash = hash * 31 + path.currentPathIndex;
            int end = Math.Min(path.pathLength, path.points.Length);
            for (int i = 0; i < end; i++)
            {
                Vector3 point = path.points[i].projectedLocation;
                hash = hash * 31 + point.x.GetHashCode();
                hash = hash * 31 + point.y.GetHashCode();
                hash = hash * 31 + point.z.GetHashCode();
            }
            return hash;
        }
    }

    private static void EnsureWorldIdentity(World world)
    {
        if (ReferenceEquals(stateWorld, world))
            return;

        stateWorld = world;
        States.Clear();
    }

    private static bool ShouldRunStaggeredClearanceCheck(
        int entityId,
        ulong now)
    {
        // Two-tick cadence, offset by entity ID, avoids concentrating all
        // physics queries on the same game tick.
        ulong interval = (ulong)ClearanceCheckIntervalTicks;
        ulong stagger = (ulong)(entityId & int.MaxValue) % interval;
        return (now + stagger) % interval == 0UL;
    }

    /// <summary>
    /// Uses the current native route as an entry-level graph guard. The route is
    /// evaluated against its own endpoint, because a path endpoint naturally
    /// lags behind a moving attack target. Comparing old path nodes with the
    /// player's newest predicted position creates false detours in open ground.
    /// </summary>
    private static bool NativeRouteSupportsStraightApproach(
        EntityAlive entity,
        PathEntity path
#if DEBUG
        , out string reason
#endif
        )
    {
#if DEBUG
        reason = null;
#endif

        int startIndex = Math.Max(0, path.currentPathIndex);
        int endIndex = Math.Min(path.pathLength - 1, path.points.Length - 1);
        if (startIndex > endIndex)
        {
#if DEBUG
            reason = "invalidPathRange";
#endif
            return false;
        }

        Vector3 from = entity.position;
        Vector3 routeEnd = path.GetEndPos();
        float maxDeviationSq =
            MaxInitialNativeRouteDeviation * MaxInitialNativeRouteDeviation;

        for (int i = startIndex; i <= endIndex; i++)
        {
            Vector3 point = path.points[i].projectedLocation;
            float deviationSq = DistancePointToSegmentXZSqr(point, from, routeEnd);
            if (deviationSq > maxDeviationSq)
            {
#if DEBUG
                reason = "nativeDetour index=" + i
                    + " deviation=" + Mathf.Sqrt(deviationSq).ToString("0.00")
                    + " max=" + MaxInitialNativeRouteDeviation.ToString("0.00")
                    + " routeEnd=" + FormatVector(routeEnd);
#endif
                return false;
            }
        }

        return true;
    }

    private static float DistancePointToSegmentXZSqr(Vector3 point, Vector3 start, Vector3 end)
    {
        float segmentX = end.x - start.x;
        float segmentZ = end.z - start.z;
        float lengthSq = segmentX * segmentX + segmentZ * segmentZ;
        if (lengthSq <= 0.0001f)
        {
            float dx0 = point.x - start.x;
            float dz0 = point.z - start.z;
            return dx0 * dx0 + dz0 * dz0;
        }

        float t = ((point.x - start.x) * segmentX
            + (point.z - start.z) * segmentZ) / lengthSq;
        t = Mathf.Clamp01(t);

        float nearestX = start.x + segmentX * t;
        float nearestZ = start.z + segmentZ * t;
        float dx = point.x - nearestX;
        float dz = point.z - nearestZ;
        return dx * dx + dz * dz;
    }

    private static bool HasBodyClearance(
        EntityAlive entity,
        EntityAlive target
#if DEBUG
        , out RaycastHit hit
        , out float castDistance
        , out float castRadius
#endif
        )
    {
#if DEBUG
        hit = new RaycastHit();
        castDistance = 0f;
        castRadius = 0f;
#else
        RaycastHit hit = new RaycastHit();
        float castDistance = 0f;
        float castRadius = 0f;
#endif

        if (entity == null || target == null || entity.m_characterController == null)
            return false;

        float controllerRadius = entity.m_characterController.GetRadius();
        float controllerHeight = entity.m_characterController.GetHeight();
        castRadius = Mathf.Max(0.08f, controllerRadius * RadiusScale + RadiusMargin);

        // Match the established direct-mover clearance test: cast a body-width
        // sphere from mid-body in Unity/rebased transform space. A full capsule
        // whose bottom rides the terrain surface can intermittently collide with
        // the terrain mesh itself while crossing otherwise open, level ground.
        Vector3 from = entity.transform.position
            + Vector3.up * controllerHeight * 0.5f;
        Vector3 to = target.transform.position
            + Vector3.up * target.GetHeight() * 0.5f;
        Vector3 delta = to - from;
        castDistance = delta.magnitude;

        if (castDistance <= 0.05f)
            return true;

        Vector3 direction = delta / castDistance;
#if DEBUG
        long benchmarkTimestamp =
            RebirthHybridPathPerformanceBenchmark.BeginClearanceQuery();
#endif
        bool hitSomething = Physics.SphereCast(
            from,
            castRadius,
            direction,
            out hit,
            castDistance,
            CollisionMask,
            QueryTriggerInteraction.Ignore);
#if DEBUG
        RebirthHybridPathPerformanceBenchmark.EndClearanceQuery(
            benchmarkTimestamp,
            hitSomething);
#endif
        return !hitSomething;
    }

    private static bool IsDirectMoverStuck(
        EntityAlive entity,
        Vector3 directTarget,
        DirectMoveState state,
        ulong now)
    {
        if (!state.IsDirect)
            return false;

        if (now - state.LastStuckCheckTick < (ulong)StuckCheckIntervalTicks)
            return false;

        float moved = (entity.position - state.LastPosition).magnitude;
        float remaining = (directTarget - entity.position).magnitude;

        state.LastPosition = entity.position;
        state.LastStuckCheckTick = now;

        return remaining > StuckMinRemainingDistance
            && moved < StuckMinMoveDistance;
    }

    private static float GetSafeAttackRange(EntityAlive entity)
    {
        if (entity == null
            || entity.inventory == null
            || entity.inventory.holdingItem == null
            || entity.inventory.holdingItem.Actions == null
            || entity.inventory.holdingItem.Actions.Length == 0
            || entity.inventory.holdingItem.Actions[0] == null)
        {
            return FallbackAttackRange;
        }

        return Mathf.Max(0.1f, entity.inventory.holdingItem.Actions[0].Range);
    }

    private static void FallBackToNative(
        EntityAlive entity,
        DirectMoveState state,
        ulong now)
    {
        if (state == null)
            return;

        ApplyCooldown(entity, state, now);
    }

    private static void ApplyCooldown(
        EntityAlive entity,
        DirectMoveState state,
        ulong now)
    {
        state.IsDirect = false;
        state.NoDirectUntilTick = now + (ulong)NoDirectCooldownTicks;
        state.LastPosition = entity.position;
        state.LastStuckCheckTick = now;
    }

#if DEBUG
    [System.Diagnostics.Conditional("REBIRTH_HYBRID_VERBOSE_LOG")]
    private static void DebugLogFallback(EntityAlive entity, string reason)
    {
        if (!DebugLogging || entity == null)
            return;

        Log.Out("[RebirthHybridPath] fallback entity=" + entity.entityId
            + " reason=" + reason
            + " cooldownTicks=" + NoDirectCooldownTicks);
    }
#endif

    private static void RemoveDirectFlag(EntityAlive entity)
    {
        if (entity == null)
            return;

        DirectMoveState state;
        if (States.TryGetValue(entity.entityId, out state)
            && ReferenceEquals(state.EntityIdentity, entity))
            state.IsDirect = false;
    }

    private static void RemoveState(EntityAlive entity)
    {
        if (entity == null)
            return;

        DirectMoveState state;
        if (States.TryGetValue(entity.entityId, out state)
            && ReferenceEquals(state.EntityIdentity, entity))
            States.Remove(entity.entityId);
    }

#if DEBUG
    private static string FormatHitDetails(RaycastHit hit, float distance, float radius)
    {
        if (hit.collider == null)
            return "hit=<none> distance=" + distance.ToString("0.00")
                + " radius=" + radius.ToString("0.00");

        return "hit=" + hit.collider.name
            + " layer=" + hit.collider.gameObject.layer
            + " hitDistance=" + hit.distance.ToString("0.00")
            + " castDistance=" + distance.ToString("0.00")
            + " radius=" + radius.ToString("0.00");
    }

    public static RebirthHybridPathCounterSnapshot GetCounterSnapshot()
    {
        return new RebirthHybridPathCounterSnapshot
        {
            Evaluated = evaluated,
            Activated = activated,
            DirectUpdates = directUpdates,
            Blocked = rejectedBlocked,
            Vertical = rejectedVertical,
            Route = rejectedRoute,
            SpecialMove = rejectedSpecialMove,
            Cooldown = rejectedCooldown,
            NoPath = rejectedNoPath,
            NoTarget = rejectedNoTarget,
            Stuck = rejectedStuck,
            StopRange = stoppedInRange,
            ClearanceSkipped = clearanceSkipped,
            VerticalPathScans = verticalPathScans,
            VerticalPathCacheHits = verticalPathCacheHits
        };
    }

    public static string GetStatusReport()
    {
        return "[RebirthHybridPath] enabled=" + Enabled
            + " debug=" + DebugLogging
            + " tracked=" + States.Count
            + " evaluated=" + evaluated
            + " activated=" + activated
            + " directUpdates=" + directUpdates
            + " blocked=" + rejectedBlocked
            + " vertical=" + rejectedVertical
            + " route=" + rejectedRoute
            + " specialMove=" + rejectedSpecialMove
            + " cooldown=" + rejectedCooldown
            + " noPath=" + rejectedNoPath
            + " noTarget=" + rejectedNoTarget
            + " stuck=" + rejectedStuck
            + " stopRange=" + stoppedInRange
            + " clearanceSkipped=" + clearanceSkipped
            + " verticalPathScans=" + verticalPathScans
            + " verticalPathCacheHits=" + verticalPathCacheHits;
    }

    public static void ResetCounters()
    {
        evaluated = 0;
        activated = 0;
        directUpdates = 0;
        rejectedBlocked = 0;
        rejectedVertical = 0;
        rejectedRoute = 0;
        rejectedSpecialMove = 0;
        rejectedCooldown = 0;
        rejectedNoPath = 0;
        rejectedNoTarget = 0;
        rejectedStuck = 0;
        stoppedInRange = 0;
        clearanceSkipped = 0;
        verticalPathScans = 0;
        verticalPathCacheHits = 0;
    }
#endif

}

#if DEBUG
[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
internal static class RebirthHybridPathCaptureLifetimePatch
{
    private static void Postfix()
    {
        RebirthHybridPathSmoothing.UpdateCaptureLifetime();
    }
}
#endif

public static class RebirthHybridPathSmoothingInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed)
            return;

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.hybrid-path-smoothing.3.1"), typeof(RebirthHybridPathSmoothingPatch));

            installed = true;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[RebirthHybridPath] Installed direct-target hybrid path smoothing."); }
        }
        catch (Exception ex)
        {
            installed = false;
            Log.Error("[RebirthHybridPath] Failed to install runtime patch: " + ex);
        }
    }
}

/// <summary>
/// Independent bootstrap prevents an unrelated shared-bootstrap failure from
/// leaving the option visible while its runtime patch is absent.
/// </summary>
public sealed class RebirthHybridPathSmoothingBootstrap : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        RebirthHybridPathSmoothingInstaller.Install();
    }
}

[HarmonyPatch(typeof(EntityMoveHelper), nameof(EntityMoveHelper.UpdateMoveHelper))]
internal static class RebirthHybridPathSmoothingPatch
{
    private static void Prefix(EntityMoveHelper __instance)
    {
#if DEBUG
        long benchmarkTimestamp =
            RebirthHybridPathPerformanceBenchmark.BeginPathEvaluation();
        try
        {
            RebirthHybridPathSmoothing.BeforeUpdateMoveHelper(__instance);
        }
        finally
        {
            RebirthHybridPathPerformanceBenchmark.EndPathEvaluation(
                benchmarkTimestamp,
                __instance);
        }
#else
        RebirthHybridPathSmoothing.BeforeUpdateMoveHelper(__instance);
#endif
    }
}
