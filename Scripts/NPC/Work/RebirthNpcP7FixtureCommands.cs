#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

#nullable disable

// TEST-ONLY console hooks for the disposable CodexTest profiler batch (C2C-133). Compiled only in DEBUG. They add NO behaviour and open NO gate:
// each mutating command is a thin caller of the production entry point with the real local owner (the primary player, resolved exactly as the other
// fixture commands do; RebirthNpcStationQueueOriginal itself then demands RebirthOriginalActionScope.Matches(owner, row owner, row creation)).
// No custody bypass: no row, station, bag or chunk is written here. Gates: none needed (RebirthNpcRecipeIngredientSet.Qualified and
// RebirthNpcStationQueueOriginal.PickupQualified are already true), so there is deliberately no override state.
//   rbnpcp7 cancel <npc> <assignmentId> confirm=CodexTest   -> RebirthNpcStationQueueOriginal.TryCancelOwnerConfirmed(owner, npc, id, true, out detail)
//   rbnpcp7 abort  <npc> <assignmentId> confirm=CodexTest   -> RebirthNpcStationQueueOriginal.TryAbortWithdrawal(owner, npc, id, out detail)
//   rbnpcp7 status [npc]                                    -> read-only (needs the local console, no confirm, no save name)
// <npc> is a stable id or an entity id (P7Local.TryNpc). Output lines are prefixed "[RBNPC] rbnpcp7 ".
public sealed class ConsoleCmdRebirthNpcP7 : ConsoleCmdAbstract
{
    private static class P7Local
    {
        internal static string Check(List<string> args, CommandSenderInfo sender, out List<string> rest)
        {
            rest = new List<string>();
            if (sender.RemoteClientInfo != null) return "FAIL refused: server-local console only.";
            if (!ThreadManager.IsMainThread()) return "FAIL refused: main thread only.";
            bool confirmed = false;
            foreach (string a in args ?? new List<string>()) { if (a == "confirm=CodexTest") confirmed = true; else rest.Add(a); }
            if (!confirmed) return "FAIL refused: explicit confirm=CodexTest required.";
            if (!RebirthNpcQualificationMutationGuard.Allows("CodexTest")) return "FAIL refused: needs the disposable CodexTest save.";
            return null;
        }
        internal static bool TryNpc(string text, out RebirthNpcStableId id, out EntityRebirthHumanoidNPC npc)
        {
            id = default(RebirthNpcStableId); npc = null;
            var world = GameManager.Instance?.World; if (world == null || string.IsNullOrEmpty(text)) return false;
            int eid;
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out eid)) npc = world.GetEntity(eid) as EntityRebirthHumanoidNPC;
            else if (RebirthNpcStableId.TryParse(text, out id) && RebirthNpcRuntimeRegistry.TryGetEntityId(id, out eid)) npc = world.GetEntity(eid) as EntityRebirthHumanoidNPC;
            if (npc == null) return false;
            id = npc.RebirthRuntimeState.StableId; return !id.IsEmpty;
        }
        internal static void Emit(string command, string result)
        {
            Log.Out("[RBNPC] " + command + " " + result);
            SdtdConsole.Instance.Output("[RBNPC] " + command + " " + result);
        }
        internal static string Safe(Func<string> run)
        {
            try { return run(); } catch (Exception ex) { return "FAIL " + ex.GetType().Name + ": " + ex.Message; }
        }
    }

    public override bool IsExecuteOnClient { get { return false; } }
    public override int DefaultPermissionLevel { get { return 0; } }
    public override string[] getCommands() { return new[] { "rbnpcp7" }; }
    public override string getDescription() { return "CodexTest only: P7 queue-original cancel/abort callers and a read-only status."; }
    public override string getHelp() { return "rbnpcp7 cancel|abort <npc> <assignmentId> confirm=CodexTest | rbnpcp7 status [npc]"; }

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        string result = P7Local.Safe(() =>
        {
            if (args == null || args.Count < 1) return "FAIL usage: " + getHelp();
            string verb = args[0].ToLowerInvariant();
            if (verb == "status")
            {
                if (sender.RemoteClientInfo != null) return "FAIL refused: server-local console only.";
                if (!ThreadManager.IsMainThread()) return "FAIL refused: main thread only.";
                return Status(args.Count > 1 ? args[1] : null);
            }
            if (verb != "cancel" && verb != "abort") return "FAIL usage: " + getHelp();
            List<string> rest; string refused = P7Local.Check(args.Skip(1).ToList(), sender, out rest); if (refused != null) return refused;
            ulong assignment;
            if (rest.Count != 2 || !ulong.TryParse(rest[1], NumberStyles.None, CultureInfo.InvariantCulture, out assignment)) return "FAIL usage: " + getHelp();
            RebirthNpcStableId id; EntityRebirthHumanoidNPC npc;
            if (!P7Local.TryNpc(rest[0], out id, out npc)) return "FAIL npc not found.";
            var owner = GameManager.Instance.World.GetPrimaryPlayer();
            if (owner == null) return "FAIL no local owner player.";
            string detail; bool ok;
            if (verb == "cancel") ok = RebirthNpcStationQueueOriginal.TryCancelOwnerConfirmed(owner, id, assignment, true, out detail);
            else ok = RebirthNpcStationQueueOriginal.TryAbortWithdrawal(owner, id, assignment, out detail);
            return (ok ? "OK " : "REFUSED ") + verb + " npc=" + id + " assignment=" + assignment + " ok=" + ok + " detail=\"" + detail + "\" | " + StatusOf(new List<RebirthNpcStableId> { id });
        });
        P7Local.Emit("rbnpcp7", result);
    }

    private static string Status(string npcText)
    {
        var world = GameManager.Instance?.World; if (world == null) return "FAIL no world.";
        var ids = new List<RebirthNpcStableId>();
        if (!string.IsNullOrEmpty(npcText))
        {
            RebirthNpcStableId one; EntityRebirthHumanoidNPC e;
            if (!P7Local.TryNpc(npcText, out one, out e)) return "FAIL npc not found.";
            ids.Add(one);
        }
        else
            foreach (var ent in world.Entities.list)
            {
                var n = ent as EntityRebirthHumanoidNPC;
                if (n != null && n.RebirthRuntimeState != null && !n.RebirthRuntimeState.StableId.IsEmpty) ids.Add(n.RebirthRuntimeState.StableId);
            }
        return StatusOf(ids);
    }

    private static string StatusOf(List<RebirthNpcStableId> ids)
    {
        var world = GameManager.Instance?.World; if (world == null) return "FAIL no world.";
        var lines = new List<string>();
        foreach (var id in ids)
        {
            lines.Add("npc=" + id + " hasPending=" + RebirthNpcStationQueueOriginal.HasPending(id) +
                " pendingRefunds=[" + string.Join(",", RebirthNpcStationQueueOriginal.PendingRefunds(id)) + "] rows=" + RowsOf(id, world));
        }
        return "status gates=none(Qualified=true,PickupQualified=true) " + (lines.Count == 0 ? "no npc" : string.Join(" ; ", lines));
    }

    private static string RowsOf(RebirthNpcStableId id, World world)
    {
        try
        {
            RebirthNpcPersistentRecord person;
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id, out person)) return "[unreadable]";
            var rows = (person.UnknownOptionalElements ?? Array.Empty<string>()).Select(XElement.Parse).Where(x => x.Name == "npcStationQueueOriginal").Select(x =>
            {
                string station = "?"; bool? hold = null; string queue = "n/a";
                int sx, sy, sz;
                if (int.TryParse((string)x.Attribute("x"), out sx) && int.TryParse((string)x.Attribute("y"), out sy) && int.TryParse((string)x.Attribute("z"), out sz))
                {
                    station = sx + "," + sy + "," + sz;
                    var ws = world.GetTileEntity(new Vector3i(sx, sy, sz)) as TileEntityWorkstation;
                    hold = ws == null ? (bool?)null : RebirthNpcStationQueueOriginal.BlocksStation(ws);
                    if (ws != null) queue = (ws.Queue ?? Array.Empty<RecipeQueueItem>()).Count(q => q?.Recipe != null && q.Multiplier > 0).ToString();
                }
                var w = x.Attributes().Where(a => a.Name.LocalName.Length > 1 && a.Name.LocalName[0] == 'w' && char.IsUpper(a.Name.LocalName[1]))
                    .Select(a => a.Name.LocalName + "=" + Trim(a.Value));
                string op = (string)x.Attribute("operation") ?? "";
                return "{assignment=" + (string)x.Attribute("assignment") + " op=" + (op.Length > 8 ? op.Substring(0, 8) : op) + " phase=" + (string)x.Attribute("phase") +
                    " units=" + (1 + x.Elements("unit").Count()) + " station=" + station + " queue=" + queue + " stationHold=" + (hold.HasValue ? hold.Value.ToString() : "n/a") +
                    " refundTicks=" + ((string)x.Attribute("refundTicks") ?? "") + " w:[" + string.Join(" ", w) + "]}";
            }).ToArray();
            return "[" + string.Join(" ", rows) + "]";
        }
        catch (Exception ex) { return "[error " + ex.GetType().Name + "]"; }
    }

    private static string Trim(string v) { return v == null ? "" : (v.Length > 24 ? v.Substring(0, 24) + "~" : v); }
}
#endif
