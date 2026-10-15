#if DEBUG
#nullable disable

#if DEBUG
public static class AdvancedFarmingDebug
{
    private static bool s_hover;
    private static bool s_tileEntity;
    private static bool s_saveLoad;
    private static bool s_water;
    private static bool s_bootstrap;
    private static bool s_catchup;

    public static bool Enabled
    {
        get { return s_hover || s_tileEntity || s_saveLoad || s_water || s_bootstrap || s_catchup; }
    }

    public static bool Hover { get { return s_hover; } }
    public static bool TileEntity { get { return s_tileEntity; } }
    public static bool SaveLoad { get { return s_saveLoad; } }
    public static bool Water { get { return s_water; } }
    public static bool Bootstrap { get { return s_bootstrap; } }
    public static bool Catchup { get { return s_catchup; } }

    public static void Set(string channel, bool enabled)
    {
        if (channel == null)
            return;

        string c = channel.ToLowerInvariant();

        if (c == "all")
        {
            s_hover = enabled;
            s_tileEntity = enabled;
            s_saveLoad = enabled;
            s_water = enabled;
            s_bootstrap = enabled;
            s_catchup = enabled;
            return;
        }

        if (c == "hover")
            s_hover = enabled;
        else if (c == "tileentity" || c == "te")
            s_tileEntity = enabled;
        else if (c == "saveload" || c == "save")
            s_saveLoad = enabled;
        else if (c == "water")
            s_water = enabled;
        else if (c == "bootstrap" || c == "boot")
            s_bootstrap = enabled;
        else if (c == "catchup")
            s_catchup = enabled;
    }

    public static string Status()
    {
        return "[AdvancedFarmingDebug] hover=" + s_hover
            + " tileEntity=" + s_tileEntity
            + " saveLoad=" + s_saveLoad
            + " water=" + s_water
            + " bootstrap=" + s_bootstrap
            + " catchup=" + s_catchup;
    }

    public static void Log(string channel, string message)
    {
        global::Log.Out("[AdvancedFarming:" + channel + "] " + message);
    }
}
#endif
#endif
