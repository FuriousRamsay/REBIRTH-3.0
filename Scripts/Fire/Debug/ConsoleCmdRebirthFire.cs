#if DEBUG
﻿using System;
using System.Collections.Generic;

#nullable disable

public sealed class ConsoleCmdRebirthFire : ConsoleCmdAbstract
{
    public static bool DebugEnabled;

    public override string getDescription()
    {
        return "REBIRTH block-fire diagnostics and gated mutation commands";
    }

    public override string[] getCommands()
    {
        return new[] { "rbfire" };
    }

    public override string getHelp()
    {
        return "rbfire status|validate|profile [x y z]|debug on|off|ignite [x y z]|extinguish [x y z]|remove [x y z]|save|snapshot";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo sender)
    {
        string operation = parameters.Count > 0 ? parameters[0].ToLowerInvariant() : "status";
        if (operation == "debug")
        {
            if (parameters.Count < 2)
            {
                SdtdConsole.Instance.Output("rbfire debug=" + DebugEnabled);
                return;
            }
            DebugEnabled = parameters[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                           parameters[1].Equals("true", StringComparison.OrdinalIgnoreCase);
            SdtdConsole.Instance.Output("rbfire debug=" + DebugEnabled);
            return;
        }

        if (operation == "status")
        {
            PrintStatus();
            return;
        }
        if (operation == "validate")
        {
            PrintValidation();
            return;
        }
        if (operation == "profile")
        {
            PrintProfile(ResolvePosition(parameters, 1));
            return;
        }

        if (!DebugEnabled)
        {
            SdtdConsole.Instance.Output("rbfire mutation commands require: rbfire debug on");
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            SdtdConsole.Instance.Output("rbfire: world is unavailable");
            return;
        }

        Vector3i position = ResolvePosition(parameters, 1);
        int source = sender.RemoteClientInfo != null
            ? sender.RemoteClientInfo.entityId
            : (world.GetPrimaryPlayer() != null ? world.GetPrimaryPlayer().entityId : -1);

        if (operation == "ignite")
        {
            bool accepted = RebirthFireService.Instance.TryIgnite(
                world,
                position,
                source,
                RebirthFireIgnitionCause.Debug);
            SdtdConsole.Instance.Output("rbfire ignite position=" + position + " accepted=" + accepted);
        }
        else if (operation == "extinguish")
        {
            bool removed = RebirthFireService.Instance.Extinguish(
                world,
                position,
                RebirthFireDefaults.SmokeSeconds);
            SdtdConsole.Instance.Output("rbfire extinguish position=" + position + " wasBurning=" + removed);
        }
        else if (operation == "remove")
        {
            bool removed = RebirthFireService.Instance.Remove(position);
            SdtdConsole.Instance.Output("rbfire remove position=" + position + " removed=" + removed);
        }
        else if (operation == "save")
        {
            bool saved = RebirthFirePersistence.SaveSafely("debug save");
            SdtdConsole.Instance.Output("rbfire save complete success=" + saved);
        }
        else if (operation == "snapshot")
        {
            RebirthFireNetwork.BroadcastSnapshot();
            SdtdConsole.Instance.Output("rbfire snapshot broadcast revision=" + RebirthFireService.Instance.Revision);
        }
        else
        {
            SdtdConsole.Instance.Output(getHelp());
        }
    }

    private static void PrintStatus()
    {
        RebirthFireService service = RebirthFireService.Instance;
        SdtdConsole.Instance.Output(
            "[rbfire status] enabled=" + RebirthFireRuntimePolicy.Enabled
            + " patches=" + RebirthFirePatchInstaller.IsInstalled
            + " patchMethods=" + RebirthFirePatchInstaller.PatchedMethods
            + " patchFailures=" + RebirthFirePatchInstaller.FailedMethods
            + " active=" + service.ActiveCount
            + " cooldowns=" + service.CooldownCount
            + " due=" + service.DueCount
            + " delayed=" + RebirthFireDelayedIgnitionQueue.Count
            + " revision=" + service.Revision
            + " checkedLast=" + service.ProcessedLastUpdate
            + " simulatedLast=" + service.SimulatedLastUpdate
            + " simulatedWindow=" + service.SimulatedThisWindow
                + "/" + RebirthFireDefaults.MaxSimulatedFiresPerWindow
            + " skippedInvisibleLast=" + service.SkippedInvisibleLastUpdate
            + " deferredWindowCapLast=" + service.DeferredSimulationCapLastUpdate
            + " spreadLast=" + service.SpreadIgnitionsLastUpdate
            + " maxChunk=" + service.MaxChunkFireCount
                + "/" + RebirthFireDefaults.MaxActiveFiresPerChunk
            + " globalCap=" + RebirthFireDefaults.MaxActiveFiresGlobal
            + " fireDamageSpeed=" + RebirthFireRuntimePolicy.BlockDamageSpeed
            + " fireDamageMultiplier=" + RebirthFireRuntimePolicy.BlockDamageMultiplier.ToString("F1")
            + " trimmedOnRestore=" + service.TrimmedOnRestore
            + " visualActive=" + RebirthFireVisualManager.ActiveCount
            + " visualCooldowns=" + RebirthFireVisualManager.CooldownCount
            + " visibleFire=" + RebirthFireVisualManager.VisibleFireCount);

        SdtdConsole.Instance.Output(
            "[rbfire counters] rejectGlobal=" + service.RejectedGlobalCap
            + " rejectChunk=" + service.RejectedChunkCap
            + " rejectTrader=" + service.RejectedTrader
            + " rejectCooldown=" + service.RejectedCooldown
            + " rejectWater=" + service.RejectedWater
            + " rejectNonFlammable=" + service.RejectedNonFlammable
            + " destroyed=" + service.DestroyedByFire
            + " extinguishedWater=" + service.ExtinguishedByWater
            + " extinguishedChance=" + service.ExtinguishedByChance
            + " unloadedDeferrals=" + service.DelayedForUnloadedChunk
            + " skippedInvisibleTotal=" + service.SkippedInvisibleTotal
            + " deferredWindowCapTotal=" + service.DeferredSimulationCapTotal
            + " particlesSpawned=" + RebirthFireVisualManager.ParticlesSpawned
            + " particlesRemoved=" + RebirthFireVisualManager.ParticlesRemoved
            + " lightsDisabled=" + RebirthFireVisualManager.LightsDisabled);
    }

    private static void PrintValidation()
    {
        bool blocksLoaded = Block.BlocksLoaded;
        bool patchState = RebirthFirePatchInstaller.IsInstalled &&
                          RebirthFirePatchInstaller.PatchedMethods == 3 &&
                          RebirthFirePatchInstaller.FailedMethods == 0;
        bool timing = Math.Abs(RebirthFireService.WorldTicksToSeconds(
            RebirthFireService.AddSeconds(0, RebirthFireDefaults.ProcessIntervalSeconds))
            - RebirthFireDefaults.ProcessIntervalSeconds) < 0.1f;
        bool caps = RebirthFireDefaults.MaxActiveFiresGlobal == 512 &&
                    RebirthFireDefaults.MaxActiveFiresPerChunk == 96 &&
                    RebirthFireDefaults.MaxSimulatedFiresPerWindow == 64 &&
                    RebirthFireVisualManager.SoftFireParticleCap == 112 &&
                    RebirthFireVisualManager.TotalParticleCap == 150;

        SdtdConsole.Instance.Output(
            "[rbfire validate] blocksLoaded=" + blocksLoaded
            + " patches=" + patchState
            + " exact20SecondTiming=" + timing
            + " caps=" + caps
            + " profilesCached=" + RebirthFireProfileRegistry.CachedCount
            + " active=" + RebirthFireService.Instance.ActiveCount);
        SdtdConsole.Instance.Output(
            blocksLoaded && patchState && timing && caps
                ? "OK: fire runtime wiring and bounded scheduler are valid. Test gameplay adapters next."
                : "ERROR: fire runtime validation failed; do not begin gameplay testing.");
    }

    private static void PrintProfile(Vector3i position)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            SdtdConsole.Instance.Output("rbfire profile: world unavailable");
            return;
        }
        BlockValue value = world.GetBlock(position);
        RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(value);
        SdtdConsole.Instance.Output(
            "[rbfire profile] pos=" + position
            + " block=" + value.Block.GetBlockName()
            + " type=" + value.type
            + " damage=" + value.damage + "/" + value.Block.MaxDamage
            + " flammable=" + profile.Flammable
            + " fireDamageBase=" + profile.DamagePerProcess
            + " fireDamageSpeed=" + RebirthFireRuntimePolicy.BlockDamageSpeed
            + " fireDamageMultiplier=" + RebirthFireRuntimePolicy.BlockDamageMultiplier.ToString("F1")
            + " fireDamageEffective=" + RebirthFireRuntimePolicy.ScaleBlockDamage(profile.DamagePerProcess)
            + " intervalSeconds=" + profile.ProcessIntervalSeconds
            + " spread=" + profile.Spread
            + " spreadChance=" + profile.SpreadChance
            + " extinguishChance=" + profile.ExtinguishChance
            + " cooldownSeconds=" + profile.SmokeSeconds
            + " downgrade=" + (string.IsNullOrEmpty(profile.DowngradeBlockName) ? "<native>" : profile.DowngradeBlockName)
            + " particle=" + profile.FireParticle
            + " sound=" + profile.FireSound
            + " burning=" + RebirthFireService.Instance.IsBurning(position));
    }

    private static Vector3i ResolvePosition(List<string> parameters, int start)
    {
        int x;
        int y;
        int z;
        if (parameters.Count >= start + 3 &&
            int.TryParse(parameters[start], out x) &&
            int.TryParse(parameters[start + 1], out y) &&
            int.TryParse(parameters[start + 2], out z))
            return new Vector3i(x, y, z);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        return player != null ? World.worldToBlockPos(player.position) : Vector3i.zero;
    }
}
#endif
