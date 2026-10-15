#if DEBUG
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// One-command client/server Advanced Farming growth diagnostic.
/// Run from a connected client while standing near the affected crops.
/// </summary>
[Preserve]
public class ConsoleCmdRebirthCropGrowthTestFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbcropgrowthtest", "rbgrowthtest", "rbcroptest" };
    }

    public override string getDescription()
    {
        return "Runs a paired client/dedicated-server crop growth and hover diagnostic.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbcropgrowthtest\n"
             + "  rbcropgrowthtest [radius] [durationSeconds] [intervalSeconds]\n"
             + "\n"
             + "Default: radius 8, duration 60 seconds, sample interval 10 seconds.\n"
             + "Stand near the affected crops, run the command once, remain nearby until the completion message, then send both logs.\n"
             + "The diagnostic is read-only and does not advance, reset, water, or reschedule crops.\n";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null)
        {
            Log.Out("[AdvancedFarming GrowthTest] Cannot start: no local world/player. Run this from a connected game client.");
            return;
        }

        int radius = AdvancedFarmingCropGrowthDiagnosticService.DefaultRadius;
        int durationSeconds = AdvancedFarmingCropGrowthDiagnosticService.DefaultDurationSeconds;
        int intervalSeconds = AdvancedFarmingCropGrowthDiagnosticService.DefaultIntervalSeconds;

        if (_params != null && _params.Count > 0 && !int.TryParse(_params[0], out radius))
        {
            Log.Out(getHelp());
            return;
        }
        if (_params != null && _params.Count > 1 && !int.TryParse(_params[1], out durationSeconds))
        {
            Log.Out(getHelp());
            return;
        }
        if (_params != null && _params.Count > 2 && !int.TryParse(_params[2], out intervalSeconds))
        {
            Log.Out(getHelp());
            return;
        }

        radius = AdvancedFarmingCropGrowthDiagnosticService.ClampRadius(radius);
        durationSeconds = AdvancedFarmingCropGrowthDiagnosticService.ClampDurationSeconds(durationSeconds);
        intervalSeconds = AdvancedFarmingCropGrowthDiagnosticService.ClampIntervalSeconds(intervalSeconds);

        Vector3i center = player.GetBlockPosition();
        Vector3i[] positions = AdvancedFarmingCropGrowthDiagnosticService.FindNearbyImmatureCrops(world, center, radius);
        if (positions.Length == 0)
        {
            Log.Out("[AdvancedFarming GrowthTest] No immature Advanced Farming crops found near " + center + " within radius " + radius + ".");
            return;
        }

        string sessionId = AdvancedFarmingCropGrowthDiagnosticService.BuildSessionId(player.entityId);
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool isServerProcess = connection != null && connection.IsServer;
        string localRole = world.IsRemote() ? "CLIENT" : "HOST";

        Log.Out(AdvancedFarmingCropGrowthDiagnosticService.Start(
            world,
            sessionId,
            localRole,
            player.entityId,
            center,
            radius,
            durationSeconds,
            intervalSeconds,
            positions));

        if (connection == null)
        {
            Log.Out("[AdvancedFarming GrowthTest] Server request not sent: ConnectionManager is unavailable.");
            return;
        }

        if (isServerProcess)
        {
            if (localRole != "SERVER")
            {
                Log.Out(AdvancedFarmingCropGrowthDiagnosticService.Start(
                    world,
                    sessionId,
                    "SERVER",
                    player.entityId,
                    center,
                    radius,
                    durationSeconds,
                    intervalSeconds,
                    positions));
            }
            return;
        }

        connection.SendToServer(
            NetPackageManager.GetPackage<NetPackageRequestCropGrowthDiagnosticRebirth>().Setup(
                player.entityId,
                sessionId,
                center,
                radius,
                durationSeconds,
                intervalSeconds,
                positions));

        Log.Out("[AdvancedFarming GrowthTest] serverRequestSent=true session=" + sessionId + " cropCount=" + positions.Length);
    }
}
#endif
