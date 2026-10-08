#nullable disable

/// <summary>
/// Compatibility wrapper retained from Milestone 5C.
/// Actual logic now lives in RebirthStumpHarvestRewardService.
/// </summary>
public static class RebirthStumpHarvestRewardPreview
{
    public static bool TryPreview(Vector3i blockPos, out string report)
    {
        return RebirthStumpHarvestRewardService.TryPreview(blockPos, out report);
    }

    public static string GetSafetyReport()
    {
        return RebirthStumpHarvestRewardService.GetSafetyReport();
    }
}
