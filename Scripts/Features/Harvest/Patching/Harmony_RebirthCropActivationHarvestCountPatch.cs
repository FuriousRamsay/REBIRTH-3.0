#nullable disable

/// <summary>
/// 7DTD 3.0 compatibility placeholder.
/// The old 2.6 target BlockCropsGrown.OnBlockActivated no longer exists in the 3.0 base source provided.
/// This class intentionally has no HarmonyPatch attribute and is not installed by the 3.0 installer.
/// </summary>
public static class Harmony_RebirthCropActivationHarvestCountPatch
{
    public static string Status()
    {
        return "[Harmony_RebirthCropActivationHarvestCountPatch] 3.0 placeholder: old BlockCropsGrown target removed; harvest replacement requires a 3.0 harvest-surface audit.";
    }
}
