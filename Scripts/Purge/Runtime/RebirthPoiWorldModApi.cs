using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthPoiWorldModApi : IModApi
{
    private static bool starting,done,update,worldShutdown,gameShutdown;
    public void InitMod(Mod mod)
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        RebirthPurgeObjectivePolicy.Load(mod.Path);RebirthPurgeDiscoveryPolicy.Load(mod.Path);RebirthPurgeSupplyPolicy.Load(mod.Path);
        // Track each successful registration so a partial InitMod failure cannot multiply
        // subscriptions on retry. Native Mod.InitModCode discovers every IModApi in assembly.
        if(!starting) { ModEvents.GameStarting.RegisterHandler(OnStarting); starting=true; }
        if(!done) { ModEvents.GameStartDone.RegisterHandler(OnDone); done=true; }
        if(!update) { ModEvents.GameUpdate.RegisterHandler(OnUpdate); update=true; }
        if(!worldShutdown) { ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShutdown); worldShutdown=true; }
        if(!gameShutdown) { ModEvents.GameShutdown.RegisterHandler(OnGameShutdown); gameShutdown=true; }
    }
    private static void OnStarting(ref ModEvents.SGameStartingData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPurgeClearNotification.Reset(); RebirthPurgeMilestoneNotification.Reset(); RebirthPurgeInformationService.Reset(); RebirthPurgeSupplyCoordinator.Reset(); RebirthPurgeSupplyService.Reset(); RebirthPurgeDiscoveryService.Reset(); RebirthPurgeMilestoneService.Reset(); RebirthPurgeObjectiveProgress.Instance.Reset(); RebirthPurgePoiCensus.Instance.Reset(); RebirthPoiWorldLifecycle.Instance.Begin(data.AsServer); }
    private static void OnDone(ref ModEvents.SGameStartDoneData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  if (RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled) RebirthPoiWorldLifecycle.Instance.StartDone(); else { RebirthPurgeClearNotification.Reset(); RebirthPurgeMilestoneNotification.Reset(); RebirthPurgeInformationService.Reset(); RebirthPurgeSupplyCoordinator.Reset(); RebirthPurgeSupplyService.Reset(); RebirthPurgeDiscoveryService.Reset(); RebirthPurgeMilestoneService.Reset(); RebirthPurgeObjectiveProgress.Instance.Reset(); RebirthPurgePoiCensus.Instance.Reset(); RebirthPoiWorldLifecycle.Instance.Stop(); } }
    private static void OnUpdate(ref ModEvents.SGameUpdateData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPoiWorldLifecycle.Instance.Tick(); RebirthPurgeClearNotification.Pulse(); RebirthPurgeMilestoneNotification.Pulse(); RebirthPurgeInformationService.Pulse(); RebirthPurgePoiCensus.Instance.Pulse(); RebirthPurgeObjectiveProgress.Instance.Pulse(); RebirthPurgeMilestoneService.Pulse(); RebirthPurgeDiscoveryService.Pulse(); RebirthPurgeSupplyService.Pulse(); RebirthPurgeHudProgress.Pulse(); RebirthPurgeSupplyCoordinator.Pulse(); }
    private static void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPurgeClearNotification.Reset(); RebirthPurgeMilestoneNotification.Reset(); RebirthPurgeInformationService.Reset(); RebirthPurgeSupplyCoordinator.Reset(); RebirthPurgeSupplyService.Reset(); RebirthPurgeDiscoveryService.Reset(); RebirthPurgeMilestoneService.Reset(); RebirthPurgeObjectiveProgress.Instance.Reset(); RebirthPurgePoiCensus.Instance.Reset(); RebirthPoiWorldLifecycle.Instance.Stop(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { if(!RebirthPurgeReleasePolicy.Enabled) return;  RebirthPurgeClearNotification.Reset(); RebirthPurgeMilestoneNotification.Reset(); RebirthPurgeInformationService.Reset(); RebirthPurgeSupplyCoordinator.Reset(); RebirthPurgeSupplyService.Reset(); RebirthPurgeDiscoveryService.Reset(); RebirthPurgeMilestoneService.Reset(); RebirthPurgeObjectiveProgress.Instance.Reset(); RebirthPurgePoiCensus.Instance.Reset(); RebirthPoiWorldLifecycle.Instance.Stop(); }
}