using HarmonyLib;

// Preserve native one-package payment, but choose by quest identity rather
// than DecItem's type-only selection. Never remove another quest's package.
[HarmonyPatch(typeof(ObjectiveBaseFetchContainer), "RemoveFetchItems")]
public static class RebirthFetchRemovalIdentity
{
    [HarmonyPrefix]
    public static bool Prefix(ObjectiveBaseFetchContainer __instance)
    {
        if (__instance == null || __instance.CurrentValue != 3) return true;
        if (__instance.expectedItemClass == null) __instance.SetupExpectedItem();
        if (!(__instance.expectedItemClass is ItemClassQuest)) return true;
        var player = __instance.OwnerQuest?.OwnerJournal?.OwnerPlayer;
        if (player == null) return false;
        RebirthFetchQuestIdentity.Bind(__instance);
        var ui = LocalPlayerUI.GetUIForPlayer(player)?.xui;
        var bag = player.bag?.ItemGrid?.items;
        int index = Find(bag, __instance);
        ItemStack removed = null;
        if (index >= 0)
        {
            removed = new ItemStack(bag[index].itemValue.Clone(), 1);
            player.bag.SetSlot(index, Remainder(bag[index]));
        }
        else
        {
            var belt = player.inventory?.ItemGrid?.items;
            index = Find(belt, __instance);
            if (index >= 0)
            {
                removed = new ItemStack(belt[index].itemValue.Clone(), 1);
                player.inventory.SetItem(index, Remainder(belt[index]));
            }
        }
        if (removed != null) ui?.CollectedItemList?.AddRemoveItemQueueEntry(removed);
        return false;
    }

    internal static int Find(ItemStack[] slots, ObjectiveBaseFetchContainer objective)
    {
        if (slots == null) return -1;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty() &&
                RebirthFetchQuestIdentity.Matches(slots[i].itemValue, objective)) return i;
        return -1;
    }

    private static ItemStack Remainder(ItemStack stack)
        => stack.count > 1 ? new ItemStack(stack.itemValue.Clone(), stack.count - 1) : ItemStack.Empty.Clone();
}
