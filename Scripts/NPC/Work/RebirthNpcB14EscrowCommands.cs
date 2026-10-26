using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

// Owner/admin tooling for the B14 harvest explicit decisions (P12_B14_20261022.md, Rounds 7-8):
//   rbnpcescrow list
//   rbnpcescrow award   <npcStableId> confirm=<token>   -> RebirthNpcB14NativeStrike.TryResolveEscrow(o, true,  save)
//   rbnpcescrow abort   <npcStableId> confirm=<token>   -> RebirthNpcB14NativeStrike.TryResolveEscrow(o, false, save)
//   rbnpcescrow release <npcStableId> confirm=<token>   -> RebirthNpcB14NativeStrike.TryReleaseOwed(o, save)
// Tokens: confirm=CodexTest (only on the disposable CodexTest save, RebirthNpcQualificationMutationGuard) or
// confirm=OwnerEscrow (any save). Server-local console only, admin level 0, main thread. Idempotent: a repeated
// call finds the phase already moved and reports NOOP without touching the row. release re-drops an owed overflow
// stack and accepts a possible duplicate, so it is always explicit.
public sealed class ConsoleCmdRebirthNpcEscrow : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return false; } }
    public override int DefaultPermissionLevel { get { return 0; } }
    public override string[] getCommands() { return new[] { "rbnpcescrow" }; }
    public override string getDescription() { return "REBIRTH NPC B14 harvest escrow: list, award, abort, release owed stacks."; }
    public override string getHelp()
    {
        return "rbnpcescrow list\n" +
            "rbnpcescrow <award|abort|release> <npcStableId> confirm=CodexTest|confirm=OwnerEscrow\n" +
            "award: escrowed deliverable proceeds to Witnessed/credit. abort: nothing awarded. release: forget an unobservable overflow drop so it is dropped once more (may duplicate).";
    }

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        string result;
        try { result = Run(args, sender); }
        catch (Exception ex) { result = "FAIL " + ex.GetType().Name + ": " + ex.Message; }
        Log.Out("[RBNPC] escrow " + result);
        SdtdConsole.Instance.Output("[RBNPC] escrow " + result);
    }

    private static string Run(List<string> args, CommandSenderInfo sender)
    {
        if (sender.RemoteClientInfo != null) return "FAIL refused: server-local console only.";
        if (!ThreadManager.IsMainThread()) return "FAIL refused: main thread only.";
        if (args == null || args.Count < 1)
            return "FAIL usage: rbnpcescrow list | rbnpcescrow <award|abort|release> <npcStableId> confirm=<token>";
        string op = args[0].ToLowerInvariant();
        if (op == "list" && args.Count == 1) return List();
        if (args.Count != 3 || (op != "award" && op != "abort" && op != "release")) return "FAIL usage: rbnpcescrow <award|abort|release> <npcStableId> confirm=<token>";
        string confirm = args[2];
        if (confirm == "confirm=CodexTest") { if (!RebirthNpcQualificationMutationGuard.Allows("CodexTest")) return "FAIL refused: confirm=CodexTest needs the disposable CodexTest save."; }
        else if (confirm != "confirm=OwnerEscrow") return "FAIL refused: explicit confirm=CodexTest or confirm=OwnerEscrow required.";
        RebirthNpcStableId id;
        if (!RebirthNpcStableId.TryParse(args[1], out id) || id.IsEmpty) return "FAIL invalid npc stable id '" + args[1] + "'.";
        RebirthNpcB14Store.Row row;
        if (!RebirthNpcB14Store.TryRead(id, out row) || row.Original == null) return "NOOP npc=" + id + " has no saved B14 original.";
        RebirthNpcB14HarvestOriginal o = row.Original;
        RebirthNpcB14Phase before = o.Phase;
        Func<string, bool> save = text => { row.Original = o; return RebirthNpcB14Store.TryWrite(id, row); };
        bool ok;
        if (op == "release") ok = RebirthNpcB14NativeStrike.TryReleaseOwed(o, save);
        else ok = RebirthNpcB14NativeStrike.TryResolveEscrow(o, op == "award", save);
        if (!ok) return "NOOP " + op + " npc=" + id + " assignment=" + o.AssignmentId + " phase=" + before + " (nothing to " + op + " or save failed; row unchanged in memory only if phase is still " + o.Phase + ").";
        return "OK " + op + " npc=" + id + " assignment=" + o.AssignmentId + " phase " + before + "->" + o.Phase;
    }

    private static string List()
    {
        RebirthNpcPersistentRecord[] all = RebirthNpcAggregatePersistenceStore.Snapshot();
        StringBuilder b = new StringBuilder("list");
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            RebirthNpcStableId id = all[i].Identity.StableNpcId;
            RebirthNpcB14Store.Row row;
            if (!RebirthNpcB14Store.TryRead(id, out row) || row.Original == null) continue;
            RebirthNpcB14HarvestOriginal o = row.Original;
            bool owed = false;
            for (int w = 0; w < o.Witnesses.Count; w++) if (!o.Witnesses[w].Credited && o.Witnesses[w].OverflowDropped) owed = true;
            if (o.Phase != RebirthNpcB14Phase.Escrowed && !(o.Phase == RebirthNpcB14Phase.Witnessed && owed)) continue;
            n++;
            b.Append("\n  npc=").Append(id).Append(" assignment=").Append(o.AssignmentId).Append(" phase=").Append(o.Phase)
                .Append(" pos=").Append(o.Position).Append(" witnesses=").Append(o.Witnesses.Count).Append(" items=[");
            for (int w = 0; w < o.Witnesses.Count; w++)
            {
                RebirthNpcB14DropWitness d = o.Witnesses[w];
                ItemClass ic = ItemClass.GetForId(d.ItemType);
                if (w > 0) b.Append(", ");
                b.Append(ic != null ? ic.GetItemName() : "#" + d.ItemType).Append(" x").Append(d.Count)
                    .Append(d.Credited ? " credited" : d.OverflowDropped ? " owed" : string.Empty);
            }
            b.Append(']');
        }
        return b.Append("\n  count=").Append(n).ToString();
    }
}
