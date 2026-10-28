using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace Rebirth.VehicleStudio.Diagnostics
{
    internal static class CamperAudioEvidence
    {
        internal static object Capture(EntityVehicle vehicle)
        {
            var sources = new List<object>();
            Action<string,string,AudioSource> add = (route, group, source) => {
                if (source == null) return;
                sources.Add(new { route, group, entityId = vehicle.entityId, sourceId = source.GetInstanceID(),
                    clip = source.clip == null ? null : source.clip.name, clipInstanceId = source.clip == null ? 0 : source.clip.GetInstanceID(),
                    clipLength = source.clip == null ? 0 : source.clip.length, clipSamples = source.clip == null ? 0 : source.clip.samples,
                    isPlaying = source.isPlaying, active = source.isActiveAndEnabled, mute = source.mute,
                    volume = source.volume, pitch = source.pitch, timeSamples = source.timeSamples, loop = source.loop,
                    spatialBlend = source.spatialBlend, minDistance = source.minDistance, maxDistance = source.maxDistance,
                    listenerDistance = Vector3.Distance(source.transform.position, Audio.Manager.currentListenerPosition),
                    mixer = source.outputAudioMixerGroup == null ? null : source.outputAudioMixerGroup.name });
            };
            Action<string,string,GameObject> addObject = (route, group, obj) => {
                if (obj != null) foreach (var source in obj.GetComponentsInChildren<AudioSource>(true)) add(route,group,source);
            };
            if (Audio.Manager.loopingOnEntity.TryGetValue(vehicle.entityId, out var loops))
                foreach (var pair in loops) { addObject("loopingOnEntity.near",pair.Key,pair.Value.near); addObject("loopingOnEntity.far",pair.Key,pair.Value.far); }
            if (Audio.Manager.sequenceOnEntity.TryGetValue(vehicle.entityId, out var sequences))
                foreach (var pair in sequences) {
                    addObject("sequenceOnEntity.nearStart",pair.Key,pair.Value.nearStart); addObject("sequenceOnEntity.nearLoop",pair.Key,pair.Value.nearLoop);
                    addObject("sequenceOnEntity.nearEnd",pair.Key,pair.Value.nearEnd); addObject("sequenceOnEntity.farStart",pair.Key,pair.Value.farStart);
                    addObject("sequenceOnEntity.farLoop",pair.Key,pair.Value.farLoop); addObject("sequenceOnEntity.farEnd",pair.Key,pair.Value.farEnd);
                }
            if (Audio.Manager.playingOnEntity.TryGetValue(vehicle.entityId, out var channels) && channels.environment != null)
                foreach (var pair in channels.environment.dict)
                    foreach (var source in pair.Value) add("playingOnEntity.environment",pair.Key,source);
            return new { entityId = vehicle.entityId, vehicleName = vehicle.vehicle.GetName(),
                hornGroup = vehicle.vehicle.GetHornSoundName(), capturedTime = Time.realtimeSinceStartup,
                listenerVolume = AudioListener.volume, listenerPaused = AudioListener.pause,
                association = "Native Audio.Manager dictionaries keyed by exact vehicle entity ID; no proximity inference.", sources };
        }
    }

    [Preserve]
    public sealed class ConsoleCmdCamperAudioTrace : ConsoleCmdAbstract
    {
        private static bool running;
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows | DeviceFlag.StandaloneLinux | DeviceFlag.StandaloneOSX;
        public override string[] getCommands() => new[] { "camperaudiotrace" };
        public override string getDescription() => "Read-only native vehicle AudioSource witness: entityId seconds(1..60). No sound/input dispatch.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            if (running || GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest"
                || args.Count != 2 || !int.TryParse(args[0], out int id) || !int.TryParse(args[1], out int seconds) || seconds < 1 || seconds > 60)
            { SdtdConsole.Instance.Output("*** ERROR read-only audio trace requires idle CodexTest client, entityId and 1..60 seconds."); return; }
            var vehicle = GameManager.Instance.World.GetEntity(id) as EntityVehicle;
            if (vehicle == null || (vehicle.vehicle.GetName() != "rebirthCamper80s" && vehicle.vehicle.GetName() != "vehicleTruck4x4"))
            { SdtdConsole.Instance.Output("*** ERROR loaded camper/native 4x4 required."); return; }
            var path = Path.Combine(CamperDiagnosticsMod.ExportFolder, "audio_" + Guid.NewGuid().ToString("N") + ".json");
            running = true;
            try { GameManager.Instance.StartCoroutine(Collect(vehicle,seconds,path)); }
            catch { running = false; throw; }
            SdtdConsole.Instance.Output("[CamperAudio] STARTED " + path);
        }
        private static IEnumerator Collect(EntityVehicle vehicle, int seconds, string path)
        {
            var samples = new List<object>(); string error = null; float start = Time.realtimeSinceStartup;
            try {
                while (Time.realtimeSinceStartup - start < seconds) {
                    if (vehicle == null || GameManager.Instance.World.GetEntity(vehicle.entityId) != vehicle) { error = "Vehicle identity disappeared."; break; }
                    try { samples.Add(CamperAudioEvidence.Capture(vehicle)); }
                    catch (Exception failure) { error = failure.ToString(); break; }
                    yield return new WaitForSecondsRealtime(.05f);
                }
            }
            finally {
                running = false; Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,JsonConvert.SerializeObject(new { kind = "REBIRTH_NATIVE_VEHICLE_AUDIO_TRACE", status = error == null ? "CAPTURED_NOT_HUMAN_LISTENING" : "FAIL", error, intervalSeconds = .05f, samples },Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperAudio] CAPTURED " + path);
            }
        }
    }
}
