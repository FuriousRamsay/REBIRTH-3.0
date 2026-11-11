using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

#nullable disable

/// <summary>
/// Direct action consumers that bypass XUiM_PlayerInventory in the 3.1 base game.
/// Server/listen actions perform fresh authoritative checks immediately before mutation.
/// Dedicated-client repair/upgrade actions are coordinated by the request/response
/// preflight layer in RemoteResourceClientTransactions.
/// </summary>
public static class RemoteResourceActionConsumerPatchInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Harmony harmony = new Harmony("rebirth.remote.resource.actions.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteUpgradeCanRemoveResourcePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteUpgradeRemoveResourcePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteRepairCanRemoveItemPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteRepairRemoveItemPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteRepairDebitResultPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemotePaintCheckAmmoPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemotePaintDecreaseAmmoPatch));
    }

    internal static void AfterCanRemoveUpgradeResource(
        ItemActionRepair __instance,
        ItemInventoryData data,
        BlockValue blockValue,
        ref bool __result)
    {
        if (__result || data == null || !TryBuildUpgradeRequirement(__instance, blockValue, out var requirement)) return;
        if (!RemoteResourceClientGrantContext.Active && data.holdingEntity is EntityPlayerLocal carried &&
            carried.inventory.GetItemCount(requirement.itemValue) + carried.bag.GetItemCount(requirement.itemValue) >= requirement.count)
        {
            __result = true;
            return;
        }
        if (!RemoteResourcesRuntimePolicy.Enabled) return;
        EntityPlayer player;
        if (TryGetServerPlayer(data, out player))
        {
            // Final block-action availability checks must not trust the short-lived UI
            // snapshot. Rebuild now so the immediately following vanilla removal runs
            // against the same authoritative source state on the game thread.
            RemoteResourceSnapshotCache.ForceRebuild(player);
            __result = RemoteResourceTransactions.HasItems(player, One(requirement));
            return;
        }
        EntityPlayerLocal local = data.holdingEntity as EntityPlayerLocal;
        if (local != null && RemoteResourceClientTransactionCoordinator.IsDedicatedClient)
            __result = RemoteResourceClientGrantContext.CanCover(local, requirement) ||
                RemoteResourceClientAvailability.HasItems(local, One(requirement), 1);
    }

    internal static bool BeforeRemoveUpgradeResource(
        ItemActionRepair __instance,
        ItemInventoryData data,
        BlockValue blockValue,
        ref bool __result)
    {
        if (data == null || !TryBuildUpgradeRequirement(__instance, blockValue, out var requirement)) return true;
        if (!RemoteResourceClientGrantContext.Active && data.holdingEntity is EntityPlayerLocal carried &&
            TryPayCarried(carried, requirement, out __result))
        {
            if (__result) carried.AddUIHarvestingItem(new ItemStack(requirement.itemValue, -requirement.count));
            return false;
        }
        if (!RemoteResourcesRuntimePolicy.Enabled) return true;
        EntityPlayer player;
        if (!TryGetServerPlayer(data, out player))
        {
            EntityPlayerLocal local = data.holdingEntity as EntityPlayerLocal;
            if (local != null && RemoteResourceClientTransactionCoordinator.IsDedicatedClient && RemoteResourceClientGrantContext.Active)
            {
                __result = RemoteResourceClientGrantContext.ConsumeLocalRemainder(local, One(requirement), 1, null);
                return false;
            }
            return true;
        }
        RemoteResourceTransactions.ConsumptionResult result =
            RemoteResourceTransactions.TryConsume(player, One(requirement));
        __result = result.Success;
        if (!result.Success)
            RemoteResourceDiagnostics.Write("upgrade transaction aborted: " + result.FailureReason);
        return false;
    }

    private static bool TryPayCarried(EntityPlayerLocal player, ItemStack requirement, out bool paid)
    {
        paid = false;
        var material = requirement.itemValue;
        int cost = requirement.count;
        if (RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return true;
        int belt = player.inventory.GetItemCount(material);
        if (belt >= cost)
        {
            paid = player.inventory.DecItem(material, cost) == cost;
            return true;
        }

        int bag = player.bag.GetItemCount(material);
        if (bag < cost - belt) return false;
        if (bag >= cost) belt = 0;
        if (belt == 0 || player.inventory.DecItem(material, belt) == belt)
            paid = player.bag.DecItem(material, cost - belt) == cost - belt;
        return true;
    }

    internal static void AfterCanRemoveRepairItem(
        ItemInventoryData _data,
        ItemStack _itemStack,
        ref bool __result)
    {
        if (__result || _itemStack == null || _itemStack.IsEmpty() || _data == null) return;
        if (!RemoteResourceClientGrantContext.Active && _data.holdingEntity is EntityPlayerLocal carried &&
            carried.inventory.GetItemCount(_itemStack.itemValue) + carried.bag.GetItemCount(_itemStack.itemValue) >= _itemStack.count)
        {
            __result = true;
            return;
        }
        if (!RemoteResourcesRuntimePolicy.Enabled) return;
        EntityPlayer player;
        if (TryGetServerPlayer(_data, out player))
        {
            RemoteResourceSnapshotCache.ForceRebuild(player);
            __result = RemoteResourceTransactions.HasItems(player, One(_itemStack));
            return;
        }
        EntityPlayerLocal local = _data.holdingEntity as EntityPlayerLocal;
        if (local != null && RemoteResourceClientTransactionCoordinator.IsDedicatedClient)
            __result = RemoteResourceClientGrantContext.CanCover(local, _itemStack) ||
                RemoteResourceClientAvailability.HasItems(local, One(_itemStack), 1);
    }

    internal static bool BeforeRemoveRepairItem(
        ItemInventoryData _data,
        ItemStack _itemStack,
        ref bool __result)
    {
        if (_itemStack == null || _itemStack.IsEmpty() || _data == null) return true;
        if (!RemoteResourceClientGrantContext.Active && _data.holdingEntity is EntityPlayerLocal carried &&
            TryPayCarried(carried, _itemStack, out __result)) return false;
        if (!RemoteResourcesRuntimePolicy.Enabled) return true;
        EntityPlayer player;
        if (!TryGetServerPlayer(_data, out player))
        {
            EntityPlayerLocal local = _data.holdingEntity as EntityPlayerLocal;
            if (local != null && RemoteResourceClientTransactionCoordinator.IsDedicatedClient && RemoteResourceClientGrantContext.Active)
            {
                __result = RemoteResourceClientGrantContext.ConsumeLocalRemainder(local, One(_itemStack), 1, null);
                return false;
            }
            return true;
        }
        RemoteResourceTransactions.ConsumptionResult result =
            RemoteResourceTransactions.TryConsume(player, One(_itemStack));
        __result = result.Success;
        if (!result.Success)
            RemoteResourceDiagnostics.Write("block repair transaction aborted: " + result.FailureReason);
        return false;
    }

    internal static void AfterPaintCheckAmmo(
        ItemActionTextureBlock __instance,
        ItemActionData _actionData,
        ref bool __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __result || __instance == null || __instance.InfiniteAmmo ||
            _actionData == null || !TryGetServerPlayer(_actionData.invData, out EntityPlayer player)) return;
        __result = RemoteResourceTransactions.CountRemote(player, __instance.currentMagazineItem) > 0;
    }

    internal static bool BeforePaintDecreaseAmmo(
        ItemActionTextureBlock __instance,
        ItemActionData _actionData,
        ref bool __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || _actionData == null || __instance.InfiniteAmmo ||
            GameStats.GetInt(EnumGameStats.GameModeId) == 2 ||
            GameStats.GetInt(EnumGameStats.GameModeId) == 8 ||
            !TryGetServerPlayer(_actionData.invData, out EntityPlayer player)) return true;

        ItemActionTextureBlock.ItemActionTextureBlockData data =
            _actionData as ItemActionTextureBlock.ItemActionTextureBlockData;
        if (data == null || data.idx < 0 || data.idx >= BlockTextureData.list.Length ||
            BlockTextureData.list[data.idx] == null) return true;

        int paintCost = (int)BlockTextureData.list[data.idx].PaintCost;
        if (paintCost <= 0)
        {
            __result = true;
            return false;
        }

        ItemStack requirement = new ItemStack(__instance.currentMagazineItem.Clone(), paintCost);
        RemoteResourceTransactions.ConsumptionResult result =
            RemoteResourceTransactions.TryConsume(player, One(requirement));
        __result = result.Success;
        if (!result.Success)
            RemoteResourceDiagnostics.Write("paint transaction aborted: " + result.FailureReason);
        return false;
    }

    internal static bool TryBuildUpgradeRequirement(
        ItemActionRepair action,
        BlockValue blockValue,
        out ItemStack requirement)
    {
        Block block = action == null ? null : blockValue.Block;
        return TryBuildUpgradeRequirement(action, block, requireHitCount: true, out requirement);
    }

    // Only for callers that just parsed UpgradeHitCount on this exact definition.
    internal static bool TryBuildUpgradeRequirementAfterHitCountCheck(
        ItemActionRepair action,
        Block block,
        out ItemStack requirement)
    {
        return TryBuildUpgradeRequirement(action, block, requireHitCount: false, out requirement);
    }

    private static bool TryBuildUpgradeRequirement(
        ItemActionRepair action,
        Block block,
        bool requireHitCount,
        out ItemStack requirement)
    {
        requirement = ItemStack.Empty.Clone();
        if (action == null || block == null) return false;
        string itemName = action.GetUpgradeItemName(block);
        if (string.IsNullOrEmpty(itemName)) return false;
        if ((action.allowedUpgradeItems.Length > 0 &&
             !action.allowedUpgradeItems.ContainsCaseInsensitive(itemName)) ||
            (action.restrictedUpgradeItems.Length > 0 &&
             action.restrictedUpgradeItems.ContainsCaseInsensitive(itemName))) return false;
        if (requireHitCount && !int.TryParse(
            block.Properties.GetString("UpgradeBlock", "UpgradeHitCount"),
            out _)) return false;
        int count;
        if (!int.TryParse(
            block.Properties.GetString(Block.PropUpgradeBlockClass, Block.PropUpgradeBlockItemCount),
            out count) || count <= 0) return false;
        ItemValue itemValue = ItemClass.GetItem(itemName);
        if (itemValue == null || itemValue.IsEmpty()) return false;
        requirement = new ItemStack(itemValue, count);
        return true;
    }

    private static bool TryGetServerPlayer(ItemInventoryData inventoryData, out EntityPlayer player)
    {
        player = inventoryData != null ? inventoryData.holdingEntity as EntityPlayer : null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return player != null && connection != null && connection.IsServer;
    }

    private static IList<ItemStack> One(ItemStack stack)
    {
        return new ItemStack[] { stack };
    }
}
[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.CanRemoveRequiredResource))]
internal static class RemoteUpgradeCanRemoveResourcePatch
{
    private static void Postfix(ItemActionRepair __instance, ItemInventoryData data, BlockValue blockValue, ref bool __result)
    { RemoteResourceActionConsumerPatchInstaller.AfterCanRemoveUpgradeResource(__instance, data, blockValue, ref __result); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.RemoveRequiredResource))]
internal static class RemoteUpgradeRemoveResourcePatch
{
    private static bool Prefix(ItemActionRepair __instance, ItemInventoryData data, BlockValue blockValue, ref bool __result)
    { return RemoteResourceActionConsumerPatchInstaller.BeforeRemoveUpgradeResource(__instance, data, blockValue, ref __result); }
}

[HarmonyPatch(typeof(ItemActionRepair), "canRemoveRequiredItem")]
internal static class RemoteRepairCanRemoveItemPatch
{
    private static void Postfix(ItemInventoryData _data, ItemStack _itemStack, ref bool __result)
    { RemoteResourceActionConsumerPatchInstaller.AfterCanRemoveRepairItem(_data, _itemStack, ref __result); }
}

[HarmonyPatch(typeof(ItemActionRepair), "removeRequiredItem")]
internal static class RemoteRepairRemoveItemPatch
{
    private static bool Prefix(ItemInventoryData _data, ItemStack _itemStack, ref bool __result)
    { return RemoteResourceActionConsumerPatchInstaller.BeforeRemoveRepairItem(_data, _itemStack, ref __result); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.ExecuteAction))]
internal static class RemoteRepairDebitResultPatch
{
    private static bool ContinueAfterDebit(bool paid, float[] credit, float prior)
    {
        if (paid || credit.Length != 1) return true;
        credit[0] = prior;
        return false;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var code = instructions.ToList();
        var debit = AccessTools.DeclaredMethod(typeof(ItemActionRepair), nameof(ItemActionRepair.removeRequiredItem));
        var credit = AccessTools.Field(typeof(ItemActionRepair.InventoryDataRepair), nameof(ItemActionRepair.InventoryDataRepair.lastRepairItemsPercents));
        var finish = AccessTools.DeclaredMethod(typeof(RemoteRepairDebitResultPatch), nameof(ContinueAfterDebit));
        if (debit == null || credit == null || finish == null || code.Count(item => item.Calls(debit)) != 1) return code;
        int call = code.FindIndex(item => item.Calls(debit));
        if (call < 14 || call + 2 >= code.Count || code[call + 1].opcode != OpCodes.Pop || code[call + 1].blocks.Count != 0)
            return code;

        // The native due branch adds the new credit and subtracts the cost
        // immediately before calling removeRequiredItem.
        int subtract = code.FindLastIndex(call - 1, item => item.opcode == OpCodes.Ldelema && Equals(item.operand, typeof(float)));
        int add = subtract - 10;
        if (add < 3 || subtract + 5 >= call || !Equals(code[add - 2].operand, credit) ||
            !Equals(code[subtract - 2].operand, credit) || code[add].opcode != OpCodes.Ldelema ||
            !Equals(code[add].operand, typeof(float)) || code[add + 5].opcode != OpCodes.Add ||
            code[add + 6].opcode != OpCodes.Stind_R4 || code[subtract + 4].opcode != OpCodes.Sub ||
            code[subtract + 5].opcode != OpCodes.Stind_R4 || code[add + 1].labels.Count != 0 || code[add + 1].blocks.Count != 0)
            return code;

        var prior = generator.DeclareLocal(typeof(float));
        var resume = generator.DefineLabel();
        var loadData = code[add - 3];
        var afterCall = code[call + 1];
        afterCall.opcode = loadData.opcode;
        afterCall.operand = loadData.operand;
        code[call + 2].labels.Add(resume);
        code.InsertRange(call + 2, new[]
        {
            new CodeInstruction(OpCodes.Ldfld, credit), new CodeInstruction(OpCodes.Ldloc, prior),
            new CodeInstruction(OpCodes.Call, finish), new CodeInstruction(OpCodes.Brtrue, resume), new CodeInstruction(OpCodes.Ret)
        });
        code.InsertRange(add + 1, new[]
        {
            new CodeInstruction(OpCodes.Dup), new CodeInstruction(OpCodes.Ldind_R4), new CodeInstruction(OpCodes.Stloc, prior)
        });
        return code;
    }
}

[HarmonyPatch(typeof(ItemActionTextureBlock), "checkAmmo")]
internal static class RemotePaintCheckAmmoPatch
{
    private static void Postfix(ItemActionTextureBlock __instance, ItemActionData _actionData, ref bool __result)
    { RemoteResourceActionConsumerPatchInstaller.AfterPaintCheckAmmo(__instance, _actionData, ref __result); }
}

[HarmonyPatch(typeof(ItemActionTextureBlock), "decreaseAmmo")]
internal static class RemotePaintDecreaseAmmoPatch
{
    private static bool Prefix(ItemActionTextureBlock __instance, ItemActionData _actionData, ref bool __result)
    { return RemoteResourceActionConsumerPatchInstaller.BeforePaintDecreaseAmmo(__instance, _actionData, ref __result); }
}

