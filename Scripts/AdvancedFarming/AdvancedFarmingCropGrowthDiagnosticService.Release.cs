#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. The paired crop-growth diagnostic is compiled only in Debug builds.
/// </summary>
public static class AdvancedFarmingCropGrowthDiagnosticService
{
    public const int DefaultRadius = 8;
    public const int DefaultDurationSeconds = 60;
    public const int DefaultIntervalSeconds = 10;
    public const int MaxCrops = 64;

    public static int ClampRadius(int value) { return System.Math.Max(1, System.Math.Min(16, value)); }
    public static int ClampDurationSeconds(int value) { return System.Math.Max(20, System.Math.Min(180, value)); }
    public static int ClampIntervalSeconds(int value) { return System.Math.Max(5, System.Math.Min(30, value)); }
    public static Vector3i[] FindNearbyImmatureCrops(World world, Vector3i center, int radius) { return System.Array.Empty<Vector3i>(); }
    public static string Start(World world, string sessionId, string role, int requestingEntityId, Vector3i center, int radius, int durationSeconds, int intervalSeconds, Vector3i[] cropPositions)
    {
        return "[AdvancedFarming] Crop-growth diagnostics are available only in a Debug build.";
    }
    public static void Pump(World world) { }
    public static void Reset(string reason) { }
    public static string BuildSessionId(int entityId) { return "debug-disabled-" + entityId; }
}
#endif
