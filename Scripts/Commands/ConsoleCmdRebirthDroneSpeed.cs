using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthDroneSpeed : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "rbdrone", "rbdronequality" };
    }

    public override string getDescription()
    {
        return "Reports/overrides REBIRTH drone Follow travel scaling by six quality-speed tiers and records live motion traces.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbdrone speed\n"
             + "  rbdrone speed <tier 1-6> [entityId]\n"
             + "  rbdrone speed auto [entityId]\n"
             + "  rbdrone speed multiplier <1.0-2.0> [entityId]\n"
             + "  rbdrone status [entityId]\n"
             + "  rbdrone telemetry on|off [seconds 1-60]\n"
             + "  rbdrone trace start [seconds] [hz] [entityId]\n"
             + "  rbdrone trace status [entityId]\n"
             + "  rbdrone trace stop [entityId]\n"
             + "\n"
             + "Speed tiers intentionally match the six drone quality levels:\n"
             + "  1 = 1.00x, 2 = 1.20x, 3 = 1.40x, 4 = 1.60x, 5 = 1.80x, 6 = 2.00x\n"
             + "\n"
             + "Examples:\n"
             + "  rbdrone speed 1 1202\n"
             + "  rbdrone trace start 10 10 1202\n"
             + "  rbdrone speed 6 1202\n"
             + "  rbdrone trace start 10 10 1202\n"
             + "  rbdrone speed auto 1202\n"
             + "  rbdrone status 1202\n"
             + "\n"
             + "Trace is diagnostic only: it never moves or retargets the drone.\n"
             + "It logs final position, follow target/distance, motion, velocity, measured speed,\n"
             + "toward/away speed, native move request, native/extra movement steps, path state,\n"
             + "network interpolation state, and a reversal/oscillation summary.\n"
             + "Tier overrides are temporary and clear on world change. 'auto' restores the drone's real quality scaling.\n"
             + "The explicit 'multiplier' form is available for intermediate 1.0-2.0x diagnostics only.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string command = parameters == null || parameters.Count == 0
            ? "speed"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        if (command == "help" || command == "?")
        {
            Log.Out(getHelp());
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[RebirthDroneSpeed] No active world.");
            return;
        }

        EntityPlayer player = ResolveSenderPlayer(world, senderInfo);
        if (player == null)
        {
            Log.Out("[RebirthDroneSpeed] Could not resolve the issuing player.");
            return;
        }

        if (command == "telemetry")
        {
            bool on = parameters.Count > 1 && string.Equals(parameters[1], "on", StringComparison.OrdinalIgnoreCase);
            bool off = parameters.Count > 1 && string.Equals(parameters[1], "off", StringComparison.OrdinalIgnoreCase);
            if (!on && !off) { Log.Out("Usage: rbdrone telemetry on|off [seconds 1-60]"); return; }
            float seconds = 30f;
            if (parameters.Count > 2 && (!float.TryParse(parameters[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || float.IsNaN(seconds) || float.IsInfinity(seconds)))
            { Log.Out("Telemetry duration must be a finite number."); return; }
            if (on) RebirthDroneQualitySpeed.StartTelemetry(seconds);
            else RebirthDroneQualitySpeed.StopTelemetry();
            Log.Out("[RebirthDroneSpeed] TelemetryActive=" + RebirthDroneQualitySpeed.TelemetryActive);
            return;
        }

        if (command == "trace")
        {
            ExecuteTrace(parameters, world, player);
            return;
        }

        if (command == "speed")
        {
            ExecuteSpeed(parameters, world, player);
            return;
        }

        if (command == "status" || command == "quality")
        {
            int requestedEntityId = ParseEntityId(parameters, 1);
            EntityDrone drone = ResolveDrone(world, player, requestedEntityId);
            if (drone == null)
            {
                LogNoDrone(requestedEntityId);
                return;
            }
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(drone));
            return;
        }

        Log.Out("[RebirthDroneSpeed] Unknown subcommand: " + command);
        Log.Out(getHelp());
    }

    private static void ExecuteSpeed(List<string> parameters, World world, EntityPlayer player)
    {
        if (parameters == null || parameters.Count < 2)
        {
            EntityDrone statusDrone = ResolveDrone(world, player, -1);
            if (statusDrone == null)
            {
                LogNoDrone(-1);
                return;
            }
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(statusDrone));
            return;
        }

        string value = (parameters[1] ?? string.Empty).Trim().ToLowerInvariant();

        if (value == "auto" || value == "quality" || value == "reset" || value == "off")
        {
            int requestedEntityId = ParseEntityId(parameters, 2);
            EntityDrone drone = ResolveDrone(world, player, requestedEntityId);
            if (drone == null)
            {
                LogNoDrone(requestedEntityId);
                return;
            }

            RebirthDroneQualitySpeed.ClearDebugOverride(drone.entityId);
            Log.Out("[RebirthDroneSpeed] Entity " + drone.entityId + " restored to automatic quality scaling.");
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(drone));
            return;
        }

        if (value == "status" || value == "report")
        {
            int requestedEntityId = ParseEntityId(parameters, 2);
            EntityDrone drone = ResolveDrone(world, player, requestedEntityId);
            if (drone == null)
            {
                LogNoDrone(requestedEntityId);
                return;
            }
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(drone));
            return;
        }

        if (value == "mult" || value == "multiplier" || value == "x")
        {
            if (parameters.Count < 3)
            {
                Log.Out("[RebirthDroneSpeed] Usage: rbdrone speed multiplier <1.0-2.0> [entityId]");
                return;
            }

            float multiplier;
            if (!float.TryParse(parameters[2], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier) ||
                float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 1f || multiplier > 2f)
            {
                Log.Out("[RebirthDroneSpeed] Multiplier must be between 1.0 and 2.0.");
                return;
            }

            int requestedEntityId = ParseEntityId(parameters, 3);
            EntityDrone drone = ResolveDrone(world, player, requestedEntityId);
            if (drone == null)
            {
                LogNoDrone(requestedEntityId);
                return;
            }

            RebirthDroneQualitySpeed.SetDebugOverride(drone.entityId, multiplier);
            Log.Out("[RebirthDroneSpeed] Forced entity " + drone.entityId + " to raw " +
                    multiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                    "x Follow travel scaling for this world session.");
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(drone));
            return;
        }

        int tier;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out tier) || tier < 1 || tier > 6)
        {
            Log.Out("[RebirthDroneSpeed] Speed tier must be an integer from 1 to 6, or use 'auto'.");
            Log.Out("[RebirthDroneSpeed] Tier curve: 1=1.00x 2=1.20x 3=1.40x 4=1.60x 5=1.80x 6=2.00x.");
            return;
        }

        int entityId = ParseEntityId(parameters, 2);
        EntityDrone selected = ResolveDrone(world, player, entityId);
        if (selected == null)
        {
            LogNoDrone(entityId);
            return;
        }

        float tierMultiplier = TierToMultiplier(tier);
        RebirthDroneQualitySpeed.SetDebugOverride(selected.entityId, tierMultiplier);
        Log.Out("[RebirthDroneSpeed] Forced entity " + selected.entityId +
                " to speed tier " + tier + "/6 (" +
                tierMultiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                "x Follow travel scaling) for this world session.");
        Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(selected));
    }

    private static float TierToMultiplier(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 6);
        return 1f + (tier - 1) * 0.2f;
    }

    private static int ParseEntityId(List<string> parameters, int index)
    {
        if (parameters == null || parameters.Count <= index)
            return -1;

        int entityId;
        return int.TryParse(parameters[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId)
            ? entityId
            : -1;
    }

    private static void ExecuteTrace(List<string> parameters, World world, EntityPlayer player)
    {
        string action = parameters != null && parameters.Count >= 2
            ? (parameters[1] ?? string.Empty).Trim().ToLowerInvariant()
            : "status";

        if (action == "start" || action == "on")
        {
            float seconds = 10f;
            int hz = 10;
            int entityId = -1;
            if (parameters.Count >= 3 && (!float.TryParse(parameters[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || float.IsNaN(seconds) || float.IsInfinity(seconds)))
            {
                Log.Out("[RebirthDroneTrace] Seconds must be a number from 1 to 60.");
                return;
            }
            if (parameters.Count >= 4 && !int.TryParse(parameters[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out hz))
            {
                Log.Out("[RebirthDroneTrace] Hz must be an integer from 2 to 30.");
                return;
            }
            if (parameters.Count >= 5)
                int.TryParse(parameters[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId);

            if (seconds < 1f || seconds > 60f || hz < 2 || hz > 30)
            {
                Log.Out("[RebirthDroneTrace] Valid range: 1-60 seconds and 2-30 Hz.");
                return;
            }

            EntityDrone drone = ResolveDrone(world, player, entityId);
            if (drone == null)
            {
                LogNoDrone(entityId);
                return;
            }
            RebirthDroneMotionTrace.Start(drone, seconds, hz);
            return;
        }

        int requestedId = -1;
        if (parameters != null && parameters.Count >= 3)
            int.TryParse(parameters[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out requestedId);
        EntityDrone selected = ResolveDrone(world, player, requestedId);
        if (selected == null)
        {
            LogNoDrone(requestedId);
            return;
        }

        if (action == "stop" || action == "off")
        {
            if (!RebirthDroneMotionTrace.Stop(selected.entityId))
                Log.Out("[RebirthDroneTrace] Entity=" + selected.entityId + " trace was not running.");
            return;
        }
        if (action == "status")
        {
            Log.Out(RebirthDroneMotionTrace.GetStatus(selected));
            Log.Out(RebirthDroneQualitySpeed.GetDiagnosticReport(selected));
            return;
        }

        Log.Out("[RebirthDroneTrace] Unknown trace action: " + action);
        Log.Out("Use: rbdrone trace start [seconds] [hz] [entityId] | status [entityId] | stop [entityId]");
    }

    private static EntityDrone ResolveDrone(World world, EntityPlayer player, int requestedEntityId)
    {
        return requestedEntityId >= 0
            ? FindOwnedLoadedDroneById(world, player, requestedEntityId)
            : FindNearestOwnedLoadedDrone(world, player);
    }

    private static void LogNoDrone(int requestedEntityId)
    {
        Log.Out(requestedEntityId >= 0
            ? "[RebirthDroneSpeed] Entity " + requestedEntityId + " is not a loaded drone owned by the issuing player."
            : "[RebirthDroneSpeed] No loaded owned drone found.");
    }

    private static EntityPlayer ResolveSenderPlayer(World world, CommandSenderInfo senderInfo)
    {
        if (world == null) return null;
        if (senderInfo.RemoteClientInfo != null && senderInfo.RemoteClientInfo.entityId >= 0)
        {
            EntityPlayer remote = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
            if (remote != null) return remote;
        }
        return world.GetPrimaryPlayer();
    }

    private static EntityDrone FindOwnedLoadedDroneById(World world, EntityPlayer player, int entityId)
    {
        EntityDrone drone = world != null ? world.GetEntity(entityId) as EntityDrone : null;
        if (drone == null || player == null || drone.IsDead() || drone.belongsPlayerId != player.entityId)
            return null;
        return drone;
    }

    private static EntityDrone FindNearestOwnedLoadedDrone(World world, EntityPlayer player)
    {
        List<OwnedEntityData> owned = player.GetOwnedEntities(EntityClass.junkDroneClass);
        if (owned == null) return null;

        EntityDrone best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < owned.Count; i++)
        {
            EntityDrone drone = world.GetEntity(owned[i].Id) as EntityDrone;
            if (drone == null || drone.IsDead() || drone.belongsPlayerId != player.entityId)
                continue;
            float sqr = (drone.position - player.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                best = drone;
                bestSqr = sqr;
            }
        }
        return best;
    }
}
