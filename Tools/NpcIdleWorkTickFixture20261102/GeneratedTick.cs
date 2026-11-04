using System; public static partial class RebirthNpcWorkAssignmentCoordinator {public static void Tick(int maximumSessions)
    {
        // No sessions or scheduled callbacks means this tick has no work to
        // authorize. Avoid recovery-generation filesystem reads for an idle
        // coordinator; admission and every nonempty tick retain their checks.
        lock (Sync) if (Sessions.Count == 0 && SessionSchedule.Count == 0) return;
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
    }}