internal static class RebirthPurgeReleasePolicy
{
    // Owner 2026-11-09: Purge is gameplay and behaves the same in Debug and Release builds. A Release build may only
    // strip debugging. PURGE_QUALIFICATION now gates nothing but the trace/observation hooks (feedback traces,
    // audit flags, bridge observers); it no longer decides whether Purge exists.
    internal static bool Enabled => true;
}
