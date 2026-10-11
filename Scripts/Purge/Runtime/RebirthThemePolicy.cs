/// <summary>Configured policy is pure; runtime activation additionally requires the release gate.</summary>
internal static class RebirthThemePolicy
{
    internal static bool IsPurge(RebirthSandboxState state) => state != null && state.Theme == RebirthWorldTheme.Purge;
    internal static bool TrackingEnabled(RebirthSandboxState state) => state != null && (IsPurge(state) || state.ShowClearedPois);
    internal static RebirthSpawnProgressionMode SpawnProgression(RebirthSandboxState state) => IsPurge(state)
        ? RebirthSpawnProgressionMode.Biome : state != null ? state.SpawnProgression : RebirthSpawnProgressionMode.Gamestage;
    internal static bool TraderJobsAllowed(RebirthSandboxState state) => !IsPurge(state);
    internal static bool BiomeHazardsAllowed(RebirthSandboxState state) => !IsPurge(state);
}