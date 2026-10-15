#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public static class RebirthVehicleRespawnDebugPolicy
{
    public static bool ForceOneTick;
    public static int ForcedCompositeDowngrades;
    public static string LastForcedCompositeBlock = string.Empty;
    public static string LastRespawnedBlock = string.Empty;
    public static string LastPreviousDowngrade = string.Empty;
    public static bool LastSpawnRepeatable;
    public static int SuppressedPassThroughHits;
    public static int LastSuppressedPassThroughDamage;
    public static int LastSuppressedPassThroughDepth;
    public static Vector3i LastSuppressedPassThroughPosition;

    public static void RecordRespawnedBlock(
        string blockName,
        string previousDowngrade,
        bool repeatable)
    {
        LastRespawnedBlock = blockName ?? string.Empty;
        LastPreviousDowngrade = previousDowngrade ?? string.Empty;
        LastSpawnRepeatable = repeatable;
    }

    public static void RecordForcedCompositeDowngrade(string blockName)
    {
        ForcedCompositeDowngrades++;
        LastForcedCompositeBlock = blockName ?? string.Empty;
    }

    public static void RecordSuppressedPassThroughDamage(
        Vector3i position,
        int damage,
        int recursionDepth)
    {
        SuppressedPassThroughHits++;
        LastSuppressedPassThroughDamage = damage;
        LastSuppressedPassThroughDepth = recursionDepth;
        LastSuppressedPassThroughPosition = position;
    }
}

/// <summary>
/// Debug-only validation and accelerated test control.
/// This entire file is excluded from non-Debug builds by RebirthUtils.csproj.
/// </summary>
public class ConsoleCmdRebirthVehicleRespawn : ConsoleCmdAbstract
{
    private static readonly string[] EligibleRootBlocks =
    {
        "cntArmyTruckOpen",
        "cntArmyTruckClosed",
        "cntBusSchool",
        "cntFarmTruckClosed",
        "cntFireTruck01White",
        "cntMinivan01White",
        "cntMinivan3Wide01White",
        "cntPickupTruck01White",
        "cntPickupTruck3Wide01White",
        "cntSemiTruck01White",
        "cntSUV01White",
        "abandonedSUV01DoorsWhite",
        "abandonedPickupTruck01White",
        "abandonedPickupTruck01DoorWhite",
        "abandonedSUV3Wide01DoorsWhite",
        "abandonedPickupTruck3Wide01White",
        "abandonedPickupTruck3Wide01DoorWhite",
        "cntSemiTruck01ModularFrontWhite",
        "tractorWhite",
        "excavatorWhite",
        "backhoeWhite",
        "forkliftWhite",
        "forklift2White",
        "cntSUV3Wide01White",
        "cntSedan01White",
        "burntSedan01",
        "burntSedan3Wide01",
        "cntFrontCrumpledSedan01White",
        "cntSedan3Wide01White",
        "cntPoliceCar01AlarmUnlocked",
        "cntPoliceCar01PickedLockBonus",
        "cntCar03SedanDamage2Master",
        "repairableVehicleBicycleRebirth",
        "repairableVehicleMinibikeRebirth",
        "repairableVehicleMotorcycleRebirth",
        "repairableVehicle4x4Rebirth",
        "repairableVehicleGyrocopterRebirth"
    };

    public override string[] getCommands()
    {
        return new[] { "rbvehiclerespawn" };
    }

    public override string getDescription()
    {
        return "Debug-only validation and accelerated testing for native vehicle respawns.";
    }

    public override string getHelp()
    {
        return "rbvehiclerespawn info\n"
             + "rbvehiclerespawn validate\n"
             + "rbvehiclerespawn fast on|off\n"
             + "rbvehiclerespawn scan [radius]\n"
             + "rbvehiclerespawn due [radius]";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string operation = _params != null && _params.Count > 0
            ? _params[0].ToLowerInvariant()
            : "info";

        if (operation == "fast")
        {
            if (_params == null || _params.Count < 2)
            {
                SdtdConsole.Instance.Output("fast=" + RebirthVehicleRespawnDebugPolicy.ForceOneTick);
                return;
            }

            RebirthVehicleRespawnDebugPolicy.ForceOneTick =
                _params[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                _params[1].Equals("true", StringComparison.OrdinalIgnoreCase) ||
                _params[1] == "1";

            SdtdConsole.Instance.Output(
                "[rbvehiclerespawn] fast=" + RebirthVehicleRespawnDebugPolicy.ForceOneTick
                + " (affects markers created after this change)");
            return;
        }

        if (operation == "validate")
        {
            SdtdConsole.Instance.Output(BuildValidationReport());
            return;
        }

        if (operation == "scan")
        {
            int radius = 24;
            if (_params != null && _params.Count > 1)
                int.TryParse(_params[1], out radius);
            radius = Utils.FastClamp(radius, 1, 64);
            SdtdConsole.Instance.Output(ScanForMarkers(radius, _senderInfo));
            return;
        }

        if (operation == "due")
        {
            int radius = 24;
            if (_params != null && _params.Count > 1)
                int.TryParse(_params[1], out radius);
            radius = Utils.FastClamp(radius, 1, 64);
            SdtdConsole.Instance.Output(ScheduleNearbyMarkersDue(radius, _senderInfo));
            return;
        }

        if (operation == "info")
        {
            BlockValue marker = Block.GetBlockValue("carRespawner_FR");
            BlockRespawnRebirth markerBlock = marker.isair
                ? null
                : marker.Block as BlockRespawnRebirth;

            SdtdConsole.Instance.Output(
                "[rbvehiclerespawn] days=" + RebirthVehicleBlockRespawnRuntimePolicy.Days
                + " enabled=" + RebirthVehicleBlockRespawnRuntimePolicy.Enabled
                + " fast=" + RebirthVehicleRespawnDebugPolicy.ForceOneTick
                + " markerResolved=" + (!marker.isair)
                + " markerClass=" + (markerBlock != null)
                + " tickRate=" + (markerBlock != null ? markerBlock.GetTickRate().ToString() : "n/a")
                + " persistence=native-WBT-chunk-data"
                + " result=carsRandomHelper"
                + " forcedCompositeDowngrades=" + RebirthVehicleRespawnDebugPolicy.ForcedCompositeDowngrades
                + " lastComposite=" + RebirthVehicleRespawnDebugPolicy.LastForcedCompositeBlock
                + " lastRespawned=" + RebirthVehicleRespawnDebugPolicy.LastRespawnedBlock
                + " previousDowngrade=" + RebirthVehicleRespawnDebugPolicy.LastPreviousDowngrade
                + " repeatable=" + RebirthVehicleRespawnDebugPolicy.LastSpawnRepeatable
                + " suppressedPassThroughHits=" + RebirthVehicleRespawnDebugPolicy.SuppressedPassThroughHits
                + " lastSuppressedDamage=" + RebirthVehicleRespawnDebugPolicy.LastSuppressedPassThroughDamage
                + " lastSuppressedDepth=" + RebirthVehicleRespawnDebugPolicy.LastSuppressedPassThroughDepth
                + " lastSuppressedPos=" + RebirthVehicleRespawnDebugPolicy.LastSuppressedPassThroughPosition);
            return;
        }

        SdtdConsole.Instance.Output(getHelp());
    }

    private static string BuildValidationReport()
    {
        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        BlockValue helper = Block.GetBlockValue("carsRandomHelper");
        int valid = 0;
        int missing = 0;
        int wrongDowngrade = 0;
        int compositeRoots = 0;
        int compositeChainsToMarker = 0;
        int helperOutputsValid = 0;
        int helperOutputsMissing = 0;
        int helperOutputsWrongDowngrade = 0;
        int helperCompositeOutputs = 0;
        int helperCompositeChainsToMarker = 0;
        RebirthVehicleRespawnRuntimeRegistry.BindingReport runtimeBinding =
            RebirthVehicleRespawnRuntimeRegistry.BindCarsRandomHelperTargets();
        StringBuilder report = new StringBuilder();

        report.AppendLine("[rbvehiclerespawn validate]");
        bool compatibilityPatchInstalled = RebirthVehicleRespawnPatchInstaller.IsInstalled;

        report.AppendLine("marker=" + (!marker.isair)
            + " markerClass=" + (!marker.isair && marker.Block is BlockRespawnRebirth)
            + " helper=" + (!helper.isair)
            + " compositeCompatibilityPatch=" + compatibilityPatchInstalled);
        report.AppendLine("runtimeBinding " + runtimeBinding);

        for (int i = 0; i < EligibleRootBlocks.Length; i++)
        {
            string name = EligibleRootBlocks[i];
            BlockValue value = Block.GetBlockValue(name);
            if (value.isair)
            {
                missing++;
                report.AppendLine("MISSING " + name);
                continue;
            }

            if (marker.isair || value.Block.DowngradeBlock.type != marker.type)
            {
                wrongDowngrade++;
                report.AppendLine("WRONG-DOWNGRADE " + name
                    + " -> " + (value.Block.DowngradeBlock.isair
                        ? "air"
                        : value.Block.DowngradeBlock.Block.blockName));
                continue;
            }

            valid++;
            if (value.Block is BlockCompositeTileEntity)
            {
                compositeRoots++;
                if (RebirthVehicleRespawnDowngradeChain.EndsAtRespawnMarker(value.Block))
                    compositeChainsToMarker++;
                else
                    report.AppendLine("BROKEN-COMPOSITE-CHAIN " + name);
            }
        }

        List<BlockPlaceholderMap.PlaceholderTarget> helperTargets;
        if (!helper.isair
            && BlockPlaceholderMap.Instance != null
            && BlockPlaceholderMap.Instance.placeholders.TryGetValue(helper, out helperTargets)
            && helperTargets != null)
        {
            HashSet<int> visitedTypes = new HashSet<int>();
            for (int i = 0; i < helperTargets.Count; i++)
            {
                BlockValue value = helperTargets[i].blockValue;
                if (value.isair || value.Block == null)
                {
                    helperOutputsMissing++;
                    continue;
                }

                if (!visitedTypes.Add(value.type))
                    continue;

                string name = value.Block.blockName;
                if (!RebirthVehicleRespawnDowngradeChain.EndsAtRespawnMarker(value.Block))
                {
                    helperOutputsWrongDowngrade++;
                    report.AppendLine("WRONG-HELPER-OUTPUT " + name
                        + " -> " + (value.Block.DowngradeBlock.isair
                            ? "air"
                            : value.Block.DowngradeBlock.Block.blockName));
                    continue;
                }

                helperOutputsValid++;
                if (value.Block is BlockCompositeTileEntity)
                {
                    helperCompositeOutputs++;
                    helperCompositeChainsToMarker++;
                }
            }
        }

        report.Append("valid=" + valid
            + " missing=" + missing
            + " wrongDowngrade=" + wrongDowngrade
            + " compositeRoots=" + compositeRoots
            + " compositeChainsToMarker=" + compositeChainsToMarker
            + " expectedRoots=" + EligibleRootBlocks.Length
            + " helperOutputsValid=" + helperOutputsValid
            + " helperOutputsMissing=" + helperOutputsMissing
            + " helperOutputsWrongDowngrade=" + helperOutputsWrongDowngrade
            + " helperCompositeOutputs=" + helperCompositeOutputs
            + " helperCompositeChainsToMarker=" + helperCompositeChainsToMarker
            + " expectedHelperOutputs=" + runtimeBinding.UniqueTargetTypes);
        return report.ToString();
    }

    private static string ScanForMarkers(int radius, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer player = ResolveRequester(world, senderInfo);
        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        if (world == null || player == null || marker.isair)
            return "[rbvehiclerespawn scan] world/player/marker unavailable";

        Vector3i center = World.worldToBlockPos(player.position);
        int count = 0;
        StringBuilder positions = new StringBuilder();

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 6; y <= center.y + 6; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    if (world.GetBlock(pos).type != marker.type)
                        continue;

                    count++;
                    if (count <= 20)
                        positions.Append(" ").Append(pos);
                }
            }
        }

        return "[rbvehiclerespawn scan] radius=" + radius
            + " markers=" + count
            + (count > 0 ? " positions=" + positions : string.Empty);
    }

    private static string ScheduleNearbyMarkersDue(int radius, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer player = ResolveRequester(world, senderInfo);
        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        if (world == null || player == null || marker.isair)
            return "[rbvehiclerespawn due] world/player/marker unavailable";
        if (world.IsRemote())
            return "[rbvehiclerespawn due] run this command on the authoritative server";

        Vector3i center = World.worldToBlockPos(player.position);
        int scheduled = 0;
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 6; y <= center.y + 6; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    if (world.GetBlock(pos).type != marker.type)
                        continue;

                    world.GetWBT().AddScheduledBlockUpdate(pos, marker.type, 1UL);
                    scheduled++;
                }
            }
        }

        return "[rbvehiclerespawn due] radius=" + radius
            + " scheduled=" + scheduled
            + " via=native-WorldBlockTicker";
    }

    private static EntityPlayer ResolveRequester(World world, CommandSenderInfo senderInfo)
    {
        if (world == null)
            return null;

        if (senderInfo.RemoteClientInfo != null)
            return world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;

        return world.GetPrimaryPlayer();
    }
}
#endif
