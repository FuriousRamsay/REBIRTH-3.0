#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthHeat : ConsoleCmdAbstract
{
    private static RebirthHeatDiagnosticRunner runner;

    public override bool IsExecuteOnClient { get { return true; } }
    public override string[] getCommands() { return new[] { "rbheat" }; }
    public override string getDescription() { return "Runs a timed REBIRTH fire heatmap diagnostic."; }
    public override string getHelp() { return "Usage: rbheat [seconds]\nExample: rbheat 20\nStay near burning blocks until the completion toolbelt message."; }

    public override void Execute(List<string> parameters, CommandSenderInfo sender)
    {
        int seconds = 20;
        if (parameters != null && parameters.Count > 0)
        {
            int parsed;
            if (int.TryParse(parameters[0], out parsed)) seconds = Mathf.Clamp(parsed, 5, 120);
        }

        if (runner != null)
        {
            UnityEngine.Object.Destroy(runner.gameObject);
            runner = null;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null)
        {
            Log.Warning("[RBHeat] Cannot start: local world/player unavailable.");
            RebirthFireTestRunner.NotifyToolbelt("Heat diagnostic could not start: local player/world unavailable.");
            return;
        }

        GameObject go = new GameObject("RebirthHeatDiagnosticRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<RebirthHeatDiagnosticRunner>();
        runner.Configure(seconds, delegate { runner = null; });
    }
}

public sealed class RebirthHeatDiagnosticRunner : MonoBehaviour
{
    private int durationSeconds;
    private Action finished;
    private long eventsStart;
    private long attemptsStart;
    private long skippedOptionStart;
    private long skippedDirectorStart;
    private long skippedStrengthStart;
    private long skippedVanillaGateStart;
    private long destinationUnavailableStart;
    private long destinationNotReadyStart;
    private long destinationAcceptedStart;
    private double destinationActivityDeltaStart;
    private long destinationEventDeltaStart;
    private int activeStart;
    private Vector3i playerStart;
    private string directorStart;

    public void Configure(int seconds, Action onFinished)
    {
        durationSeconds = seconds;
        finished = onFinished;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null) yield break;

        RebirthFireService service = RebirthFireService.Instance;
        eventsStart = service.HeatMapEventsTotal;
        attemptsStart = service.HeatMapAttemptsTotal;
        skippedOptionStart = service.HeatMapSkippedOptionTotal;
        skippedDirectorStart = service.HeatMapSkippedDirectorTotal;
        skippedStrengthStart = service.HeatMapSkippedStrengthTotal;
        skippedVanillaGateStart = service.HeatMapSkippedVanillaGateTotal;
        destinationUnavailableStart = service.HeatMapDestinationUnavailableTotal;
        destinationNotReadyStart = service.HeatMapDestinationNotReadyTotal;
        destinationAcceptedStart = service.HeatMapDestinationAcceptedTotal;
        destinationActivityDeltaStart = service.HeatMapDestinationActivityDeltaTotal;
        destinationEventDeltaStart = service.HeatMapDestinationEventDeltaTotal;
        activeStart = service.ActiveCount;
        playerStart = World.worldToBlockPos(player.position);
        directorStart = CaptureDirectorNumbers(world.GetAIDirector());

        Log.Out("[RBHeat] === BEGIN durationSeconds=" + durationSeconds + " ===");
        Log.Out("[RBHeat] setup blocksCatchFireOption=" + RebirthSandboxOptionManager.Current.BlocksCatchFire
            + " fireAffectsHeatmapOption=" + RebirthSandboxOptionManager.Current.FireAffectsHeatmap
            + " runtimeEnabled=" + RebirthFireRuntimePolicy.Enabled
            + " runtimeAffectsHeatmap=" + RebirthFireRuntimePolicy.AffectsHeatmap
            + " activeFires=" + activeStart
            + " playerBlock=" + playerStart
            + " playerChunk=" + World.toChunkXZ(playerStart.x) + "," + World.toChunkXZ(playerStart.z)
            + " defaultStrengthPerProcess=" + RebirthFireDefaults.HeatMapStrengthPerProcess.ToString("F6")
            + " processIntervalSeconds=" + RebirthFireDefaults.ProcessIntervalSeconds.ToString("F1")
            + " zombieHordeMeter=" + GameStats.GetBool(EnumGameStats.ZombieHordeMeter)
            + " spawnEnemies=" + GameStats.GetBool(EnumGameStats.IsSpawnEnemies));
        Log.Out("[RBHeat] directorBefore " + directorStart);
        RebirthFireTestRunner.NotifyToolbelt("Heat diagnostic started for " + durationSeconds + "s. Stay near burning blocks.");

        float elapsed = 0f;
        bool halfway = false;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            if (!halfway && elapsed >= durationSeconds * 0.5f)
            {
                halfway = true;
                RebirthFireTestRunner.NotifyToolbelt("Heat diagnostic halfway. Keep burning blocks active.");
                Log.Out("[RBHeat] progress elapsed=" + elapsed.ToString("F1")
                    + " activeFires=" + service.ActiveCount
                    + " attemptsDelta=" + (service.HeatMapAttemptsTotal - attemptsStart)
                    + " eventsDelta=" + (service.HeatMapEventsTotal - eventsStart)
                    + " lastPosition=" + service.LastHeatMapPosition
                    + " lastStrength=" + service.LastHeatMapStrength.ToString("F6")
                    + " destinationAvailable=" + service.LastHeatMapDestinationAvailable
                    + " destinationReady=" + service.LastHeatMapDestinationReady
                    + " activityBefore=" + service.LastHeatMapActivityBefore.ToString("F6")
                    + " activityAfter=" + service.LastHeatMapActivityAfter.ToString("F6")
                    + " eventCountBefore=" + service.LastHeatMapEventCountBefore
                    + " eventCountAfter=" + service.LastHeatMapEventCountAfter);
            }
            yield return null;
        }

        string directorEnd = CaptureDirectorNumbers(world.GetAIDirector());
        long attempts = service.HeatMapAttemptsTotal - attemptsStart;
        long eventsAdded = service.HeatMapEventsTotal - eventsStart;
        long skippedOption = service.HeatMapSkippedOptionTotal - skippedOptionStart;
        long skippedDirector = service.HeatMapSkippedDirectorTotal - skippedDirectorStart;
        long skippedStrength = service.HeatMapSkippedStrengthTotal - skippedStrengthStart;
        long skippedVanillaGate = service.HeatMapSkippedVanillaGateTotal - skippedVanillaGateStart;
        long destinationUnavailable = service.HeatMapDestinationUnavailableTotal - destinationUnavailableStart;
        long destinationNotReady = service.HeatMapDestinationNotReadyTotal - destinationNotReadyStart;
        long destinationAccepted = service.HeatMapDestinationAcceptedTotal - destinationAcceptedStart;
        double destinationActivityDelta = service.HeatMapDestinationActivityDeltaTotal - destinationActivityDeltaStart;
        long destinationEventDelta = service.HeatMapDestinationEventDeltaTotal - destinationEventDeltaStart;

        Log.Out("[RBHeat] result activeStart=" + activeStart
            + " activeEnd=" + service.ActiveCount
            + " attempts=" + attempts
            + " notifyEvents=" + eventsAdded
            + " skippedOption=" + skippedOption
            + " skippedDirector=" + skippedDirector
            + " skippedStrength=" + skippedStrength
            + " skippedVanillaGate=" + skippedVanillaGate
            + " destinationUnavailable=" + destinationUnavailable
            + " destinationNotReady=" + destinationNotReady
            + " destinationAccepted=" + destinationAccepted
            + " destinationActivityDelta=" + destinationActivityDelta.ToString("F6")
            + " destinationEventDelta=" + destinationEventDelta
            + " lastPosition=" + service.LastHeatMapPosition
            + " lastStrength=" + service.LastHeatMapStrength.ToString("F6")
            + " lastDestinationAvailable=" + service.LastHeatMapDestinationAvailable
            + " lastDestinationReady=" + service.LastHeatMapDestinationReady
            + " lastActivityBefore=" + service.LastHeatMapActivityBefore.ToString("F6")
            + " lastActivityAfter=" + service.LastHeatMapActivityAfter.ToString("F6")
            + " lastEventCountBefore=" + service.LastHeatMapEventCountBefore
            + " lastEventCountAfter=" + service.LastHeatMapEventCountAfter);
        Log.Out("[RBHeat] directorAfter " + directorEnd);

        string conclusion;
        if (!RebirthSandboxOptionManager.Current.BlocksCatchFire)
            conclusion = "FAIL: Blocks Catch Fire option is disabled.";
        else if (!RebirthSandboxOptionManager.Current.FireAffectsHeatmap)
            conclusion = "FAIL: Fire Affects Heatmap option is disabled in saved state.";
        else if (!RebirthFireRuntimePolicy.AffectsHeatmap)
            conclusion = "FAIL: saved option is enabled but runtime heatmap policy is false; option application/synchronization is broken.";
        else if (service.ActiveCount == 0 && activeStart == 0)
            conclusion = "INCONCLUSIVE: no active burning blocks were present.";
        else if (attempts == 0)
            conclusion = "FAIL: active fires never reached the heat contribution path during the test.";
        else if (skippedVanillaGate > 0 && destinationAccepted == 0)
            conclusion = "FAIL: vanilla AIDirector gates rejected all fire heat attempts; inspect zombieHordeMeter/spawnEnemies/bloodMoon/bossHorde.";
        else if (destinationNotReady > 0 && destinationAccepted == 0)
            conclusion = "FAIL: target AI heat region was in cooldown/not ready, so every fire heat event was discarded.";
        else if (destinationUnavailable > 0 && destinationAccepted == 0)
            conclusion = "FAIL: the destination AIDirectorChunkData could not be resolved or did not change after NotifyActivity.";
        else if (destinationAccepted == 0 || destinationActivityDelta <= 0.0)
            conclusion = "FAIL: fire reached the heat path but ActivityLevel never increased.";
        else
            conclusion = "PASS: " + destinationAccepted + " fire heat event(s) increased destination ActivityLevel by " + destinationActivityDelta.ToString("F6") + ".";

        Log.Out("[RBHeat] conclusion=" + conclusion);
        Log.Out("[RBHeat] === END ===");
        RebirthFireTestRunner.NotifyToolbelt("Heat diagnostic complete. " + conclusion);
        if (finished != null) finished();
        UnityEngine.Object.Destroy(gameObject);
    }

    private static string CaptureDirectorNumbers(AIDirector director)
    {
        if (director == null) return "director=<null>";
        try
        {
            List<string> values = new List<string>();
            object[] targets = { director };
            for (int t = 0; t < targets.Length; t++)
            {
                object target = targets[t];
                Type type = target.GetType();
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length && values.Count < 24; i++)
                {
                    FieldInfo field = fields[i];
                    string n = field.Name.ToLowerInvariant();
                    if (n.IndexOf("heat") < 0 && n.IndexOf("activity") < 0 && n.IndexOf("chunk") < 0) continue;
                    object value = field.GetValue(target);
                    if (value == null || value is string) continue;
                    if (value.GetType().IsPrimitive || value is decimal)
                        values.Add(field.Name + "=" + value);
                }
            }
            return "type=" + director.GetType().FullName + " numeric={" + string.Join(",", values.ToArray()) + "}";
        }
        catch (Exception ex)
        {
            return "reflectionError=" + ex.GetType().Name + ":" + ex.Message;
        }
    }
}
#endif
