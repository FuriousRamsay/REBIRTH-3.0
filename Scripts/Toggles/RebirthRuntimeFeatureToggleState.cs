using System;
using System.Text;

#nullable disable

public enum RebirthRuntimeFeatureToggleSource
{
    Unknown,
    ExplicitDefault,
    ManualConsolePreview,
    FutureConfigOption,
    FutureServerSnapshot,
    FutureBuildProfile
}

public enum RebirthRuntimeFeatureStateValue
{
    Unknown,
    Disabled,
    Enabled,
    Blocked,
    ManualReviewRequired
}

public sealed class RebirthRuntimeFeatureToggleSnapshot
{
    public string FeatureId;
    public RebirthRuntimeFeatureStateValue State;
    public RebirthRuntimeFeatureToggleSource Source;
    public bool IsRuntimeConsumerAllowed;
    public bool IsHotPathUseAllowed;
    public string Reason;
}

/// <summary>
/// Runtime feature toggle state shell.
/// This is intentionally inert:
/// - no XML parsing
/// - no custom game option lookup
/// - no Harmony patches
/// - no gameplay consumers
/// - no network packets
/// - no save/load writes
/// </summary>
public static class RebirthRuntimeFeatureToggleState
{
    private static readonly object s_lock = new object();

    private static RebirthRuntimeFeatureToggleSnapshot[] s_toggles = CreateExplicitDefaults();

    public static string GetSummaryReport()
    {
        RebirthRuntimeFeatureToggleSnapshot[] toggles = GetSnapshot();

        int enabled = 0;
        int disabled = 0;
        int blocked = 0;
        int hotAllowed = 0;

        for (int i = 0; i < toggles.Length; i++)
        {
            RebirthRuntimeFeatureToggleSnapshot t = toggles[i];

            if (t.State == RebirthRuntimeFeatureStateValue.Enabled)
                enabled++;
            if (t.State == RebirthRuntimeFeatureStateValue.Disabled)
                disabled++;
            if (t.State == RebirthRuntimeFeatureStateValue.Blocked || t.State == RebirthRuntimeFeatureStateValue.ManualReviewRequired)
                blocked++;
            if (t.IsHotPathUseAllowed)
                hotAllowed++;
        }

        return "[RebirthRuntimeFeatureToggles] Features: " + toggles.Length
            + "; enabled: " + enabled
            + "; disabled: " + disabled
            + "; blocked/manual: " + blocked
            + "; hot-path allowed: " + hotAllowed
            + ". Shell is inert.";
    }

    public static string GetToggleReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        RebirthRuntimeFeatureToggleSnapshot[] toggles = GetSnapshot();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthRuntimeFeatureToggles] runtime feature toggle state shell.");
        sb.AppendLine("  Explicit defaults only. No gameplay reads these toggles yet.");
        sb.AppendLine("feature | state | source | runtime consumer allowed | hot-path use allowed | reason");

        bool any = false;
        for (int i = 0; i < toggles.Length; i++)
        {
            RebirthRuntimeFeatureToggleSnapshot t = toggles[i];
            if (!Matches(t, f))
                continue;

            any = true;
            sb.Append(t.FeatureId).Append(" | ")
              .Append(t.State).Append(" | ")
              .Append(t.Source).Append(" | ")
              .Append(t.IsRuntimeConsumerAllowed).Append(" | ")
              .Append(t.IsHotPathUseAllowed).Append(" | ")
              .AppendLine(t.Reason);
        }

        if (!any)
            sb.AppendLine("No runtime feature toggle matched the filter.");

        return sb.ToString();
    }

    public static void ResetToExplicitDefaults()
    {
        lock (s_lock)
        {
            s_toggles = CreateExplicitDefaults();
        }
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthRuntimeFeatureToggles] safety:");
        sb.AppendLine("  1. Explicit defaults only.");
        sb.AppendLine("  2. No XML parsing.");
        sb.AppendLine("  3. No custom game option lookup.");
        sb.AppendLine("  4. No Harmony patch install/uninstall.");
        sb.AppendLine("  5. No gameplay consumer reads these toggles yet.");
        sb.AppendLine("  6. No network packets are sent.");
        sb.AppendLine("  7. No save/load state is written.");
        sb.AppendLine("  8. Hot-path use is false for every feature in this shell.");
        return sb.ToString();
    }

    private static RebirthRuntimeFeatureToggleSnapshot[] GetSnapshot()
    {
        lock (s_lock)
        {
            RebirthRuntimeFeatureToggleSnapshot[] copy = new RebirthRuntimeFeatureToggleSnapshot[s_toggles.Length];
            for (int i = 0; i < s_toggles.Length; i++)
            {
                RebirthRuntimeFeatureToggleSnapshot t = s_toggles[i];
                copy[i] = new RebirthRuntimeFeatureToggleSnapshot
                {
                    FeatureId = t.FeatureId,
                    State = t.State,
                    Source = t.Source,
                    IsRuntimeConsumerAllowed = t.IsRuntimeConsumerAllowed,
                    IsHotPathUseAllowed = t.IsHotPathUseAllowed,
                    Reason = t.Reason
                };
            }

            return copy;
        }
    }

    private static RebirthRuntimeFeatureToggleSnapshot[] CreateExplicitDefaults()
    {
        return new[]
        {
            Disabled("player.crawl", "Blocked from first migration; PlayerMoveController hot path."),
            Disabled("combat.damage", "Hot/server-authoritative; requires patch strategy and tests."),
            Disabled("items.stats.randomization", "ItemValue hot path; requires persistence and no-allocation proof."),
            Disabled("paint.render", "renderFace hot path; requires perf harness."),
            Disabled("zombie.ai.special", "AI/pathing hot path; requires sub-slicing."),
            Disabled("vehicles.ai.interaction", "Depends on AI, network authority, and audio sync tests."),
            Disabled("npc.companions", "Stateful/server-sensitive; requires persistence tests."),
            Disabled("map.markers.reservations", "Stateful; requires event-based marker source truth."),
            Disabled("loot.persistence", "Server-owned state; requires access validation tests."),
            Disabled("workstations.crafting", "Stateful and hitch-sensitive; requires queue/fuel tests."),
            Disabled("harvest.salvage", "Possible first gameplay slice after tests."),
            Disabled("audio.sounds", "Possible isolated slice after audio sync tests."),
            Disabled("ui.hud.compass", "Display-only candidate after cached snapshot tests."),
            Disabled("spawning.events", "Server-owned; requires scenario/world policy tests."),
            Disabled("world.caves.descent", "Requires cave tunnel vs POI contract tests."),
            Disabled("weather.environment", "Lower-risk after patch ownership tests."),
            Blocked("survivor.scenario", "Blocked until survivor token disambiguation."),
            Disabled("diagnostics.profiling", "Disabled by default; future profile-specific activation only."),
            Disabled("external.compatibility", "Manual review; external surfaces stay separate.")
        };
    }

    private static RebirthRuntimeFeatureToggleSnapshot Disabled(string featureId, string reason)
    {
        return new RebirthRuntimeFeatureToggleSnapshot
        {
            FeatureId = featureId,
            State = RebirthRuntimeFeatureStateValue.Disabled,
            Source = RebirthRuntimeFeatureToggleSource.ExplicitDefault,
            IsRuntimeConsumerAllowed = false,
            IsHotPathUseAllowed = false,
            Reason = reason
        };
    }

    private static RebirthRuntimeFeatureToggleSnapshot Blocked(string featureId, string reason)
    {
        return new RebirthRuntimeFeatureToggleSnapshot
        {
            FeatureId = featureId,
            State = RebirthRuntimeFeatureStateValue.Blocked,
            Source = RebirthRuntimeFeatureToggleSource.ExplicitDefault,
            IsRuntimeConsumerAllowed = false,
            IsHotPathUseAllowed = false,
            Reason = reason
        };
    }

    private static bool Matches(RebirthRuntimeFeatureToggleSnapshot t, string filter)
    {
        if (t == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (t.FeatureId != null && t.FeatureId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Reason != null && t.Reason.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.State.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Source.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
