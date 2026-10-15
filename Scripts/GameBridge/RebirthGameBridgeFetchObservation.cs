using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Native console discovery supplies this command; the authenticated bridge's
// existing /console route executes it on the main thread. No fixture mutation.
public sealed class ConsoleCmdRebirthFetchObservation : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbfetchtestobserve" };
    public override string getDescription() => "Read native fetch package identity and storage evidence as JSON.";
    public override string getHelp() => "rbfetchtestobserve [storageX storageY storageZ] (CodexTest Bridge only).";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if ((args.Count != 0 && args.Count != 3) || sender.RemoteClientInfo != null
            || player == null || !RebirthGameBridge.IsRunning
            || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
        { SdtdConsole.Instance.Output("*** ERROR " + getHelp()); return; }
        try
        {
            var quests = new JArray();
            var journal = new JArray();
            var packages = new List<ItemStack>();
            var bag = Slots(player.bag?.ItemGrid?.items, "backpack", packages);
            var bagLocks = player.bag.LockedSlots;
            foreach (JObject cell in bag)
            {
                int slot = (int)cell["slot"];
                cell["quickStackSourceLocked"] = bagLocks != null && slot < bagLocks.Length && bagLocks[slot];
            }
            var belt = Slots(player.inventory?.ItemGrid?.items, "toolbelt", packages);
            var cursor = RebirthGameBridgeUi.OwnedCursorStack();
            var cursorJson = cursor == null ? null : Cell(cursor, -1, "cursor", packages);
            var windowManager = LocalPlayerUI.GetUIForPlayer(player)?.windowManager;
            if (windowManager == null) throw new InvalidOperationException("local UI unavailable");
            if (player.QuestJournal?.quests == null) throw new InvalidOperationException("quest journal unavailable");
            foreach (var quest in player.QuestJournal.quests)
            {
                if(quest!=null)
                {
                    var objectiveStates=new JArray();
                    if(quest.Objectives!=null)for(int n=0;n<quest.Objectives.Count;n++)
                    {
                        var objective=quest.Objectives[n];
                        if(objective!=null)objectiveStates.Add(new JObject{["index"]=n,["type"]=objective.GetType().Name,
                            ["id"]=objective.ID,["currentValue"]=objective.CurrentValue,["complete"]=objective.Complete});
                    }
                    journal.Add(new JObject{["id"]=quest.ID,["questCode"]=quest.QuestCode,["uniqueId"]=quest.QuestUniqueId,
                        ["state"]=quest.CurrentState.ToString(),["phase"]=quest.CurrentPhase,["giverId"]=quest.QuestGiverID,
                        ["sharedOwnerId"]=quest.SharedOwnerID,["objectives"]=objectiveStates,
                        ["nativeSerializationQualified"]=false});
                }
                if (quest?.Objectives == null) continue;
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    var objective = quest.Objectives[i] as ObjectiveBaseFetchContainer;
                    if (objective == null) continue;
                    // Do not call SetupExpectedItem or Bind: observing must not alter
                    // quest codes, expected values or saved metadata.
                    var expected = objective.expectedItem;
                    quests.Add(new JObject {
                        ["questCode"] = quest.QuestCode, ["questId"] = quest.ID,
                        ["state"] = quest.CurrentState.ToString(), ["objectiveIndex"] = i,
                        ["currentValue"] = objective.CurrentValue, ["complete"] = objective.Complete,
                        ["expectedReady"] = expected != null && !expected.IsEmpty(),
                        ["expectedType"] = expected?.type, ["expectedSeed"] = expected?.Seed,
                        ["expectedMeta"] = expected?.Meta,
                        ["backpackCount"] = RebirthFetchQuestIdentity.Count(player.bag?.ItemGrid?.items, objective),
                        ["toolbeltCount"] = RebirthFetchQuestIdentity.Count(player.inventory?.ItemGrid?.items, objective)
                    });
                }
            }
            var pairs = new JArray();
            for (int a = 0; a < packages.Count; a++)
                for (int b = a; b < packages.Count; b++)
                    pairs.Add(new JObject { ["a"] = a, ["b"] = b,
                        ["canStack"] = packages[a].CanStackWith(packages[b], false),
                        ["canStackPartial"] = packages[a].CanStackWith(packages[b], true) });
            var result = new JObject {
                ["schema"] = 1, ["observationOnly"] = true,
                ["gameName"] = GamePrefs.GetString(EnumGamePrefs.GameName), ["remote"] = world.IsRemote(),
                ["playerId"] = player.entityId, ["quests"] = quests,
                ["allQuestJournal"] = journal,
                ["ownerInventory"] = new JObject{["schema"]=1,
                    ["bagPayload"]=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items)),
                    ["beltPayload"]=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items)),
                    ["bagLocks"]=bagLocks==null?null:new JArray(bagLocks),
                    ["beltLocks"]=player.inventory.ItemGrid.SlotLocks==null?null:new JArray(player.inventory.ItemGrid.SlotLocks),
                    ["cursorPayload"]=windowManager==null||LocalPlayerUI.GetUIForPlayer(player)?.xui?.DragAndDropWindow==null?null:
                        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{LocalPlayerUI.GetUIForPlayer(player).xui.DragAndDropWindow.CurrentStack}))},
                ["backpack"] = bag, ["toolbelt"] = belt, ["cursor"] = cursorJson,
                ["stackPairs"] = pairs, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds(),
                ["modalOpen"] = windowManager.IsModalWindowOpen(), ["inputActive"] = windowManager.IsInputActive()
            };
            if (args.Count == 3)
            {
                int x, y, z;
                if (!int.TryParse(args[0], out x) || !int.TryParse(args[1], out y) || !int.TryParse(args[2], out z))
                    throw new ArgumentException("integer storage coordinates required");
                var pos = new Vector3i(x, y, z);
                var tile = world.GetTileEntity(pos);
                TEFeatureStorage storage;
                if (tile == null || !tile.TryGetSelfOrFeature(out storage) || storage.ItemGrid?.items == null)
                    throw new InvalidOperationException("loaded storage unavailable");
                var storageCells = Slots(storage.ItemGrid.items, "storage", null);
                int emptyUnlocked = 0;
                var locks = storage.ItemGrid.SlotLocks;
                for (int i = 0; i < storage.ItemGrid.items.Length; i++)
                    if ((storage.ItemGrid.items[i] == null || storage.ItemGrid.items[i].IsEmpty())
                        && !(locks != null && i < locks.Length && locks[i])) emptyUnlocked++;
                TEFeatureStorage usableStorage;
                string refusal;
                bool usable = QuickStackService.CanUse(world, player, tile, false, out usableStorage, out refusal);
                var routing = new JArray();
                for (int i = 0; i < packages.Count; i++)
                {
                    var category = QuickStackCategoryRegistry.Get(packages[i].itemValue.type);
                    routing.Add(new JObject { ["packageIndex"] = i, ["category"] = category.ToString(),
                        ["acceptsCategory"] = QuickStackAcceptedCategoryRegistry.Accepts(pos, category) });
                }
                result["storage"] = new JObject {
                    ["x"] = x, ["y"] = y, ["z"] = z,
                    ["block"] = world.GetBlock(pos).Block.GetBlockName(),
                    ["capacity"] = storage.ItemGrid.items.Length, ["slots"] = storageCells,
                    ["emptyUnlocked"] = emptyUnlocked, ["canQuickStackUse"] = usable,
                    ["quickStackRefusal"] = refusal, ["packageRouting"] = routing,
                    ["quickStackMode"] = RebirthSandboxOptionManager.Current.QuickStack.ToString(),
                    ["distance"] = UnityEngine.Vector3.Distance(player.position, tile.ToWorldPos().ToVector3()),
                    ["quickStackRadius"] = QuickStackService.Radius,
                    ["source"] = "tile; close storage UI before persistence assertions"
                };
            }
            SdtdConsole.Instance.Output(result.ToString(Formatting.None));
        }
        catch (Exception error)
        { SdtdConsole.Instance.Output("*** ERROR fetch observation: " + error.GetType().Name + ": " + error.Message); }
    }

    private static JArray Slots(ItemStack[] slots, string location, List<ItemStack> packages)
    {
        if (slots == null) throw new InvalidOperationException(location + " slots unavailable");
        var result = new JArray();
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty()) result.Add(Cell(slots[i], i, location, packages));
        return result;
    }

    private static JObject Cell(ItemStack stack, int slot, string location, List<ItemStack> packages)
    {
        var value = stack.itemValue;
        var bytes = RebirthResourceItemFingerprint.Capture(value);
        if (bytes == null) throw new InvalidOperationException("native item payload unavailable");
        int code;
        bool hasCode = value.TryGetMetadata(RebirthFetchQuestIdentity.MetadataKey, out code);
        bool questPackage = value.ItemClass is ItemClassQuest;
        int? index = null;
        if (questPackage && packages != null) { index = packages.Count; packages.Add(stack); }
        return new JObject {
            ["location"] = location, ["slot"] = slot, ["name"] = value.ItemClass?.Name,
            ["count"] = stack.count, ["type"] = value.type, ["seed"] = value.Seed,
            ["meta"] = value.Meta, ["hasQuestCode"] = hasCode,
            ["questCodeMetadata"] = hasCode ? (int?)code : null,
            ["questPackage"] = questPackage, ["packageIndex"] = index,
            ["canPlaceInContainer"] = value.ItemClass != null && value.ItemClass.CanPlaceInContainer(),
            ["canDeposit"] = QuickStackItemPolicy.CanDeposit(value),
            ["quickStackCategory"] = QuickStackCategoryRegistry.Get(value.type).ToString(),
            ["payload"] = Convert.ToBase64String(bytes)
        };
    }
}
