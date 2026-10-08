using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBlockPickupFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpickup", "rbblockpickup" };
    }

    public override string getDescription()
    {
        return "Reports REBIRTH block-pickup eligibility for the block under the crosshair.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpickup\n"
             + "  rbpickup status\n"
             + "  rbpickup clearcache\n"
             + "  rbpickup lagtest [on|off]\n"
             + "\nlagtest off schedules one clean disabled startup and leaves the current session unchanged.\n"
             + "Restart once to test. The flag is consumed automatically; restart again to restore.\n"
             + "lagtest on cancels a scheduled test. Legacy persistent disable flags are cleared automatically.\n"
             + "Punch a block with empty hands to log the same report automatically.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string sub = parameters != null && parameters.Count > 0
            ? (parameters[0] ?? string.Empty).Trim().ToLowerInvariant()
            : string.Empty;

        if (sub == "help" || sub == "?")
        {
            Log.Out(getHelp());
            return;
        }

        if (sub == "status")
        {
            Log.Out(RebirthBlockPickupPatchInstaller.Status());
            return;
        }

        if (sub == "clearcache")
        {
            RebirthBlockPickupClassifier.ClearCache();
            RebirthBlockPickupService.ClearRuntimeCaches();
            Log.Out("[REBIRTH BlockPickup] classifier and command caches cleared.");
            return;
        }

        if (sub == "lagtest")
        {
            string mode = parameters != null && parameters.Count > 1
                ? (parameters[1] ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;
            bool disable;
            if (mode == "off" || mode == "disable" || mode == "disabled")
                disable = true;
            else if (mode == "on" || mode == "enable" || mode == "enabled")
                disable = false;
            else
                disable = !RebirthBlockPickupPatchInstaller.LagTestDisabled;

            Log.Out(RebirthBlockPickupPatchInstaller.SetLagTestDisabled(disable));
            if (disable)
            {
                Log.Out("[REBIRTH BlockPickup] One disabled startup is scheduled. The current session remains unchanged. Restart to test; restart once more afterward to restore automatically.");
            }
            else if (RebirthBlockPickupPatchInstaller.RestartRequired)
            {
                Log.Out("[REBIRTH BlockPickup] Scheduled lag test cleared. Restart is required because this process started without the early Block Pickup definition pass.");
            }
            else
            {
                Log.Out("[REBIRTH BlockPickup] No lag-test disable is scheduled and Block Pickup is active.");
            }
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        WorldRayHitInfo hit = Voxel.voxelRayHitInfo;
        if (world == null || player == null || hit == null || !hit.bHitValid || !GameUtils.IsBlockOrTerrain(hit.tag))
        {
            Log.Out("[REBIRTH BlockPickup] Aim at a block and run rbpickup.");
            return;
        }

        Vector3i pos = hit.hit.blockPos;
        BlockValue value = world.GetBlock(pos);
        if (value.ischild && value.Block != null && value.Block.multiBlockPos != null)
        {
            pos = value.Block.multiBlockPos.GetParentPos(pos, value);
            value = world.GetBlock(pos);
        }

        Log.Out(RebirthBlockPickupService.DescribeBlock(value, pos, world));
    }
}
