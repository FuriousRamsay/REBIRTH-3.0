#if DEBUG
using System;
using System.Text;

#nullable disable

public enum RebirthDiagnosticGateKind
{
    Unknown,
    ReleaseSafe,
    DeveloperOnly,
    ProfilingOnly,
    CompatibilityAuditOnly,
    ManualReviewRequired
}

public enum RebirthDiagnosticGateCostClass
{
    Unknown,
    FreeFlagRead,
    CheapBranch,
    Allocates,
    ScansCollections,
    UsesReflection,
    CapturesStackTrace,
    ManualReviewRequired
}

/// <summary>
/// Diagnostic policy helpers for migrated callers and legacy manual reports.
/// Category flags remain independent; automatic_logging is a fallback, not a master switch.
/// 
/// Rule:
/// Callers must check a bool gate before constructing diagnostic strings,
/// running scans, invoking helpers, evaluating lambdas, or touching reflection.
/// </summary>
public static class RebirthDiagnosticGates
{
    public static bool MayPrepare(bool categoryEnabled)
    {
        return RebirthDiagnosticPolicy.MayPrepare(categoryEnabled);
    }
    public const bool ReleaseDiagnosticsEnabled = false;
    public const bool DeveloperDiagnosticsEnabled = false;
    public const bool ProfilingDiagnosticsEnabled = false;
    public const bool CompatibilityAuditDiagnosticsEnabled = false;

    public static bool IsReleaseDiagnosticEnabled
    {
        get { return ReleaseDiagnosticsEnabled; }
    }

    public static bool IsDeveloperDiagnosticEnabled
    {
        get { return DeveloperDiagnosticsEnabled; }
    }

    public static bool IsProfilingDiagnosticEnabled
    {
        get { return ProfilingDiagnosticsEnabled; }
    }

    public static bool IsCompatibilityAuditDiagnosticEnabled
    {
        get { return CompatibilityAuditDiagnosticsEnabled; }
    }

    public static bool IsAnyDiagnosticEnabled
    {
        get
        {
            return ReleaseDiagnosticsEnabled
                || DeveloperDiagnosticsEnabled
                || ProfilingDiagnosticsEnabled
                || CompatibilityAuditDiagnosticsEnabled;
        }
    }

    public static bool CanBuildDeveloperMessage()
    {
        return DeveloperDiagnosticsEnabled;
    }

    public static bool CanBuildProfilingMessage()
    {
        return ProfilingDiagnosticsEnabled;
    }

    public static bool CanBuildCompatibilityAuditMessage()
    {
        return CompatibilityAuditDiagnosticsEnabled;
    }

    public static bool CanPerformExpensiveDiagnosticWork(RebirthDiagnosticGateKind kind)
    {
        switch (kind)
        {
            case RebirthDiagnosticGateKind.ReleaseSafe:
                return ReleaseDiagnosticsEnabled;

            case RebirthDiagnosticGateKind.DeveloperOnly:
                return DeveloperDiagnosticsEnabled;

            case RebirthDiagnosticGateKind.ProfilingOnly:
                return ProfilingDiagnosticsEnabled;

            case RebirthDiagnosticGateKind.CompatibilityAuditOnly:
                return CompatibilityAuditDiagnosticsEnabled;

            default:
                return false;
        }
    }

    public static string GetSummaryReport()
    {
        return "[RebirthDiagnosticGates] release: " + ReleaseDiagnosticsEnabled
            + "; developer: " + DeveloperDiagnosticsEnabled
            + "; profiling: " + ProfilingDiagnosticsEnabled
            + "; compatibilityAudit: " + CompatibilityAuditDiagnosticsEnabled
            + "; any: " + IsAnyDiagnosticEnabled
            + ". Legacy constants are inert; migrated callers use independent category/manual gates.";
    }

    public static string GetRulesReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthDiagnosticGates] diagnostic gating rules:");
        sb.AppendLine("  1. Check the bool gate before building any diagnostic message.");
        sb.AppendLine("  2. Do not interpolate, concatenate, format, or allocate strings before the gate.");
        sb.AppendLine("  3. Do not run LINQ, collection scans, helper calls, reflection, or stack traces before the gate.");
        sb.AppendLine("  4. Do not use lazy lambdas that capture ref/out/in parameters.");
        sb.AppendLine("  5. Do not place diagnostic wrappers into release PatchAll.");
        sb.AppendLine("  6. Do not use object[] __args on hot diagnostic patches.");
        sb.AppendLine("  7. Release defaults are false.");
        sb.AppendLine("  8. Essential warnings/errors are not category-disabled.");
        return sb.ToString();
    }

    public static string GetExampleReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthDiagnosticGates] intended pattern:");
        sb.AppendLine("  if (RebirthDiagnosticGates.CanBuildDeveloperMessage())");
        sb.AppendLine("  {");
        sb.AppendLine("      Log.Out(\"[REBIRTH] \" + alreadyCheapValue);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("Avoid:");
        sb.AppendLine("  Log.Out(\"[REBIRTH] expensive=\" + ExpensiveCall());");
        sb.AppendLine("  Debug(() => \"expensive=\" + ExpensiveCall());");
        sb.AppendLine();
        sb.AppendLine("Reason:");
        sb.AppendLine("  Disabled diagnostics must not construct messages or evaluate expensive arguments.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthDiagnosticGates] safety:");
        sb.AppendLine("  1. No Harmony patches installed.");
        sb.AppendLine("  2. No logging performed by this shell.");
        sb.AppendLine("  3. No reflection performed.");
        sb.AppendLine("  4. Gameplay/error paths do not depend on optional diagnostic gates.");
        sb.AppendLine("  5. Manual scopes expire on the existing GameUpdate lifecycle.");
        sb.AppendLine("  6. All diagnostic defaults are false.");
        sb.AppendLine("  7. Future build profiles may replace constants intentionally.");
        return sb.ToString();
    }
}
#endif
