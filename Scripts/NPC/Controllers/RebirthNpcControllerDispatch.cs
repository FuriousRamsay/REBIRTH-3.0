using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public readonly struct RebirthNpcControllerContext
{
    public readonly EntityRebirthNPC Npc;
    public readonly RebirthNpcExecutionLease Lease;
    public readonly RebirthNpcActivityRecoveryTicket Recovery;
    public readonly ulong ActivityId;

    public bool IsRecovery => Recovery != null;

    public RebirthNpcControllerContext(EntityRebirthNPC npc, RebirthNpcExecutionLease lease,
        RebirthNpcActivityRecoveryTicket recovery, ulong activityId)
    {
        Npc = npc;
        Lease = lease;
        Recovery = recovery;
        ActivityId = activityId;
    }
}

public interface IRebirthNpcActivityController
{
    string ControllerId { get; }
    int Priority { get; }
    bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease);
    int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease);
    void Start(RebirthNpcControllerContext context);
    void Recover(RebirthNpcControllerContext context);
}

public static class RebirthNpcActivityControllerRegistry
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcActivityController> Controllers =
        new List<IRebirthNpcActivityController>();

    public static bool Register(IRebirthNpcActivityController controller)
    {
        if (controller == null || string.IsNullOrWhiteSpace(controller.ControllerId)) return false;
        lock (Sync)
        {
            for (int i = 0; i < Controllers.Count; i++)
                if (string.Equals(Controllers[i].ControllerId, controller.ControllerId,
                        StringComparison.Ordinal)) return false;
            Controllers.Add(controller);
            Controllers.Sort(CompareControllers);
            return true;
        }
    }

    public static bool Unregister(string controllerId)
    {
        if (string.IsNullOrWhiteSpace(controllerId)) return false;
        lock (Sync)
        {
            for (int i = 0; i < Controllers.Count; i++)
                if (string.Equals(Controllers[i].ControllerId, controllerId,
                        StringComparison.Ordinal))
                { Controllers.RemoveAt(i); return true; }
            return false;
        }
    }

    public static IRebirthNpcActivityController Resolve(EntityRebirthNPC npc,
        RebirthNpcExecutionLease lease)
    {
        lock (Sync)
        {
            for (int i = 0; i < Controllers.Count; i++)
            {
                try { if (Controllers[i].CanHandle(npc, lease)) return Controllers[i]; }
                catch (Exception ex) { Log.Warning("[REBIRTH NPC] Controller " +
                    Controllers[i].ControllerId + " CanHandle failed: " + ex.Message); }
            }
        }
        return null;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder();
            b.Append("[REBIRTH NPC] activity controllers registered=").Append(Controllers.Count).AppendLine();
            for (int i = 0; i < Controllers.Count; i++)
                b.Append("  id=").Append(Controllers[i].ControllerId)
                    .Append(" priority=").Append(Controllers[i].Priority)
                    .Append(" type=").Append(Controllers[i].GetType().FullName).AppendLine();
            return b.ToString().TrimEnd();
        }
    }

    private static int CompareControllers(IRebirthNpcActivityController left,
        IRebirthNpcActivityController right)
    {
        int priority = right.Priority.CompareTo(left.Priority);
        return priority != 0 ? priority : string.Compare(left.ControllerId, right.ControllerId,
            StringComparison.Ordinal);
    }
}

public static class RebirthNpcActivityDispatcher
{
    private const int MaxDispatchesPerFrame = 8;

    public static void Tick()
    {
        RebirthNpcExecutionLease[] leases = RebirthNpcExecutionLeaseRegistry.GetSnapshot();
        int dispatched = 0;
        for (int i = 0; i < leases.Length && dispatched < MaxDispatchesPerFrame; i++)
        {
            RebirthNpcExecutionLease lease = leases[i];
            if(RebirthNpcWorkReleaseGate.Hold(lease.Order)) continue; // Before resolve/recovery claim/start.
            if (lease.Status != RebirthNpcExecutionLeaseStatus.Active) continue;
            if (RebirthNpcActivityRegistry.HasRunningActivity(lease.TargetEntityId, lease.LeaseId)) continue;

            Entity entity = GameManager.Instance.World.GetEntity(lease.TargetEntityId);
            EntityRebirthNPC npc = entity as EntityRebirthNPC;
            if (npc == null) continue;

            IRebirthNpcActivityController controller =
                RebirthNpcActivityControllerRegistry.Resolve(npc, lease);
            if (controller == null) continue;

            RebirthNpcActivityRecoveryTicket recovery;
            bool hasPendingRecovery = RebirthNpcActivityRecoveryRegistry.HasPending(
                lease.TargetEntityId, lease.LeaseId);
            bool isRecovery = RebirthNpcActivityRecoveryRegistry.TryClaim(
                lease.TargetEntityId, lease.LeaseId, controller.ControllerId, out recovery);
            if (hasPendingRecovery && !isRecovery) continue;
            int timeout = controller.GetHeartbeatTimeoutSeconds(lease);
            RebirthNpcActivityStartResult start = RebirthNpcActivityRegistry.TryStart(
                lease.TargetEntityId, lease.LeaseId, controller.ControllerId, timeout);
            if (!start.Succeeded) continue;

            RebirthNpcControllerContext context = new RebirthNpcControllerContext(
                npc, lease, isRecovery ? recovery : null, start.ActivityId);
            try
            {
                if (isRecovery) controller.Recover(context); else controller.Start(context);
                RebirthNpcActivityDispatchDiagnostics.RecordDispatched(isRecovery);
            }
            catch (Exception ex)
            {
                RebirthNpcActivityRegistry.TryReportResult(lease.TargetEntityId,
                    start.ActivityId, lease.LeaseId, lease.AcceptedRevision,
                    RebirthNpcActivityOutcome.ControllerFault,
                    "Controller " + controller.ControllerId + " threw during dispatch: " + ex.Message);
                RebirthNpcActivityDispatchDiagnostics.RecordFault();
            }
            dispatched++;
        }
    }
}

public static class RebirthNpcActivityDispatchDiagnostics
{
    private static long starts, recoveries, faults;
    internal static void RecordDispatched(bool recovery) { if (recovery) recoveries++; else starts++; }
    internal static void RecordFault() { faults++; }
    public static string GetReport() => RebirthNpcActivityControllerRegistry.GetReport() +
        "\n[REBIRTH NPC] dispatch starts=" + starts + " recoveries=" + recoveries + " faults=" + faults;
}
