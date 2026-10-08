using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthShadowToggleState
{
    Unknown,
    Default,
    Enabled,
    Disabled,
    Blocked
}

public sealed class RebirthModuleShadowToggle
{
    public string ModuleId;
    public RebirthShadowToggleState State;
    public string Reason;
    public DateTime ChangedUtc;
}

/// <summary>
/// In-memory shadow toggle state registry.
/// These states are intentionally not connected to gameplay behavior yet.
/// They exist so future performance/test workflows can be designed and tested safely.
/// </summary>
public static class RebirthModuleToggleStateRegistry
{
    private static readonly Dictionary<string, RebirthModuleShadowToggle> s_shadowStates =
        new Dictionary<string, RebirthModuleShadowToggle>(StringComparer.OrdinalIgnoreCase);

    public static string GetStatusReport(string filter)
    {
        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthToggleState] in-memory shadow toggle state.");
        sb.AppendLine("  These states do not affect gameplay in this phase.");
        sb.AppendLine("module | shadow state | contract default | mode | safety | impact | reason");

        bool any = false;
        for (int i = 0; i < modules.Length; i++)
        {
            RebirthModuleToggleDecl m = modules[i];
            if (!Matches(m, f))
                continue;

            any = true;
            RebirthModuleShadowToggle state = GetExistingOrDefault(m.ModuleId);

            sb.Append(m.ModuleId).Append(" | ")
              .Append(state.State).Append(" | ")
              .Append(m.DefaultState).Append(" | ")
              .Append(m.ToggleMode).Append(" | ")
              .Append(m.SafetyPolicy).Append(" | ")
              .Append(m.ImpactMode).Append(" | ")
              .AppendLine(state.Reason);
        }

        if (!any)
            sb.AppendLine("No module matched the filter.");

        return sb.ToString();
    }

    public static string SetShadowState(string moduleFilter, bool enabled, string reason)
    {
        string f = (moduleFilter ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthToggleState] Missing module id/filter.";

        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(4096);
        sb.Append("[RebirthToggleState] shadow ")
          .Append(enabled ? "enable" : "disable")
          .Append(" request for filter: ")
          .AppendLine(f);

        bool any = false;
        for (int i = 0; i < modules.Length; i++)
        {
            RebirthModuleToggleDecl m = modules[i];
            if (!Matches(m, f))
                continue;

            any = true;
            RebirthModuleShadowToggle state = new RebirthModuleShadowToggle
            {
                ModuleId = m.ModuleId,
                State = enabled ? RebirthShadowToggleState.Enabled : RebirthShadowToggleState.Disabled,
                Reason = string.IsNullOrEmpty(reason) ? "manual shadow state" : reason,
                ChangedUtc = DateTime.UtcNow
            };

            s_shadowStates[m.ModuleId] = state;

            sb.Append("  ").Append(m.ModuleId)
              .Append(" => ").Append(state.State)
              .Append(" (").Append(m.ToggleMode)
              .Append(", ").Append(m.SafetyPolicy)
              .AppendLine(")");
        }

        if (!any)
            sb.AppendLine("No module matched the filter.");

        sb.AppendLine("No gameplay behavior was changed. This is shadow state only.");
        return sb.ToString();
    }

    public static string ResetShadowState(string moduleFilter)
    {
        string f = (moduleFilter ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(f))
        {
            int count = s_shadowStates.Count;
            s_shadowStates.Clear();
            return "[RebirthToggleState] Cleared all shadow toggle states. Count: " + count
                + ". No gameplay behavior was changed.";
        }

        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();
        StringBuilder sb = new StringBuilder(4096);
        sb.Append("[RebirthToggleState] reset request for filter: ").AppendLine(f);

        bool any = false;
        for (int i = 0; i < modules.Length; i++)
        {
            RebirthModuleToggleDecl m = modules[i];
            if (!Matches(m, f))
                continue;

            any = true;
            bool removed = s_shadowStates.Remove(m.ModuleId);
            sb.Append("  ").Append(m.ModuleId)
              .Append(removed ? " reset to Default" : " was already Default")
              .AppendLine();
        }

        if (!any)
            sb.AppendLine("No module matched the filter.");

        sb.AppendLine("No gameplay behavior was changed. This is shadow state only.");
        return sb.ToString();
    }


    public static RebirthModuleShadowToggle GetShadowStateSnapshot(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
        {
            return new RebirthModuleShadowToggle
            {
                ModuleId = moduleId,
                State = RebirthShadowToggleState.Unknown,
                Reason = "missing module id",
                ChangedUtc = DateTime.MinValue
            };
        }

        RebirthModuleShadowToggle existing;
        if (s_shadowStates.TryGetValue(moduleId, out existing))
        {
            return new RebirthModuleShadowToggle
            {
                ModuleId = existing.ModuleId,
                State = existing.State,
                Reason = existing.Reason,
                ChangedUtc = existing.ChangedUtc
            };
        }

        return new RebirthModuleShadowToggle
        {
            ModuleId = moduleId,
            State = RebirthShadowToggleState.Default,
            Reason = "contract default; no shadow override",
            ChangedUtc = DateTime.MinValue
        };
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthToggleState] safety rules:");
        sb.AppendLine("  1. Shadow states are in-memory only.");
        sb.AppendLine("  2. Shadow states are not saved to disk.");
        sb.AppendLine("  3. Shadow states do not enable/disable gameplay.");
        sb.AppendLine("  4. Shadow states do not patch/unpatch Harmony.");
        sb.AppendLine("  5. Shadow states exist to test command workflows and future reporting.");
        sb.AppendLine("  6. Future real toggles must still obey ToggleMode and SafetyPolicy.");
        sb.AppendLine("  7. ManualReviewRequired modules must remain blocked from real toggles until reviewed.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthToggleState] Shadow states stored: " + s_shadowStates.Count
            + ". This phase changes command-layer state only, not gameplay.";
    }

    private static RebirthModuleShadowToggle GetExistingOrDefault(string moduleId)
    {
        RebirthModuleShadowToggle state;
        if (!string.IsNullOrEmpty(moduleId) && s_shadowStates.TryGetValue(moduleId, out state))
            return state;

        return new RebirthModuleShadowToggle
        {
            ModuleId = moduleId,
            State = RebirthShadowToggleState.Default,
            Reason = "contract default; no shadow override",
            ChangedUtc = DateTime.MinValue
        };
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
