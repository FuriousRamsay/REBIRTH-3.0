using HarmonyLib;
using UnityEngine.Scripting;
[Preserve]
public sealed class RebirthPoiMapModApi : IModApi
{
    private static bool installed;
    private static bool active;
    public void InitMod(Mod mod)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || installed) return;
        new Harmony("rebirth.purge.map").CreateClassProcessor(typeof(RebirthPoiMapRenderHook)).Patch();
        ModEvents.GameStarting.RegisterHandler(Starting);
        ModEvents.GameUpdate.RegisterHandler(Update);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        installed = true;
    }
    private static void Starting(ref ModEvents.SGameStartingData data) { RebirthPoiMapSync.Reset(); }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled)
        { if (active) RebirthPoiMapSync.Reset(); active = false; return; }
        active = true;
        RebirthPoiMapSync.Pulse();
        if (!GameManager.IsDedicatedServer) RebirthPoiMapPresentation.Pulse();
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data) { RebirthPoiMapSync.Reset(); }
}

