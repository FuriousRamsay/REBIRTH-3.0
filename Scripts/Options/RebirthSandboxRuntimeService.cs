#nullable disable

public static class RebirthSandboxRuntimeService
{
    public static void OnGameStarting(bool asServer)
    {
        // A pure client must wait for the server snapshot. Hosts, single-player games,
        // and dedicated servers load the authoritative per-world file.
        if (!asServer)
        {
            // The server now sends its REBIRTH snapshot during RequestToEnterGame, before native
            // world/config packets. Depending on frame/network timing that packet may arrive just
            // before or just after this GameStarting callback. Preserve and re-apply an early
            // authoritative packet instead of overwriting it with the temporary client-safe state.
            if (RebirthSandboxSyncService.TryApplyReceivedClientSnapshot("game-starting"))
                return;

            // Until the authoritative server snapshot arrives, keep systems that can materially
            // alter world processing on their conservative/base path. Revision 0 from a new server
            // is still accepted and replaces this state immediately when received.
            RebirthSandboxOptionManager.Current.LoadAuthoritativeSnapshot(
                RebirthSandboxOptionManager.Encode(new RebirthSandboxState { PlayerProgression = RebirthPlayerProgressionMode.BaseGame, AdvancedFarming = false }),
                0,
                true);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[RebirthSandbox] Client waiting for authoritative server option snapshot."); }
            return;
        }

        string source;
        RebirthSandboxSaveData data = RebirthSandboxPersistence.LoadForGameStarting(out source);
        RebirthSandboxOptionManager manager = RebirthSandboxOptionManager.Current;
        if (!manager.LoadAuthoritativeSnapshot(data.Code, data.Revision, true))
        {
            manager.ResetToDefaultsAtRevision(0, true);
            source = "decode-fallback-default";
        }

        // Do not call WorldStaticData.Reset here. Base 3.1 performs its normal
        // static-data load immediately after GameStarting, after this authoritative
        // option snapshot is available. The AddDecoBlock patch will therefore see
        // the correct tree and vehicle multipliers without forcing an unsafe reload.
        RebirthWorldDecorationDensityRuntimePolicy.BeginNativeDefinitionLoad(source);

        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[RebirthSandbox] GameStarting source=" + source
            + " preset=" + data.PresetName
            + " tree=" + manager.TreeDensityMultiplier + "%"
            + " vehicle=" + manager.VehicleDensityMultiplier + "%"
            + " quickStackDistance=" + manager.QuickStackDistance + "m"
            + " remoteResourcesDistance=" + manager.RemoteResourcesDistance + "m"
            + " progression=" + manager.PlayerProgression
            + " revision=" + manager.Revision); }
    }

    public static void OnGameShutdown()
    {
        // Prevent one world's value or a prior server's received snapshot from leaking into a
        // later client/server session.
        RebirthSandboxSyncService.ResetClientSnapshot();
        RebirthSandboxOptionManager.Current.ResetToDefaultsAtRevision(0, true);
    }
}
