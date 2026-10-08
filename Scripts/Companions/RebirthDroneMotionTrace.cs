using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>
/// Temporary live diagnostics for normal/tethered drone motion. This never mutates
/// movement, orders, targets, networking, or quality scaling. It samples the final
/// drone position from LateUpdate at a user-selected rate and writes a compact trace
/// that can be compared between tier 1 and tier 6/quality scaling. v200 keeps the
/// complete native flight controller unchanged, reports the small post-native
/// displacement continuation separately, and excludes teleport-sized samples from
/// aggregate speed/reversal statistics.
/// </summary>
public static class RebirthDroneMotionTrace
{
    private sealed class TraceSession
    {
        public int EntityId;
        public float StartTime;
        public float EndTime;
        public float NextSampleTime;
        public float Interval;
        public int RequestedHz;

        public bool HasPrevious;
        public float PreviousTime;
        public Vector3 PreviousPosition;
        public Vector3 PreviousVelocity;
        public Vector3 PreviousTargetVector;
        public float PreviousTowardSpeed;
        public float PreviousDistanceDelta;
        public long PreviousMoveCalls;
        public long PreviousBoostCalls;

        public int Samples;
        public int SpeedSamples;
        public int FollowSpeedSamples;
        public int FollowStationarySamples;
        public int ExcludedJumpSamples;
        public int DirectionReversals;
        public int TowardReversals;
        public int DistanceTrendReversals;
        public float SumMeasuredSpeed;
        public float MaxMeasuredSpeed;
        public float SumFollowMeasuredSpeed;
        public float MaxFollowMeasuredSpeed;
        public float SumTargetDistance;
        public float MinTargetDistance = float.MaxValue;
        public float MaxTargetDistance;
    }

    // A displacement this large between diagnostic samples cannot be normal drone travel.
    // Exclude it from speed/reversal summaries while still logging the raw sample.
    private const float SummaryJumpDistance = 5f;

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, TraceSession> Sessions = new Dictionary<int, TraceSession>();

    public static void Start(EntityDrone drone, float seconds, int hz)
    {
        if (drone == null) return;
        seconds = Mathf.Clamp(seconds, 1f, 60f);
        hz = Mathf.Clamp(hz, 2, 30);
        float now = Time.realtimeSinceStartup;
        TraceSession session = new TraceSession
        {
            EntityId = drone.entityId,
            StartTime = now,
            EndTime = now + seconds,
            NextSampleTime = now,
            Interval = 1f / hz,
            RequestedHz = hz,
            PreviousMoveCalls = RebirthDroneQualitySpeed.GetTelemetry(drone.entityId).MovePatchCalls,
            PreviousBoostCalls = RebirthDroneQualitySpeed.GetTelemetry(drone.entityId).BoostApplyCalls
        };
        lock (Sync)
            Sessions[drone.entityId] = session;

        Log.Out("[RebirthDroneTrace] START Entity=" + drone.entityId +
                " Duration=" + seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s" +
                " Rate=" + hz + "Hz " + RebirthDroneQualitySpeed.GetDiagnosticReport(drone));
    }

    public static bool Stop(int entityId, string reason = "manual")
    {
        TraceSession session = null;
        lock (Sync)
        {
            if (Sessions.TryGetValue(entityId, out session))
                Sessions.Remove(entityId);
        }
        if (session == null) return false;
        LogSummary(session, reason);
        return true;
    }

    public static string GetStatus(EntityDrone drone)
    {
        if (drone == null) return "[RebirthDroneTrace] No drone resolved.";
        lock (Sync)
        {
            TraceSession session;
            if (!Sessions.TryGetValue(drone.entityId, out session) || session == null)
                return "[RebirthDroneTrace] Entity=" + drone.entityId + " trace=OFF";
            float now = Time.realtimeSinceStartup;
            return "[RebirthDroneTrace] Entity=" + drone.entityId +
                   " trace=ON elapsed=" + Mathf.Max(0f, now - session.StartTime).ToString("0.00", CultureInfo.InvariantCulture) + "s" +
                   " remaining=" + Mathf.Max(0f, session.EndTime - now).ToString("0.00", CultureInfo.InvariantCulture) + "s" +
                   " rate=" + session.RequestedHz + "Hz" +
                   " samples=" + session.Samples;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
            Sessions.Clear();
    }

    internal static void Sample(EntityDrone drone)
    {
        if (drone == null) return;

        TraceSession session;
        lock (Sync)
        {
            if (!Sessions.TryGetValue(drone.entityId, out session) || session == null)
                return;
        }

        float now = Time.realtimeSinceStartup;
        if (now >= session.EndTime)
        {
            Stop(drone.entityId, "duration complete");
            return;
        }
        if (now + 0.0001f < session.NextSampleTime)
            return;

        // Advance by whole intervals so a slow frame does not create a burst of catch-up logs.
        do session.NextSampleTime += session.Interval;
        while (session.NextSampleTime <= now);

        Vector3 position = drone.position;
        Vector3 velocity = drone.GetVelocityPerSecond();
        Vector3 motion = drone.motion;
        EntityAlive owner = drone.Owner;
        Vector3 ownerPos = owner != null ? owner.getChestPosition() : position;
        Vector3 followTarget = ResolveFollowTarget(drone, owner, ownerPos);
        Vector3 targetVector = followTarget - position;
        float targetDistance = targetVector.magnitude;
        float ownerDistance = owner != null ? Vector3.Distance(ownerPos, position) : 0f;
        float netTargetDistance = Vector3.Distance(drone.targetPos, position);

        float measuredSpeed = 0f;
        float towardSpeed = 0f;
        float distanceDelta = 0f;
        float sampleDt = 0f;
        bool hasMeasuredSample = session.HasPrevious;
        bool excludedJumpSample = false;
        if (session.HasPrevious)
        {
            sampleDt = Mathf.Max(0.0001f, now - session.PreviousTime);
            Vector3 displacement = position - session.PreviousPosition;
            float displacementDistance = displacement.magnitude;
            measuredSpeed = displacementDistance / sampleDt;
            excludedJumpSample = displacementDistance > SummaryJumpDistance;

            Vector3 previousTargetDir = session.PreviousTargetVector.sqrMagnitude > 0.0001f
                ? session.PreviousTargetVector.normalized
                : Vector3.zero;
            towardSpeed = Vector3.Dot(displacement / sampleDt, previousTargetDir);
            distanceDelta = targetDistance - session.PreviousTargetVector.magnitude;

            if (!excludedJumpSample)
            {
                if (session.PreviousVelocity.sqrMagnitude > 0.0625f && velocity.sqrMagnitude > 0.0625f &&
                    Vector3.Angle(session.PreviousVelocity, velocity) >= 120f)
                    session.DirectionReversals++;

                if (Mathf.Abs(session.PreviousTowardSpeed) >= 0.25f && Mathf.Abs(towardSpeed) >= 0.25f &&
                    Mathf.Sign(session.PreviousTowardSpeed) != Mathf.Sign(towardSpeed))
                    session.TowardReversals++;

                if (Mathf.Abs(session.PreviousDistanceDelta) >= 0.03f && Mathf.Abs(distanceDelta) >= 0.03f &&
                    Mathf.Sign(session.PreviousDistanceDelta) != Mathf.Sign(distanceDelta))
                    session.DistanceTrendReversals++;
            }
        }

        RebirthDroneQualitySpeed.TelemetrySnapshot telemetry = RebirthDroneQualitySpeed.GetTelemetry(drone.entityId);
        long callDelta = telemetry.MovePatchCalls - session.PreviousMoveCalls;
        long boostDelta = telemetry.BoostApplyCalls - session.PreviousBoostCalls;
        float multiplier = RebirthDroneQualitySpeed.GetMultiplier(drone);
        bool tethered = RebirthDroneLockToPlayerService.IsLockedToPlayer(drone);

        session.Samples++;
        if (hasMeasuredSample && !excludedJumpSample)
        {
            session.SpeedSamples++;
            session.SumMeasuredSpeed += measuredSpeed;
            session.MaxMeasuredSpeed = Mathf.Max(session.MaxMeasuredSpeed, measuredSpeed);

            if (drone.OrderState == EntityDrone.Orders.Follow && drone.GetState() == EntityDrone.State.Follow)
            {
                session.FollowSpeedSamples++;
                session.SumFollowMeasuredSpeed += measuredSpeed;
                session.MaxFollowMeasuredSpeed = Mathf.Max(session.MaxFollowMeasuredSpeed, measuredSpeed);
                if (measuredSpeed < 0.10f)
                    session.FollowStationarySamples++;
            }
        }
        else if (excludedJumpSample)
        {
            session.ExcludedJumpSamples++;
        }

        session.SumTargetDistance += targetDistance;
        session.MinTargetDistance = Mathf.Min(session.MinTargetDistance, targetDistance);
        session.MaxTargetDistance = Mathf.Max(session.MaxTargetDistance, targetDistance);

        Log.Out("[RebirthDroneTrace]" +
                " t=" + (now - session.StartTime).ToString("0.000", CultureInfo.InvariantCulture) +
                " id=" + drone.entityId +
                " q=" + RebirthDroneQualitySpeed.GetQuality(drone) +
                " mult=" + multiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x" +
                " tether=" + (tethered ? "1" : "0") +
                " state=" + drone.GetState().ToString() +
                " order=" + drone.OrderState.ToString() +
                " pos=" + V(position) +
                " follow=" + V(followTarget) +
                " dist=" + targetDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                " ownerDist=" + ownerDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                " motion=" + V(motion) +
                " motionMag=" + motion.magnitude.ToString("0.000", CultureInfo.InvariantCulture) +
                " vel=" + V(velocity) +
                " velMag=" + velocity.magnitude.ToString("0.000", CultureInfo.InvariantCulture) +
                " measured=" + measuredSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " jumpSample=" + (excludedJumpSample ? "1" : "0") +
                " toward=" + towardSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " baseCruise=" + telemetry.BaseCruiseSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " cruise=" + drone.SpeedFlying.ToString("0.000", CultureInfo.InvariantCulture) +
                " currentSpeed=" + drone.currentSpeedFlying.ToString("0.000", CultureInfo.InvariantCulture) +
                " accel=" + drone.accelerationTime.ToString("0.000", CultureInfo.InvariantCulture) +
                " decel=" + drone.decelerationTime.ToString("0.000", CultureInfo.InvariantCulture) +
                " nativeMove=" + telemetry.LastBaseSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " nativeStep=" + telemetry.LastNativeStep.ToString("0.000", CultureInfo.InvariantCulture) +
                " extraStep=" + telemetry.LastExtraStep.ToString("0.000", CultureInfo.InvariantCulture) +
                " appliedMult=" + telemetry.AppliedMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x" +
                " calls+=" + callDelta +
                " boost+=" + boostDelta +
                " path=" + (drone.currentPath != null ? drone.currentPath.Count : 0) +
                " netUpdate=" + (drone.isUpdatePosition ? "1" : "0") +
                " netTargetDist=" + netTargetDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                " dt=" + sampleDt.ToString("0.000", CultureInfo.InvariantCulture));

        session.HasPrevious = true;
        session.PreviousTime = now;
        session.PreviousPosition = position;
        session.PreviousVelocity = excludedJumpSample ? Vector3.zero : velocity;
        session.PreviousTargetVector = targetVector;
        session.PreviousTowardSpeed = excludedJumpSample ? 0f : towardSpeed;
        session.PreviousDistanceDelta = excludedJumpSample ? 0f : distanceDelta;
        session.PreviousMoveCalls = telemetry.MovePatchCalls;
        session.PreviousBoostCalls = telemetry.BoostApplyCalls;
    }

    private static Vector3 ResolveFollowTarget(EntityDrone drone, EntityAlive owner, Vector3 fallback)
    {
        if (drone == null || owner == null || drone.GetState() != EntityDrone.State.Follow)
            return fallback;
        try
        {
            Vector3[] positions = EntityDrone.GetGroupPositions(owner, 5f);
            if (positions == null || positions.Length == 0) return fallback;
            Vector3 best = positions[0];
            float bestSqr = (best - drone.position).sqrMagnitude;
            for (int i = 1; i < positions.Length; i++)
            {
                float sqr = (positions[i] - drone.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = positions[i];
                    bestSqr = sqr;
                }
            }
            return best;
        }
        catch
        {
            return fallback;
        }
    }

    private static void LogSummary(TraceSession session, string reason)
    {
        if (session == null) return;
        float averageSpeed = session.SpeedSamples > 0 ? session.SumMeasuredSpeed / session.SpeedSamples : 0f;
        float averageFollowSpeed = session.FollowSpeedSamples > 0 ? session.SumFollowMeasuredSpeed / session.FollowSpeedSamples : 0f;
        float averageDistance = session.Samples > 0 ? session.SumTargetDistance / session.Samples : 0f;
        float minDistance = session.MinTargetDistance == float.MaxValue ? 0f : session.MinTargetDistance;
        Log.Out("[RebirthDroneTrace] STOP Entity=" + session.EntityId +
                " Reason=" + reason +
                " Samples=" + session.Samples +
                " SpeedSamples=" + session.SpeedSamples +
                " AvgMeasuredSpeed=" + averageSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " MaxMeasuredSpeed=" + session.MaxMeasuredSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " FollowSpeedSamples=" + session.FollowSpeedSamples +
                " AvgFollowMeasuredSpeed=" + averageFollowSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " MaxFollowMeasuredSpeed=" + session.MaxFollowMeasuredSpeed.ToString("0.000", CultureInfo.InvariantCulture) +
                " FollowStationarySamples=" + session.FollowStationarySamples +
                " TeleportSamplesExcluded=" + session.ExcludedJumpSamples +
                " AvgFollowDist=" + averageDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                " FollowDistRange=" + minDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ".." + session.MaxTargetDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                " VelocityDirectionReversals=" + session.DirectionReversals +
                " TowardAwayReversals=" + session.TowardReversals +
                " DistanceTrendReversals=" + session.DistanceTrendReversals);
    }

    private static string V(Vector3 value)
    {
        return "(" + value.x.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                     value.y.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                     value.z.ToString("0.000", CultureInfo.InvariantCulture) + ")";
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.LateUpdate))]
internal static class RebirthDroneMotionTraceLateUpdatePatch
{
    private static void Postfix(EntityDrone __instance)
    {
        RebirthDroneMotionTrace.Sample(__instance);
    }
}
