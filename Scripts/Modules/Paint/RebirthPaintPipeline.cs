using System;
using System.Text;

#nullable disable

public enum RebirthPaintTenantArea
{
    Unknown,
    RenderFaceDispatcher,
    NoDataFastPath,
    PaintStorage,
    TintStorage,
    ChunkPaintCache,
    PrefabCopyFromWorld,
    PrefabCopyIntoLocal,
    NetworkSync,
    Diagnostics
}

public enum RebirthPaintTenantStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired
}

public enum RebirthPaintHotRule
{
    Unknown,
    NoCoordinateWorkOnNoData,
    NoDictionaryLookupOnNoData,
    NoStringFormatting,
    NoXmlLookup,
    NoPrefabCopyAssumption,
    StableCopyFromWorld,
    CompatCopyIntoLocal,
    EventDrivenNetworkSync,
    DiagnosticsCompileOut
}

public sealed class RebirthPaintTenantDecl
{
    public RebirthPaintTenantArea Area;
    public RebirthPaintTenantStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthPaintHotRuleDecl
{
    public RebirthPaintHotRule Rule;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthPaintRenderContext
{
    public bool ShouldFastExit;
    public bool HasPaintOrTintData;
    public string Reason;

    public static RebirthPaintRenderContext Empty(string reason)
    {
        return new RebirthPaintRenderContext
        {
            ShouldFastExit = true,
            HasPaintOrTintData = false,
            Reason = reason
        };
    }
}

/// <summary>
/// No-behavior paint/render tenant scaffold.
/// This intentionally does not alter renderFace, paint storage, tint storage, prefab copy, or network sync.
/// </summary>
public static class RebirthPaintPipeline
{
    private static readonly RebirthPaintTenantDecl[] s_tenants = new[]
    {
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.RenderFaceDispatcher,
            Status = RebirthPaintTenantStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "paint.render",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior renderFace performance findings",
            Notes = "Future single owner for BlockShapeNew.renderFace paint/tint behavior."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.NoDataFastPath,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.render",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Chunks/blocks with no paint/tint data must exit before coordinate packing or dictionary lookup."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.PaintStorage,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.storage",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior paint persistence fixes",
            Notes = "Paint storage must be chunk-indexed and event-updated, not repeatedly scanned."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.TintStorage,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.tint",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior tint files",
            Notes = "Tint lookups must share fast no-data gating with paint storage."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.ChunkPaintCache,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.cache",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Chunk-level hasAnyPaintOrTint flags should prevent per-face work."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.PrefabCopyFromWorld,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.copyblock",
            HotPath = false,
            MayAllocate = false,
            Evidence = "P5A2: Prefab.copyFromWorld stable around 3.5% drift",
            Notes = "Lower migration risk than CopyIntoLocal, but still isolate behind adapter."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.PrefabCopyIntoLocal,
            Status = RebirthPaintTenantStatus.ManualReviewRequired,
            OwnerModuleId = "paint.copyblock",
            HotPath = false,
            MayAllocate = false,
            Evidence = "P5A2: Prefab.CopyIntoLocal moderate drift around 34%",
            Notes = "Needs 2.6/3.0 compat isolation. Do not assume same as copyFromWorld."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.NetworkSync,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.network",
            HotPath = false,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior paint sync issues",
            Notes = "Network sync should be event-driven and batched, not repeated broadcast loops."
        },
        new RebirthPaintTenantDecl
        {
            Area = RebirthPaintTenantArea.Diagnostics,
            Status = RebirthPaintTenantStatus.Planned,
            OwnerModuleId = "paint.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "debug policy",
            Notes = "Diagnostics must compile out or be gated before string construction."
        }
    };

    private static readonly RebirthPaintHotRuleDecl[] s_rules = new[]
    {
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.NoCoordinateWorkOnNoData,
            OwnerModuleId = "paint.render",
            Evidence = "Blueprint v2",
            Notes = "No coordinate packing or block position work if chunk has no paint/tint data."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.NoDictionaryLookupOnNoData,
            OwnerModuleId = "paint.render",
            Evidence = "Blueprint v2",
            Notes = "No dictionary/hash lookup if chunk-level no-data flag is false."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.NoStringFormatting,
            OwnerModuleId = "paint.diagnostics",
            Evidence = "debug policy",
            Notes = "No interpolation/formatting unless compile-time or bool-gated before construction."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.NoXmlLookup,
            OwnerModuleId = "paint.render",
            Evidence = "XML ledger / Blueprint v2",
            Notes = "Paint metadata must be indexed outside renderFace."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.NoPrefabCopyAssumption,
            OwnerModuleId = "paint.copyblock",
            Evidence = "P5A2",
            Notes = "copyFromWorld and CopyIntoLocal have different 2.6->3.0 drift and must be handled separately."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.StableCopyFromWorld,
            OwnerModuleId = "paint.copyblock",
            Evidence = "P5A2",
            Notes = "Prefab.copyFromWorld is lower drift, but still adapter-owned."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.CompatCopyIntoLocal,
            OwnerModuleId = "paint.copyblock",
            Evidence = "P5A2",
            Notes = "Prefab.CopyIntoLocal needs compatibility isolation."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.EventDrivenNetworkSync,
            OwnerModuleId = "paint.network",
            Evidence = "Blueprint v2",
            Notes = "Paint sync should occur on changes/login/chunk events, not repeated loops."
        },
        new RebirthPaintHotRuleDecl
        {
            Rule = RebirthPaintHotRule.DiagnosticsCompileOut,
            OwnerModuleId = "paint.diagnostics",
            Evidence = "debug policy",
            Notes = "Release builds must not carry diagnostic renderFace Harmony wrappers."
        }
    };

    public static RebirthPaintTenantDecl[] GetTenantsSnapshot()
    {
        RebirthPaintTenantDecl[] copy = new RebirthPaintTenantDecl[s_tenants.Length];
        Array.Copy(s_tenants, copy, copy.Length);
        return copy;
    }

    public static RebirthPaintRenderContext BuildNoBehaviorRenderContext()
    {
        return RebirthPaintRenderContext.Empty("scaffold only; no renderFace behavior yet");
    }

    public static void OnRenderFaceNoBehavior()
    {
        // Intentional no-op.
        // Future BlockShapeNew.renderFace adapter target only.
        BuildNoBehaviorRenderContext();
    }

    public static string GetTenantReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPaint] no-behavior paint/render tenant scaffold.");
        sb.AppendLine("  No Harmony patch is installed in this phase.");
        sb.AppendLine("  No paint/tint/renderFace/prefab-copy behavior is changed in this phase.");
        sb.AppendLine("area | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_tenants.Length; i++)
        {
            RebirthPaintTenantDecl t = s_tenants[i];
            sb.Append(t.Area).Append(" | ")
              .Append(t.Status).Append(" | ")
              .Append(t.OwnerModuleId).Append(" | ")
              .Append(t.HotPath).Append(" | ")
              .Append(t.MayAllocate).Append(" | ")
              .Append(t.Evidence).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetRuleReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPaint] hot-path paint/render rules.");
        sb.AppendLine("rule | owner | evidence | notes");

        for (int i = 0; i < s_rules.Length; i++)
        {
            RebirthPaintHotRuleDecl r = s_rules[i];
            sb.Append(r.Rule).Append(" | ")
              .Append(r.OwnerModuleId).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPaint] safety rules:");
        sb.AppendLine("  1. Only one future REBIRTH patch may own BlockShapeNew.renderFace paint/tint work.");
        sb.AppendLine("  2. No coordinate work on no-data chunks.");
        sb.AppendLine("  3. No dictionary lookups on no-data chunks.");
        sb.AppendLine("  4. No XML lookups in renderFace.");
        sb.AppendLine("  5. copyFromWorld and CopyIntoLocal must be separate adapters.");
        sb.AppendLine("  6. Paint network sync must be event-driven/batched.");
        sb.AppendLine("  7. Diagnostics must compile out or be bool-gated before construction.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthPaint] Paint/render tenant entries: " + s_tenants.Length
            + "; hot-path rules: " + s_rules.Length
            + ". This phase is no-behavior and installs no Harmony patches.";
    }
}
