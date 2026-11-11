using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

internal static partial class RebirthRepairOriginalCorrection
{
    // The owner sets OutputInvoking only after saving the exact invocation.
    // A known native refusal may still be waiting for its outcome CAS.
    internal static bool HoldsCachedOutput(XUiC_RecipeStack card)
    {
        if (card.Owner is not XUiC_RebirthCraftingQueue ||
            card.windowGroup?.Controller is not XUiC_RebirthPersonalCrafting ||
            !ThreadManager.IsMainThread() || !Cards.TryGetValue(card, out var client) ||
            !HasIndependentPayment(client.Receipt) || !client.Receipt.OutputInvoking ||
            !Clients.TryGetValue(client.Receipt.Operation, out var active) || !ReferenceEquals(active, client))
            return false;

        return ReferenceEquals(client.NativeCard, card) &&
            ReferenceEquals(card.originalItem, client.NativeQueueItem) &&
            ReferenceEquals(card.GetRecipe(), client.NativeRecipe) &&
            SameOperation(card.originalItem, client.Receipt.Operation, OriginalKey) &&
            ReferenceEquals(card.xui?.playerUI?.entityPlayer, client.Player) &&
            ReferenceEquals(GameManager.Instance?.World, client.World) &&
            ReferenceEquals(client.World.GetPrimaryPlayer(), client.Player) &&
            ReferenceEquals(client.Player.world, client.World) &&
            ReferenceEquals(client.World.GetEntity(client.Player.entityId), client.Player) &&
            GameIO.GetSaveGameDir() == client.Directory;
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.Update), new[] { typeof(float) })]
internal static class RebirthRepairOriginalCachedOutput
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var native = AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.AddItemNoPartial),
            new[] { typeof(ItemStack), typeof(bool) });
        var guarded = AccessTools.Method(typeof(RebirthRepairOriginalCachedOutput), nameof(InsertCached));
        int replacements = 0;
        foreach (var instruction in instructions)
        {
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !Equals(instruction.operand, native))
            {
                yield return instruction;
                continue;
            }

            var card = new CodeInstruction(OpCodes.Ldarg_0);
            card.labels.AddRange(instruction.labels);
            card.blocks.AddRange(instruction.blocks);
            yield return card;
            yield return new CodeInstruction(OpCodes.Call, guarded);
            replacements++;
        }
        if (replacements != 1)
            throw new InvalidOperationException("Repair cached output requires one native inventory insert in RecipeStack.Update.");
    }

    private static bool InsertCached(XUiM_PlayerInventory inventory, ItemStack offered, bool playCollectSound,
        XUiC_RecipeStack card)
    {
        // A refusal leaves the offer unchanged so Update keeps the original card
        // and runs the native refresh path without treating it as a partial add.
        return !RebirthRepairOriginalCorrection.HoldsCachedOutput(card) &&
            (RebirthEquippedArmorRepair.TryReturn(card, offered) || inventory.AddItemNoPartial(offered, playCollectSound));
    }
}
