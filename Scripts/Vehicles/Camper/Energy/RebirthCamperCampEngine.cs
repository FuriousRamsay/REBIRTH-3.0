using HarmonyLib;
using System.Runtime.CompilerServices;

// Keep native seat/camera admission. Refuse the engine start before it can
// produce a startup sound, consume fuel or begin alternator charging in Camp.
[HarmonyPatch(typeof(VPEngine), "startEngine")]
internal static class RebirthCamperCampEngine
{
    private sealed class DeferredStart { internal Entity Driver; }
    private static readonly ConditionalWeakTable<VPEngine,DeferredStart> deferred=new ConditionalWeakTable<VPEngine,DeferredStart>();
    internal static bool Prefix(VPEngine __instance)
    {
        var vehicle=__instance?.vehicle?.entity;
        if(!RebirthCamperEnergyDrivingInput.Blocked(vehicle))return true;
        // Native seat entry requests a start only once. Remember that request,
        // rather than requiring the driver to exit and enter after packing.
        if(vehicle?.AttachedMainEntity is EntityAlive driver)
            deferred.GetValue(__instance,_=>new DeferredStart()).Driver=driver;
        return false;
    }
    internal static void Resume(VPEngine engine)
    {
        if(engine==null||!deferred.TryGetValue(engine,out var pending))return;
        var vehicle=engine.vehicle?.entity;
        if(vehicle==null||vehicle.AttachedMainEntity!=pending.Driver)
        {deferred.Remove(engine);return;}
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Travel)return;
        deferred.Remove(engine);
        // Keep native broken-part checks and the existing restoration start guard.
        if(!engine.IsBroken())engine.startEngine();
    }
}

[HarmonyPatch(typeof(VPEngine),nameof(VPEngine.Update))]
internal static class RebirthCamperCampEngineResume
{
    private static void Postfix(VPEngine __instance)=>RebirthCamperCampEngine.Resume(__instance);
}
