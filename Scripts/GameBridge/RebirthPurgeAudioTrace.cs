#if PURGE_QUALIFICATION
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Opt-in observation only: never dispatches sound or changes mixer/listener settings.
public sealed class ConsoleCmdRebirthPurgeAudioTrace : ConsoleCmdAbstract
{
    private static bool running;
    private static readonly JArray samples = new JArray();
    private static string error;
    private static int generation;
    public override string[] getCommands() => new[] { "rbpurgeaudiotrace" };
    public override string getDescription() => "Observe native Purge clips in disposable CodexTest.";
    public override string getHelp() => "rbpurgeaudiotrace start [seconds 1..120] | status | stop";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        if (sender.RemoteClientInfo != null || world == null || world.IsRemote()
            || !RebirthGameBridge.IsRunning || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
        { SdtdConsole.Instance.Output("*** ERROR local CodexTest Bridge required."); return; }
        if (args.Count >= 1 && args[0] == "start")
        {
            int seconds = 60;
            if (running || args.Count > 2 || (args.Count == 2 && !int.TryParse(args[1], out seconds)) || seconds < 1 || seconds > 120)
            { SdtdConsole.Instance.Output("*** ERROR idle trace and duration 1..120 required."); return; }
            samples.Clear(); error = null; running = true;
            try { GameManager.Instance.StartCoroutine(Collect(world, seconds, ++generation)); }
            catch (Exception failure) { running = false; error = failure.ToString(); }
        }
        else if (args.Count == 1 && args[0] == "stop") { running = false; generation++; }
        else if (args.Count != 1 || args[0] != "status")
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        SdtdConsole.Instance.Output(new JObject {
            ["running"] = running, ["error"] = error, ["samples"] = samples.DeepClone(),
            ["masterVolumePreference"] = GamePrefs.GetFloat(EnumGamePrefs.OptionsOverallAudioVolumeLevel),
            ["applicationFocused"] = Application.isFocused,
            ["evidence"] = "Native source observation; not human listening or event attribution by itself."
        }.ToString(Formatting.None));
    }
    private static IEnumerator Collect(World world, int seconds, int token)
    {
        float start = Time.realtimeSinceStartup;
        try
        {
            while (running && token == generation && Time.realtimeSinceStartup - start < seconds)
            {
                if (!ReferenceEquals(GameManager.Instance?.World, world) || !RebirthGameBridge.IsRunning
                    || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") break;
                try
                {
                    foreach (var source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    {
                        var clip = source.clip;
                        if (clip == null || !source.isPlaying || !clip.name.StartsWith("Purge", StringComparison.OrdinalIgnoreCase)) continue;
                        if (samples.Count >= 4096) { error = "Sample limit reached."; running = false; break; }
                        samples.Add(new JObject {
                            ["elapsed"] = Time.realtimeSinceStartup - start,
                            ["sourceId"] = source.GetInstanceID(), ["clip"] = clip.name,
                            ["timeSamples"] = source.timeSamples, ["clipSamples"] = clip.samples,
                            ["volume"] = source.volume, ["mute"] = source.mute,
                            ["listenerVolume"] = AudioListener.volume, ["listenerPaused"] = AudioListener.pause,
                            ["masterVolumePreference"] = GamePrefs.GetFloat(EnumGamePrefs.OptionsOverallAudioVolumeLevel),
                            ["applicationFocused"] = Application.isFocused,
                            ["mixer"] = source.outputAudioMixerGroup?.name
                        });
                    }
                }
                catch (Exception failure) { error = failure.ToString(); break; }
                yield return new WaitForSecondsRealtime(.1f);
            }
        }
        finally { if (token == generation) running = false; }
    }
}
#endif
