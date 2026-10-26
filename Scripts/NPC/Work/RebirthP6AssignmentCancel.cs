using System;
using System.Threading;

// P6: owner-initiated Remove of a physical assignment that never started (works under the release hold).
public static partial class RebirthNpcWorkAssignmentService
{
    // Succeeds only for a qualified assignment with no session, owner, reservation, custody or saved
    // original of any kind. Otherwise returns Conflict with a refusal reason (custody held / busy).
    public static RebirthNpcWorkAssignmentResult CancelUnstarted(string sessionKey, ulong assignmentId,
        out RebirthNpcWorkAssignment assignment, out string error)
        => CancelUnstarted(sessionKey, assignmentId, 0U, out assignment, out error);

    public static RebirthNpcWorkAssignmentResult CancelUnstarted(string sessionKey, ulong assignmentId,
        uint expectedRevision, out RebirthNpcWorkAssignment assignment, out string error)
    {
        assignment = null; error = string.Empty;
        var world = GameManager.Instance?.World;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote())
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied, "Only the authoritative server may remove an assignment.", out error);
        if (!UseLiteratureAssignmentScope(assignmentId))
            return Reject(RebirthNpcWorkAssignmentResult.NotFound, "Assignment was not found or is not a physical assignment.", out error);
        var originalScope = assignmentRecordScope;
        RebirthNpcInteractionContext context;
        var authorization = RebirthNpcInteractionSessionService.Authorize(sessionKey, RebirthNpcInteractionAction.AssignWork, out context);
        if (authorization != RebirthNpcInteractionResult.Allowed)
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied, "Interaction authorization failed: " + authorization, out error);
        RebirthNpcWorkAssignment seen;
        if (!TryGet(assignmentId, out seen))
            return Reject(RebirthNpcWorkAssignmentResult.NotFound, "Assignment was not found.", out error);
        if (seen.NpcId != context.NpcId)
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied, "Assignment does not belong to the interaction NPC.", out error);
        if (!string.Equals(seen.ActorId, context.ActorId, StringComparison.OrdinalIgnoreCase) &&
            (context.Permissions & RebirthNpcInteractionPermissions.Admin) == 0)
            return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied, "Only the assigning actor or an administrator may remove this assignment.", out error);
        if (expectedRevision != 0U && seen.Revision != expectedRevision)
            return Reject(RebirthNpcWorkAssignmentResult.RevisionConflict, "Assignment revision changed.", out error);
        // Released craft jobs are settled to Suspended and released water jobs to Cancelled (terminal) by the
        // coordinator, so a Failed row never reaches here as removable work: Failed is refused by StartedReason.
        string held = StartedReason(seen);
        if (held != null) return Reject(RebirthNpcWorkAssignmentResult.Conflict, held, out error);
        lock (Sync)
        {
            RebirthNpcWorkAssignment current;
            if (!ById.TryGetValue(assignmentId, out current))
                return Reject(RebirthNpcWorkAssignmentResult.NotFound, "Assignment was not found.", out error);
            if (current.Revision != seen.Revision || current.Status != seen.Status)
                return Reject(RebirthNpcWorkAssignmentResult.RevisionConflict, "Assignment changed while it was being removed.", out error);
            if (!AssignmentOriginalCurrent(originalScope))
                return Reject(RebirthNpcWorkAssignmentResult.PermissionDenied, "Original Work scope changed.", out error);
            SetStatusLocked(current, RebirthNpcWorkAssignmentStatus.Cancelled, "Removed by its owner before it started.");
            RebirthNpcWorkReservationService.ReleaseAssignment(assignmentId, "Assignment removed.");
            assignment = current.Clone();
            Interlocked.Increment(ref cancelled);
            if (RebirthNpcRecipeQueueWork.Allows(current))
                RebirthNpcStationQueueOriginal.ReleaseWatch(current.NpcId, current.AssignmentId);
            return RebirthNpcWorkAssignmentResult.Succeeded;
        }
    }

    // Null when nothing has started; otherwise the player-facing reason. Conservative: any doubt refuses.
    internal static string StartedReason(RebirthNpcWorkAssignment a)
    {
        if (a == null) return "Assignment is unavailable.";
        if (a.Status != RebirthNpcWorkAssignmentStatus.Pending && a.Status != RebirthNpcWorkAssignmentStatus.Scheduled &&
            a.Status != RebirthNpcWorkAssignmentStatus.Ready && a.Status != RebirthNpcWorkAssignmentStatus.Suspended)
            return "The worker is busy with this assignment.";
        if (RebirthNpcWorkAssignmentCoordinator.HasSession(a.AssignmentId) ||
            RebirthNpcWorkReservationService.TryGetForAssignment(a.AssignmentId, out _))
            return "The worker is busy with this assignment.";
        if (RebirthNpcEmbodiedWorkOwner.TryGetLiteratureIntent(ActorOf(a), out var intent) && intent.PrimaryId == a.AssignmentId)
            return "The worker is busy with this assignment.";
        if (RebirthNpcRetainedWorkCustody.BlocksAssignment(a.AssignmentId) || RebirthNpcRetainedWorkPackages.BlocksAssignment(a.AssignmentId))
            return "Work custody is held for this assignment.";
        Guid op = RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        Guid credit = RebirthNpcStorageOrganizationWork.CreditOperation(a.AssignmentId);
        try
        {
            bool settledBook = a.Status == RebirthNpcWorkAssignmentStatus.Suspended && RebirthNpcWorkAssignmentCoordinator.LiteratureReturnedUnderReceipt(a);
            if (!settledBook && RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId, op, out _) ||
                RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId, credit, out _) ||
                RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId, op, out _) ||
                RebirthNpcStationQueueOriginal.TryRead(a.NpcId, op, out _) &&
                    !RebirthNpcStationQueueOriginal.IsReleasedTerminal(a.NpcId, a.AssignmentId) ||
                RebirthNpcFarmOriginal.TryReadSaved(a.NpcId, op, out _) ||
                RebirthP8NpcWaterRefillWork.TryReadSaved(a.NpcId, op, out _) &&
                    !RebirthP8NpcWaterRefillWork.IsReleasedTerminal(a.NpcId, a.AssignmentId))
                return "An item or station is already in this worker's custody for the assignment.";
            if (RebirthNpcSingleRepairWork.Allows(a) && RebirthNpcStructureRepairOriginal.HasNativePending(a.NpcId))
                return "A repair is already in progress for this worker.";
        }
        catch { return "Custody could not be verified; the assignment is kept."; }
        return null;
    }
    private static EntityRebirthNPC ActorOf(RebirthNpcWorkAssignment a)
        => RebirthNpcWorkNavigationService.TryResolveNpc(a.NpcId, out var npc) ? npc : null;
}
