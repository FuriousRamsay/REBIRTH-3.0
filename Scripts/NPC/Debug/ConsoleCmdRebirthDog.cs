using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthDog : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;

    public override string[] getCommands()
    {
        return new[] { "rbdog" };
    }

    public override string getDescription()
    {
        return "REBIRTH dog companion diagnostics; one subcommand per audited system.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbdog ownership [playerEntityId]\n"
             + "  rbdog reconcile [playerEntityId]\n"
             + "  rbdog movement [dogEntityId]\n"
             + "  rbdog targeting [dogEntityId] [zombieEntityId]\n"
             + "  rbdog combat [dogEntityId] [zombieEntityId]\n"
             + "  rbdog recoverkillall <stableId>\n"
             + "  rbdog forgetmissing <stableId>\n"
             + "\nOwnership prints the durable record, live owner projection, cap contribution,"
             + " embedded StableId and Raw Meat locations in one report."
             + "\nReconcile restores the same owned StableIds and schedules bounded missing-Follow recovery; it does not free capacity or create a replacement identity."
             + "\nMovement automatically records a 12-second trace for the specified dog or the"
             + " nearest loaded owned dog; it logs owner distance, combat mode/radius, dog/owner LOS to the"
             + " active target, target distances, move/stop decisions, path destinations, path-request churn,"
             + " blocked time, movement delta and yaw delta; no separate on/off command is required."
             + "\nTargeting automatically records the sleeper-to-awake transition and the exact live target-policy"
             + " decision for one dog/zombie pair: sleep/wake flags, owner/dog LOS, configured combat radius,"
             + " dog/owner/target distances, retaliation state, current attack/revenge target, and the runtime"
             + " selector's last candidate/rejection reason. Start it before waking a sleeper."
             + "\nCombat automatically records a 12-second dog/zombie trace including faction"
             + " decisions, damage permission, attack/revenge targets, EAI tasks and dog target-class filters."
             + "\nRecoverKillAll repairs ONLY a dog record explicitly marked as removed by the old"
             + " v230-v234 killall handler; it restores AwaitingRespawn and never touches legitimate pickups."
             + "\nForgetMissing is an explicit rollback-cleanup command. It removes ONLY an owned Active"
             + " dog record that has no live projection; it refuses PickedUp, AwaitingRespawn, and live dogs.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string operation = parameters == null || parameters.Count == 0
            ? "ownership"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        if (operation != "ownership" && operation != "reconcile" && operation != "movement" && operation != "targeting" && operation != "combat" && operation != "recoverkillall" && operation != "forgetmissing")
        {
            Log.Out(getHelp());
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[REBIRTH Dog] world is not available.");
            return;
        }

        EntityPlayer player = null;
        if (operation == "ownership" || operation == "reconcile")
        {
            int requestedPlayerId;
            if (parameters != null && parameters.Count > 1 && int.TryParse(parameters[1], out requestedPlayerId))
                player = world.GetEntity(requestedPlayerId) as EntityPlayer;
        }

        if (player == null && senderInfo.RemoteClientInfo != null)
            player = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
        if (player == null)
            player = world.GetPrimaryPlayer() as EntityPlayer;

        if (operation == "ownership")
        {
            Log.Out(RebirthDogLifecycleService.GetOwnershipDebugReport(player));
            return;
        }

        if (operation == "reconcile")
        {
            if (player == null || world.IsRemote() || SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true)
            { Log.Out("[REBIRTH Dog] reconcile requires an authoritative player/world."); return; }
            try
            {
                RebirthNpcPersistenceCoordinator.EnsureLoaded();
                RebirthDogLifecycleService.ScheduleGhostRepair(player);
                RebirthDogChunkObserverService.RefreshForPlayer(player);
                RebirthDogLifecycleService.ReconcileOwnerProjectionForPlayer(player);
                string saveError;
                bool saved = RebirthNpcPersistenceCoordinator.SaveAll(out saveError);
                string result = "[REBIRTH Dog] reconciliation attempted for player=" + player.entityId +
                    "; same-identity retries queued; checkpoint=" + (saved ? "saved" : "FAILED: " + saveError);
                SdtdConsole.Instance.Output(result); Log.Out(result);
                string report = RebirthDogLifecycleService.GetOwnershipDebugReport(player);
                SdtdConsole.Instance.Output(report); Log.Out(report);
            }
            catch (Exception ex)
            {
                string error = "[REBIRTH Dog] reconciliation incomplete; no replacement identity or capacity reset: " + ex.GetType().Name + ": " + ex.Message;
                SdtdConsole.Instance.Output(error); Log.Warning(error);
            }
            return;
        }

        if (operation == "recoverkillall")
        {
            if (parameters == null || parameters.Count < 2)
            {
                Log.Out("Usage: rbdog recoverkillall <stableId>");
                return;
            }
            RebirthNpcStableId stableId;
            if (!RebirthNpcStableId.TryParse(parameters[1], out stableId))
            {
                Log.Out("[REBIRTH Dog] invalid StableId: " + parameters[1]);
                return;
            }
            string recoveryReason;
            bool recovered = RebirthDogLifecycleService.TryRecoverAdministrativeKillAllRecord(player, stableId, out recoveryReason);
            Log.Out("[REBIRTH Dog] recoverkillall " + (recovered ? "SUCCESS" : "DENIED") + ": " + recoveryReason);
            if (recovered) Log.Out(RebirthDogLifecycleService.GetOwnershipDebugReport(player));
            return;
        }


        if (operation == "forgetmissing")
        {
            if (parameters == null || parameters.Count < 2)
            {
                Log.Out("Usage: rbdog forgetmissing <stableId>");
                return;
            }
            RebirthNpcStableId stableId;
            if (!RebirthNpcStableId.TryParse(parameters[1], out stableId))
            {
                Log.Out("[REBIRTH Dog] invalid StableId: " + parameters[1]);
                return;
            }
            string forgetReason;
            bool forgotten = RebirthDogLifecycleService.TryForgetMissingRecord(player, stableId, out forgetReason);
            Log.Out("[REBIRTH Dog] forgetmissing " + (forgotten ? "SUCCESS" : "DENIED") + ": " + forgetReason);
            if (forgotten) Log.Out(RebirthDogLifecycleService.GetOwnershipDebugReport(player));
            return;
        }

        int requestedDogId = -1;
        if (parameters != null && parameters.Count > 1)
            int.TryParse(parameters[1], out requestedDogId);

        if (operation == "movement")
        {
            string movementReason;
            RebirthDogRuntimeService.StartMovementDebug(player, requestedDogId, out movementReason);
            Log.Out(movementReason);
            return;
        }

        int requestedZombieId = -1;
        if (parameters != null && parameters.Count > 2)
            int.TryParse(parameters[2], out requestedZombieId);

        if (operation == "targeting")
        {
            string targetingReason;
            RebirthDogCombatDebugService.StartTargetingDebug(player, requestedDogId, requestedZombieId, out targetingReason);
            Log.Out(targetingReason);
            return;
        }


        string combatReason;
        RebirthDogCombatDebugService.StartCombatDebug(player, requestedDogId, requestedZombieId, out combatReason);
        Log.Out(combatReason);
    }
}
