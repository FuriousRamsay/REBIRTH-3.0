#if DEBUG
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthNpcIdentityAdmin : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;

    public override string[] getCommands()
    {
        return new[] { "rbnpcidentity", "rbnpcid" };
    }

    public override string getDescription()
    {
        return "Server-authoritative REBIRTH NPC stable identity conflict resolution.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbnpcidentity resolve <loadedEntityId> <stableId>\n"
             + "  rbnpcidentity forget <stableId>\n"
             + "  rbnpcidentity conflicts\n"
             + "  rbnpcidentity audit [count]\n"
             + "\nResolve requires matching entity class and REBIRTH profile.\n"
             + "Forget permanently discards only a pending unmatched identity record.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string operation = parameters == null || parameters.Count == 0
            ? "help"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        if (operation == "conflicts")
        {
            Log.Out(RebirthNpcStableIdentityStore.GetConflictReport());
            return;
        }

        if (operation == "audit")
        {
            int count = 20;
            if (parameters.Count > 1 && !int.TryParse(parameters[1], out count))
            {
                Log.Out("Usage: rbnpcidentity audit [count]");
                return;
            }
            Log.Out(RebirthNpcIdentityAuditLedger.GetReport(count));
            return;
        }

        if (operation == "resolve")
        {
            int entityId;
            RebirthNpcStableId stableId;
            if (parameters.Count < 3 || !int.TryParse(parameters[1], out entityId)
                || !RebirthNpcStableId.TryParse(parameters[2], out stableId))
            {
                Log.Out("Usage: rbnpcidentity resolve <loadedEntityId> <stableId>");
                return;
            }
            string message;
            bool success = RebirthNpcStableIdentityStore.TryResolve(entityId, stableId, out message);
            RebirthNpcIdentityAuditLedger.Append(ResolveActor(senderInfo), "resolve", entityId, stableId, success, message);
            Log.Out("[REBIRTH NPC] identity resolve " + (success ? "succeeded: " : "rejected: ") + message);
            return;
        }

        if (operation == "forget")
        {
            RebirthNpcStableId stableId;
            if (parameters.Count < 2 || !RebirthNpcStableId.TryParse(parameters[1], out stableId))
            {
                Log.Out("Usage: rbnpcidentity forget <stableId>");
                return;
            }
            string message;
            bool success = RebirthNpcStableIdentityStore.TryForgetPending(stableId, out message);
            RebirthNpcIdentityAuditLedger.Append(ResolveActor(senderInfo), "forget", 0, stableId, success, message);
            Log.Out("[REBIRTH NPC] identity forget " + (success ? "succeeded: " : "rejected: ") + message);
            return;
        }

        Log.Out(getHelp());
    }

    private static string ResolveActor(CommandSenderInfo senderInfo)
    {
        if (senderInfo.RemoteClientInfo != null)
            return "remote-entity:" + senderInfo.RemoteClientInfo.entityId;
        return "server-console";
    }
}
#endif
