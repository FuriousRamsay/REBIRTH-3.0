using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

// Only empty recovery enumerations are memoized. Owned publication invalidates
// the folder before attempting IO; nonempty/uncertain originals keep retrying.
// Weak world keys do not retain an unloaded world or its per-save paths.
internal static class RebirthWorkstationCompletionEmptyRecovery
{
    private sealed class Scope
    {
        internal string Identity;
        internal long Admissions, Skips, Invalidations;
        internal readonly HashSet<string> Empty = new HashSet<string>(Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<object, Scope> scopes = new ConditionalWeakTable<object, Scope>();
    private static readonly object gate = new object();

    internal static bool ShouldScan(object world, string identity, string folder)
    {
        if (world == null || string.IsNullOrEmpty(identity)) return true;
        lock (gate)
        {
            var state = Current(world, identity);
            if (state.Empty.Contains(folder)) { state.Skips++; return false; }
            state.Admissions++; return true;
        }
    }
    internal static void ObservedEmpty(object world, string identity, string folder)
    {
        if (world == null || string.IsNullOrEmpty(identity)) return;
        lock (gate)
        {
            var state = Current(world, identity);
            // Only intents/pending for two roles are expected. Never grow an
            // unbounded negative-path cache if a caller introduces more scopes.
            if (state.Empty.Count >= 4 && !state.Empty.Contains(folder)) state.Empty.Clear();
            state.Empty.Add(folder);
        }
    }
    internal static void BeforePublication(object world, string fullPath)
    {
        if (world == null || string.IsNullOrEmpty(fullPath)) return;
        lock (gate)
            if (scopes.TryGetValue(world, out var state))
                if (state.Empty.Remove(Path.GetDirectoryName(fullPath))) state.Invalidations++;
    }
    internal static string Diagnostic(object world)
    {
        lock (gate)
            return world != null && scopes.TryGetValue(world, out var state)
                ? "emptyFolders=" + state.Empty.Count + ", scanAdmissions=" + state.Admissions + ", emptySkips=" + state.Skips + ", publicationInvalidations=" + state.Invalidations
                : "unobserved";
    }
    private static Scope Current(object world, string identity)
    {
        var state = scopes.GetOrCreateValue(world);
        if (!string.Equals(state.Identity, identity, StringComparison.Ordinal))
        { state.Identity = identity; state.Empty.Clear(); state.Admissions = state.Skips = state.Invalidations = 0; }
        return state;
    }
}
