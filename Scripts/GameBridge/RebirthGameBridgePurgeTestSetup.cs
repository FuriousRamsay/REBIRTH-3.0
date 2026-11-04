using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;

// Explicit disposable-save preparation. Never grants kills, clears or supply credits.
public sealed class ConsoleCmdRebirthPurgeTestSetup : ConsoleCmdAbstract
{
    private static World owner;
    private static bool needs, agenda, explore, heal, supply, glance;
    private static bool DisposableSave()
    {
        string save=GamePrefs.GetString(EnumGamePrefs.GameName);
        return save=="CodexTest"||save=="CodexTestPurgeRegression20261102"||save=="CodexTestPurgeFresh20261102";
    }
    internal static bool SuppressHostileAnimals(World world) => ReferenceEquals(owner, world)
        && world != null && !world.IsRemote() && RebirthGameBridge.IsRunning
        && DisposableSave();
    public override string[] getCommands() => new[] { "rbpurgetestsetup" };
    public override string getDescription() => "Configure the disposable Purge test environment.";
    public override string getHelp() => "rbpurgetestsetup animals|on|off|status (authorized disposable Purge test saves only; animals preserves guard/heal/reload).";
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        if (sender.RemoteClientInfo != null || world == null || world.IsRemote()
            || !RebirthGameBridge.IsRunning || !DisposableSave()
            || args.Count != 1)
        { SdtdConsole.Instance.Output("*** ERROR requires local disposable Purge Bridge and animals|on|off|status."); return; }
        if (args[0] == "on"||args[0]=="animals")
        {
            if (owner != null && !ReferenceEquals(owner, world))
            { SdtdConsole.Instance.Output("*** ERROR restore the previous setup before changing worlds."); return; }
            if (owner == null)
            {
                needs = RebirthGameBridgeNeeds.Enabled; agenda = RebirthGameBridgeAgenda.Enabled;
                explore = RebirthGameBridgeWorld.ExploreEnabled; heal = RebirthGameBridgeCombat.InstinctHeal;
                supply = RebirthGameBridgeCombat.InstinctSupply; glance = RebirthGameBridgeCombat.GlanceWhenIdle;
                RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.bridge.purge.testsetup"), typeof(AnimalFilter));
                owner = world;
            }
            if(args[0]=="on")
            {
                RebirthGameBridgeNeeds.Enabled = false; RebirthGameBridgeAgenda.Enabled = false;
                RebirthGameBridgeWorld.ExploreEnabled = false; RebirthGameBridgeCombat.InstinctHeal = false;
                RebirthGameBridgeCombat.InstinctSupply = false; RebirthGameBridgeCombat.GlanceWhenIdle = false;
            }
        }
        else if (args[0] == "off")
        {
            if (owner != null)
            {
                RebirthGameBridgeNeeds.Enabled = needs; RebirthGameBridgeAgenda.Enabled = agenda;
                RebirthGameBridgeWorld.ExploreEnabled = explore; RebirthGameBridgeCombat.InstinctHeal = heal;
                RebirthGameBridgeCombat.InstinctSupply = supply; RebirthGameBridgeCombat.GlanceWhenIdle = glance;
                owner = null;
            }
        }
        else if (args[0] != "status")
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        SdtdConsole.Instance.Output(new JObject {
            ["fixtureOnly"] = true, ["hostileAmbientAnimalsSuppressed"] = SuppressHostileAnimals(world),
            ["needs"] = RebirthGameBridgeNeeds.Enabled, ["agenda"] = RebirthGameBridgeAgenda.Enabled,
            ["heal"] = RebirthGameBridgeCombat.InstinctHeal, ["supply"] = RebirthGameBridgeCombat.InstinctSupply,
            ["combatGuard"] = RebirthGameBridgeCombat.GuardEnabled,
            ["note"] = "Existing animals and authored POI spawns are unchanged; no progression is awarded."
        }.ToString(Newtonsoft.Json.Formatting.None));
    }
    [HarmonyPatch(typeof(RebirthPurgeBiomeZombieSpawnPatch), nameof(RebirthPurgeBiomeZombieSpawnPatch.ShouldSuppress))]
    private static class AnimalFilter
    {
        private static void Postfix(int entityClassId, SpawnManagerBiomes manager, ref bool __result)
        {
            if (__result || !SuppressHostileAnimals(manager?.world)) return;
            var definition = EntityClass.GetEntityClass(entityClassId);
            __result = definition != null && definition.bIsAnimalEntity && definition.classname != null
                && typeof(EntityEnemyAnimal).IsAssignableFrom(definition.classname);
        }
    }
}
