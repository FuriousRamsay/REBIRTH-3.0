using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcProductionResourceQualificationSnapshot
{
    public bool IsValid;
    public int WorkstationJobs;
    public int TerminalWorkstationJobsRetained;
    public int StaleSuspendedWorkstationJobs;
    public int ConstructionPackages;
    public int MiningPackages;
    public int ScavengingPackages;
    public int InvalidPackageIndexes;
    public int AssignmentReferences;
    public int DuplicateAssignmentReferences;
    public int MissingAssignments;
    public int TerminalAssignmentsRetained;
    public long CapturedUtcTicks;
}

/// <summary>Read-only qualification boundary for workstation orchestration and the
/// construction, mining, and scavenging package owners.</summary>
public static class RebirthNpcProductionResourceQualificationService
{
    private static readonly object Sync = new object();
    private static RebirthNpcProductionResourceQualificationSnapshot last = Empty();
    private static long audits, failures, resets;
    private static readonly long StaleSuspendedTicks = TimeSpan.FromMinutes(30).Ticks;

    public static RebirthNpcProductionResourceQualificationSnapshot Audit()
    {
        RebirthNpcProductionResourceQualificationSnapshot result = Empty();
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcWorkstationJobSnapshot[] jobs = RebirthNpcWorkstationExecutionService.GetSnapshots();
        result.WorkstationJobs = jobs.Length;
        for (int i = 0; i < jobs.Length; i++)
        {
            RebirthNpcWorkstationJobSnapshot job = jobs[i];
            if (job.State == RebirthNpcWorkstationJobState.Completed ||
                job.State == RebirthNpcWorkstationJobState.Failed ||
                job.State == RebirthNpcWorkstationJobState.Cancelled)
                result.TerminalWorkstationJobsRetained++;
            if (job.State == RebirthNpcWorkstationJobState.Suspended &&
                job.UpdatedUtcTicks > 0 && now - job.UpdatedUtcTicks >= StaleSuspendedTicks)
                result.StaleSuspendedWorkstationJobs++;
        }

        HashSet<ulong> seen = new HashSet<ulong>();
        RebirthNpcConstructionPackageSnapshot[] construction = RebirthNpcConstructionWorkPackageService.GetSnapshots();
        result.ConstructionPackages = construction.Length;
        for (int i = 0; i < construction.Length; i++)
            AuditPackage(construction[i].AssignmentIds, construction[i].CurrentIndex,
                construction[i].AssignmentCount, seen, result);

        RebirthNpcMiningPackageSnapshot[] mining = RebirthNpcMiningWorkPackageService.GetSnapshots();
        result.MiningPackages = mining.Length;
        for (int i = 0; i < mining.Length; i++)
            AuditPackage(mining[i].AssignmentIds, mining[i].CurrentIndex,
                mining[i].AssignmentCount, seen, result);

        RebirthNpcScavengingPackageSnapshot[] scavenging = RebirthNpcScavengingWorkPackageService.GetSnapshots();
        result.ScavengingPackages = scavenging.Length;
        for (int i = 0; i < scavenging.Length; i++)
            AuditPackage(scavenging[i].AssignmentIds, scavenging[i].CurrentIndex,
                scavenging[i].AssignmentCount, seen, result);

        result.IsValid = result.InvalidPackageIndexes == 0 &&
            result.DuplicateAssignmentReferences == 0 && result.MissingAssignments == 0;
        result.CapturedUtcTicks = now;
        lock (Sync) last = result;
        Interlocked.Increment(ref audits);
        if (!result.IsValid) Interlocked.Increment(ref failures);
        return result;
    }

    private static void AuditPackage(ulong[] ids, int currentIndex, int assignmentCount,
        HashSet<ulong> seen, RebirthNpcProductionResourceQualificationSnapshot result)
    {
        ids = ids ?? new ulong[0];
        if (assignmentCount != ids.Length || currentIndex < 0 || currentIndex > ids.Length)
            result.InvalidPackageIndexes++;
        for (int i = 0; i < ids.Length; i++)
        {
            result.AssignmentReferences++;
            if (!seen.Add(ids[i])) result.DuplicateAssignmentReferences++;
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(ids[i], out assignment))
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

    public static void ResetForWorldChange()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;

        RebirthNpcWorkstationExecutionService.ResetForWorldChange();
        RebirthNpcConstructionWorkPackageService.ResetForWorldChange();
        RebirthNpcMiningWorkPackageService.ResetForWorldChange();
        RebirthNpcScavengingWorkPackageService.ResetForWorldChange();
        lock (Sync) last = Empty();
        Interlocked.Increment(ref resets);
    }

    public static string GetReport()
    {
        RebirthNpcProductionResourceQualificationSnapshot s;
        lock (Sync) s = last;
        return new StringBuilder("[REBIRTH NPC Production/Resource Qualification] status=").Append(Interlocked.Read(ref audits)==0?"NotRun":(s.IsValid?"StructuralPass":"StructuralFail")).Append(" valid=").Append(s.IsValid)
            .Append(" audits=").Append(Interlocked.Read(ref audits)).Append(" failures=").Append(Interlocked.Read(ref failures))
            .Append(" resets=").Append(Interlocked.Read(ref resets)).Append(" workstationJobs=").Append(s.WorkstationJobs)
            .Append(" terminalWorkstationRetained=").Append(s.TerminalWorkstationJobsRetained)
            .Append(" staleSuspendedWorkstations=").Append(s.StaleSuspendedWorkstationJobs)
            .Append(" constructionPackages=").Append(s.ConstructionPackages).Append(" miningPackages=").Append(s.MiningPackages)
            .Append(" scavengingPackages=").Append(s.ScavengingPackages).Append(" invalidPackageIndexes=").Append(s.InvalidPackageIndexes)
            .Append(" assignmentReferences=").Append(s.AssignmentReferences).Append(" duplicateAssignments=").Append(s.DuplicateAssignmentReferences)
            .Append(" missingAssignments=").Append(s.MissingAssignments).Append(" terminalAssignmentsRetained=").Append(s.TerminalAssignmentsRetained)
            .ToString();
    }

    private static RebirthNpcProductionResourceQualificationSnapshot Empty()
    { return new RebirthNpcProductionResourceQualificationSnapshot { IsValid = false, CapturedUtcTicks = 0L }; }
}
