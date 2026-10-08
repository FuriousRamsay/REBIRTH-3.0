#nullable disable

/// <summary>
/// Shared safety classification for Harmony patch declarations and evidence records.
/// Kept outside Scripts/Performance because that tree is excluded by the normal project profile.
/// </summary>
public enum RebirthPatchSafetyKind
{
    Structural,
    Behavioral,
    Diagnostic
}
