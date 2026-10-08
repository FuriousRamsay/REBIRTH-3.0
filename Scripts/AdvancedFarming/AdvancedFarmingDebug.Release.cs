#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Advanced Farming debug channels and logging are
/// absent from non-Debug builds; every check is false and every action is a no-op.
/// </summary>
public static class AdvancedFarmingDebug
{
    public static bool Enabled { get { return false; } }
    public static bool Hover { get { return false; } }
    public static bool TileEntity { get { return false; } }
    public static bool SaveLoad { get { return false; } }
    public static bool Water { get { return false; } }
    public static bool Bootstrap { get { return false; } }
    public static bool Catchup { get { return false; } }
    public static void Set(string channel, bool enabled) { }
    public static string Status() { return "[AdvancedFarmingDebug] Debug build required."; }
    public static void Log(string channel, string message) { }
}
#endif
