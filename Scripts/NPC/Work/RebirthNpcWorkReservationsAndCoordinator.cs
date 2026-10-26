using System;
using System.Collections.Generic;
using System.Linq;
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

public static partial class RebirthNpcWorkReservationService
{
    private static readonly object Sync = new object();
    private static Dictionary<ulong, RebirthNpcWorkReservation> ById =
        new Dictionary<ulong, RebirthNpcWorkReservation>();
    private static Dictionary<ulong, ulong> ByAssignment = new Dictionary<ulong, ulong>();
    private static Dictionary<string, List<ulong>> ByTarget =
        new Dictionary<string, List<ulong>>(StringComparer.OrdinalIgnoreCase);
    private static ulong nextReservationId = 1UL;
    private static long granted, denied, renewed, released, expired;

    public static RebirthNpcWorkAssignmentResult TryReserve(RebirthNpcWorkAssignment assignment,
        RebirthNpcWorkDefinition definition, out RebirthNpcWorkReservation reservation,
        out string error)
    {
        reservation = null;
        error = string.Empty;
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():
            assignment!=null&&RebirthNpcQualifiedWorkGate.Allows(assignment)&&UseLiteratureReservationScope(assignment.AssignmentId)))
        {error="Original Work reservation is held or belongs to another scope";return RebirthNpcWorkAssignmentResult.InvalidRequest;}
        if (assignment == null || definition == null ||
            assignment.Status != RebirthNpcWorkAssignmentStatus.Ready)
        {
            error = "Assignment must be ready before reservation.";
            Interlocked.Increment(ref denied);
            return RebirthNpcWorkAssignmentResult.InvalidRequest;
        }
        if(RebirthNpcRetainedWorkPackages.BlocksAssignment(assignment.AssignmentId)||RebirthNpcRetainedWorkPackages.BlocksOwner(assignment.NpcId)||RebirthNpcRetainedWorkPackages.BlocksTarget(BuildReservationTargetKey(assignment)))
        {error="Original Work reservation owner/target remains retained.";return RebirthNpcWorkAssignmentResult.ReservationDenied;}
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
        ulong assignmentId=0;
        lock(Sync)if(ById.TryGetValue(reservationId,out var observed))assignmentId=observed.AssignmentId;
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():
            assignmentId!=0&&UseLiteratureReservationScope(assignmentId)))
        {reservation=null;return false;}
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
        ulong assignmentId=0;lock(Sync)if(ById.TryGetValue(reservationId,out var observed))assignmentId=observed.AssignmentId;
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():
            assignmentId!=0&&UseLiteratureReservationScope(assignmentId)))return false;
        lock (Sync) return ReleaseLocked(reservationId, false);
    }

    public static bool ReleaseAssignment(ulong assignmentId, string reason)
    {
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():UseLiteratureReservationScope(assignmentId)))return false;
        lock (Sync)
        {
            ulong id;
            return ByAssignment.TryGetValue(assignmentId, out id) && ReleaseLocked(id, false);
        }
    }

    public static bool TryGetForAssignment(ulong assignmentId,
        out RebirthNpcWorkReservation reservation)
    {
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseReservationScope():UseLiteratureReservationScope(assignmentId)))
        {reservation=null;return false;}
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
            if(!CanRetireEmptyReservations)throw new InvalidOperationException("Original Work reservations remain retained");reservationScope=null;
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
            if(UseReservationScope())PruneExpiredLocked(DateTime.UtcNow.Ticks);
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

public static partial class RebirthNpcWorkAssignmentCoordinator
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
        public long NextCancelProbeTicks,NextReleaseProbeTicks,NextSettleRetryTicks;
        public bool StatusDone;
        public int SettleAttempts;
        public Func<bool> PendingSettle;
    }

    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcWorkAssignmentExecutor> Executors =
        new List<IRebirthNpcWorkAssignmentExecutor>();
    private static Dictionary<ulong, Session> Sessions = new Dictionary<ulong, Session>();
    private static RebirthNpcFairRoundRobin<ulong> SessionSchedule =
        new RebirthNpcFairRoundRobin<ulong>();
    private static long started, completed, failed, suspended, noExecutor, executorFaults;
    private static World qualifiedStarterWorld;
    private static string qualifiedStarterSave;
    private static int qualifiedStarterCursor;

    // Existing exact-target assignments need a start trigger after creation and
    // after a temporary Follow/target hold clears. Process only a small rotating
    // slice; every candidate goes through the existing admission and revision
    // checks, and generic held definitions never enter this snapshot.
    internal static void StartQualifiedReadyBatch(int maximumNpcs)
    {
        var world = GameManager.Instance?.World;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
            RebirthNpcRuntimeRegistry.HasForeignDomain) return;
        string save = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(save)) return;
        if (!ReferenceEquals(qualifiedStarterWorld, world) ||
            !string.Equals(qualifiedStarterSave, save, StringComparison.OrdinalIgnoreCase))
        {
            qualifiedStarterWorld = world;
            qualifiedStarterSave = save;
            qualifiedStarterCursor = 0;
            ClearP6WorldState(); // assignment ids restart per world: no old-world recovery state may carry over
        }
        RecoverRestartedLiteratureBatch(2);
        var ids = RebirthNpcWorkAssignmentService.GetQualifiedReadyNpcSnapshot();
        if (ids.Length == 0) { qualifiedStarterCursor = 0; return; }
        int count = Math.Min(Math.Max(1, maximumNpcs), ids.Length);
        for (int i = 0; i < count; i++)
        {
            int index = qualifiedStarterCursor++ % ids.Length;
            if (qualifiedStarterCursor == int.MaxValue) qualifiedStarterCursor = 0;
            StartNextFeasible(ids[index], out _);
        }
    }

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

    // A read-only admission probe for the exact-purpose qualified Work kinds.
    // It never reserves a station, stages an owner, moves an NPC, or consumes
    // an item. Unknown native custody stops selection rather than guessing.
    public static bool TrySelectNextFeasible(RebirthNpcStableId npcId,
        out RebirthNpcWorkAssignment selected)
    {
        selected=null;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||npcId.IsEmpty||
            RebirthNpcEmbodiedWorkOwner.HasRetainedOwner(npcId)||
            RebirthNpcRetainedWorkPackages.BlocksOwner(npcId)||
            RebirthNpcRetainedControllerCustody.BlocksOwner(npcId)||
            RebirthNpcRetainedWorkCustody.BlocksOwner(npcId)||
            RebirthNpcWorkNativeTransferOriginal.HasPending(npcId)||
            RebirthSmartLootingHumanOriginal.HasPending(npcId)||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(npcId)||
            !RebirthNpcWorkNavigationService.TryResolveNpc(npcId,out var actor)||
            actor.IsDead()||actor.RebirthRuntimeState?.Order==RebirthNpcOrderState.Follow||
            RebirthNpcExecutionLeaseRegistry.TryGetActive(actor.entityId,out _))return false;
        lock(Sync)if(Sessions.Values.Any(s=>s.Assignment?.NpcId==npcId))return false;
        var candidates=RebirthNpcWorkAssignmentService.GetForNpc(npcId,false);
        foreach(var assignment in candidates)
        {
            if(assignment.Status!=RebirthNpcWorkAssignmentStatus.Ready||
                !RebirthNpcQualifiedWorkGate.Allows(assignment)||
                !RebirthNpcWorkDefinitionRegistry.TryGet(assignment.DefinitionId,out var definition))continue;
            if(!RebirthNpcWorkTargetAdapterRegistry.TryResolve(assignment,definition,
                out _,out var target,out _)||target==null||!target.IsLoaded||!target.IsAvailable)continue;
            if(actor is not EntityRebirthHumanoidNPC reader||reader.bag?.ItemGrid==null||
                !LiteratureSourcePotentiallyReady(assignment,reader))continue;
            IRebirthNpcWorkAssignmentExecutor[] executors;
            lock(Sync)executors=Executors.ToArray();
            bool executable=false;
            foreach(var executor in executors)
            {try{if(executor.CanExecute(assignment,definition)){executable=true;break;}}catch{}}
            if(!executable)continue;
            if(!RebirthNpcWorkAssignmentService.TryGet(assignment.AssignmentId,out var current)||
                current.Revision!=assignment.Revision||current.Status!=assignment.Status)continue;
            selected=current;return true;
        }
        return false;
    }

    private static bool LiteratureSourcePotentiallyReady(RebirthNpcWorkAssignment assignment,
        EntityRebirthHumanoidNPC reader)
    {
        if(RebirthNpcRecipeStagingWork.Allows(assignment))return RebirthNpcRecipeStagingWork.PotentiallyReady(assignment,reader);
        if(RebirthNpcStorageOrganizationWork.Allows(assignment))return RebirthNpcStorageOrganizationWork.PotentiallyReady(assignment,reader);
        if(RebirthNpcSingleCropWork.Allows(assignment))return RebirthNpcSingleCropWork.PotentiallyReady(assignment,reader);
        if(RebirthP8NpcWaterRefillWork.Allows(assignment))return RebirthP8NpcWaterRefillWork.PotentiallyReady(assignment,reader);
        if(RebirthNpcSingleRepairWork.Allows(assignment))return RebirthNpcSingleRepairWork.PotentiallyReady(assignment,reader);
        if(RebirthNpcRecipeQueueWork.Allows(assignment))return RebirthNpcRecipeQueueWork.PotentiallyReady(assignment,reader);
        // B14 has no PotentiallyReady of its own: a cheap admission-only probe (permission, idle worker). The native
        // harvest gate is re-checked inside the adapter, so a held B14 job never mutates anything.
        if(RebirthNpcB14MiningWork.Allows(assignment))
            return RebirthNpcB14MiningWork.Authorized(assignment)&&reader.bag?.ItemGrid!=null&&
                reader.GetAttackTarget()==null&&reader.Hand!=null&&!reader.Hand.IsBusy;
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
        if(owner==null||owner.IsDead()||reader.GetAttackTarget()!=null||
            reader.Hand==null||reader.Hand.IsBusy)return false;
        var parts=assignment.TargetKey.Split(':');
        if(parts.Length!=3||!int.TryParse(parts[2],out int slot))return false;
        var source=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(parts[1],out _);
        if(source?.Kind!=RemoteResourceSourceKind.StaticContainer)return false;
        if(!RebirthHumanProgression.TryGet(assignment.NpcId,out var progression))return false;
        for(int bag=0;bag<reader.bag.SlotCount;bag++)
        {
            if(reader.bag.ItemGrid[bag]!=null&&!reader.bag.ItemGrid[bag].IsEmpty())continue;
            if(!source.TryProbeSourceSlotForNpc(owner,reader,slot,out var stack)||
                stack?.itemValue?.ItemClass==null)continue;
            if(RebirthProgressionRuntimeConfig.TryGetLiterature(
                stack.itemValue.ItemClass.GetItemName(),out var book)&&
                book!=null&&string.Equals(book.Kind,"discovery",StringComparison.OrdinalIgnoreCase)&&
                !string.IsNullOrEmpty(book.KnowledgeId)&&
                progression?.Progression?.KnowledgeIds?.Contains(
                    RebirthLiteratureService.RecipeReadMarker(book.KnowledgeId))!=true)return true;
        }
        return false;
    }

    public static bool StartNextFeasible(RebirthNpcStableId npcId,out string detail)
    {
        detail="No qualified ready assignment has an available original target.";
        return TrySelectNextFeasible(npcId,out var assignment)&&Start(assignment.AssignmentId,out detail);
    }

    public static bool Start(ulong assignmentId, out string detail)
    {
        detail = string.Empty;
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseAssignmentCoordinatorScope():UseLiteratureCoordinatorScope(assignmentId)))
        {detail="Original Work assignment is held or belongs to another scope";return false;}
        if(RebirthNpcRetainedWorkPackages.BlocksAssignment(assignmentId)){detail="Original embodied Work callback remains retained.";return false;}
        lock (Sync) if (Sessions.ContainsKey(assignmentId)) { detail = "Original session remains held."; return false; }
        RebirthNpcWorkAssignment assignment;
        if (!RebirthNpcWorkAssignmentService.TryGet(assignmentId, out assignment) ||
            assignment.Status != RebirthNpcWorkAssignmentStatus.Ready) { detail = "Assignment unavailable or not ready."; return false; }
        if(!RebirthNpcWorkReleaseGate.Enabled&&
            (!TrySelectNextFeasible(assignment.NpcId,out var chosen)||
             chosen.AssignmentId!=assignmentId))
        {detail="A higher-priority qualified assignment is ready, or this worker is temporarily unavailable.";return false;}
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
        { BeginUnstartedSettle(session, assignmentId); detail = "Original embodiment unavailable; Ready return pending reservation release."; return false; }
        session.Intent = intent;
        lock (Sync) Sessions.Add(assignmentId, session);
        if (!RebirthNpcEmbodiedWorkOwner.TryStage(intent, out detail))
        {
            // TryStage refuses before any owner entry exists, so nothing is staged: settle through the typed
            // readback path (the session stays until the status and reservation readbacks succeed).
            BeginUnstartedSettle(session, assignmentId);
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

    // Start failed after Reserved and before any original callback was staged: no physical effect exists, so the
    // saved Reserved->Ready transition plus ReleaseAndConfirm (retryable, 1..64 s backoff) returns the row to the
    // scheduler. A later Start creates a NEW run/intent; nothing resumes under the old operation id.
    private static void BeginUnstartedSettle(Session session, ulong assignmentId)
    {
        session.Intent = null;
        lock (Sync) { Sessions[assignmentId] = session; SessionSchedule.Add(assignmentId); }
        RunSettle(session, "Start refused before staging; returned to Ready.", "Start refused before staging.",
            RebirthNpcWorkAssignmentStatus.Ready, null);
    }

    public static void Tick(int maximumSessions)
    {
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseAssignmentCoordinatorScope():UseLiteratureCoordinatorTickScope()))return;
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
        if(!(RebirthNpcWorkReleaseGate.Enabled?UseAssignmentCoordinatorScope():UseLiteratureCoordinatorScope(assignmentId)))return false;
        Session session; lock (Sync) if (!Sessions.TryGetValue(assignmentId, out session)) return false;
        if(session.PendingSettle!=null)return true; // TickSession owns durable retry.
        if(RebirthNpcLiteratureWorkGate.Allows(session.Intent))
        {
            if(RebirthHumanRecipeReading.HasSavedWorkRead(session.Intent)||
                !RebirthNpcEmbodiedWorkOwner.TryCancelAndRetireLiterature(session.Intent))return false;
            RunSettle(session,reason,"Literature interrupted.",
                RebirthNpcWorkAssignmentStatus.Suspended,()=>MarkLiteratureRecovered(assignmentId));
            return true; // The session stays until status and reservation read back.
        }
        // Generic path: the session, status and reservation are only dropped by the typed retryable settlement
        // (authoritative status readback + ReleaseAndConfirm); a failed first attempt keeps the original and retries.
        if(!RebirthNpcEmbodiedWorkOwner.RequestCancel(session.Intent,out _))return false;
        RunSettle(session,reason,reason,RebirthNpcWorkAssignmentStatus.Suspended,null);
        return true;
    }

    public static void Reset()
    {
        lock(Sync){if(!CanRetireEmptyAssignmentCoordinator)throw new InvalidOperationException("Original opaque Work assignment callbacks remain retained");assignmentCoordinatorScope=null;SessionSchedule.Clear();}
        ClearP6WorldState();
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
        if(RebirthNpcWorkAssignmentService.TryGet(session.AssignmentId,out assignment)&&
            assignment.Status==RebirthNpcWorkAssignmentStatus.Completed&&
            RebirthNpcQualifiedWorkGate.HasSavedCompletion(assignment))
        {
            SettleSavedLiterature(session,assignment);
            return;
        }
        if(session.PendingSettle!=null){RetrySettle(session);return;}
        if(SettleCancelledLiterature(session)||SettleReleasedCraft(session)||SettleReleasedWater(session))return;
        // A first settle attempt may have failed after the owner was retired: never fall into the generic block
        // (its Suspend would drop the session without the confirmed release and the post action).
        if(session.PendingSettle!=null)return;
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
        if(success&&RebirthNpcQualifiedWorkGate.HasSavedCompletion(assignment)&&
            RebirthNpcEmbodiedWorkOwner.IsDispatching(session.Intent))
        {
            if(!RebirthNpcWorkAssignmentService.TryTransition(session.AssignmentId,assignment.Revision,
                RebirthNpcWorkAssignmentStatus.Completed,RebirthNpcRecipeStagingWork.Allows(assignment)?
                    "Saved ingredient staging outcome committed; recipe execution remains pending.":RebirthNpcStorageOrganizationWork.Allows(assignment)?
                    "Saved exact named-container move committed.":RebirthNpcSingleCropWork.Allows(assignment)?
                    "Saved one-crop harvest and native worker credit committed.":RebirthNpcB14MiningWork.Allows(assignment)?
                    "Saved native block harvest committed.":RebirthP8NpcWaterRefillWork.Allows(assignment)?
                    "Saved provider water refill (source debit, provider credit, bucket conversions) committed.":RebirthNpcRecipeQueueWork.Allows(assignment)?
                    "Saved native recipe queue admission committed; cooking and output collection remain pending.":RebirthNpcSingleRepairWork.Allows(assignment)?
                    "Saved selected-block native repair and material debit committed.":"Saved physical literature outcome committed.",out var completedAssignment))return;
            if(!RebirthNpcEmbodiedWorkOwner.TryCompleteLiterature(session.Intent,completedAssignment))return;
            if(!ReleaseSavedLiteratureReservation(session))return;
            lock(Sync){Sessions.Remove(session.AssignmentId);SessionSchedule.Remove(session.AssignmentId);}
            Interlocked.Increment(ref completed);
            return;
        }
        // Tick completion and void End cannot authenticate native settlement.
        // Begun work remains held without terminal transition or generic release.
        Suspend(session.AssignmentId, detail);
    }
    // Proved interruption (Follow/combat/unload Reset): the book is back in its exact
    // bag cell under a returned operation receipt. Retire the owner, release the
    // reservation and leave the job Suspended (incomplete, never Ready) so the next
    // feasible job may run. A relaunch of the same operation is refused by
    // TryBeginFromWorkBag. Not a completion and not an award.
    private static bool SettleCancelledLiterature(Session session)
    {
        if(session?.Intent==null||!RebirthNpcLiteratureWorkGate.Allows(session.Intent))return false;
        // The proof reads saved rows and parses XML: probe at most once a second per session.
        long now=DateTime.UtcNow.Ticks;
        if(now<session.NextCancelProbeTicks)return false;
        session.NextCancelProbeTicks=now+TimeSpan.TicksPerSecond;
        // Retire only after the saved returned-book receipt (checked inside TryRetire...) AND proof that the
        // original literature navigation route is stopped (true when no route state remains).
        // The receipt check is side-effect free and must come first: stopping the route is a real navigator action
        // that a healthy session must never receive.
        if(!RebirthHumanRecipeReading.HasReturnedCancelReceipt(session.Intent)||
            !RebirthNpcWorkNavigationService.TryStopInterruptedLiterature(session.Intent)||
            !RebirthNpcEmbodiedWorkOwner.TryRetireCancelledLiterature(session.Intent))return false;
        return RunSettle(session,"Interrupted; exact book returned under saved receipt.","Literature interrupted.",
            RebirthNpcWorkAssignmentStatus.Suspended,()=>MarkLiteratureRecovered(session.AssignmentId));
    }
    // Escalating backoff (1 s .. 64 s) so an unresolvable settlement (assignment trimmed from history, different
    // terminal status, reservation scope staying unavailable) never spins; one loud warning, the session is kept.
    private static void RetrySettle(Session session)
    {
        long now=DateTime.UtcNow.Ticks;
        if(now<session.NextSettleRetryTicks)return;
        if(session.PendingSettle())return;
        session.SettleAttempts++;
        if(session.SettleAttempts==20)
            Log.Warning("[REBIRTH NPC Work] Settlement of assignment "+session.AssignmentId+
                " cannot be proven (status or reservation readback keeps failing); session and saved original are kept.");
        session.NextSettleRetryTicks=now+TimeSpan.TicksPerSecond*(1L<<Math.Min(session.SettleAttempts,6));
    }
    // The owner is retired from here on, so the settlement is stored on the session and re-entered by
    // TickSession until the authoritative readbacks succeed; no physical effect is ever replayed.
    private static bool RunSettle(Session session,string detail,string releaseReason,
        RebirthNpcWorkAssignmentStatus to,Action post)
    {
        session.PendingSettle=()=>
        {
            if(!FinishSuspended(session,detail,releaseReason,to))return false;
            post?.Invoke();return true;
        };
        return session.PendingSettle();
    }
    // Shared tail of the settle paths: authoritative assignment-status readback, then exact reservation
    // release-or-absence readback (scope-unavailable is NOT absence), and only then the session is dropped.
    private static bool FinishSuspended(Session session,string detail,string releaseReason,
        RebirthNpcWorkAssignmentStatus to=RebirthNpcWorkAssignmentStatus.Suspended)
    {
        if(!session.StatusDone)
        {
            if(!RebirthNpcWorkAssignmentService.TryGet(session.AssignmentId,out var live))return false;
            if(live.Status!=to)
            {
                if(!RebirthNpcWorkAssignmentService.TryTransition(session.AssignmentId,live.Revision,to,detail,out _)||
                    !RebirthNpcWorkAssignmentService.TryGet(session.AssignmentId,out live)||live.Status!=to)return false;
            }
            session.StatusDone=true;
        }
        if(!RebirthNpcWorkReservationService.ReleaseAndConfirm(session.AssignmentId,releaseReason))return false;
        // Final re-read: a retry after StatusDone must still see the target status before the session disappears.
        if(!RebirthNpcWorkAssignmentService.TryGet(session.AssignmentId,out var finalRead)||finalRead.Status!=to)
        {session.StatusDone=false;return false;}
        lock(Sync){Sessions.Remove(session.AssignmentId);SessionSchedule.Remove(session.AssignmentId);}
        Interlocked.Increment(ref suspended);return true;
    }
    // Authoritative assignment-level settlement used by restart recovery (no session exists).
    internal static bool SettleAssignmentAuthoritative(ulong assignmentId,RebirthNpcWorkAssignmentStatus to,
        string detail,string releaseReason)
    {
        if(!RebirthNpcWorkAssignmentService.TryGet(assignmentId,out var live))return false;
        if(live.Status!=to&&(!RebirthNpcWorkAssignmentService.TryTransition(assignmentId,live.Revision,to,detail,out _)||
            !RebirthNpcWorkAssignmentService.TryGet(assignmentId,out live)||live.Status!=to))return false;
        return RebirthNpcWorkReservationService.ReleaseAndConfirm(assignmentId,releaseReason);
    }
    // P8 water refill that ended with a safe-to-release row (bucket-kept filled, or settled): same
    // settlement as a released craft; P8 releases harvest-publication attempts through ReleaseFor.
    private static bool SettleReleasedWater(Session session)
    {
        var a=session?.Assignment;
        if(a==null||session.Intent==null||!RebirthP8NpcWaterRefillWork.Allows(a))return false;
        long now=DateTime.UtcNow.Ticks;
        if(now<session.NextReleaseProbeTicks)return false;
        session.NextReleaseProbeTicks=now+TimeSpan.TicksPerSecond;
        if(!RebirthP8NpcWaterRefillWork.IsReleasedTerminal(a.NpcId,session.AssignmentId)||
            !RebirthNpcEmbodiedWorkOwner.TryRetireReleasedTerminal(session.Intent))return false;
        // Terminal Cancelled (not Suspended): Assignment.Create rejects an equivalent non-terminal assignment for
        // the same provider, which would block the maintainer's new assignment that resumes the kept row.
        return RunSettle(session,"Water refill ended; the saved row is kept and a new assignment resumes it.",
            "Water refill ended.",RebirthNpcWorkAssignmentStatus.Cancelled,
            ()=>RebirthP8NpcWaterRefillWork.ReleaseFor(a.NpcId,session.AssignmentId));
    }
    // P7 ended the v2 craft job terminally (Failed/PersistenceRecovery) with the queue original
    // released (unresolved, or output-attributed while pickup is held). The saved row is kept as the
    // record of the spent ingredient and attributed output; the job ends Cancelled (terminal, incomplete,
    // no award), exactly like the restart settle, so the next job may run and Assignment.Create's duplicate
    // check does not block re-assigning the same recipe/station. Suspended is reserved for resumable jobs.
    private static bool SettleReleasedCraft(Session session)
    {
        var a=session?.Assignment;
        if(a==null||session.Intent==null||!RebirthNpcRecipeQueueWork.Allows(a))return false;
        long now=DateTime.UtcNow.Ticks;
        if(now<session.NextReleaseProbeTicks)return false;
        session.NextReleaseProbeTicks=now+TimeSpan.TicksPerSecond;
        if(!RebirthNpcStationQueueOriginal.IsReleasedTerminal(a.NpcId,session.AssignmentId)||
            !RebirthNpcEmbodiedWorkOwner.TryRetireReleasedTerminal(session.Intent))return false;
        return RunSettle(session,"Craft ended; station queue original released and kept.","Craft ended.",
            RebirthNpcWorkAssignmentStatus.Cancelled,
            ()=>RebirthNpcStationQueueOriginal.ReleaseWatch(a.NpcId,session.AssignmentId));
    }
    private static bool SettleSavedLiterature(Session session,RebirthNpcWorkAssignment completedAssignment)
    {
        if(session==null||completedAssignment?.AssignmentId!=session.AssignmentId||
            completedAssignment.Status!=RebirthNpcWorkAssignmentStatus.Completed||
            !RebirthNpcQualifiedWorkGate.HasSavedCompletion(completedAssignment)||
            !RebirthNpcEmbodiedWorkOwner.TryRetireSavedLiterature(session.Intent,completedAssignment))return false;
        if(!ReleaseSavedLiteratureReservation(session))return false;
        lock(Sync){Sessions.Remove(session.AssignmentId);SessionSchedule.Remove(session.AssignmentId);}
        Interlocked.Increment(ref completed);return true;
    }
    private static bool ReleaseSavedLiteratureReservation(Session session)
    {
        // TryGetForAssignment is false for both "absent" and "scope unavailable": use the distinguishing readback and
        // keep the session (the caller retries from the Completed assignment) until absence is proven.
        var state=RebirthNpcWorkReservationService.GetPresence(session.AssignmentId);
        if(state==RebirthNpcWorkReservationService.P6Presence.ScopeUnavailable)return false;
        if(state==RebirthNpcWorkReservationService.P6Presence.Present&&
            RebirthNpcWorkReservationService.TryGetForAssignment(session.AssignmentId,out var live)&&
            live.ReservationId!=session.ReservationId)return false;
        return RebirthNpcWorkReservationService.ReleaseAndConfirm(session.AssignmentId,"Saved literature complete.");
    }
}
