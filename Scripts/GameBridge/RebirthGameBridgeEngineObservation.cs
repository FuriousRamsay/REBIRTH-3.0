using Newtonsoft.Json.Linq;

// Actual native state only. No start/stop, driver, physics or sound method calls.
internal static class RebirthGameBridgeEngineObservation
{
    internal static JObject Snapshot(EntityVehicle vehicle)
    {
        var parts=vehicle?.vehicle?.GetParts();
        if(parts==null)return new JObject{["available"]=false,["missingReason"]="native vehicle parts unavailable"};
        var engines=new JArray();
        foreach(var part in parts)
        {
            if(!(part is VPEngine engine))continue;
            var sounds=new JArray();
            if(engine.gears!=null)for(int gear=0;gear<engine.gears.Count;gear++)
            {
                var ranges=engine.gears[gear]?.soundRanges;if(ranges==null)continue;
                foreach(var sound in ranges)if(sound!=null)sounds.Add(new JObject{
                    ["gear"]=gear,["name"]=sound.name,["handlePresent"]=sound.soundHandle!=null});
            }
            engines.Add(new JObject{["nativePartRunning"]=engine.isRunning,["rpm"]=engine.rpm,
                ["gearIndex"]=engine.gearIndex,["accelerationPhase"]=engine.acceleratePhase,
                ["accelDecelSoundName"]=engine.accelDecelSoundName,["soundRanges"]=sounds});
        }
        return new JObject{["available"]=engines.Count>0,["entityEngineRunning"]=vehicle.IsEngineRunning,
            ["nativeFuelLevel"]=vehicle.vehicle.GetFuelLevel(),["fuelUsageModifier"]=EntityVehicle.VehicleFuelUsageModifier,
            ["engines"]=engines,["sampleRealTime"]=UnityEngine.Time.realtimeSinceStartup,
            ["scope"]="Native current flags/fuel and sound-handle presence only; compare successive samples for fuel consumption. No separate engineStarted field; no startup-event history or audible playback proof."};
    }
}
