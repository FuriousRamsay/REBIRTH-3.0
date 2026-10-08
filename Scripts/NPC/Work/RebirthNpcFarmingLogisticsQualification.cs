using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcFarmingLogisticsQualificationSnapshot
{
    public bool IsValid;
    public int FarmingPackages;
    public int LogisticsPackages;
    public int AssignmentReferences;
    public int DuplicateAssignmentReferences;
    public int MissingAssignments;
    public int TerminalAssignmentsRetained;
    public int StalledLogisticsPackages;
    public int SettlementEndpoints;
    public int ActiveEndpointReservations;
    public int InvalidEndpointReservations;
    public long CapturedUtcTicks;
}

/// <summary>Read-only qualification boundary for farming packages, settlement logistics packages,
/// assignment ownership, and settlement endpoint reservations.</summary>
public static class RebirthNpcFarmingLogisticsQualificationService
{
    private static readonly object Sync = new object();
    private static RebirthNpcFarmingLogisticsQualificationSnapshot last = Empty();
    private static long audits, failures, resets;

    public static RebirthNpcFarmingLogisticsQualificationSnapshot Audit()
    {
        RebirthNpcFarmingLogisticsQualificationSnapshot result = Empty();
        HashSet<ulong> seen = new HashSet<ulong>();
        AuditPackages(RebirthNpcFarmingWorkPackageService.CaptureActivePackages(), true, seen, result);
        AuditPackages(RebirthNpcSettlementLogisticsPackageService.CaptureActivePackages(), false, seen, result);
        RebirthNpcSettlementEndpointSnapshot[] endpoints =
            RebirthNpcSettlementInventoryEndpointRegistry.CaptureSnapshots();
        result.SettlementEndpoints = endpoints.Length;
        for (int i = 0; i < endpoints.Length; i++)
        {
            RebirthNpcSettlementEndpointSnapshot endpoint = endpoints[i];
            result.ActiveEndpointReservations += endpoint.ActiveDebitReservations + endpoint.ActiveCreditReservations;
            if (endpoint.ActiveDebitReservations < 0 || endpoint.ActiveCreditReservations < 0 ||
                endpoint.ReservedDebitQuantity < 0) result.InvalidEndpointReservations++;
        }
        result.IsValid = result.DuplicateAssignmentReferences == 0 && result.MissingAssignments == 0 &&
            result.InvalidEndpointReservations == 0;
        result.CapturedUtcTicks = DateTime.UtcNow.Ticks;
        lock (Sync) last = result;
        Interlocked.Increment(ref audits);
        if (!result.IsValid) Interlocked.Increment(ref failures);
        return result;
    }

    private static void AuditPackages(RebirthNpcWorkPackageSnapshot[] packages, bool farming,
        HashSet<ulong> seen, RebirthNpcFarmingLogisticsQualificationSnapshot result)
    {
        if (farming) result.FarmingPackages = packages.Length; else result.LogisticsPackages = packages.Length;
        for (int i = 0; i < packages.Length; i++)
        {
            RebirthNpcWorkPackageSnapshot package = packages[i];
            if (!farming && package.ConsecutiveStalledReconciliations >= 30) result.StalledLogisticsPackages++;
            ulong[] ids = package.AssignmentIds ?? new ulong[0];
            for (int n = 0; n < ids.Length; n++)
            {
                result.AssignmentReferences++;
                if (!seen.Add(ids[n])) result.DuplicateAssignmentReferences++;
                RebirthNpcWorkAssignment assignment;
                if (!RebirthNpcWorkAssignmentService.TryGet(ids[n], out assignment))
                {
                    result.MissingAssignments++;
                    continue;
                }
                if (assignment.Status == RebirthNpcWorkAssignmentStatus.Completed ||
                    assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled ||
                    assignment.Status == RebirthNpcWorkAssignmentStatus.Failed)
                    result.TerminalAssignmentsRetained++;
            }
        }
    }

    public static void ResetForWorldChange()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;

        RebirthNpcFarmingWorkPackageService.ResetForWorldChange();
        RebirthNpcSettlementLogisticsPackageService.ResetForWorldChange();
        RebirthNpcSettlementInventoryEndpointRegistry.ResetForWorldChange();
        lock (Sync) last = Empty();
        Interlocked.Increment(ref resets);
    }

    public static string GetReport()
    {
        RebirthNpcFarmingLogisticsQualificationSnapshot s; lock (Sync) s = last;
        return new StringBuilder("[REBIRTH NPC Farming/Logistics Qualification] status=").Append(Interlocked.Read(ref audits)==0?"NotRun":(s.IsValid?"StructuralPass":"StructuralFail")).Append(" valid=").Append(s.IsValid)
            .Append(" audits=").Append(Interlocked.Read(ref audits)).Append(" failures=").Append(Interlocked.Read(ref failures))
            .Append(" resets=").Append(Interlocked.Read(ref resets)).Append(" farmingPackages=").Append(s.FarmingPackages)
            .Append(" logisticsPackages=").Append(s.LogisticsPackages).Append(" assignmentReferences=").Append(s.AssignmentReferences)
            .Append(" duplicateAssignments=").Append(s.DuplicateAssignmentReferences).Append(" missingAssignments=").Append(s.MissingAssignments)
            .Append(" terminalRetained=").Append(s.TerminalAssignmentsRetained).Append(" stalledLogistics=").Append(s.StalledLogisticsPackages)
            .Append(" settlementEndpoints=").Append(s.SettlementEndpoints).Append(" activeEndpointReservations=").Append(s.ActiveEndpointReservations)
            .Append(" invalidEndpointReservations=").Append(s.InvalidEndpointReservations).ToString();
    }

    private static RebirthNpcFarmingLogisticsQualificationSnapshot Empty()
    { return new RebirthNpcFarmingLogisticsQualificationSnapshot { IsValid = false, CapturedUtcTicks = 0L }; }
}
