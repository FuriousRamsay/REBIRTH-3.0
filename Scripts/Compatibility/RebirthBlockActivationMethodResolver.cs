using System;

#nullable disable

/// <summary>
/// Pass 5 compatibility marker. Runtime activation-method discovery was removed
/// in Pass 6; all affected storage and door targets now use explicit 3.1 Harmony
/// signatures. The file remains so changed-files overlays overwrite the former
/// reflection resolver instead of leaving stale source in an existing project.
/// </summary>
[Obsolete("Runtime block activation reflection was removed. Use explicit 3.1 Harmony bindings.", true)]
internal static class RebirthBlockActivationMethodResolver
{
}
