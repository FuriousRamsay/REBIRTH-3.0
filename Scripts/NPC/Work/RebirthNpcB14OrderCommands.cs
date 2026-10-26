using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

// Owner tooling that creates B14 native Mining/Logging assignments WITHOUT the DEBUG fixture command and without any
// UI (the Desk assignment control is a separate UI package). It goes through the same session authorization,
// RebirthNpcMiningWorkPackageService (Plan -> CreateAndStart) and RebirthNpcB14MiningWork.Assign path a Desk control
// would use, so it is not a parallel framework:
//   rbnpcmine start  <npc> <mining|logging> <desk volume id N> [count=1..32] confirm=OwnerMining
//   rbnpcmine block  <npc> <mining|logging> <desk volume id N> <x> <y> <z> confirm=OwnerMining
//   rbnpcmine report
// start creates up to `count` assignments for the workable blocks inside the saved Desk Mining/Logging volume (the
// package service starts them one after the other; the assignments themselves are saved, so a restart keeps the
// remaining ones). block assigns one exact block. Server-local console only, admin level 0, main thread. Everything
// stays behind RebirthNpcBlockMiningAdapter.NativeHarvestQualified: while it is false the planner rejects the order and
// this command reports that gate instead of creating anything.
public sealed class ConsoleCmdRebirthNpcMine : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return false; } }
    public override int DefaultPermissionLevel { get { return 0; } }
    public override string[] getCommands() { return new[] { "rbnpcmine" }; }
    public override string getDescription() { return "REBIRTH NPC B14: assign native mining/logging work inside a saved Desk volume."; }
    public override string getHelp()
    {
        return "rbnpcmine start <npc> <mining|logging> <volumeId> [count=1..32] confirm=OwnerMining\n" +
            "rbnpcmine block <npc> <mining|logging> <volumeId> <x> <y> <z> confirm=OwnerMining\n" +
            "rbnpcmine report\n" +
            "<npc> is an entity id or stable id. The worker must carry a pickaxe/axe on its toolbelt; it equips it itself.";
    }

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        string result;
        try { result = Run(args, sender); }
        catch (Exception ex) { result = "FAIL " + ex.GetType().Name + ": " + ex.Message; }
        Log.Out("[RBNPC] mine " + result);
        SdtdConsole.Instance.Output("[RBNPC] mine " + result);
    }

    private static string Run(List<string> args, CommandSenderInfo sender)
    {
        if (sender.RemoteClientInfo != null) return "FAIL refused: server-local console only.";
        if (!ThreadManager.IsMainThread()) return "FAIL refused: main thread only.";
        if (args == null || args.Count < 1) return "FAIL usage: see rbnpcmine help.";
        string op = args[0].ToLowerInvariant();
        if (op == "report" && args.Count == 1) return RebirthNpcMiningWorkPackageService.GetReport();
        bool confirmed = false;
        List<string> rest = new List<string>();
        int count = 8;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "confirm=OwnerMining") confirmed = true;
            else if (args[i].StartsWith("count=", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(args[i].Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > 32)
                    return "FAIL count must be 1..32.";
            }
            else rest.Add(args[i]);
        }
        if (!confirmed) return "FAIL refused: explicit confirm=OwnerMining required.";
        bool single = op == "block";
        if ((!single && op != "start") || rest.Count != (single ? 7 : 4)) return "FAIL usage: see rbnpcmine help.";
        RebirthNpcB14Kind kind;
        if (string.Equals(rest[2], "mining", StringComparison.OrdinalIgnoreCase)) kind = RebirthNpcB14Kind.Mining;
        else if (string.Equals(rest[2], "logging", StringComparison.OrdinalIgnoreCase)) kind = RebirthNpcB14Kind.Logging;
        else return "FAIL kind must be mining or logging.";
        string volume = rest[3];
        Guid volumeGuid;
        if (!Guid.TryParseExact(volume, "N", out volumeGuid) || volumeGuid == Guid.Empty) return "FAIL volume id must be a 32-digit Desk target id.";
        RebirthNpcStableId id; EntityRebirthHumanoidNPC npc;
        if (!TryNpc(rest[1], out id, out npc) || string.IsNullOrEmpty(npc.RebirthRuntimeState.OwnerId))
            return "FAIL owned npc not found.";
        RebirthNpcInteractionContext session;
        if (RebirthNpcInteractionSessionService.Open(npc.RebirthRuntimeState.OwnerId, id, TimeSpan.FromSeconds(30), out session) !=
            RebirthNpcInteractionResult.Allowed || session == null) return "FAIL owner session refused.";
        try
        {
            if (single)
            {
                int x, y, z;
                if (!int.TryParse(rest[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                    !int.TryParse(rest[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                    !int.TryParse(rest[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out z))
                    return "FAIL coordinates must be integers.";
                RebirthNpcWorkAssignment assignment; string detail;
                RebirthNpcWorkAssignmentResult r = RebirthNpcB14MiningWork.Assign(session.SessionKey, volume, kind, new Vector3i(x, y, z), out assignment, out detail);
                return (r == RebirthNpcWorkAssignmentResult.Succeeded ? "OK" : "FAIL " + r) + " block npc=" + id + " volume=" + volume +
                    " cell=" + x + "," + y + "," + z + " detail=" + detail;
            }
            RebirthNpcMiningPackageResult package = RebirthNpcMiningWorkPackageService.CreateAndStart(new RebirthNpcMiningPackageRequest
            {
                SessionKey = session.SessionKey, Kind = kind, VolumeTargetId = volume, MaxAssignments = count,
                StartImmediately = true, StopOnFailure = false
            });
            return (package.Created > 0 ? "OK" : "FAIL") + " start npc=" + id + " volume=" + volume + " planned=" + package.Planned +
                " created=" + package.Created + " started=" + package.Started + " conflicts=" + package.Conflicts +
                " rejected=" + package.Rejected + " detail=" + package.Detail;
        }
        finally { RebirthNpcInteractionSessionService.Close(session.SessionKey); }
    }

    private static bool TryNpc(string text, out RebirthNpcStableId id, out EntityRebirthHumanoidNPC npc)
    {
        id = default(RebirthNpcStableId); npc = null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || string.IsNullOrEmpty(text)) return false;
        int eid;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out eid)) npc = world.GetEntity(eid) as EntityRebirthHumanoidNPC;
        else if (RebirthNpcStableId.TryParse(text, out id) && RebirthNpcRuntimeRegistry.TryGetEntityId(id, out eid)) npc = world.GetEntity(eid) as EntityRebirthHumanoidNPC;
        if (npc == null || npc.RebirthRuntimeState == null) return false;
        id = npc.RebirthRuntimeState.StableId;
        return !id.IsEmpty;
    }
}
