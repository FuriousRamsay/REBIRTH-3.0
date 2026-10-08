using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthPoiWorldModApi : IModApi
{
    private static bool starting,done,update,worldShutdown,gameShutdown;
    public void InitMod(Mod mod)
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        // Track each successful registration so a partial InitMod failure cannot multiply
        // subscriptions on retry. Native Mod.InitModCode discovers every IModApi in assembly.
        if(!starting) { ModEvents.GameStarting.RegisterHandler(OnStarting); starting=true; }
        if(!done) { ModEvents.GameStartDone.RegisterHandler(OnDone); done=true; }
        if(!update) { ModEvents.GameUpdate.RegisterHandler(OnUpdate); update=true; }
        if(!worldShutdown) { ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShutdown); worldShutdown=true; }
        if(!gameShutdown) { ModEvents.GameShutdown.RegisterHandler(OnGameShutdown); gameShutdown=true; }
    }
    private static void OnStarting(ref ModEvents.SGameStartingData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.Begin(data.AsServer); }
    private static void OnDone(ref ModEvents.SGameStartDoneData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.StartDone(); }
    private static void OnUpdate(ref ModEvents.SGameUpdateData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.Tick(); }
    private static void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.Stop(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.Stop(); }
}