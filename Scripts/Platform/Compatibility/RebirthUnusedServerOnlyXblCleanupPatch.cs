using HarmonyLib;
using Platform;
using Platform.XBL;

// Unapplied candidate: prevent cleanup of the exact server-only API which never
// entered its client initialization lifecycle. Do not hide HRESULT/log failures.
[HarmonyPatch(typeof(XblPlatformApi), nameof(XblPlatformApi.Destroy))]
internal static class RebirthUnusedServerOnlyXblCleanupCandidate
{
    private static bool Prefix(XblPlatformApi __instance)
    {
        try {
        if (!ThreadManager.IsMainThread()) return true;
        if (!RebirthUnusedServerOnlyXblCleanupInstaller.CleanupQualified()) return true;
        if (__instance == null || __instance.GetType() != typeof(Api)) return true;
        if (__instance.ClientApiStatus != EApiStatus.Uninitialized ||
            __instance.m_dispatchGXDKTaskQueueThread != null ||
            __instance.m_dispatchGXDKTaskQueueRunning) return true;
        IPlatform server;
        if (!PlatformManager.ServerPlatforms.TryGetValue(EPlatformIdentifier.XBL, out server) ||
            server == null || !server.AsServerOnly || !ReferenceEquals(server.Api, __instance)) return true;
        // A native/crossplatform XBL client retains all its original cleanup.
        if (PlatformManager.NativePlatform == null ||
            PlatformManager.NativePlatform.PlatformIdentifier != EPlatformIdentifier.Steam ||
            (PlatformManager.CrossplatformPlatform == null || PlatformManager.CrossplatformPlatform.PlatformIdentifier != EPlatformIdentifier.EOS)) return true;
        // Other GDK-backed API objects would make global SDK ownership ambiguous.
        if (PlatformManager.NativePlatform.Api is XblPlatformApi ||
            (PlatformManager.CrossplatformPlatform != null && PlatformManager.CrossplatformPlatform.Api is XblPlatformApi) ||
            (PlatformManager.MultiPlatform != null && PlatformManager.MultiPlatform.Api is XblPlatformApi)) return true;
        foreach (var entry in PlatformManager.ServerPlatforms)
            if (entry.Value != null && entry.Value.Api is XblPlatformApi &&
                !ReferenceEquals(entry.Value.Api, __instance)) return true;
        return false;
        } catch { return true; } // uncertainty retains native cleanup
    }
}





