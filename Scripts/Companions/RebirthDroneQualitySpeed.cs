using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>
/// Scales normal Follow travel from the displacement that vanilla EntityDrone actually
/// completed during its own update, without changing SpeedFlying/currentSpeedFlying,
/// steering, acceleration/deceleration, path requests, move() arguments, or tether logic.
///
/// Runtime traces from the previous two approaches showed why those hooks were wrong:
/// multiplying move() impulses caused overshoot/reversal, while raising SpeedFlying made
/// the native acceleration/deceleration controller burst and stall.  This implementation
/// leaves that controller completely vanilla.  Quality adds only a small collision-checked
/// continuation along a movement step that vanilla already completed successfully.
///
/// Quality 1 is exactly vanilla. Quality 6 is 2x at normal cruise.  Long-distance native
/// catch-up remains primarily controlled by vanilla and the extra continuation is bounded,
/// preventing large per-tick jumps near path nodes or the owner.
/// </summary>
public static class RebirthDroneQualitySpeed
{
    private const float MaximumAppliedMultiplier = 2f;
    private const float MinimumBoostOwnerDistance = 6f;
    private const float MaximumNativeStepForBoost = 1f;
    private const float MaximumExtraStep = 0.25f;
    private const float MinimumStep = 0.001f;
    private const float PathNodeMargin = 0.05f;
    private const int CollisionMask = 1073807360; // EntityDrone's native movement mask (0x40010000).

    public sealed class TelemetrySnapshot
    {
        // Native move() observation (never mutated by Rebirth).
        public long MovePatchCalls;
        public float LastBaseSpeed;
        public float LastScaledSpeed;
        public float LastMultiplier = 1f;
        public long LastUtcTicks;

        // Displacement boost diagnostics.
        public long BoostEvaluationCalls;
        public long BoostApplyCalls;
        public long BoostSkippedRemote;
        public long BoostSkippedTether;
        public long BoostSkippedState;
        public long BoostSkippedNearOwner;
        public long BoostSkippedNoMovement;
        public long BoostSkippedTeleport;
        public long BoostSkippedDirection;
        public long BoostSkippedPathNode;
        public long BoostSkippedBlocked;
        public float LastNativeStep;
        public float LastExtraStep;
        public float BaseCruiseSpeed = 3f;
        public float RequestedMultiplier = 1f;
        public float AppliedMultiplier = 1f;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, float> DebugOverrides = new Dictionary<int, float>();
    private static readonly Dictionary<int, TelemetrySnapshot> Telemetry = new Dictionary<int, TelemetrySnapshot>();

    private static bool telemetryEnabled;
    private static float telemetryDeadline;
    private static float nextTelemetryPrune;
    private const int MaximumTelemetryRecords = 256;
    public static bool TelemetryActive
    {
        get { return telemetryEnabled && Time.realtimeSinceStartup < telemetryDeadline; }
    }
    public static void StartTelemetry(float seconds = 30f)
    {
        lock (Sync)
        {
            Telemetry.Clear();
            telemetryEnabled = true;
            telemetryDeadline = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 1f, 60f);
            nextTelemetryPrune = 0f;
        }
        Log.Out("[RebirthDroneSpeed] movement telemetry enabled for at most 60 seconds; movement policy unchanged.");
    }
    public static void StopTelemetry()
    {
        lock (Sync)
        {
            telemetryEnabled = false;
            telemetryDeadline = nextTelemetryPrune = 0f;
            Telemetry.Clear();
        }
    }
    public static void TickTelemetry()
    {
        if (!telemetryEnabled) return;
        if (!TelemetryActive) { StopTelemetry(); return; }
        float now = Time.realtimeSinceStartup;
        if (now < nextTelemetryPrune) return;
        nextTelemetryPrune = now + 1f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) { StopTelemetry(); return; }
        lock (Sync)
        {
            List<int> removed = null;
            foreach (int id in Telemetry.Keys)
            {
                if (world.GetEntity(id) is EntityDrone) continue;
                if (removed == null) removed = new List<int>();
                removed.Add(id);
            }
            if (removed != null) foreach (int id in removed) Telemetry.Remove(id);
        }
    }

    public static int GetQuality(EntityDrone drone)
    {
        if (drone == null || drone.OriginalItemValue == null)
            return 1;
        return Mathf.Clamp((int)drone.OriginalItemValue.Quality, 1, 6);
    }

    public static float GetQualityMultiplier(EntityDrone drone)
    {
        int quality = GetQuality(drone);
        return 1f + (quality - 1) * 0.2f;
    }

    /// <summary>Requested multiplier: debug override when present, otherwise quality.</summary>
    public static float GetMultiplier(EntityDrone drone)
    {
        if (drone == null)
            return 1f;

        lock (Sync)
        {
            float value;
            if (DebugOverrides.TryGetValue(drone.entityId, out value))
                return value;
        }
        return GetQualityMultiplier(drone);
    }

    public static float GetAppliedMultiplier(EntityDrone drone)
    {
        return Mathf.Clamp(GetMultiplier(drone), 1f, MaximumAppliedMultiplier);
    }

    public static bool TryGetDebugOverride(int entityId, out float multiplier)
    {
        lock (Sync)
            return DebugOverrides.TryGetValue(entityId, out multiplier);
    }

    public static void SetDebugOverride(int entityId, float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 1f, MaximumAppliedMultiplier);
        lock (Sync)
            DebugOverrides[entityId] = multiplier;
    }

    public static void ClearDebugOverride(int entityId)
    {
        lock (Sync)
            DebugOverrides.Remove(entityId);
    }

    public static TelemetrySnapshot GetTelemetry(int entityId)
    {
        lock (Sync)
        {
            TelemetrySnapshot source;
            if (!Telemetry.TryGetValue(entityId, out source) || source == null)
                return new TelemetrySnapshot();
            return Copy(source);
        }
    }

    private static TelemetrySnapshot Copy(TelemetrySnapshot source)
    {
        return new TelemetrySnapshot
        {
            MovePatchCalls = source.MovePatchCalls,
            LastBaseSpeed = source.LastBaseSpeed,
            LastScaledSpeed = source.LastScaledSpeed,
            LastMultiplier = source.LastMultiplier,
            LastUtcTicks = source.LastUtcTicks,
            BoostEvaluationCalls = source.BoostEvaluationCalls,
            BoostApplyCalls = source.BoostApplyCalls,
            BoostSkippedRemote = source.BoostSkippedRemote,
            BoostSkippedTether = source.BoostSkippedTether,
            BoostSkippedState = source.BoostSkippedState,
            BoostSkippedNearOwner = source.BoostSkippedNearOwner,
            BoostSkippedNoMovement = source.BoostSkippedNoMovement,
            BoostSkippedTeleport = source.BoostSkippedTeleport,
            BoostSkippedDirection = source.BoostSkippedDirection,
            BoostSkippedPathNode = source.BoostSkippedPathNode,
            BoostSkippedBlocked = source.BoostSkippedBlocked,
            LastNativeStep = source.LastNativeStep,
            LastExtraStep = source.LastExtraStep,
            BaseCruiseSpeed = source.BaseCruiseSpeed,
            RequestedMultiplier = source.RequestedMultiplier,
            AppliedMultiplier = source.AppliedMultiplier
        };
    }

    private static TelemetrySnapshot GetOrCreateTelemetryLocked(EntityDrone drone, float requested, float applied)
    {
        TelemetrySnapshot telemetry;
        if (!Telemetry.TryGetValue(drone.entityId, out telemetry) || telemetry == null)
        {
            if (Telemetry.Count >= MaximumTelemetryRecords) return null;
            telemetry = new TelemetrySnapshot();
            Telemetry[drone.entityId] = telemetry;
        }
        telemetry.BaseCruiseSpeed = Mathf.Max(0.1f, drone.SpeedFlying);
        telemetry.RequestedMultiplier = requested;
        telemetry.AppliedMultiplier = applied;
        telemetry.LastMultiplier = applied;
        telemetry.LastUtcTicks = DateTime.UtcNow.Ticks;
        return telemetry;
    }

    /// <summary>
    /// Called after EntityDrone.OnUpdateEntity.  startPosition is the position at entry.
    /// The vanilla update has already performed steering/path selection/collision and moved
    /// the drone by nativeStep. We may extend that successful step by a small amount.
    /// </summary>
    public static void ApplyNativeDisplacementBoost(EntityDrone drone, Vector3 startPosition)
    {
        if (drone == null)
            return;

        float requested = GetMultiplier(drone);
        float applied = Mathf.Clamp(requested, 1f, MaximumAppliedMultiplier);
        Vector3 endPosition = drone.position;
        Vector3 nativeDelta = endPosition - startPosition;
        float nativeDistance = nativeDelta.magnitude;

        if (TelemetryActive) lock (Sync)
        {
            TelemetrySnapshot telemetry = GetOrCreateTelemetryLocked(drone, requested, applied);
            if (telemetry != null)
            {
                telemetry.BoostEvaluationCalls++;
                telemetry.LastNativeStep = nativeDistance;
                telemetry.LastExtraStep = 0f;
            }
        }

        // Multiplier 1 is intentionally a byte-for-byte vanilla movement path: no position,
        // speed, path, motion, or interpolation fields are changed.
        if (applied <= 1.0001f)
            return;

        if (drone.isEntityRemote || DroneManager.Debug_LocalControl)
        {
            IncrementSkip(drone.entityId, 0);
            return;
        }

        if (RebirthDroneLockToPlayerService.IsLockedToPlayer(drone))
        {
            IncrementSkip(drone.entityId, 1);
            return;
        }

        // Quality travel scaling is deliberately limited to normal Follow flight. Attack,
        // Sentry, Heal, Shutdown, and other specialist states keep exact vanilla movement.
        if (drone.OrderState != EntityDrone.Orders.Follow || drone.GetState() != EntityDrone.State.Follow)
        {
            IncrementSkip(drone.entityId, 2);
            return;
        }

        EntityAlive owner = drone.Owner;
        if (owner == null || owner.IsDead())
        {
            IncrementSkip(drone.entityId, 2);
            return;
        }

        float ownerDistance = Vector3.Distance(endPosition, owner.getChestPosition());
        if (ownerDistance <= MinimumBoostOwnerDistance)
        {
            IncrementSkip(drone.entityId, 3);
            return;
        }

        if (nativeDistance <= MinimumStep)
        {
            IncrementSkip(drone.entityId, 4);
            return;
        }

        // Teleports/stuck recovery are not speed steps and must never be multiplied.
        if (nativeDistance > MaximumNativeStepForBoost)
        {
            IncrementSkip(drone.entityId, 5);
            return;
        }

        Vector3 direction = nativeDelta / nativeDistance;
        Vector3 toOwner = owner.getChestPosition() - endPosition;
        if (toOwner.sqrMagnitude > 0.01f && Vector3.Dot(direction, toOwner.normalized) <= 0f)
        {
            IncrementSkip(drone.entityId, 6);
            return;
        }

        float extraDistance = Mathf.Min(nativeDistance * (applied - 1f), MaximumExtraStep);
        if (extraDistance <= MinimumStep)
            return;

        // When following an explicit path, never extend the step past the next native node.
        if (drone.currentPath != null && drone.currentPath.Count > 0)
        {
            Vector3 toNode = drone.currentPath[0] - endPosition;
            float along = Vector3.Dot(toNode, direction);
            if (along <= PathNodeMargin)
            {
                IncrementSkip(drone.entityId, 7);
                return;
            }
            extraDistance = Mathf.Min(extraDistance, Mathf.Max(0f, along - PathNodeMargin));
            if (extraDistance <= MinimumStep)
            {
                IncrementSkip(drone.entityId, 7);
                return;
            }
        }

        Vector3 proposed = endPosition + direction * extraDistance;
        if (EntityDrone.IsPositionBlocked(endPosition, proposed, CollisionMask, false))
        {
            IncrementSkip(drone.entityId, 8);
            return;
        }

        // SetPosition is intentional here. We do not call entityCollision a second time in
        // the same tick because that mutates CharacterController/ySize bookkeeping. The
        // tiny continuation has already been ray-validated and follows the exact native step.
        drone.SetPosition(proposed, true);

        if (TelemetryActive) lock (Sync)
        {
            TelemetrySnapshot telemetry = GetOrCreateTelemetryLocked(drone, requested, applied);
            if (telemetry != null)
            {
                telemetry.BoostApplyCalls++;
                telemetry.LastExtraStep = extraDistance;
            }
        }
    }

    // index: 0 remote/debug, 1 tether, 2 state/owner, 3 near, 4 no movement,
    // 5 teleport, 6 wrong direction, 7 path node, 8 blocked.
    private static void IncrementSkip(int entityId, int index)
    {
        if (!TelemetryActive) return;
        lock (Sync)
        {
            TelemetrySnapshot telemetry;
            if (!Telemetry.TryGetValue(entityId, out telemetry) || telemetry == null)
            {
                if (Telemetry.Count >= MaximumTelemetryRecords) return;
                telemetry = new TelemetrySnapshot();
                Telemetry[entityId] = telemetry;
            }
            switch (index)
            {
                case 0: telemetry.BoostSkippedRemote++; break;
                case 1: telemetry.BoostSkippedTether++; break;
                case 2: telemetry.BoostSkippedState++; break;
                case 3: telemetry.BoostSkippedNearOwner++; break;
                case 4: telemetry.BoostSkippedNoMovement++; break;
                case 5: telemetry.BoostSkippedTeleport++; break;
                case 6: telemetry.BoostSkippedDirection++; break;
                case 7: telemetry.BoostSkippedPathNode++; break;
                case 8: telemetry.BoostSkippedBlocked++; break;
            }
        }
    }

    /// <summary>Observes native move requests without modifying their arguments.</summary>
    internal static void ObserveMove(EntityDrone drone, float speedFlying)
    {
        if (!TelemetryActive) return;
        if (drone == null)
            return;

        float requested = GetMultiplier(drone);
        float applied = Mathf.Clamp(requested, 1f, MaximumAppliedMultiplier);
        if (TelemetryActive) lock (Sync)
        {
            TelemetrySnapshot telemetry = GetOrCreateTelemetryLocked(drone, requested, applied);
            if (telemetry == null) return;
            telemetry.MovePatchCalls++;
            telemetry.LastBaseSpeed = speedFlying;
            telemetry.LastScaledSpeed = speedFlying;
        }
    }

    public static string GetDiagnosticReport(EntityDrone drone)
    {
        if (drone == null)
            return "[RebirthDroneSpeed] No drone resolved.";

        int quality = GetQuality(drone);
        float qualityMultiplier = GetQualityMultiplier(drone);
        float debugMultiplier;
        bool hasOverride = TryGetDebugOverride(drone.entityId, out debugMultiplier);
        float requested = hasOverride ? debugMultiplier : qualityMultiplier;
        float applied = Mathf.Clamp(requested, 1f, MaximumAppliedMultiplier);
        TelemetrySnapshot telemetry = GetTelemetry(drone.entityId);
        float velocity = drone.GetVelocityPerSecond().magnitude;

        return "[RebirthDroneSpeed] TelemetryActive=" + TelemetryActive + " Entity=" + drone.entityId +
               " Quality=" + quality + "/6" +
               " QualityMultiplier=" + qualityMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x" +
               " DebugOverride=" + (hasOverride ? debugMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x" : "auto") +
               " RequestedMultiplier=" + requested.ToString("0.00", CultureInfo.InvariantCulture) + "x" +
               " AppliedMultiplier=" + applied.ToString("0.00", CultureInfo.InvariantCulture) + "x" +
               " NativeSpeedFlying=" + drone.SpeedFlying.ToString("0.00", CultureInfo.InvariantCulture) +
               " NativeCurrentSpeed=" + drone.currentSpeedFlying.ToString("0.00", CultureInfo.InvariantCulture) +
               " VelocityPerSecond=" + velocity.ToString("0.00", CultureInfo.InvariantCulture) +
               " MoveObserveCalls=" + telemetry.MovePatchCalls +
               " LastNativeMove=" + telemetry.LastBaseSpeed.ToString("0.00", CultureInfo.InvariantCulture) +
               " BoostEvalCalls=" + telemetry.BoostEvaluationCalls +
               " BoostApplyCalls=" + telemetry.BoostApplyCalls +
               " LastNativeStep=" + telemetry.LastNativeStep.ToString("0.000", CultureInfo.InvariantCulture) +
               " LastExtraStep=" + telemetry.LastExtraStep.ToString("0.000", CultureInfo.InvariantCulture) +
               " Skips(remote/tether/state/near/zero/teleport/away/node/blocked)=" +
               telemetry.BoostSkippedRemote + "/" + telemetry.BoostSkippedTether + "/" + telemetry.BoostSkippedState + "/" +
               telemetry.BoostSkippedNearOwner + "/" + telemetry.BoostSkippedNoMovement + "/" + telemetry.BoostSkippedTeleport + "/" +
               telemetry.BoostSkippedDirection + "/" + telemetry.BoostSkippedPathNode + "/" + telemetry.BoostSkippedBlocked;
    }

    public static void ResetForWorldChange()
    {
        StopTelemetry();
        lock (Sync)
        {
            DebugOverrides.Clear();
            Telemetry.Clear();
        }
    }
}

/// <summary>
/// Captures the start of the native drone update and adds a bounded continuation only after
/// vanilla has completed its own steering/path/collision movement.
/// </summary>
[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.OnUpdateEntity))]
internal static class RebirthDroneQualitySpeedOnUpdateEntityPatch
{
    private static void Prefix(EntityDrone __instance, out Vector3 __state)
    {
        __state = __instance != null ? __instance.position : Vector3.zero;
    }

    private static void Postfix(EntityDrone __instance, Vector3 __state)
    {
        RebirthDroneQualitySpeed.ApplyNativeDisplacementBoost(__instance, __state);
    }
}

/// <summary>Telemetry only: never mutates the native move argument.</summary>
[HarmonyPatch(typeof(EntityDrone), "move", new Type[] { typeof(Vector3), typeof(float), typeof(bool) })]
internal static class RebirthDroneQualitySpeedMovePatch
{
    private static void Prefix(EntityDrone __instance, float __1)
    {
        RebirthDroneQualitySpeed.ObserveMove(__instance, __1);
    }
}

/// <summary>Telemetry only: never mutates the native move argument.</summary>
[HarmonyPatch(typeof(EntityDrone), "move", new Type[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(bool) })]
internal static class RebirthDroneQualitySpeedMoveTargetPatch
{
    private static void Prefix(EntityDrone __instance, float __2)
    {
        RebirthDroneQualitySpeed.ObserveMove(__instance, __2);
    }
}
