using HarmonyLib;
using UnityEngine.Scripting;
[Preserve]
public sealed class RebirthPoiMapModApi : IModApi
{
    private static bool installed;
    private static bool active;
    private static bool starting,update,worldShutdown,gameShutdown;
    public void InitMod(Mod mod)
    {
        if (!RebirthPurgeReleasePolicy.Enabled || installed) return;
        var harmony=new Harmony("rebirth.purge.map");
        try
        {
            harmony.CreateClassProcessor(typeof(RebirthPoiMapRenderHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPoiMapSpriteHook)).Patch();
            harmony.CreateClassProcessor(typeof(RebirthPoiMapHoverHook)).Patch();
            if(!starting){ModEvents.GameStarting.RegisterHandler(Starting);starting=true;}
            if(!update){ModEvents.GameUpdate.RegisterHandler(Update);update=true;}
            if(!worldShutdown){ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);worldShutdown=true;}
            if(!gameShutdown){ModEvents.GameShutdown.RegisterHandler(GameShutdown);gameShutdown=true;}
        }
        catch
        {
            harmony.UnpatchSelf();
            throw;
        }
        installed = true;
    }
    private static void Starting(ref ModEvents.SGameStartingData data) { Reset(); }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        if(!installed)return;
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled)
        { if (active) RebirthPoiMapSync.Reset(); active = false; return; }
        active = true;
        RebirthPoiMapSync.Pulse();
        RebirthPurgeNoticeDelivery.Pulse();
        if (!GameManager.IsDedicatedServer) RebirthPoiMapPresentation.Pulse();
    }
    private static void Reset(){active=false;RebirthPoiMapSync.Reset();}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data) { Reset(); }
    private static void GameShutdown(ref ModEvents.SGameShutdownData data) { Reset(); }
}

