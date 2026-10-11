using System;
using System.IO;
using System.Xml;

#nullable disable

/// <summary>
/// Central INFO-level logging gates for REBIRTH automatic runtime and startup traces.
///
/// Config/_Survivor/debug.xml is read directly from the mod folder on first use so the gates
/// work before a world exists and before the Survivor registry has finished installing.
/// Warnings and errors are never routed through this class and are therefore never suppressed.
/// </summary>
public static class RebirthLogSettings
{
    private static readonly object Sync = new object();
    private static bool loaded;
    private static bool traitStartupAnnounced;
    private static bool characterProgressionStartupAnnounced;
    private static bool preSpawnStartupAnnounced;
    private static bool spawnFlowStartupAnnounced;
    private static string configPath = string.Empty;

    private static bool configuredTraitUi;
    private static bool traitUi;
    private static bool characterProgressionUi;
    private static bool harmonyPatch;
    private static bool runtimeInstall;
    private static bool uiRoute;
    private static bool playerProfileBridge;
    private static bool playerProfilePortrait;
    private static bool exitTrace;
    private static bool progressionExplorer;
    private static bool preSpawn;
    private static bool spawnFlow;
    private static bool skillKnowledge;
    private static bool literature;
    private static bool craftingUi;
    private static bool craftAdmission;
    private static bool blockPickup;
    private static bool heatMap;
    private static bool automaticLoggingDefault;

    // Routine runtime/load/save success messages use this existing switch directly.
    // Explicit category attributes override the fallback for their own traces only.
    // Compiler DEBUG/TRACE symbols and the game's debug mode never enable these gates.
    public static bool AutomaticLoggingDefault { get { EnsureLoaded(); return automaticLoggingDefault; } }
    public static bool BlockPickupLoggingEnabled { get { EnsureLoaded(); return blockPickup; } }
    public static bool HeatMapLoggingEnabled { get { EnsureLoaded(); return heatMap; } }
    public static string ConfigPath { get { EnsureLoaded(); return configPath; } }
    public static bool TraitUiLoggingEnabled { get { EnsureLoaded(); return traitUi; } }
    public static bool TraitUiLoggingConfigured { get { EnsureLoaded(); return configuredTraitUi; } }
    public static bool CharacterProgressionUiLoggingEnabled { get { EnsureLoaded(); return characterProgressionUi; } }
    public static bool HarmonyPatchLoggingEnabled { get { EnsureLoaded(); return harmonyPatch; } }
    public static bool RuntimeInstallLoggingEnabled { get { EnsureLoaded(); return runtimeInstall; } }
    public static bool UiRouteLoggingEnabled { get { EnsureLoaded(); return uiRoute; } }
    public static bool PlayerProfileBridgeLoggingEnabled { get { EnsureLoaded(); return playerProfileBridge; } }
    public static bool PlayerProfilePortraitLoggingEnabled { get { EnsureLoaded(); return playerProfilePortrait; } }
    public static bool ExitTraceLoggingEnabled { get { EnsureLoaded(); return exitTrace; } }
    public static bool ProgressionExplorerLoggingEnabled { get { EnsureLoaded(); return progressionExplorer; } }
    public static bool PreSpawnLoggingEnabled { get { EnsureLoaded(); return preSpawn; } }
    public static bool SpawnFlowLoggingEnabled { get { EnsureLoaded(); return spawnFlow; } }
    public static bool SkillKnowledgeLoggingEnabled { get { EnsureLoaded(); return skillKnowledge; } }
    public static bool LiteratureLoggingEnabled { get { EnsureLoaded(); return literature; } }
    public static bool CraftAdmissionLoggingEnabled { get { EnsureLoaded(); return craftAdmission; } }
    public static bool CraftingUiLoggingEnabled { get { EnsureLoaded(); return craftingUi; } }

    public static void LoadFromConfigRoot(string configRoot)
    {
        string path = Path.Combine(configRoot ?? string.Empty, "debug.xml");
        Load(path);
    }

    public static void SetTraitUiRuntimeOverride(bool value)
    {
        EnsureLoaded();
        traitUi = value;
        Log.Out("[REBIRTH Survivor][TraitUI] logging=" + traitUi + " source=runtime-override");
    }

    public static void RestoreConfiguredTraitUi()
    {
        EnsureLoaded();
        traitUi = configuredTraitUi;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        lock (Sync)
        {
            if (loaded) return;
            string path = string.Empty;
            try
            {
                string assemblyPath = typeof(RebirthLogSettings).Assembly.Location;
                string modRoot = !string.IsNullOrEmpty(assemblyPath) ? Path.GetDirectoryName(assemblyPath) : string.Empty;
                path = Path.Combine(modRoot ?? string.Empty, "Config", "_Survivor", "debug.xml");
            }
            catch { }
            Load(path);
        }
    }

    private static void Load(string path)
    {
        bool automaticDefault = false;
        bool nextTraitUi = false;
        bool nextCharacterProgressionUi = false;
        bool nextHarmony = false;
        bool nextRuntimeInstall = false;
        bool nextUiRoute = false;
        bool nextProfileBridge = false;
        bool nextProfilePortrait = false;
        bool nextExitTrace = false;
        bool nextProgressionExplorer = false;
        bool nextPreSpawn = false;
        bool nextSpawnFlow = false;
        bool nextSkillKnowledge = false;
        bool nextLiterature = false;
        bool nextCraftingUi = false;
        bool nextCraftAdmission = false;
        bool nextBlockPickup = false;
        bool nextHeatMap = false;

        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlElement root = doc.DocumentElement;
                if (root != null)
                {
                    automaticDefault = ReadBool(root, "automatic_logging", false);
                    nextTraitUi = ReadBool(root, "trait_ui_logging", false);
                    nextCharacterProgressionUi = ReadBool(root, "character_progression_ui_logging", false);
                    nextHarmony = ReadBool(root, "harmony_patch_logging", automaticDefault);
                    nextRuntimeInstall = ReadBool(root, "runtime_install_logging", automaticDefault);
                    nextUiRoute = ReadBool(root, "ui_route_logging", automaticDefault);
                    nextProfileBridge = ReadBool(root, "player_profile_bridge_logging", automaticDefault);
                    nextProfilePortrait = ReadBool(root, "player_profile_portrait_logging", automaticDefault);
                    nextExitTrace = ReadBool(root, "exit_trace_logging", automaticDefault);
                    nextProgressionExplorer = ReadBool(root, "progression_explorer_logging", automaticDefault);
                    nextPreSpawn = ReadBool(root, "pre_spawn_logging", automaticDefault);
                    nextSpawnFlow = ReadBool(root, "spawn_flow_logging", automaticDefault);
                    nextSkillKnowledge = ReadBool(root, "skill_knowledge_logging", automaticDefault);
                    nextLiterature = ReadBool(root, "literature_logging", automaticDefault);
                    nextCraftAdmission = ReadBool(root, "craft_admission_logging", false);
                    nextCraftingUi = ReadBool(root, "crafting_ui_logging", automaticDefault);
                    nextBlockPickup = ReadBool(root, "block_pickup_logging", automaticDefault);
                    nextHeatMap = ReadBool(root, "heat_map_logging", automaticDefault);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][DebugConfig] failed to read Main Menu debug config: " + ex.GetType().Name + ": " + ex.Message);
        }

        lock (Sync)
        {
            configPath = path ?? string.Empty;
            configuredTraitUi = nextTraitUi;
            traitUi = nextTraitUi;
            characterProgressionUi = nextCharacterProgressionUi;
            harmonyPatch = nextHarmony;
            runtimeInstall = nextRuntimeInstall;
            uiRoute = nextUiRoute;
            playerProfileBridge = nextProfileBridge;
            playerProfilePortrait = nextProfilePortrait;
            exitTrace = nextExitTrace;
            progressionExplorer = nextProgressionExplorer;
            preSpawn = nextPreSpawn;
            spawnFlow = nextSpawnFlow;
            skillKnowledge = nextSkillKnowledge;
            literature = nextLiterature;
            craftingUi = nextCraftingUi;
            craftAdmission = nextCraftAdmission;
            blockPickup = nextBlockPickup;
            heatMap = nextHeatMap;
            automaticLoggingDefault = automaticDefault;
            loaded = true;
        }

        // Trait tracing is temporary and must visibly confirm itself when enabled. Other disabled
        // automatic categories deliberately produce no startup chatter.
        if (nextTraitUi && !traitStartupAnnounced)
        {
            traitStartupAnnounced = true;
            Log.Out("[REBIRTH Survivor][TraitUI] startup logging=" + nextTraitUi +
                " source=config path='" + (path ?? string.Empty) + "'");
        }

        if (nextCharacterProgressionUi && !characterProgressionStartupAnnounced)
        {
            characterProgressionStartupAnnounced = true;
            Log.Out("[REBIRTH Options][CharacterProgressionUI] startup logging=True" +
                " source=config path='" + (path ?? string.Empty) + "'");
        }

        if (nextPreSpawn && !preSpawnStartupAnnounced)
        {
            preSpawnStartupAnnounced = true;
            Log.Out("[REBIRTH Survivor][PreSpawnTrace] startup logging=True source=config path='" + (path ?? string.Empty) + "'");
        }

        if (nextSpawnFlow && !spawnFlowStartupAnnounced)
        {
            spawnFlowStartupAnnounced = true;
            Log.Out("[REBIRTH Survivor][SpawnFlow] startup logging=True source=config path='" + (path ?? string.Empty) + "'");
        }

        if (nextCraftingUi)
            Log.Out("[REBIRTH Crafting Trace] startup logging=True source=config path='" + (path ?? string.Empty) + "'");
    }

    private static bool ReadBool(XmlElement root, string attribute, bool fallback)
    {
        if (root == null || string.IsNullOrEmpty(attribute)) return fallback;
        string raw = root.GetAttribute(attribute);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        raw = raw.Trim();
        bool parsed;
        if (bool.TryParse(raw, out parsed)) return parsed;
        if (raw == "1" || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)) return true;
        if (raw == "0" || string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase)) return false;
        return fallback;
    }

    public static void TraceHarmonyPatch(string message)
    {
        if (HarmonyPatchLoggingEnabled) Log.Out("[REBIRTH Harmony][PatchClass] " + (message ?? string.Empty));
    }

    public static void TraceRuntimeInstall(string message)
    {
        if (RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor][RuntimeInstall] " + (message ?? string.Empty));
    }

    public static void TraceUiRoute(string message)
    {
        if (UiRouteLoggingEnabled) Log.Out("[REBIRTH Survivor][UiRoute] " + (message ?? string.Empty));
    }

    public static void TracePlayerProfileBridge(string message)
    {
        if (PlayerProfileBridgeLoggingEnabled) Log.Out("[REBIRTH Survivor][PlayerProfileBridge] " + (message ?? string.Empty));
    }

    public static void TracePlayerProfilePortrait(string message)
    {
        if (PlayerProfilePortraitLoggingEnabled) Log.Out("[REBIRTH Survivor][PlayerProfilePortrait] " + (message ?? string.Empty));
    }

    public static void TraceExit(string message)
    {
        if (ExitTraceLoggingEnabled) Log.Out("[REBIRTH Survivor][ExitTrace] " + (message ?? string.Empty));
    }

    public static void TraceProgressionExplorer(string message)
    {
        if (ProgressionExplorerLoggingEnabled) Log.Out("[REBIRTH Progression Explorer][PE-06] " + (message ?? string.Empty));
    }

    public static void TraceSkillKnowledge(string message)
    {
        if (SkillKnowledgeLoggingEnabled) Log.Out("[REBIRTH Survivor][SkillKnowledge] " + (message ?? string.Empty));
    }

    public static void TraceCharacterProgression(string message)
    {
        if (CharacterProgressionUiLoggingEnabled)
            Log.Out("[REBIRTH Options][CharacterProgressionUI] " + (message ?? string.Empty));
    }

    public static void TracePreSpawn(string message)
    {
        if (PreSpawnLoggingEnabled)
            Log.Out("[REBIRTH Survivor][PreSpawnTrace] " + (message ?? string.Empty));
    }

    public static void TraceLiterature(string message)
    {
        if (!LiteratureLoggingEnabled) return;
        Log.Out("[REBIRTH Survivor][Literature] " + (message ?? string.Empty));
    }

    public static void TraceSpawnFlow(string message)
    {
        if (SpawnFlowLoggingEnabled)
            Log.Out("[REBIRTH Survivor][SpawnFlow] " + (message ?? string.Empty));
    }
}
