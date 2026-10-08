using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcBuildQualificationState : byte
{
    Unknown = 0,
    ReadyForExternalBuild = 1,
    StructuralQualified = 2,
    Degraded = 3,
    RuntimeQualified = 4
}

public sealed class RebirthNpcBuildQualificationSnapshot
{
    public RebirthNpcBuildQualificationState State { get; internal set; }
    public string RuntimeVersion { get; internal set; }
    public string AssemblyCSharpVersion { get; internal set; }
    public string AssemblyCSharpMvid { get; internal set; }
    public bool ExactB259ModuleIdentity { get; internal set; }
    public int VerifiedNativeContracts { get; internal set; }
    public int MissingNativeContracts { get; internal set; }
    public int LoadedAssemblyCount { get; internal set; }
    public int ResolvedCompatibilityMembers { get; internal set; }
    public int MissingCompatibilityMembers { get; internal set; }
    public string CompatibilityState { get; internal set; }
    public string LastReason { get; internal set; }
    public long AuditCount { get; internal set; }
}

/// <summary>
/// Compile-time 3.1 qualification report. Runtime assembly/member reflection was
/// removed: exact API compatibility is enforced by the compiler and by the
/// generated Harmony signature matrix under _Documentation.
/// </summary>
public static class RebirthNpcBuildQualificationService
{
    private const int NativeContractCount = 7;
    private static readonly object Sync = new object();
    private static RebirthNpcBuildQualificationSnapshot snapshot = NewSnapshot("not-audited");

    public static RebirthNpcBuildQualificationSnapshot Audit()
    {
        RebirthNpcCompatibilitySeverity compatibility =
            RebirthNpcCompatibilityQualificationService.GetSeverity();
        RebirthNpcApiCompatibilitySnapshot api = RebirthNpcApiCompatibility.GetSnapshot();

        lock (Sync)
        {
            snapshot = new RebirthNpcBuildQualificationSnapshot
            {
                State = compatibility == RebirthNpcCompatibilitySeverity.Blocking
                    ? RebirthNpcBuildQualificationState.Degraded
                    : RebirthNpcBuildQualificationState.StructuralQualified,
                RuntimeVersion = Environment.Version.ToString(),
                AssemblyCSharpVersion = "3.1.0-b14-source-baseline",
                AssemblyCSharpMvid = "not-reflected",
                ExactB259ModuleIdentity = false,
                VerifiedNativeContracts = 0,
                MissingNativeContracts = NativeContractCount,
                LoadedAssemblyCount = 0,
                ResolvedCompatibilityMembers = api.ResolvedMethodCount,
                MissingCompatibilityMembers = api.MissingMethodCount,
                CompatibilityState = compatibility.ToString(),
                LastReason = "STRUCTURAL: source declares bindings against the supplied 3.1 b14 API corpus; native/runtime contracts are NotRun until external compile/integration evidence is supplied.",
                AuditCount = snapshot.AuditCount + 1
            };
            return Copy(snapshot);
        }
    }

    public static RebirthNpcBuildQualificationSnapshot GetSnapshot()
    {
        lock (Sync)
            return Copy(snapshot);
    }

    public static string GetReport()
    {
        RebirthNpcBuildQualificationSnapshot value = GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC Build Qualification] state=").Append(value.State)
            .Append(" audits=").Append(value.AuditCount)
            .Append(" runtime=").Append(value.RuntimeVersion)
            .Append(" baseline=").Append(value.AssemblyCSharpVersion)
            .Append(" runtimeVerifiedNativeContracts=").Append(value.VerifiedNativeContracts)
            .Append('/').Append(value.VerifiedNativeContracts + value.MissingNativeContracts)
            .Append(" compatibility=").Append(value.CompatibilityState)
            .Append(" structuralBindings=").Append(NativeContractCount).Append(" runtimeReflection=disabled runtimeAcceptance=NotRun")
            .AppendLine()
            .Append("  reason=").Append(value.LastReason)
            .AppendLine()
            .Append("  harmonyEvidence=_Documentation/3.1 Migration/PASS6_HARMONY_SIGNATURE_MATRIX.csv");
        return builder.ToString();
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
            snapshot = NewSnapshot("world-reset");
    }

    private static RebirthNpcBuildQualificationSnapshot NewSnapshot(string reason)
    {
        return new RebirthNpcBuildQualificationSnapshot
        {
            State = RebirthNpcBuildQualificationState.ReadyForExternalBuild,
            RuntimeVersion = Environment.Version.ToString(),
            AssemblyCSharpVersion = "3.1.0-b14-source-baseline",
            AssemblyCSharpMvid = "not-reflected",
            ExactB259ModuleIdentity = false,
            LoadedAssemblyCount = 0,
            CompatibilityState = "not-audited",
            LastReason = reason
        };
    }

    private static RebirthNpcBuildQualificationSnapshot Copy(RebirthNpcBuildQualificationSnapshot value)
    {
        return new RebirthNpcBuildQualificationSnapshot
        {
            State = value.State,
            RuntimeVersion = value.RuntimeVersion,
            AssemblyCSharpVersion = value.AssemblyCSharpVersion,
            AssemblyCSharpMvid = value.AssemblyCSharpMvid,
            ExactB259ModuleIdentity = value.ExactB259ModuleIdentity,
            VerifiedNativeContracts = value.VerifiedNativeContracts,
            MissingNativeContracts = value.MissingNativeContracts,
            LoadedAssemblyCount = value.LoadedAssemblyCount,
            ResolvedCompatibilityMembers = value.ResolvedCompatibilityMembers,
            MissingCompatibilityMembers = value.MissingCompatibilityMembers,
            CompatibilityState = value.CompatibilityState,
            LastReason = value.LastReason,
            AuditCount = value.AuditCount
        };
    }
}

public sealed class ConsoleCmdRebirthNpcBuildQualification : ConsoleCmdAbstract
{
    public override string[] getCommands() { return new[] { "rbnpcbuild", "rbnpccompile" }; }
    public override string getDescription() { return "Reports the REBIRTH NPC 3.1 compile-time API qualification state."; }
    public override string getHelp()
    {
        return "Usage: rbnpcbuild [audit|status]\n" +
            "audit  - refresh the compile-time compatibility report\n" +
            "status - print the current qualification snapshot";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string operation = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
        if (operation == "audit")
            RebirthNpcBuildQualificationService.Audit();
        SdtdConsole.Instance.Output(RebirthNpcBuildQualificationService.GetReport());
    }
}
