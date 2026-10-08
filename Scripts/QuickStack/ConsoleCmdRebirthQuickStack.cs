using System;
using System.Collections.Generic;

#nullable disable

public sealed class ConsoleCmdRebirthQuickStack : ConsoleCmdAbstract
{
    public override string[] getCommands() { return new[] { "rbquickstack" }; }
    public override string getDescription() { return "Controls REBIRTH Quick Stack diagnostics and test requests."; }
    public override string getHelp() { return "rbquickstack debug on|off | uitrace on|off | ui | diagnose [itemName] | run [owned] | restock [owned] | preview [owned]"; }
    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        if (args.Count >= 2 && args[0].Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            QuickStackDiagnostics.Enabled = args[1].Equals("on", StringComparison.OrdinalIgnoreCase) || args[1].Equals("true", StringComparison.OrdinalIgnoreCase);
            Log.Out("[QuickStack] diagnostics=" + QuickStackDiagnostics.Enabled);
            return;
        }
        if (args.Count >= 2 && args[0].Equals("uitrace", StringComparison.OrdinalIgnoreCase))
        {
            QuickStackUiDiagnostics.Enabled = args[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                                               args[1].Equals("true", StringComparison.OrdinalIgnoreCase);
            Log.Out("[QuickStackUI] trace=" + QuickStackUiDiagnostics.Enabled);
            return;
        }
        if (args.Count >= 1 && args[0].Equals("ui", StringComparison.OrdinalIgnoreCase))
        {
            XUiC_RebirthLogistics.DumpDebugStateLocal();
            return;
        }
        if (args.Count >= 1 && args[0].Equals("diagnose", StringComparison.OrdinalIgnoreCase))
        {
            DiagnoseItem(args.Count > 1 ? args[1] : "medicalFirstAidKit");
            return;
        }
        if (args.Count >= 1 && args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            QuickStackService.RequestLocal(args.Count > 1 && args[1].Equals("owned", StringComparison.OrdinalIgnoreCase));
            return;
        }
        if (args.Count >= 1 && args[0].Equals("restock", StringComparison.OrdinalIgnoreCase))
        {
            QuickStackRestockService.RequestLocal(args.Count > 1 && args[1].Equals("owned", StringComparison.OrdinalIgnoreCase));
            return;
        }
        if (args.Count >= 1 && args[0].Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            QuickStackPreviewService.RequestLocal(args.Count > 1 && args[1].Equals("owned", StringComparison.OrdinalIgnoreCase));
            return;
        }
        Log.Out(getHelp());
    }


    private static void DiagnoseItem(string itemName)
    {
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (world == null || player == null)
            {
                Log.Out("[QuickStackDiag] no active world/local player");
                return;
            }

            ItemValue itemValue = ItemClass.GetItem(itemName, false);
            if (itemValue.IsEmpty() || itemValue.ItemClass == null)
            {
                Log.Out("[QuickStackDiag] item not found name='" + itemName + "'");
                return;
            }

            int itemType = itemValue.type;
            ItemClass itemClass = ItemClass.GetForId(itemType);
            QuickStackCategoryRegistry.Clear();
            QuickStackCategory category = QuickStackCategoryRegistry.Get(itemType);
            RebirthQuickStackMode mode = RebirthSandboxOptionManager.Current.QuickStack;

            Log.Out("[QuickStackDiag] ===== item routing diagnosis =====");
            Log.Out("[QuickStackDiag] item='" + itemName + "' type=" + itemType +
                " localized='" + (itemClass != null ? itemClass.GetLocalizedItemName() : "<null>") +
                "' category=" + category + " mode=" + mode);

            ItemStack[] bag = player.bag.ItemGrid.items;
            PackedBoolArray bagLocks = player.bag.LockedSlots;
            int bagStacks = 0;
            int bagCount = 0;
            for (int i = 0; bag != null && i < bag.Length; i++)
            {
                ItemStack stack = bag[i];
                if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType) continue;
                bool locked = bagLocks != null && i < bagLocks.Length && bagLocks[i];
                bagStacks++;
                bagCount += stack.count;
                Log.Out("[QuickStackDiag] backpack slot=" + i + " count=" + stack.count +
                    " protected=" + locked + (locked ? "  <-- excluded as a source by current code" : string.Empty));
            }
            if (bagStacks == 0)
                Log.Out("[QuickStackDiag] backpack contains no stack of this item type");
            else
                Log.Out("[QuickStackDiag] backpack summary stacks=" + bagStacks + " count=" + bagCount);

            List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(
                world, player.position, QuickStackService.Radius);
            Log.Out("[QuickStackDiag] nearby static candidate count=" + candidates.Count);

            ItemStack probe = new ItemStack(itemValue.Clone(), Math.Max(1, bagCount));
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3i pos = candidates[i].Position;
                TileEntity te = world.GetTileEntity(pos);
                TEFeatureStorage loot;
                string reason;
                bool canUse = QuickStackService.CanUse(world, player, te, false, out loot, out reason);
                QuickStackCategory[] assigned = QuickStackAcceptedCategoryRegistry.Get(pos);
                bool acceptsCategory = category != QuickStackCategory.None &&
                    QuickStackAcceptedCategoryRegistry.Accepts(pos, category);

                string displayName = "<unknown storage>";
                try
                {
                    StaticContainerResourceSource source = new StaticContainerResourceSource(world, pos);
                    displayName = source.DisplayName;
                }
                catch { }

                int emptyUnlocked = 0;
                int emptyProtected = 0;
                int exactStacks = 0;
                int partialStacks = 0;
                int categoryStacks = 0;
                if (canUse && loot != null && loot.ItemGrid.items != null)
                {
                    PackedBoolArray locks = loot.ItemGrid.SlotLocks;
                    for (int slotIndex = 0; slotIndex < loot.ItemGrid.items.Length; slotIndex++)
                    {
                        ItemStack stack = loot.ItemGrid.items[slotIndex];
                        bool locked = locks != null && slotIndex < locks.Length && locks[slotIndex];
                        if (stack == null || stack.IsEmpty())
                        {
                            if (locked) emptyProtected++; else emptyUnlocked++;
                            continue;
                        }
                        if (stack.itemValue.type == itemType) exactStacks++;
                        if (category != QuickStackCategory.None &&
                            QuickStackCategoryRegistry.Get(stack.itemValue.type) == category) categoryStacks++;
                        int amount;
                        if (stack.CanStackPartlyWith(probe, out amount) && amount > 0) partialStacks++;
                    }
                }

                Log.Out("[QuickStackDiag] target #" + i + " pos=" + pos + " name='" + displayName +
                    "' assigned=[" + string.Join(",", assigned) + "] acceptsItemCategory=" + acceptsCategory +
                    " canUse=" + canUse + (canUse ? string.Empty : " reason='" + reason + "'") +
                    " exactStacks=" + exactStacks + " categoryStacks=" + categoryStacks +
                    " partialStacks=" + partialStacks + " emptyUnlocked=" + emptyUnlocked +
                    " emptyProtected=" + emptyProtected);
            }

            QuickStackTransferPlanner.Plan regular = QuickStackTransferPlanner.Find(
                world, player, probe, false, mode);
            Log.Out("[QuickStackDiag] planner regular categoryPass=" + regular.CategoryPass +
                " category=" + regular.Category + " destinations=" + regular.Destinations.Count);
            for (int i = 0; i < regular.Destinations.Count; i++)
                Log.Out("[QuickStackDiag] planner regular destination #" + i + " pos=" +
                    regular.Destinations[i].Position + " category=" + regular.Destinations[i].Category);

            QuickStackTransferPlanner.Plan owned = QuickStackTransferPlanner.Find(
                world, player, probe, true, mode);
            Log.Out("[QuickStackDiag] planner owned categoryPass=" + owned.CategoryPass +
                " category=" + owned.Category + " destinations=" + owned.Destinations.Count);
            for (int i = 0; i < owned.Destinations.Count; i++)
                Log.Out("[QuickStackDiag] planner owned destination #" + i + " pos=" +
                    owned.Destinations[i].Position + " category=" + owned.Destinations[i].Category);

            Log.Out("[QuickStackDiag] ===== end diagnosis =====");
        }
        catch (Exception ex)
        {
            Log.Error("[QuickStackDiag] diagnose failed: " + ex);
        }
    }
}
