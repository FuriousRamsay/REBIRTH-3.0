#nullable disable

/// <summary>
/// Authoritative per-save option value used when a destroyed vehicle creates its
/// native scheduled respawn marker. The marker itself is persisted by the game's
/// WorldBlockTicker data stored with the chunk.
/// </summary>
public static class RebirthVehicleBlockRespawnRuntimePolicy
{
    private static int days;

    public static int Days
    {
        get { return days; }
    }

    public static bool Enabled
    {
        get { return days > 0; }
    }

    public static void SetDays(int value)
    {
        int normalized = value == 3 || value == 7 || value == 14 || value == 21 ? value : 0;
        if (days == normalized) return;
        days = normalized;
        RebirthVehicleRespawnRuntimeRegistry.ApplyCurrentOptionState();
    }
}
