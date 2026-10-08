using UnityEngine.Scripting;
using HarmonyLib;
using System;

#nullable disable

/// <summary>
/// REBIRTH metabolism intake gateway. It deliberately reuses ItemActionEat's start/animation
/// flow, but replaces the final consume step so food and fluid enter the stomach instead of
/// directly modifying Food/Water.
/// </summary>
[Preserve]
public class ItemActionConsumeMetabolismRebirth : ItemActionEat
{
    public override bool ExecuteInstantAction(EntityAlive ent, ItemStack stack, bool isHeldItem, XUiC_ItemStack stackController)
    {
        EntityPlayer player = ent as EntityPlayer;
        RebirthConsumableDefinition definition;
        if (player == null || stack == null || stack.IsEmpty() || !RebirthConsumableResolver.TryResolve(stack.itemValue, out definition))
            return base.ExecuteInstantAction(ent, stack, isHeldItem, stackController);

        // Catalogue entries are templates, not inventory custody. Materialize one item
        // through the native creative inventory path before authoritative consumption.
        if (stackController is XUiC_Creative2Stack && stackController.xui?.PlayerInventory != null)
        {
            ItemStack source = stack.Clone();
            source.count = 1;
            if (!stackController.xui.PlayerInventory.AddItem(source, true))
            {
                GameManager.ShowTooltip(player as EntityPlayerLocal, "Make room in your backpack to use this item.");
                return true;
            }
        }
        Dispatch(player, stack.itemValue, false);
        return true;
    }

    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if (actionData?.invData?.itemStack == null) return;
        if (!RebirthConsumableResolver.TryResolve(actionData.invData.itemStack.itemValue, out var definition))
        {
            base.OnHoldingUpdate(actionData);
            return;
        }
        if (PercentDone(actionData) >= 1f) CompleteConsumption(actionData);
    }

    // Both the native ConsumeComplete animation event and the native time-based fallback
    // enter here. Claim bEatingStarted BEFORE dispatch; a second completion is a no-op.
    // The server's existing ConsumeAt path owns volume, stomach capacity, empty replacement,
    // inventory mutation and food effects. No second vanilla whole-item consumption follows.
    internal void CompleteConsumption(ItemActionData actionData)
    {
        var data = actionData as ItemActionEat.MyInventoryData;
        if (data == null || !data.bEatingStarted) return;
        data.bEatingStarted = false;
        if (actionData.invData?.holdingEntity is EntityPlayer player &&
            actionData.invData.itemStack != null && !actionData.invData.itemStack.IsEmpty())
            Dispatch(player, actionData.invData.itemStack.itemValue, false);
    }

    public override void StopHolding(ItemActionData data)
    {
        base.StopHolding(data);
        ItemActionEat.MyInventoryData eatData = data as ItemActionEat.MyInventoryData;
        if (eatData != null)
            eatData.bEatingStarted = false;
    }

    private static void Dispatch(EntityPlayer player, ItemValue itemValue, bool autoSip)
    {
        if (player == null || itemValue == null)
            return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || connection.IsServer)
        {
            RebirthConsumeResult result = RebirthMetabolismService.ConsumeMatchingInventoryItem(player, itemValue.type, itemValue.Seed, autoSip);
            RebirthMetabolismUiFeedback.Receive(result);
            return;
        }

        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeRequest>()
            .Setup(player.entityId, itemValue, autoSip));
    }
}

// ItemActionEat.Completed calls this private/non-virtual method directly in 3.2 b10.
// Intercept only the REBIRTH action; medical items and all other native actions retain
// their original consume/effect behavior. The native start/holster/animation path stays intact.
[HarmonyPatch(typeof(ItemActionEat), "consume", new Type[] { typeof(ItemActionData) })]
public static class RebirthMetabolismAnimatedCompletionPatch
{
    public static bool Prefix(ItemActionEat __instance, ItemActionData __0)
    {
        var action = __instance as ItemActionConsumeMetabolismRebirth;
        if (action == null) return true;
        if (__0?.invData?.itemStack == null) return false;
        if (!RebirthConsumableResolver.TryResolve(__0.invData.itemStack.itemValue, out var definition))
            return true;
        action.CompleteConsumption(__0);
        return false;
    }
}

// Native animated Use has no source grid for a creative catalogue template.
// Route that template through the instant gateway; normal inventory Use stays animated.
[HarmonyPatch(typeof(ItemActionEntryUse), "OnActivated")]
internal static class RebirthCreativeConsumableUsePatch
{
    private static bool Prefix(ItemActionEntryUse __instance)
    {
        var controller = __instance.ItemController as XUiC_Creative2Stack;
        if (controller == null || controller.ItemStack == null || controller.ItemStack.IsEmpty()) return true;
        var item = controller.ItemStack.itemValue;
        var action = item.ItemClass.Actions[0] as ItemActionConsumeMetabolismRebirth;
        if (action == null) return true;
        var player = controller.xui?.playerUI?.entityPlayer;
        if (player == null || controller.xui.IsUsingItemActionEntryUse || player.AttachedToEntity ||
            controller.xui.PlayerInventory.Toolbelt.IsHoldingItemActionRunning()) return false;
        if (!item.ItemClass.CanExecuteAction(0, player, item)) return false;
        action.ExecuteInstantAction(player, controller.ItemStack, false, controller);
        return false;
    }
}