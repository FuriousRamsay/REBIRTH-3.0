using System;
using System.Text;

#nullable disable

public sealed class RebirthModuleManifestView
{
    public string ModuleId;
    public string DisplayName;
    public string ToggleMode;
    public string SafetyPolicy;
    public string ImpactMode;
    public string DefaultState;
    public string ShadowState;
    public string HotMethod;
    public bool IsHotPath;
    public bool CanMeasureImpact;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only module manifest aggregator.
/// This consolidates toggle contract, shadow state, performance context, and module identity.
/// </summary>
public static class RebirthModuleManifestRegistry
{
    public static string GetManifestReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        RebirthModuleToggleDecl[] toggles = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthModuleManifest] consolidated module manifest view.");
        sb.AppendLine("  Read-only. No gameplay state is changed.");
        sb.AppendLine("module | display | shadow | mode | safety | impact | default | hot | measurable | hot method | evidence | notes");

        bool any = false;
        for (int i = 0; i < toggles.Length; i++)
        {
            RebirthModuleToggleDecl t = toggles[i];
            if (!Matches(t, f))
                continue;

            any = true;
            RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(t.ModuleId);

            sb.Append(t.ModuleId).Append(" | ")
              .Append(t.DisplayName).Append(" | ")
              .Append(shadow.State).Append(" | ")
              .Append(t.ToggleMode).Append(" | ")
              .Append(t.SafetyPolicy).Append(" | ")
              .Append(t.ImpactMode).Append(" | ")
              .Append(t.DefaultState).Append(" | ")
              .Append(t.IsHotPath).Append(" | ")
              .Append(t.CanMeasureImpact).Append(" | ")
              .Append(t.HotMethod).Append(" | ")
              .Append(t.Evidence).Append(" | ")
              .AppendLine(t.Notes);
        }

        if (!any)
            sb.AppendLine("No module matched the filter.");

        return sb.ToString();
    }

    public static string GetHotPathManifestReport()
    {
        RebirthModuleToggleDecl[] toggles = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthModuleManifest] hot-path module manifest.");
        sb.AppendLine("module | shadow | mode | safety | impact | hot method | notes");

        for (int i = 0; i < toggles.Length; i++)
        {
            RebirthModuleToggleDecl t = toggles[i];
            if (!t.IsHotPath)
                continue;

            RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(t.ModuleId);

            sb.Append(t.ModuleId).Append(" | ")
              .Append(shadow.State).Append(" | ")
              .Append(t.ToggleMode).Append(" | ")
              .Append(t.SafetyPolicy).Append(" | ")
              .Append(t.ImpactMode).Append(" | ")
              .Append(t.HotMethod).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetMeasurementManifestReport()
    {
        RebirthModuleToggleDecl[] toggles = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthModuleManifest] measurable module manifest.");
        sb.AppendLine("module | shadow | impact | mode | safety | hot method | notes");

        for (int i = 0; i < toggles.Length; i++)
        {
            RebirthModuleToggleDecl t = toggles[i];
            if (!t.CanMeasureImpact)
                continue;

            RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(t.ModuleId);

            sb.Append(t.ModuleId).Append(" | ")
              .Append(shadow.State).Append(" | ")
              .Append(t.ImpactMode).Append(" | ")
              .Append(t.ToggleMode).Append(" | ")
              .Append(t.SafetyPolicy).Append(" | ")
              .Append(t.HotMethod).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetOwnershipSummaryReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthModuleManifest] ownership summary.");
        sb.AppendLine("  Player:");
        sb.AppendLine("    - player.movement owns future PlayerMoveController.Update adapter.");
        sb.AppendLine("    - player.crawl must register under player.movement, not patch separately.");
        sb.AppendLine("  Combat:");
        sb.AppendLine("    - combat.damage owns future EntityAlive.DamageEntity dispatcher.");
        sb.AppendLine("    - individual combat features register as policies, not separate DamageEntity patches.");
        sb.AppendLine("  Pathing:");
        sb.AppendLine("    - pathing.vanilla owns future EntityMoveHelper/FindPath adapter.");
        sb.AppendLine("    - zombie.ai depends on pathing; pathing must not depend on zombie.ai.");
        sb.AppendLine("  Items:");
        sb.AppendLine("    - items.stats owns future ItemValue.ModifyValue dispatcher.");
        sb.AppendLine("    - item features must use cached snapshots, not scans or XML lookups.");
        sb.AppendLine("  Paint:");
        sb.AppendLine("    - paint.render owns future BlockShapeNew.renderFace paint/tint adapter.");
        sb.AppendLine("    - copyFromWorld and CopyIntoLocal remain separate adapters.");
        sb.AppendLine("  World/Caves:");
        sb.AppendLine("    - world.caves must be split into generation, runtime, spawn policy, XML index, and worldgen compatibility.");
        sb.AppendLine("    - cave tunnel helper stays tunnel-only; cave POI uses tags.");
        sb.AppendLine("  Diagnostics:");
        sb.AppendLine("    - diagnostics.profiling is profiling-only and must not ship as always-patched release wrappers.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthModuleManifest] safety rules:");
        sb.AppendLine("  1. Manifest reports are read-only.");
        sb.AppendLine("  2. Shadow state in this report is command-layer state only.");
        sb.AppendLine("  3. Real migrated features must register a manifest/toggle contract before behavior is connected.");
        sb.AppendLine("  4. Hot-method ownership must be singular.");
        sb.AppendLine("  5. Diagnostics/profiling must remain separate from release behavior.");
        sb.AppendLine("  6. Future patch adapters must consult toggle contract and safety policy.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        RebirthModuleToggleDecl[] toggles = RebirthModuleToggleRegistry.GetSnapshot();

        int hot = 0;
        int measurable = 0;
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i].IsHotPath)
                hot++;
            if (toggles[i].CanMeasureImpact)
                measurable++;
        }

        return "[RebirthModuleManifest] Modules: " + toggles.Length
            + "; hot-path modules: " + hot
            + "; measurable modules: " + measurable
            + ". Manifest aggregation is read-only.";
    }

    private static bool Matches(RebirthModuleToggleDecl m, string filter)
    {
        if (m == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (m.ModuleId != null && m.ModuleId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.DisplayName != null && m.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.HotMethod != null && m.HotMethod.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
