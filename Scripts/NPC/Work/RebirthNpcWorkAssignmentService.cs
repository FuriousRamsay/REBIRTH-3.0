using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcWorkAssignmentRequest
{
    public string SessionKey { get; set; }
    public string DefinitionId { get; set; }
    public string TargetKey { get; set; }
    public Vector3 TargetPosition { get; set; }
    public bool HasTargetPosition { get; set; }
    public RebirthNpcWorkSchedule Schedule { get; set; }
    public int Priority { get; set; }
    public uint ExpectedRuntimeRevision { get; set; }
}

public static class RebirthNpcWorkAssignmentService
{
    private const int MaxHistory = 2048;
    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, RebirthNpcWorkAssignment> ById =
        new Dictionary<ulong, RebirthNpcWorkAssignment>();
    private static readonly Dictionary<RebirthNpcStableId, List<ulong>> ByNpc =
        new Dictionary<RebirthNpcStableId, List<ulong>>();
    private static readonly Queue<ulong> HistoryOrder = new Queue<ulong>();
    private static ulong nextAssignmentId = 1UL;
    private static long created, cancelled, completed, failed, suspended, rejected;

    public static RebirthNpcWorkAssignmentResult Create(RebirthNpcWorkAssignmentRequest request,
        out RebirthNpcWorkAssignment assignment, out string error)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled){assignment=null;error=RebirthNpcWorkReleaseGate.Detail;return RebirthNpcWorkAssignmentResult.PermissionDenied;}

        assignment = null;
        error = string.Empty;
        if (request == null || string.IsNullOrWhiteSpace(request.SessionKey) ||
            string.IsNullOrWhiteSpace(request.DefinitionId) || string.IsNullOrWhiteSpace(request.TargetKey))
            return Reject(RebirthNpcWorkAssignmentResult.InvalidRequest,
                "Session, work definition and target key are required.", out error);

        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult authorization = RebirthNpcInteractionSessionService.Authorize(
            request.SessionKey, RebirthNpcInteractionAction.AssignWork, out context);
        if (authorization != RebirthNpcInteractionResult.Allowed)
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied,
                "Interaction authorization failed: " + authorization, out error);
        if (request.ExpectedRuntimeRevision != 0U &&
            request.ExpectedRuntimeRevision != context.RuntimeRevision)
            return Reject(RebirthNpcWorkAssignmentResult.RevisionConflict,
                "NPC runtime revision changed before assignment creation.", out error);

        RebirthNpcWorkDefinition definition;
        if (!RebirthNpcWorkDefinitionRegistry.TryGet(request.DefinitionId, out definition))
            return Reject(RebirthNpcWorkAssignmentResult.InvalidRequest,
                "Unknown work definition: " + request.DefinitionId, out error);

        RebirthNpcWorkCapability available;
        string denial;
        if (!RebirthNpcWorkCapabilityService.CanPerform(context.NpcId, definition,
            context.ActorId, out available, out denial))
            return Reject(RebirthNpcWorkAssignmentResult.CapabilityDenied, denial, out error);

        lock (Sync)
        {
            List<ulong> existing;
            if (ByNpc.TryGetValue(context.NpcId, out existing))
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    RebirthNpcWorkAssignment current;
                    if (!ById.TryGetValue(existing[i], out current) || IsTerminal(current.Status)) continue;
                    if (SameTargetIdentity(current, request) &&
                        string.Equals(current.DefinitionId, definition.DefinitionId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        assignment = current.Clone();
                        error = "Equivalent active assignment already exists.";
                        Interlocked.Increment(ref rejected);
                        return RebirthNpcWorkAssignmentResult.Conflict;
                    }
                    if (current.Priority > request.Priority && !definition.AllowsPreemption)
                    {
                        assignment = current.Clone();
                        error = "A higher-priority assignment is already active.";
                        Interlocked.Increment(ref rejected);
                        return RebirthNpcWorkAssignmentResult.Conflict;
                    }
                }
            }

            ulong id = nextAssignmentId++;
            if (id == 0UL) id = nextAssignmentId++;
            long now = DateTime.UtcNow.Ticks;
            RebirthNpcWorkAssignment createdRecord = new RebirthNpcWorkAssignment
            {
                AssignmentId = id,
                NpcId = context.NpcId,
                DefinitionId = definition.DefinitionId,
                ActorId = context.ActorId,
                TargetKey = request.TargetKey.Trim(),
                TargetPosition = request.TargetPosition,
                HasTargetPosition = request.HasTargetPosition,
                Schedule = request.Schedule,
                Status = request.Schedule == null
                    ? RebirthNpcWorkAssignmentStatus.Ready
                    : RebirthNpcWorkAssignmentStatus.Scheduled,
                Revision = 1U,
                Priority = request.Priority == 0 ? definition.Priority : request.Priority,
                Detail = "Authoritative work assignment created.",
                CreatedUtcTicks = now,
                UpdatedUtcTicks = now
            };
            ById[id] = createdRecord;
            if (!ByNpc.TryGetValue(context.NpcId, out existing))
            {
                existing = new List<ulong>();
                ByNpc[context.NpcId] = existing;
            }
            existing.Add(id);
            assignment = createdRecord.Clone();
            Interlocked.Increment(ref created);
            return RebirthNpcWorkAssignmentResult.Succeeded;
        }
    }

    public static RebirthNpcWorkAssignmentResult Cancel(string sessionKey, ulong assignmentId,
        uint expectedRevision, out RebirthNpcWorkAssignment assignment, out string error)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled){assignment=null;error=RebirthNpcWorkReleaseGate.Detail;return RebirthNpcWorkAssignmentResult.PermissionDenied;}

        assignment = null;
        error = string.Empty;
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult authorization = RebirthNpcInteractionSessionService.Authorize(
            sessionKey, RebirthNpcInteractionAction.AssignWork, out context);
        if (authorization != RebirthNpcInteractionResult.Allowed)
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied,
                "Interaction authorization failed: " + authorization, out error);
        lock (Sync)
        {
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(assignmentId, out current))
                return Reject(RebirthNpcWorkAssignmentResult.NotFound,
                    "Assignment was not found.", out error);
            if (current.NpcId != context.NpcId)
                return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied,
                    "Assignment does not belong to the interaction NPC.", out error);
            if (expectedRevision != 0U && current.Revision != expectedRevision)
                return Reject(RebirthNpcWorkAssignmentResult.RevisionConflict,
                    "Assignment revision changed.", out error);
            if (!string.Equals(current.ActorId, context.ActorId, StringComparison.OrdinalIgnoreCase) &&
                (context.Permissions & RebirthNpcInteractionPermissions.Admin) == 0)
                return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied,
                    "Only the assigning actor or an administrator may cancel this assignment.", out error);
            SetStatusLocked(current, RebirthNpcWorkAssignmentStatus.Cancelled,
                "Cancelled through an authorized interaction session.");
            RebirthNpcWorkReservationService.ReleaseAssignment(assignmentId, "Assignment cancelled.");
            assignment = current.Clone();
            Interlocked.Increment(ref cancelled);
            return RebirthNpcWorkAssignmentResult.Succeeded;
        }
    }

    public static bool EvaluateSchedule(ulong assignmentId, int dayIndex, int minuteOfDay,
        out RebirthNpcWorkAssignment assignment)
    {
        lock (Sync)
        {
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(assignmentId, out current) || IsTerminal(current.Status))
            {
                assignment = null;
                return false;
            }
            bool active = current.Schedule == null || current.Schedule.IsActive(dayIndex, minuteOfDay);
            RebirthNpcWorkAssignmentStatus desired = active
                ? RebirthNpcWorkAssignmentStatus.Ready
                : RebirthNpcWorkAssignmentStatus.Scheduled;
            if (current.Status == RebirthNpcWorkAssignmentStatus.Scheduled ||
                current.Status == RebirthNpcWorkAssignmentStatus.Ready ||
                current.Status == RebirthNpcWorkAssignmentStatus.Suspended)
                SetStatusLocked(current, desired, active
                    ? "Assignment schedule is active." : "Waiting for scheduled work window.");
            assignment = current.Clone();
            return active;
        }
    }

    public static bool TryTransition(ulong assignmentId, uint expectedRevision,
        RebirthNpcWorkAssignmentStatus status, string detail,
        out RebirthNpcWorkAssignment assignment)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled){assignment=null;return false;}

        RebirthNpcWorkAssignment completedAssignment = null;
        RebirthNpcWorkAssignment failedAssignment = null;
        lock (Sync)
        {
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(assignmentId, out current) ||
                (expectedRevision != 0U && current.Revision != expectedRevision) ||
                !CanTransition(current.Status, status))
            {
                assignment = null;
                return false;
            }
            SetStatusLocked(current, status, detail);
            assignment = current.Clone();
            if (status == RebirthNpcWorkAssignmentStatus.Completed)
            {
                Interlocked.Increment(ref completed);
                completedAssignment = assignment;
            }
            else if (status == RebirthNpcWorkAssignmentStatus.Failed)
            {
                Interlocked.Increment(ref failed);
                failedAssignment = assignment;
            }
            else if (status == RebirthNpcWorkAssignmentStatus.Suspended) Interlocked.Increment(ref suspended);
            if (IsTerminal(status)) HistoryOrder.Enqueue(assignmentId);
            TrimHistoryLocked();
        }

        if (completedAssignment != null)
        {
            float importance = ResolveSocialImportance(completedAssignment);
            RebirthNpcSocialGameplayEventProducers.PublishAssistance(
                completedAssignment.NpcId, completedAssignment.ActorId, importance);
            RebirthNpcSocialGameplayEventProducers.PublishPromise(
                completedAssignment.NpcId, completedAssignment.ActorId, true, importance);
        }
        else if (failedAssignment != null)
        {
            RebirthNpcSocialGameplayEventProducers.PublishPromise(
                failedAssignment.NpcId, failedAssignment.ActorId, false,
                ResolveSocialImportance(failedAssignment));
        }
        return true;
    }

    private static float ResolveSocialImportance(RebirthNpcWorkAssignment assignment)
    {
        if (assignment == null) return 0.25f;
        int priority = Math.Max(0, Math.Min(1000, assignment.Priority));
        return Math.Max(0.2f, Math.Min(0.8f, 0.2f + priority / 1600f));
    }

    public static bool TryGet(ulong assignmentId, out RebirthNpcWorkAssignment assignment)
    {
        lock (Sync)
        {
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(assignmentId, out current))
            {
                assignment = null;
                return false;
            }
            assignment = current.Clone();
            return true;
        }
    }

    public static RebirthNpcWorkAssignment[] GetForNpc(RebirthNpcStableId npcId, bool includeTerminal)
    {
        lock (Sync)
        {
            List<ulong> ids;
            if (!ByNpc.TryGetValue(npcId, out ids)) return new RebirthNpcWorkAssignment[0];
            List<RebirthNpcWorkAssignment> result = new List<RebirthNpcWorkAssignment>();
            for (int i = 0; i < ids.Count; i++)
            {
                RebirthNpcWorkAssignment current;
                if (!ById.TryGetValue(ids[i], out current)) continue;
                if (!includeTerminal && IsTerminal(current.Status)) continue;
                result.Add(current.Clone());
            }
            result.Sort((a, b) =>
            {
                int priority = b.Priority.CompareTo(a.Priority);
                return priority != 0 ? priority : a.AssignmentId.CompareTo(b.AssignmentId);
            });
            return result.ToArray();
        }
    }

    public static void Reset()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        lock (Sync)
        {
            ById.Clear();
            ByNpc.Clear();
            HistoryOrder.Clear();
            nextAssignmentId = 1UL;
        }
        RebirthNpcWorkReservationService.Reset();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            int active = 0;
            foreach (KeyValuePair<ulong, RebirthNpcWorkAssignment> pair in ById)
                if (!IsTerminal(pair.Value.Status)) active++;
            return "[REBIRTH NPC Work Assignments] total=" + ById.Count + " active=" + active +
                " created=" + Interlocked.Read(ref created) +
                " cancelled=" + Interlocked.Read(ref cancelled) +
                " completed=" + Interlocked.Read(ref completed) +
                " failed=" + Interlocked.Read(ref failed) +
                " suspended=" + Interlocked.Read(ref suspended) +
                " rejected=" + Interlocked.Read(ref rejected);
        }
    }


    private static bool SameTargetIdentity(RebirthNpcWorkAssignment current,
        RebirthNpcWorkAssignmentRequest request)
    {
        if (current == null || request == null ||
            !string.Equals(current.TargetKey, request.TargetKey, StringComparison.OrdinalIgnoreCase))
            return false;
        if (current.HasTargetPosition != request.HasTargetPosition) return false;
        if (!current.HasTargetPosition) return true;
        Vector3 delta = current.TargetPosition - request.TargetPosition;
        return delta.sqrMagnitude <= 0.0001f;
    }

    private static RebirthNpcWorkAssignmentResult Reject(RebirthNpcWorkAssignmentResult result,
        string detail, out string error)
    {
        error = detail ?? string.Empty;
        Interlocked.Increment(ref rejected);
        return result;
    }

    private static void SetStatusLocked(RebirthNpcWorkAssignment assignment,
        RebirthNpcWorkAssignmentStatus status, string detail)
    {
        if (assignment.Status == status && string.Equals(assignment.Detail, detail,
            StringComparison.Ordinal)) return;
        assignment.Status = status;
        assignment.Detail = detail ?? string.Empty;
        assignment.Revision++;
        if (assignment.Revision == 0U) assignment.Revision = 1U;
        assignment.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
    }

    private static bool CanTransition(RebirthNpcWorkAssignmentStatus from,
        RebirthNpcWorkAssignmentStatus to)
    {
        if (IsTerminal(from)) return false;
        if (to == RebirthNpcWorkAssignmentStatus.Cancelled ||
            to == RebirthNpcWorkAssignmentStatus.Failed) return true;
        switch (from)
        {
            case RebirthNpcWorkAssignmentStatus.Pending:
            case RebirthNpcWorkAssignmentStatus.Scheduled:
                return to == RebirthNpcWorkAssignmentStatus.Ready ||
                    to == RebirthNpcWorkAssignmentStatus.Suspended;
            case RebirthNpcWorkAssignmentStatus.Ready:
                return to == RebirthNpcWorkAssignmentStatus.Reserved ||
                    to == RebirthNpcWorkAssignmentStatus.Scheduled ||
                    to == RebirthNpcWorkAssignmentStatus.Suspended;
            case RebirthNpcWorkAssignmentStatus.Reserved:
                return to == RebirthNpcWorkAssignmentStatus.Executing ||
                    to == RebirthNpcWorkAssignmentStatus.Ready ||
                    to == RebirthNpcWorkAssignmentStatus.Suspended;
            case RebirthNpcWorkAssignmentStatus.Executing:
                return to == RebirthNpcWorkAssignmentStatus.Completed ||
                    to == RebirthNpcWorkAssignmentStatus.Suspended ||
                    to == RebirthNpcWorkAssignmentStatus.Ready;
            case RebirthNpcWorkAssignmentStatus.Suspended:
                return to == RebirthNpcWorkAssignmentStatus.Ready ||
                    to == RebirthNpcWorkAssignmentStatus.Scheduled;
            default: return false;
        }
    }

    private static bool IsTerminal(RebirthNpcWorkAssignmentStatus status)
    {
        return status == RebirthNpcWorkAssignmentStatus.Completed ||
            status == RebirthNpcWorkAssignmentStatus.Cancelled ||
            status == RebirthNpcWorkAssignmentStatus.Failed;
    }

    private static void TrimHistoryLocked()
    {
        while (HistoryOrder.Count > MaxHistory)
        {
            ulong id = HistoryOrder.Dequeue();
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(id, out current) || !IsTerminal(current.Status)) continue;
            ById.Remove(id);
            List<ulong> ids;
            if (ByNpc.TryGetValue(current.NpcId, out ids))
            {
                ids.Remove(id);
                if (ids.Count == 0) ByNpc.Remove(current.NpcId);
            }
        }
    }
}
