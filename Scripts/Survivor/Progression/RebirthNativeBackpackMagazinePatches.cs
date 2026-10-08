using System;
using HarmonyLib;

#nullable disable

// Native backpack magazines unlock vanilla equipment recipes, which are suppressed
// in REBIRTH progression. Refuse reading before events or debit; preserve the item.
[HarmonyPatch]
public static class RebirthNativeBackpackMagazinePatches
{
    public static bool ShouldSuppress(ItemStack stack)
    {
        return RebirthSurvivorMode.IsEnabledForCurrentWorld()
            && stack != null && !stack.IsEmpty()
            && string.Equals(stack.itemValue?.ItemClass?.GetItemName(),
                "backpackSkillMagazine", StringComparison.Ordinal);
    }

    private static void Explain(EntityAlive entity)
    {
        var local = entity as EntityPlayerLocal;
        if (local != null)
            GameManager.ShowTooltip(local, Localization.Get("xuiRebirthNativeBackpackMagazineUnavailable"));
    }

    private static void CancelPending(ItemActionData data)
    {
        var eat = data as ItemActionEat.MyInventoryData;
        if (eat != null) { eat.bEatingStarted = false; eat.bEatingFinished = true; }
        if (data?.invData?.holdingEntity != null) data.invData.holdingEntity.RightArmAnimationUse = false;
    }

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemActionEat.ExecuteAction))]
    [HarmonyPrefix]
    public static bool ExecutePrefix(ItemActionData _actionData, bool _bReleased)
    {
        if (!ShouldSuppress(_actionData?.invData?.itemStack)) return true;
        CancelPending(_actionData);
        if (_bReleased) Explain(_actionData.invData.holdingEntity);
        return false;
    }

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemActionEat.ExecuteInstantAction))]
    [HarmonyPrefix]
    public static bool InstantPrefix(EntityAlive ent, ItemStack stack, ref bool __result)
    {
        if (!ShouldSuppress(stack)) return true;
        Explain(ent);
        __result = false;
        return false;
    }

    // Both native Completed and OnHoldingUpdate reach consume. A mode switch after
    // vanilla admission must not run progression triggers or remove the magazine.
    [HarmonyPatch(typeof(ItemActionEat), "consume")]
    [HarmonyPrefix]
    public static bool ConsumePrefix(ItemActionData _actionData)
    {
        if (!ShouldSuppress(_actionData?.invData?.itemStack)) return true;
        var eat = _actionData as ItemActionEat.MyInventoryData;
        bool wasStarted = eat != null && eat.bEatingStarted;
        CancelPending(_actionData);
        if (wasStarted) Explain(_actionData.invData.holdingEntity);
        return false;
    }
}