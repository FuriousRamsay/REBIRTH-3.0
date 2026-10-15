using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Opt-in actual-native API qualification. Only detached stacks are passed to
// stack predicates; no synthetic quest identity or live inventory writes.
public sealed class ConsoleCmdRebirthFetchStackQualification : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbfetchteststackguards" };
    public override string getDescription() => "Qualify fetch stack guards with detached genuine package metadata.";
    public override string getHelp() => "rbfetchteststackguards (CodexTest Bridge only).";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if (args.Count != 0 || sender.RemoteClientInfo != null || player == null
            || !RebirthGameBridge.IsRunning || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        var result = new JObject { ["schema"] = 1, ["observationOnly"] = true,
            ["detachedNativeCalls"] = true, ["playerId"] = player.entityId,
            ["qualified"] = false, ["passed"] = false };
        try
        {
            var sources = new List<ItemStack>();
            var locations = new List<string>();
            Collect(player.bag?.ItemGrid?.items, "backpack", sources, locations);
            Collect(player.inventory?.ItemGrid?.items, "toolbelt", sources, locations);
            var before = Witness(sources);
            var cases = new JArray();
            int control = -1;
            for (int i = 0; i < sources.Count; i++)
            {
                var value = sources[i].itemValue;
                if (!(value.ItemClass is ItemClassQuest) && value.ItemClass?.CanStack() == true
                    && value.ItemClass.MaxCount >= 2 && control < 0) control = i;
                if (!(value.ItemClass is ItemClassQuest) || player.QuestJournal?.quests == null) continue;
                foreach (var quest in player.QuestJournal.quests)
                {
                    if (quest?.Objectives == null) continue;
                    for (int objectiveIndex = 0; objectiveIndex < quest.Objectives.Count; objectiveIndex++)
                    {
                        var objective = quest.Objectives[objectiveIndex] as ObjectiveBaseFetchContainer;
                        // Never SetupExpectedItem/Bind; require a genuine current
                        // native objective's already-established metadata match.
                        if (objective?.expectedItem == null || objective.expectedItem.IsEmpty()
                            || !RebirthFetchQuestIdentity.Matches(value, objective)) continue;
                        var proof = Probe(sources[i], true);
                        proof["source"] = locations[i]; proof["questCode"] = quest.QuestCode;
                        proof["questId"] = quest.ID; proof["objectiveIndex"] = objectiveIndex;
                        cases.Add(proof);
                    }
                }
            }
            result["packages"] = cases;
            if (control >= 0)
            {
                var proof = Probe(sources[control], false);
                proof["source"] = locations[control]; result["ordinaryControl"] = proof;
            }
            result["sourceBefore"] = before;
            var after = Witness(sources); result["sourceAfter"] = after;
            bool unchanged = JToken.DeepEquals(before, after);
            result["liveSourcesUnchanged"] = unchanged;
            bool qualified = cases.Count > 0 && control >= 0;
            result["qualified"] = qualified;
            bool passed = qualified && unchanged && (bool)result["ordinaryControl"]["passed"];
            foreach (JObject proof in cases) passed &= (bool)proof["passed"];
            result["passed"] = passed;
            result["status"] = !qualified ? "not-qualified" : passed ? "pass" : "fail";
            result["reason"] = cases.Count == 0 ? "No live package matching an initialized current fetch objective."
                : control < 0 ? "No live ordinary stackable item with native capacity for two." : "";
        }
        catch (Exception error)
        { result["status"] = "error"; result["error"] = error.ToString(); }
        SdtdConsole.Instance.Output(result.ToString(Formatting.None));
        // The existing scenario console step rejects this marker. Preserve the
        // complete JSON first, including an explicit not-qualified outcome.
        if (!(bool)result["passed"])
            SdtdConsole.Instance.Output("*** ERROR fetch stack guard qualification: " + (string)result["status"]);
    }

    private static JObject Probe(ItemStack source, bool quest)
    {
        var destination = new ItemStack(source.itemValue.Clone(), 1);
        var incoming = new ItemStack(source.itemValue.Clone(), 1);
        var before = Witness(new List<ItemStack> { destination, incoming });
        bool full = destination.CanStack(1);
        int amount = 1;
        bool partial = destination.CanStackPartly(ref amount);
        int transfer = destination.StackTransferCount(incoming);
        bool pair = destination.CanStackWith(incoming, false);
        bool partialPair = destination.CanStackWith(incoming, true);
        var after = Witness(new List<ItemStack> { destination, incoming });
        bool unchanged = JToken.DeepEquals(before, after);
        bool expected = quest ? !full && !partial && transfer == 0 && !pair && !partialPair
            : full && partial && transfer == 1 && pair && partialPair;
        return new JObject { ["questPackage"] = quest, ["canStack"] = full,
            ["canStackPartly"] = partial, ["partialInput"] = 1, ["partialOutput"] = amount,
            ["stackTransferCount"] = transfer, ["canStackWith"] = pair,
            ["canStackWithPartial"] = partialPair, ["before"] = before, ["after"] = after,
            ["detachedStacksUnchanged"] = unchanged, ["passed"] = expected && amount == 1 && unchanged };
    }

    private static void Collect(ItemStack[] cells, string kind, List<ItemStack> stacks, List<string> locations)
    {
        if (cells == null) return;
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null && !cells[i].IsEmpty())
            { stacks.Add(cells[i]); locations.Add(kind + "/" + i); }
    }

    private static JArray Witness(List<ItemStack> stacks)
    {
        var result = new JArray();
        foreach (var stack in stacks)
            result.Add(new JObject { ["count"] = stack.count, ["type"] = stack.itemValue.type,
                ["seed"] = stack.itemValue.Seed, ["meta"] = stack.itemValue.Meta,
                ["payload"] = Convert.ToBase64String(RebirthResourceItemFingerprint.Capture(stack.itemValue)) });
        return result;
    }
}
