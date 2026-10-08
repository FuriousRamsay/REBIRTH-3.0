using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthSpawnFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }
    public override string[] getCommands() { return new[] { "rbspawn", "rbspawnfresh", "rbspawn2" }; }
    public override string getDescription() { return "REBIRTH spawn composition diagnostics, validation and non-spawning simulation."; }
    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbspawn status\n"
             + "  rbspawn validate\n"
             + "  rbspawn policy <requestedSleeperGroup>\n"
             + "  rbspawn poirisk\n"
             + "  rbspawn poisense\n"
             + "  rbspawn simulate <count> <biome|gamestage> <gs> <biome> [surface] [prefab] [seed]\n"
             + "  rbspawn explain <biome|gamestage> <gs> <biome> [surface] [group] [prefab] [seed]\n"
             + "  rbspawn entities [filter]\n"
             + "  rbspawn groups [filter]\n"
             + "  rbspawn rules\n"
             + "\nSurfaces: Biome, Sleeper, WanderingHorde, BloodMoon. Simulation never spawns entities.\n";
    }

    public override void Execute(List<string> p, CommandSenderInfo sender)
    {
        string cmd = p == null || p.Count == 0 ? "status" : (p[0] ?? string.Empty).Trim().ToLowerInvariant();
        switch (cmd)
        {
            case "help": case "?": Log.Out(getHelp()); return;
            case "status": case "summary":
                Log.Out(RebirthEntityClassRegistry.GetSummaryReport());
                Log.Out(RebirthSpawnRegistry.GetSummaryReport());
                Log.Out(RebirthSpawnCompositionService.GetStatus());
                return;
            case "validate": case "validation": Log.Out(RebirthSpawnCompositionService.GetValidationReport()); return;
            case "policy":
                if (p.Count < 2) { Log.Out(getHelp()); return; }
                Log.Out("[RebirthSpawn] group=" + p[1] + " policy=" + RebirthSpawnCompositionService.GetSleeperGroupPolicy(p[1]));
                return;
            case "poirisk": case "poi": case "risk":
                ExecutePoiRisk();
                return;
            case "poisense": case "sense":
                ExecutePoiSense();
                return;
            case "simulate": ExecuteSimulate(p); return;
            case "explain": ExecuteExplain(p); return;
            case "entities": case "entity": case "classes": case "class":
                Log.Out(RebirthEntityClassRegistry.GetReport(p.Count > 1 ? p[1] : null)); return;
            case "groups": case "group": case "spawns": case "spawn":
                Log.Out(RebirthSpawnRegistry.GetGroupsReport(p.Count > 1 ? p[1] : null)); return;
            case "rules": case "policyreport": case "policies": Log.Out(RebirthSpawnRegistry.GetRulesReport()); return;
            default: Log.Out("[RebirthSpawn] Unknown subcommand: " + cmd); Log.Out(getHelp()); return;
        }
    }

    private static void ExecutePoiRisk()
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null)
        {
            Log.Out("[RebirthPOIRisk] No active world.");
            return;
        }

        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null)
        {
            Log.Out("[RebirthPOIRisk] No local player.");
            return;
        }

        RebirthPoiRiskDisplaySnapshot snapshot;
        bool insidePoi = RebirthPoiRiskRuntimePolicy.TryGetDisplaySnapshot(player, out snapshot);
        if (!insidePoi)
        {
            Log.Out("[RebirthPOIRisk] outside qualifying POI"
                + " progression=" + RebirthSandboxOptionManager.Current.SpawnProgression
                + " configured=" + RebirthSandboxOptionManager.Current.PoiRisk
                + " effectiveMode=" + RebirthPoiRiskRuntimePolicy.Mode
                + " personalGS=" + Math.Max(1, player.gameStage));
            return;
        }

        Log.Out("[RebirthPOIRisk] prefab=" + snapshot.PrefabName
            + " tier=" + snapshot.PoiTier
            + " progression=" + snapshot.ProgressionMode
            + " configured=" + RebirthSandboxOptionManager.Current.PoiRisk
            + " effectiveMode=" + snapshot.Mode
            + " personalGS=" + snapshot.PersonalGameStage
            + " nativeSleeperGS=" + snapshot.NativeSleeperGameStage
            + " bonus=" + snapshot.Bonus
            + " effectiveSleeperGS=" + snapshot.EffectiveSleeperGameStage
            + " applied=" + snapshot.Applied);
    }

    private static void ExecutePoiSense()
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null)
        {
            Log.Out("[RebirthPOISense] No active world.");
            return;
        }

        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null)
        {
            Log.Out("[RebirthPOISense] No local player.");
            return;
        }

        Log.Out(RebirthPoiSenseRuntimePolicy.GetDiagnosticReport(GameManager.Instance.World, player));
    }

    private static void ExecuteSimulate(List<string> p)
    {
        if (p.Count < 5) { Log.Out("[RebirthSpawn] simulate requires count, mode, gamestage and biome."); return; }
        int count, gs, seed;
        if (!int.TryParse(p[1], out count) || count < 1 || count > 1000000 || !int.TryParse(p[3], out gs)) { Log.Out("[RebirthSpawn] Invalid count or gamestage."); return; }
        RebirthSpawnContext c = ParseContext(p[2], gs, p[4], p.Count > 5 ? p[5] : "Sleeper", null, p.Count > 6 ? p[6] : null);
        seed = p.Count > 7 && int.TryParse(p[7], out seed) ? seed : 1337;
        Log.Out(RebirthSpawnCompositionService.Simulate(count, c, seed));
    }

    private static void ExecuteExplain(List<string> p)
    {
        if (p.Count < 4) { Log.Out("[RebirthSpawn] explain requires mode, gamestage and biome."); return; }
        int gs, seed;
        if (!int.TryParse(p[2], out gs)) { Log.Out("[RebirthSpawn] Invalid gamestage."); return; }
        RebirthSpawnContext c = ParseContext(p[1], gs, p[3], p.Count > 4 ? p[4] : "Sleeper", p.Count > 5 ? p[5] : "sleeperHordeStage", p.Count > 6 ? p[6] : null);
        seed = p.Count > 7 && int.TryParse(p[7], out seed) ? seed : 1337;
        System.Random r = new System.Random(seed); RebirthSpawnTrace trace;
        bool selected = RebirthSpawnCompositionService.TrySelectDetailed(c, r.NextDouble, out trace);
        Log.Out("[RebirthSpawn] selected=" + selected + "\n" + trace.Text);
    }

    private static RebirthSpawnContext ParseContext(string mode, int gs, string biome, string surface, string group, string prefab)
    {
        RebirthSpawnProgressionMode m; if (!Enum.TryParse(mode, true, out m)) m = RebirthSpawnProgressionMode.Gamestage;
        RebirthSpawnSurface s; if (!Enum.TryParse(surface, true, out s)) s = RebirthSpawnSurface.Sleeper;
        return new RebirthSpawnContext { ProgressionMode=m, GameStage=gs, Biome=biome, Surface=s, RequestedGroup=group, PrefabName=prefab, HistoryKey=null };
    }
}
