using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcDutyMedicalQualificationSnapshot
{
    public bool IsValid;
    public int GuardPackages;
    public int MedicalPackages;
    public int AssignmentReferences;
    public int DuplicateAssignmentReferences;
    public int MissingAssignments;
    public int WrongAssignmentKinds;
    public int TerminalAssignmentsRetained;
    public int StalePackages;
    public int GuardAdapterStates;
    public int MedicalAdapterStates;
    public int OrphanAdapterStates;
    public int AdapterKindMismatches;
    public long CapturedUtcTicks;
}

/// <summary>Read-only qualification boundary for guard-duty and medical-treatment package,
/// assignment, and active adapter ownership.</summary>
public static class RebirthNpcDutyMedicalQualificationService
{
    private static readonly object Sync = new object();
    private static readonly long StalePackageTicks = TimeSpan.FromMinutes(30).Ticks;
    private static RebirthNpcDutyMedicalQualificationSnapshot last = Empty();
    private static long audits, failures, resets;

    public static RebirthNpcDutyMedicalQualificationSnapshot Audit()
    {
        RebirthNpcDutyMedicalQualificationSnapshot result = Empty();
        long now = DateTime.UtcNow.Ticks;
        HashSet<ulong> packageAssignments = new HashSet<ulong>();

        RebirthNpcGuardDutyPackageSnapshot[] guards = RebirthNpcGuardDutyWorkPackageService.GetSnapshots();
        result.GuardPackages = guards.Length;
        for (int i = 0; i < guards.Length; i++)
            AuditPackage(guards[i].AssignmentId, guards[i].UpdatedUtcTicks, RebirthNpcWorkKind.GuardDuty, now, packageAssignments, result);

        RebirthNpcMedicalTreatmentPackageSnapshot[] medical = RebirthNpcMedicalTreatmentWorkPackageService.GetSnapshots();
        result.MedicalPackages = medical.Length;
        for (int i = 0; i < medical.Length; i++)
            AuditPackage(medical[i].AssignmentId, medical[i].UpdatedUtcTicks, RebirthNpcWorkKind.Medical, now, packageAssignments, result);

        AuditAdapterStates(RebirthNpcGuardPostWorkAdapter.GetActiveAssignmentIds(), RebirthNpcWorkKind.GuardDuty, result, true);
        AuditAdapterStates(RebirthNpcMedicalTreatmentAdapter.GetActiveAssignmentIds(), RebirthNpcWorkKind.Medical, result, false);

        result.IsValid = result.DuplicateAssignmentReferences == 0 && result.MissingAssignments == 0 &&
            result.WrongAssignmentKinds == 0 && result.OrphanAdapterStates == 0 && result.AdapterKindMismatches == 0;
        result.CapturedUtcTicks = now;
        lock (Sync) last = result;
        Interlocked.Increment(ref audits);
        if (!result.IsValid) Interlocked.Increment(ref failures);
        return result;
    }

    private static void AuditPackage(ulong assignmentId, long updatedUtcTicks, RebirthNpcWorkKind expectedKind, long now,
        HashSet<ulong> seen, RebirthNpcDutyMedicalQualificationSnapshot result)
    {
        result.AssignmentReferences++;
        if (!seen.Add(assignmentId)) result.DuplicateAssignmentReferences++;
        if (updatedUtcTicks > 0 && now - updatedUtcTicks >= StalePackageTicks) result.StalePackages++;
        RebirthNpcWorkAssignment assignment;
        if (!RebirthNpcWorkAssignmentService.TryGet(assignmentId, out assignment)) { result.MissingAssignments++; return; }
        RebirthNpcWorkDefinition definition;
        if (!RebirthNpcWorkDefinitionRegistry.TryGet(assignment.DefinitionId, out definition) || definition == null || definition.Kind != expectedKind)
            result.WrongAssignmentKinds++;
        if (assignment.Status == RebirthNpcWorkAssignmentStatus.Completed || assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled || assignment.Status == RebirthNpcWorkAssignmentStatus.Failed)
            result.TerminalAssignmentsRetained++;
    }

    private static void AuditAdapterStates(ulong[] ids, RebirthNpcWorkKind expectedKind, RebirthNpcDutyMedicalQualificationSnapshot result, bool guard)
    {
        ids = ids ?? new ulong[0];
        if (guard) result.GuardAdapterStates = ids.Length; else result.MedicalAdapterStates = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(ids[i], out assignment)) { result.OrphanAdapterStates++; continue; }
            RebirthNpcWorkDefinition definition;
            if (!RebirthNpcWorkDefinitionRegistry.TryGet(assignment.DefinitionId, out definition) || definition == null || definition.Kind != expectedKind)
                result.AdapterKindMismatches++;
        }
    }

    public static void ResetForWorldChange()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;

        RebirthNpcGuardDutyWorkPackageService.ResetForWorldChange();
        RebirthNpcMedicalTreatmentWorkPackageService.ResetForWorldChange();
        lock (Sync) last = Empty();
        Interlocked.Increment(ref resets);
    }

    public static string GetReport()
    {
        RebirthNpcDutyMedicalQualificationSnapshot s; lock (Sync) s = last;
        return new StringBuilder("[REBIRTH NPC Duty/Medical Qualification] status=").Append(Interlocked.Read(ref audits)==0?"NotRun":(s.IsValid?"StructuralPass":"StructuralFail")).Append(" valid=").Append(s.IsValid)
            .Append(" audits=").Append(Interlocked.Read(ref audits)).Append(" failures=").Append(Interlocked.Read(ref failures))
            .Append(" resets=").Append(Interlocked.Read(ref resets)).Append(" guardPackages=").Append(s.GuardPackages)
            .Append(" medicalPackages=").Append(s.MedicalPackages).Append(" assignmentReferences=").Append(s.AssignmentReferences)
            .Append(" duplicateAssignments=").Append(s.DuplicateAssignmentReferences).Append(" missingAssignments=").Append(s.MissingAssignments)
            .Append(" wrongKinds=").Append(s.WrongAssignmentKinds).Append(" terminalAssignmentsRetained=").Append(s.TerminalAssignmentsRetained)
            .Append(" stalePackages=").Append(s.StalePackages).Append(" guardAdapterStates=").Append(s.GuardAdapterStates)
            .Append(" medicalAdapterStates=").Append(s.MedicalAdapterStates).Append(" orphanAdapterStates=").Append(s.OrphanAdapterStates)
            .Append(" adapterKindMismatches=").Append(s.AdapterKindMismatches).ToString();
    }

    private static RebirthNpcDutyMedicalQualificationSnapshot Empty()
    { return new RebirthNpcDutyMedicalQualificationSnapshot { IsValid = false, CapturedUtcTicks = 0L }; }
}
