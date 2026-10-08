using System;
using System.Text;

#nullable disable

public enum RebirthExternalProjectKind
{
    OwnedExternalSource,
    VendoredDependency,
    ThirdPartyCompatibilityXml
}

public enum RebirthExternalDecision
{
    Unknown,
    AbsorbNative,
    AbsorbBehaviorOnly,
    KeepVendoredAndGoverned,
    CompatibilityXmlOnly,
    ExcludePatchShape,
    ManualReviewRequired,
    DiscardOrDiagnosticOnly
}

public sealed class RebirthExternalProjectDecl
{
    public string ProjectName;
    public RebirthExternalProjectKind Kind;
    public RebirthExternalDecision Decision;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
    public string HardExclusion;
}

/// <summary>
/// Read-only external project absorption/governance ledger.
/// This does not load or patch external project code.
/// </summary>
public static class RebirthExternalProjectRegistry
{
    private static readonly RebirthExternalProjectDecl[] s_entries = new[]
    {
        new RebirthExternalProjectDecl
        {
            ProjectName = "0-Material Modifier",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.AbsorbNative,
            OwnerModuleId = "visuals.materials",
            Evidence = "P4",
            Notes = "Low-risk event-driven EntityAlive.Init/post-init style material override.",
            HardExclusion = "No per-frame material scans."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "DroneLockToPlayer",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.AbsorbBehaviorOnly,
            OwnerModuleId = "deployables.drones",
            Evidence = "P4/V8",
            Notes = "Absorb follow-lock behavior only.",
            HardExclusion = "Do not patch Entity.OnUpdatePosition globally."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "Morecrosshairs",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.AbsorbBehaviorOnly,
            OwnerModuleId = "ui.crosshair",
            Evidence = "P4/V8",
            Notes = "Absorb crosshair options/render behavior only.",
            HardExclusion = "Do not patch GUIUtils.DrawLine globally."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "DayCustomShaders",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.ManualReviewRequired,
            OwnerModuleId = "visuals.shaders",
            Evidence = "P3",
            Notes = "Mostly real render pipeline/shader work, but LayerScanner is hot diagnostic risk.",
            HardExclusion = "LayerScanner_OnUpdateLive_Patch with Object.FindObjectsOfType<Renderer>() must be discarded or diagnostic-build only."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "PathSmoothing",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.KeepVendoredAndGoverned,
            OwnerModuleId = "pathing.smoothing",
            Evidence = "P4/P6",
            Notes = "Behavior is live today, but A* internals are high-risk.",
            HardExclusion = "Do not absorb GridGraph.Linecast, LayerGridGraph.GetNearest, RaycastModifier.ValidateLine transpilers/reverse patches by default."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "TheDescent",
            Kind = RebirthExternalProjectKind.OwnedExternalSource,
            Decision = RebirthExternalDecision.ManualReviewRequired,
            OwnerModuleId = "world.caves",
            Evidence = "P3/P5A2",
            Notes = "Split into cave generation/runtime/spawn/XML/worldgen. WorldBuilder.GenerateData has severe 3.0 drift.",
            HardExclusion = "Do not merge as monolith. Do not conflate cave tunnel helper with cave POI tag check."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "SCore / Score",
            Kind = RebirthExternalProjectKind.VendoredDependency,
            Decision = RebirthExternalDecision.KeepVendoredAndGoverned,
            OwnerModuleId = "external.score",
            Evidence = "P6.5",
            Notes = "63 files / 10,177 lines; real framework dependency. Must declare patch/cost/conflict boundaries.",
            HardExclusion = "Do not silently blend into authored REBIRTH code."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "IzayoGuns",
            Kind = RebirthExternalProjectKind.ThirdPartyCompatibilityXml,
            Decision = RebirthExternalDecision.CompatibilityXmlOnly,
            OwnerModuleId = "external.compatxml",
            Evidence = "V8",
            Notes = "Cleanup XML compatibility target; no owned source in Mods.zip.",
            HardExclusion = "Do not treat as source-owned external absorption."
        },
        new RebirthExternalProjectDecl
        {
            ProjectName = "SonjaArmor",
            Kind = RebirthExternalProjectKind.ThirdPartyCompatibilityXml,
            Decision = RebirthExternalDecision.CompatibilityXmlOnly,
            OwnerModuleId = "external.compatxml",
            Evidence = "V8",
            Notes = "Cleanup XML compatibility target; no owned source in Mods.zip.",
            HardExclusion = "Do not treat as source-owned external absorption."
        }
    };

    public static RebirthExternalProjectDecl[] GetSnapshot()
    {
        RebirthExternalProjectDecl[] copy = new RebirthExternalProjectDecl[s_entries.Length];
        Array.Copy(s_entries, copy, copy.Length);
        return copy;
    }

    public static string GetReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthExternal] read-only external absorption/governance ledger.");
        sb.AppendLine("project | kind | decision | owner | evidence | exclusion | notes");

        for (int i = 0; i < s_entries.Length; i++)
        {
            RebirthExternalProjectDecl e = s_entries[i];
            sb.Append(e.ProjectName).Append(" | ")
              .Append(e.Kind).Append(" | ")
              .Append(e.Decision).Append(" | ")
              .Append(e.OwnerModuleId).Append(" | ")
              .Append(e.Evidence).Append(" | ")
              .Append(e.HardExclusion).Append(" | ")
              .AppendLine(e.Notes);
        }

        return sb.ToString();
    }
}
