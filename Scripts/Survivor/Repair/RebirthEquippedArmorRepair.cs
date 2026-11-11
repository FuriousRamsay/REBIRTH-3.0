using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

[HarmonyPatch(typeof(ItemActionEntryRepair), nameof(ItemActionEntryRepair.OnActivated))]
internal static class RebirthEquippedArmorRepair
{
    private sealed class Origin
    {
        internal Equipment Equipment;
        internal int Slot;
    }

    private static readonly ConditionalWeakTable<ItemValue, Origin> Origins = new();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var queue = AccessTools.Method(typeof(XUiC_CraftingWindowGroup), nameof(XUiC_CraftingWindowGroup.AddRepairItemToQueue),
            new[] { typeof(float), typeof(ItemValue), typeof(int) });
        return PassController(instructions, queue, AccessTools.Method(typeof(RebirthEquippedArmorRepair), nameof(QueueRepair)));
    }

    private static bool QueueRepair(XUiC_CraftingWindowGroup group, float time, ItemValue item, int amount, ItemActionEntryRepair action)
    {
        if (item.ItemClass is ItemClassArmor && action.ItemController is XUiC_EquipmentStack source &&
            source.xui?.playerUI?.entityPlayer?.equipment is Equipment equipment)
            Origins.Add(item, new Origin { Equipment = equipment, Slot = source.SlotNumber });

        return group.AddRepairItemToQueue(time, item, amount);
    }

    internal static void RememberQueueCopy(ItemValue incoming, ItemValue queued)
    {
        // The native queue copies the item on admission and moves that copy when the queue advances.
        if (incoming != null && queued != null && Origins.TryGetValue(incoming, out var origin))
            Origins.GetValue(queued, _ => origin);
    }

    internal static bool TryReturn(XUiC_RecipeStack card, ItemStack offered)
    {
        if (offered?.count != 1 || offered.itemValue.ItemClass is not ItemClassArmor ||
            card.originalItem == null || offered.itemValue.type != card.originalItem.type ||
            !Origins.TryGetValue(card.originalItem, out var origin))
            return false;

        var ui = card.xui;
        var equipment = ui?.playerUI?.entityPlayer?.equipment;
        int slot = origin.Slot;
        if (!ReferenceEquals(equipment, origin.Equipment) || slot < 0 || slot >= equipment.GetSlotCount() ||
            !equipment.GetSlotItem(slot).IsEmpty())
            return false;

        equipment.SetSlotItem(slot, offered.itemValue);
        offered.count = 0;
        ui.PlayerEquipment.RefreshEquipment();
        return true;
    }

    internal static IEnumerable<CodeInstruction> PassController(IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement)
    {
        var code = new List<CodeInstruction>(instructions);
        var matches = code.FindAll(instruction => instruction.Calls(original));
        if (matches.Count != 1) return code;

        var call = matches[0];
        var controller = new CodeInstruction(OpCodes.Ldarg_0);
        controller.labels.AddRange(call.labels);
        controller.blocks.AddRange(call.blocks);
        call.labels.Clear();
        call.blocks.Clear();
        code.Insert(code.IndexOf(call), controller);
        call.opcode = OpCodes.Call;
        call.operand = replacement;
        return code;
    }
}
