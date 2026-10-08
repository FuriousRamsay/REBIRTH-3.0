using System.Text;

#nullable disable

/// <summary>
/// Explicit manual policy for the stump/vehicle harvest context slice.
/// Defaults disabled.
/// </summary>
public static class RebirthStumpHarvestContextPolicy
{
    private static bool s_enabled;
    private static bool s_serverOnly = true;
    private static bool s_requireStumpName = true;
    private static bool s_requirePlayerDestroyer = true;
    private static bool s_requireVehicleAttachedPlayer = true;

    public static bool Enabled { get { return s_enabled; } }
    public static bool ServerOnly { get { return s_serverOnly; } }
    public static bool RequireStumpName { get { return s_requireStumpName; } }
    public static bool RequirePlayerDestroyer { get { return s_requirePlayerDestroyer; } }
    public static bool RequireVehicleAttachedPlayer { get { return s_requireVehicleAttachedPlayer; } }

    public static bool CanStoreContext(WorldBase world)
    {
        if (!s_enabled)
            return false;

        if (world == null)
            return false;

        if (s_serverOnly)
        {
            bool isRemote = false;
            try { isRemote = world.IsRemote(); } catch { isRemote = false; }
            if (isRemote)
                return false;
        }

        return true;
    }

    public static void EnableManualTest(bool requireVehicleAttachedPlayer)
    {
        s_enabled = true;
        s_serverOnly = true;
        s_requireStumpName = true;
        s_requirePlayerDestroyer = true;
        s_requireVehicleAttachedPlayer = requireVehicleAttachedPlayer;
    }

    public static void Disable()
    {
        s_enabled = false;
        s_serverOnly = true;
        s_requireStumpName = true;
        s_requirePlayerDestroyer = true;
        s_requireVehicleAttachedPlayer = true;
    }

    public static string GetSummaryReport()
    {
        return "[RebirthStumpHarvestContextPolicy] enabled: " + s_enabled
            + "; serverOnly: " + s_serverOnly
            + "; requireStumpName: " + s_requireStumpName
            + "; requirePlayerDestroyer: " + s_requirePlayerDestroyer
            + "; requireVehicleAttachedPlayer: " + s_requireVehicleAttachedPlayer;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  Stores stump context only after explicit manual enable.");
        sb.AppendLine("  Default is disabled.");
        sb.AppendLine("  Intended old behavior source: Harmony/Harmony_Block_HarvestContext.cs.");
        return sb.ToString();
    }
}
