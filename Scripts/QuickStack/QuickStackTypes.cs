#nullable disable

/// <summary>
/// Runtime policy modes for the REBIRTH Quick Stack feature.
/// Kept with Quick Stack rather than the sandbox-options implementation so every
/// Quick Stack source file has a stable feature-owned type dependency.
/// </summary>
public enum RebirthQuickStackMode
{
    Off = 0,
    Strict = 1,
    Full = 2
}
