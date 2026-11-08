using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Diagnostic for the camper's POI lights (owner report 2026-11-07: "12 lights shown, many more in the POI, and they
/// were not lit"). Stand at the POI with the camper nearby and run "rbcamperlights". It lists every light in the POI
/// with the reason it is or is not claimed, plus the selection limit, range and how many are lit. Read-only.
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthCamperLights : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbcamperlights" };
    public override string getDescription() => "Explains which lights of the camper's current POI are counted, selected and lit.";
    public override string getHelp() => "rbcamperlights [entityId]  (default: nearest camper within 200 m)";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var con = SingletonMonoBehaviour<SdtdConsole>.Instance;
        var world = GameManager.Instance?.World;
        var player = world?.GetPrimaryPlayer();
        if (world == null || player == null) { con.Output("No world or player."); return; }
        if (world.IsRemote()) { con.Output("Run this on the host or in single player (the server holds the camper energy state)."); return; }
        EntityVehicle camper = null;
        if (args.Count > 0 && int.TryParse(args[0], out int id)) camper = world.GetEntity(id) as EntityVehicle;
        if (camper == null)
        {
            float best = 200f * 200f;
            foreach (var e in world.Entities.list)
                if (e is EntityVehicle v && RebirthCamperRadial.IsCamper(v))
                {
                    float d = (v.position - player.position).sqrMagnitude;
                    if (d < best) { best = d; camper = v; }
                }
        }
        if (camper == null || !RebirthCamperRadial.IsCamper(camper)) { con.Output("No camper within 200 m."); return; }

        var lines = new List<string>();
        try
        {
            var poi = RebirthCamperEnergyDeviceAdapter.ObservePoi(camper);
            lines.Add("[REBIRTH CamperLights] camper=" + camper.entityId + " " + JObject.FromObject(poi).ToString(Newtonsoft.Json.Formatting.None));
            var devices = RebirthCamperEnergyDeviceAdapter.ObserveDevices(camper, 512);
            lines.Add("[REBIRTH CamperLights] counted (claimable) POI lights=" + devices["ordinaryPoiLightCount"]
                + " lit=" + (devices["ordinaryPoiLights"] as JArray)?.Count(r => (bool?)r["nativeLightOn"] == true)
                + " ownedByCamper=" + (devices["ordinaryPoiLights"] as JArray)?.Count(r => (bool?)r["ownedByCamper"] == true)
                + " range=" + devices["poiRange"] + " electricalRadius=" + devices["electricalRadius"] + " session=" + devices["sessionExists"]);
            var candidates = devices["poiLightCandidates"] as JArray;
            if (candidates == null || candidates.Count == 0)
                lines.Add("[REBIRTH CamperLights] per-light reasons need the camper in Camp mode at the POI (none reported).");
            else
            {
                foreach (var group in candidates.GroupBy(r => (string)r["reason"] + " / " + (string)r["blockClass"] + " / " + (string)r["tileType"]))
                    lines.Add("[REBIRTH CamperLights] " + group.Count() + " x " + group.Key + "  e.g. " + group.First()["block"] + " at " + group.First()["position"]);
                lines.Add("[REBIRTH CamperLights] authored POI data loaded=" + candidates[0]["authoredDataLoaded"]);
            }
            foreach (var row in (devices["ordinaryPoiLights"] as JArray) ?? new JArray())
                lines.Add("[REBIRTH CamperLights] light " + row["position"] + " " + row["block"] + " on=" + row["nativeLightOn"] + " owned=" + row["ownedByCamper"] + " choice=" + row["manualChoice"]);
        }
        catch (Exception e) { lines.Add("[REBIRTH CamperLights] failed: " + e); }
        foreach (var line in lines) { Log.Out(line); con.Output(line); }
    }
}
