using System;
using System.Collections.Generic;
using System.Threading;

#nullable disable

public sealed class RebirthNpcWorkReservation
{
    public ulong ReservationId { get; internal set; }
    public ulong AssignmentId { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public string TargetKey { get; internal set; }
    public int Slot { get; internal set; }
    public long ExpiresUtcTicks { get; internal set; }
    public uint AssignmentRevision { get; internal set; }

    internal RebirthNpcWorkReservation Clone()
    {
        return (RebirthNpcWorkReservation)MemberwiseClone();
    }
}

public static class RebirthNpcWorkReservationService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, RebirthNpcWorkReservation> ById =
        new Dictionary<ulong, RebirthNpcWorkReservation>();
    private static readonly Dictionary<ulong, ulong> ByAssignment = new Dictionary<ulong, ulong>();
    private static readonly Dictionary<string, List<ulong>> ByTarget =
        new Dictionary<string, List<ulong>>(StringComparer.OrdinalIgnoreCase);
    private static ulong nextReservationId = 1UL;
    private static long granted, denied, renewed, released, expired;

    public static RebirthNpcWorkAssignmentResult TryReserve(RebirthNpcWorkAssignment assignment,
        RebirthNpcWorkDefinition definition, out RebirthNpcWorkReservation reservation,
        out string error)
    {
        reservation = null;
        error = string.Empty;
        if (assignment == null || definition == null ||
            assignment.Status != RebirthNpcWorkAssignmentStatus.Ready)
        {
            error = "Assignment must be ready before reservation.";
            Interlocked.Increment(ref denied);
            return RebirthNpcWorkAssignmentResult.InvalidRequest;
        }
        lock (Sync)
        {
            PruneExpiredLocked(DateTime.UtcNow.Ticks);
            ulong existingId;
            if (ByAssignment.TryGetValue(assignment.AssignmentId, out existingId))
            {
                reservation = ById[existingId].Clone();
                return RebirthNpcWorkAssignmentResult.Succeeded;
            }
            string reservationTargetKey = BuildReservationTargetKey(assignment);
            List<ulong> targetReservations;
            if (!ByTarget.TryGetValue(reservationTargetKey, out targetReservations))
            {
                targetReservations = new List<ulong>();
                ByTarget[reservationTargetKey] = targetReservations;
            }
            if (targetReservations.Count >= definition.MaxWorkersPerTarget)
            {
                error = "Work target has reached its worker reservation limit.";
                Interlocked.Increment(ref denied);
                return RebirthNpcWorkAssignmentResult.ReservationDenied;
            }
            int slot = 0;
            while (ContainsSlotLocked(targetReservations, slot)) slot++;
            ulong id = nextReservationId++;
            if (id == 0UL) id = nextReservationId++;
            RebirthNpcWorkReservation created = new RebirthNpcWorkReservation
            {
                ReservationId = id,
                AssignmentId = assignment.AssignmentId,
                NpcId = assignment.NpcId,
                TargetKey = reservationTargetKey,
                Slot = slot,
                ExpiresUtcTicks = DateTime.UtcNow.Ticks + definition.ReservationLifetime.Ticks,
                AssignmentRevision = assignment.Revision
            };
            ById[id] = created;
            ByAssignment[assignment.AssignmentId] = id;
            targetReservations.Add(id);
            reservation = created.Clone();
            Interlocked.Increment(ref granted);
            return RebirthNpcWorkAssignmentResult.Succeeded;
        }
    }

    public static bool Renew(ulong reservationId, TimeSpan lifetime,
        out RebirthNpcWorkReservation reservation)
    {
        lock (Sync)
        {
            PruneExpiredLocked(DateTime.UtcNow.Ticks);
            RebirthNpcWorkReservation current;
            if (!ById.TryGetValue(reservationId, out current))
            {
                reservation = null;
                return false;
            }
            current.ExpiresUtcTicks = DateTime.UtcNow.Ticks +
                (lifetime <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : lifetime).Ticks;
            reservation = current.Clone();
            Interlocked.Increment(ref renewed);
            return true;
        }
    }

    public static bool Release(ulong reservationId, string reason)
    {
        lock (Sync) return ReleaseLocked(reservationId, false);
    }

    public static bool ReleaseAssignment(ulong assignmentId, string reason)
    {
        lock (Sync)
        {
            ulong id;
            return ByAssignment.TryGetValue(assignmentId, out id) && ReleaseLocked(id, false);
        }
    }

    public static bool TryGetForAssignment(ulong assignmentId,
        out RebirthNpcWorkReservation reservation)
    {
        lock (Sync)
        {
            PruneExpiredLocked(DateTime.UtcNow.Ticks);
            ulong id;
            RebirthNpcWorkReservation current;
            if (!ByAssignment.TryGetValue(assignmentId, out id) || !ById.TryGetValue(id, out current))
            {
                reservation = null;
                return false;
            }
            reservation = current.Clone();
            return true;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            ById.Clear();
            ByAssignment.Clear();
            ByTarget.Clear();
            nextReservationId = 1UL;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            PruneExpiredLocked(DateTime.UtcNow.Ticks);
            return "[REBIRTH NPC Work Reservations] active=" + ById.Count +
                " granted=" + Interlocked.Read(ref granted) +
                " denied=" + Interlocked.Read(ref denied) +
                " renewed=" + Interlocked.Read(ref renewed) +
                " released=" + Interlocked.Read(ref released) +
                " expired=" + Interlocked.Read(ref expired);
        }
    }

    private static string BuildReservationTargetKey(RebirthNpcWorkAssignment assignment)
    {
        string key = assignment.TargetKey ?? string.Empty;
        if (!assignment.HasTargetPosition) return key;
        int x = UnityEngine.Mathf.FloorToInt(assignment.TargetPosition.x);
        int y = UnityEngine.Mathf.FloorToInt(assignment.TargetPosition.y);
        int z = UnityEngine.Mathf.FloorToInt(assignment.TargetPosition.z);
        return key + "@" + x + "," + y + "," + z;
    }

    private static bool ContainsSlotLocked(List<ulong> ids, int slot)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            RebirthNpcWorkReservation current;
            if (ById.TryGetValue(ids[i], out current) && current.Slot == slot) return true;
        }
        return false;
    }

    private static void PruneExpiredLocked(long now)
    {
        List<ulong> remove = null;
        foreach (KeyValuePair<ulong, RebirthNpcWorkReservation> pair in ById)
        {
            if (pair.Value.ExpiresUtcTicks > now) continue;
            if (remove == null) remove = new List<ulong>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) ReleaseLocked(remove[i], true);
    }

    private static bool ReleaseLocked(ulong reservationId, bool wasExpired)
    {
        RebirthNpcWorkReservation current;
        if (!ById.TryGetValue(reservationId, out current)) return false;
        ById.Remove(reservationId);
        ByAssignment.Remove(current.AssignmentId);
        List<ulong> target;
        if (ByTarget.TryGetValue(current.TargetKey, out target))
        {
            target.Remove(reservationId);
            if (target.Count == 0) ByTarget.Remove(current.TargetKey);
        }
        if (wasExpired) Interlocked.Increment(ref expired);
        else Interlocked.Increment(ref released);
        return true;
    }
}

public interface IRebirthNpcWorkAssignmentExecutor
{
    string ExecutorId { get; }
    int Priority { get; }
    bool CanExecute(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition);
    bool Begin(RebirthNpcWorkAssignment assignment, RebirthNpcWorkReservation reservation,
        out string detail);
    bool Tick(RebirthNpcWorkAssignment assignment, RebirthNpcWorkReservation reservation,
        out bool completed, out string detail);
    void End(RebirthNpcWorkAssignment assignment, RebirthNpcWorkReservation reservation,
        string reason);
}

public static class RebirthNpcWorkAssignmentCoordinator
{
    private sealed class Session
    {
        public ulong AssignmentId;
        public ulong ReservationId;
        public IRebirthNpcWorkAssignmentExecutor Executor;
        public long StartedUtcTicks;
        public RebirthNpcEmbodiedWorkIntent Intent;
        public RebirthNpcWorkAssignment Assignment;
        public RebirthNpcWorkReservation Reservation;
    }

    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcWorkAssignmentExecutor> Executors =
        new List<IRebirthNpcWorkAssignmentExecutor>();
    private static readonly Dictionary<ulong, Session> Sessions = new Dictionary<ulong, Session>();
    private static readonly RebirthNpcFairRoundRobin<ulong> SessionSchedule =
        new RebirthNpcFairRoundRobin<ulong>();
    private static long started, completed, failed, suspended, noExecutor, executorFaults;

    public static void Register(IRebirthNpcWorkAssignmentExecutor executor)
    {
        if (executor == null || string.IsNullOrWhiteSpace(executor.ExecutorId))
            throw new ArgumentException("A valid executor is required.", nameof(executor));
        lock (Sync)
        {
            if (!Executors.Contains(executor)) Executors.Add(executor);
            Executors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public static void Unregister(IRebirthNpcWorkAssignmentExecutor executor)
    {
        if (executor == null) return;
        lock (Sync) Executors.Remove(executor);
    }

    public static bool Start(ulong assignmentId, out string detail)
    {
        detail = string.Empty;
        lock (Sync) if (Sessions.ContainsKey(assignmentId)) { detail = "Original session remains held."; return false; }
        RebirthNpcWorkAssignment assignment;
        if (!RebirthNpcWorkAssignmentService.TryGet(assignmentId, out assignment) ||
            assignment.Status != RebirthNpcWorkAssignmentStatus.Ready) { detail = "Assignment unavailable or not ready."; return false; }
        RebirthNpcWorkDefinition definition;
        if (!RebirthNpcWorkDefinitionRegistry.TryGet(assignment.DefinitionId, out definition)) { detail = "Definition unavailable."; return false; }
        EntityRebirthNPC actor;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(assignment.NpcId, out actor)) { detail = "Original NPC unloaded."; return false; }
        IRebirthNpcWorkAssignmentExecutor selected = null;
        IRebirthNpcWorkAssignmentExecutor[] snapshot;
        lock (Sync) snapshot = Executors.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            try { if (snapshot[i].CanExecute(assignment, definition)) { selected = snapshot[i]; break; } }
            catch (Exception ex) { Interlocked.Increment(ref executorFaults); Log.Warning("[REBIRTH NPC Work] CanExecute failed: " + ex.Message); }
        }
        if (selected == null) { Interlocked.Increment(ref noExecutor); detail = "No assignment executor."; return false; }
        RebirthNpcWorkReservation reservation;
        if (RebirthNpcWorkReservationService.TryReserve(assignment, definition, out reservation, out detail) != RebirthNpcWorkAssignmentResult.Succeeded) return false;
        RebirthNpcWorkAssignment reserved;
        if (!RebirthNpcWorkAssignmentService.TryTransition(assignmentId, assignment.Revision,
            RebirthNpcWorkAssignmentStatus.Reserved, "Queued for owned EAI admission.", out reserved))
        { detail = "Assignment changed; reservation retained because original ownership is unknown."; return false; }
        Session session = new Session { AssignmentId = assignmentId, ReservationId = reservation.ReservationId,
            Executor = selected, StartedUtcTicks = DateTime.UtcNow.Ticks, Assignment = reserved, Reservation = reservation };
        Func<bool> current = () =>
        {
            Session original; RebirthNpcWorkAssignment live; RebirthNpcWorkReservation lease;
            lock (Sync) if (!Sessions.TryGetValue(assignmentId, out original) || !ReferenceEquals(original, session)) return false;
            return RebirthNpcWorkAssignmentService.TryGet(assignmentId, out live) &&
                live.Revision == session.Assignment.Revision && live.Status == session.Assignment.Status &&
                live.NpcId.Equals(session.Assignment.NpcId) && live.TargetKey == session.Assignment.TargetKey &&
                live.DefinitionId == assignment.DefinitionId && live.ActorId == assignment.ActorId &&
                live.Priority == assignment.Priority && live.CreatedUtcTicks == assignment.CreatedUtcTicks &&
                live.HasTargetPosition == assignment.HasTargetPosition && live.TargetPosition.Equals(assignment.TargetPosition) &&
                ReferenceEquals(live.Schedule, assignment.Schedule) &&
                RebirthNpcWorkReservationService.TryGetForAssignment(assignmentId, out lease) &&
                lease.ReservationId == session.ReservationId;
        };
        Action begin = () =>
        {
            if (!RebirthNpcEmbodiedWorkOwner.IsDispatching(session.Intent) || !current()) throw new InvalidOperationException("Original dispatch changed.");
            string beginDetail;
            if (!session.Executor.Begin(session.Assignment.Clone(), session.Reservation.Clone(), out beginDetail))
                throw new InvalidOperationException("Begin declined; settlement unresolved: " + beginDetail);
            if (!RebirthNpcEmbodiedWorkOwner.IsDispatching(session.Intent) || !current()) throw new InvalidOperationException("Original changed after Begin; settlement unresolved.");
            RebirthNpcWorkAssignment executing;
            if (!RebirthNpcWorkAssignmentService.TryTransition(assignmentId, session.Assignment.Revision,
                RebirthNpcWorkAssignmentStatus.Executing, "Owned EAI began: " + session.Executor.ExecutorId, out executing))
                throw new InvalidOperationException("Assignment changed after Begin; settlement unresolved.");
            session.Assignment = executing; // Exact acknowledged self-transition only.
        };
        Action tick = () =>
        {
            if (!RebirthNpcEmbodiedWorkOwner.IsDispatching(session.Intent) || !current()) throw new InvalidOperationException("Original dispatch changed.");
            bool done; string tickDetail;
            bool accepted = session.Executor.Tick(session.Assignment.Clone(), session.Reservation.Clone(), out done, out tickDetail);
            if (!RebirthNpcEmbodiedWorkOwner.IsDispatching(session.Intent) || !current())
                throw new InvalidOperationException("Original changed after Tick; settlement unresolved.");
            if (!accepted) Finish(session, session.Assignment, session.Reservation, false, tickDetail);
            else if (done) Finish(session, session.Assignment, session.Reservation, true, tickDetail);
        };
        RebirthNpcEmbodiedWorkIntent intent;
        if (!RebirthNpcEmbodiedWorkIntent.TryCreate(actor, "concrete", assignmentId, reservation.ReservationId,
            reserved.Revision, selected.ExecutorId, session.StartedUtcTicks, current, begin, tick, out intent))
        { detail = "Original embodiment unavailable; reservation retained."; return false; }
        session.Intent = intent;
        lock (Sync) Sessions.Add(assignmentId, session);
        if (!RebirthNpcEmbodiedWorkOwner.TryStage(intent, out detail))
        {
            lock (Sync) Sessions.Remove(assignmentId);
            // A refused candidate is not the original owner ticket; do not release a possibly reused reservation.
            return false;
        }
        RebirthNpcEmbodiedWorkIntent originalIntent;
        if (!RebirthNpcEmbodiedWorkOwner.TryGet(actor, "concrete", assignmentId, reservation.ReservationId,
            reserved.Revision, out originalIntent) || !ReferenceEquals(originalIntent, intent))
        {
            detail = "Original callbacks differ; session and reservation held for review.";
            return false;
        }
        lock (Sync) SessionSchedule.Add(assignmentId);
        Interlocked.Increment(ref started);
        detail = "Queued; owned EAI activation disabled.";
        return true;
    }

    public static void Tick(int maximumSessions)
    {
        int budget;
        lock (Sync) budget = maximumSessions <= 0 ? Sessions.Count : Math.Min(maximumSessions, Sessions.Count);
        for (int i = 0; i < budget; i++)
        {
            ulong assignmentId;
            Session session;
            lock (Sync)
            {
                if (!SessionSchedule.TryTake(out assignmentId)) break;
                if (!Sessions.TryGetValue(assignmentId, out session)) continue;
            }
            TickSession(session);
            lock (Sync)
            {
                if (Sessions.ContainsKey(assignmentId)) SessionSchedule.Return(assignmentId);
            }
        }
    }


    public static bool Suspend(ulong assignmentId, string reason)
    {
        Session session; lock (Sync) if (!Sessions.TryGetValue(assignmentId, out session)) return false;
        string cancellation;
        if (!RebirthNpcEmbodiedWorkOwner.RequestCancel(session.Intent, out cancellation)) return false;
        lock (Sync) { Sessions.Remove(assignmentId); SessionSchedule.Remove(assignmentId); }
        RebirthNpcWorkAssignment live, ignored;
        if (RebirthNpcWorkAssignmentService.TryGet(assignmentId, out live) && live.Revision == session.Assignment.Revision)
            RebirthNpcWorkAssignmentService.TryTransition(assignmentId, live.Revision, RebirthNpcWorkAssignmentStatus.Suspended, reason, out ignored);
        RebirthNpcWorkReservationService.Release(session.ReservationId, reason);
        Interlocked.Increment(ref suspended);
        return true;
    }

    public static void Reset()
    {
        Session[] sessions;
        lock (Sync) { sessions = new Session[Sessions.Count]; Sessions.Values.CopyTo(sessions, 0); }
        for (int i = 0; i < sessions.Length; i++) Suspend(sessions[i].AssignmentId, "Coordinator reset.");
        lock (Sync) if (Sessions.Count == 0) { SessionSchedule.Clear(); Executors.Clear(); }
    }

    public static string GetReport()
    {
        lock (Sync) return "[REBIRTH NPC Work Coordinator] executors=" + Executors.Count +
            " sessions=" + Sessions.Count + " started=" + Interlocked.Read(ref started) +
            " completed=" + Interlocked.Read(ref completed) +
            " failed=" + Interlocked.Read(ref failed) +
            " suspended=" + Interlocked.Read(ref suspended) +
            " noExecutor=" + Interlocked.Read(ref noExecutor) +
            " executorFaults=" + Interlocked.Read(ref executorFaults);
    }

    private static void TickSession(Session session)
    {
        RebirthNpcWorkAssignment assignment; RebirthNpcWorkReservation reservation;
        if (!RebirthNpcWorkAssignmentService.TryGet(session.AssignmentId, out assignment) ||
            assignment.Revision != session.Assignment.Revision || assignment.Status != session.Assignment.Status ||
            !RebirthNpcWorkReservationService.TryGetForAssignment(session.AssignmentId, out reservation) ||
            reservation.ReservationId != session.ReservationId)
        { Suspend(session.AssignmentId, "Original assignment or reservation changed."); return; }
        // Maintenance only. All executor callbacks belong to EAI dispatch.
        RebirthNpcWorkReservation renewed;
        if (RebirthNpcWorkReservationService.Renew(session.ReservationId, TimeSpan.FromSeconds(30), out renewed))
            session.Reservation = renewed;
        else Suspend(session.AssignmentId, "Original reservation disappeared.");
    }

    private static void Finish(Session session, RebirthNpcWorkAssignment assignment,
        RebirthNpcWorkReservation reservation, bool success, string detail)
    {
        // Tick completion and void End cannot authenticate native settlement.
        // Begun work remains held without terminal transition or generic release.
        Suspend(session.AssignmentId, detail);
    }
}
