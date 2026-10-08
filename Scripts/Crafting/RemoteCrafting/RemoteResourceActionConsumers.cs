using HarmonyLib;
using System;
using System.Collections.Generic;

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
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemotePaintCheckAmmoPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemotePaintDecreaseAmmoPatch));
    }

    internal static void AfterCanRemoveUpgradeResource(
        ItemActionRepair __instance,
        ItemInventoryData data,
        BlockValue blockValue,
        ref bool __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __result || data == null) return;
        ItemStack requirement;
        if (!TryBuildUpgradeRequirement(__instance, blockValue, out requirement)) return;
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
        if (!RemoteResourcesRuntimePolicy.Enabled || data == null) return true;
        ItemStack requirement;
        if (!TryBuildUpgradeRequirement(__instance, blockValue, out requirement)) return true;
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

    internal static void AfterCanRemoveRepairItem(
        ItemInventoryData _data,
        ItemStack _itemStack,
        ref bool __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __result || _itemStack == null || _itemStack.IsEmpty() || _data == null) return;
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
        if (!RemoteResourcesRuntimePolicy.Enabled || _itemStack == null || _itemStack.IsEmpty() || _data == null) return true;
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
        requirement = ItemStack.Empty.Clone();
        if (action == null || blockValue.Block == null) return false;
        string itemName = action.GetUpgradeItemName(blockValue.Block);
        if (string.IsNullOrEmpty(itemName)) return false;
        if ((action.allowedUpgradeItems.Length > 0 &&
             !action.allowedUpgradeItems.ContainsCaseInsensitive(itemName)) ||
            (action.restrictedUpgradeItems.Length > 0 &&
             action.restrictedUpgradeItems.ContainsCaseInsensitive(itemName))) return false;
        int hitCount;
        if (!int.TryParse(
            blockValue.Block.Properties.GetString("UpgradeBlock", "UpgradeHitCount"),
            out hitCount)) return false;
        int count;
        if (!int.TryParse(
            blockValue.Block.Properties.GetString(Block.PropUpgradeBlockClass, Block.PropUpgradeBlockItemCount),
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

