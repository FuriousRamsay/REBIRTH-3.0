using System.Runtime.CompilerServices;
using UnityEngine;

// Select real native equipment; never materialize a weapon from a semantic item name.
internal static class RebirthHumanWeaponSelection
{
    private sealed class State { internal float NextCheck, NextSwitch; }
    private static readonly ConditionalWeakTable<EntityRebirthHumanoidNPC, State> States =
        new ConditionalWeakTable<EntityRebirthHumanoidNPC, State>();

    internal static void Tick(EntityRebirthNPC npc)
    {
        if (!RebirthHumanAiPolicy.Ready(npc, true) || !(npc is EntityRebirthHumanoidNPC human)) return;
        var target = npc.GetAttackTarget();
        var inventory = npc.inventory;
        if (target == null || target.IsDead() || target.world != npc.world || inventory == null) return;
        var state = States.GetOrCreateValue(human);
        if (Time.time < state.NextCheck) return;
        state.NextCheck = Time.time + .25f;
        if (Time.time < state.NextSwitch || npc.IsBreakingBlocks || npc.IsBreakingDoors ||
            inventory.IsHandSwitching() || inventory.IsHoldingItemActionRunning()) return;

        int held = inventory.holdingItemIdx, melee = -1, ranged = -1, empty = -1;
        var slots = inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length; i++)
        {
            var stack = slots[i];
            if (stack == null || stack.IsEmpty()) { if(empty < 0)empty=i; continue; }
            var value = stack?.itemValue;
            if (stack == null || stack.IsEmpty() || !Usable(value)) continue;
            var action = value.ItemClass.Actions[0];
            if (action is ItemActionMelee || action is ItemActionDynamicMelee)
            { if (melee < 0 || i == held) melee = i; }
            else if (action is ItemActionRanged gun && !(action is ItemActionCatapult) && !(action is ItemActionLauncher) &&
                HasAmmo(npc, value, gun))
            { if (ranged < 0 || i == held) ranged = i; }
        }

        float distance = npc.GetDistance(target);
        int selected = RebirthHumanWeaponRules.Choose(distance, held, melee, ranged);
        // Native empty-hand fallback only when no usable equipped weapon remains.
        if (selected < 0) selected = empty;
        if (selected < 0 || selected == held) return;
        inventory.SetHoldingItemIdx(selected); // native holster, action data, model and held-item synchronization
        state.NextSwitch = Time.time + 1.5f;
    }

    private static bool Usable(ItemValue value) => value != null && !value.IsEmpty() &&
        value.ItemClass?.Actions != null && value.ItemClass.Actions.Length > 0 &&
        (value.MaxUseTimes <= 0 || value.UseTimes < value.MaxUseTimes);

    private static bool HasAmmo(EntityRebirthNPC npc, ItemValue value, ItemActionRanged action)
    {
        if (value.Meta > 0) return true;
        var names = action.MagazineItemNames;
        if (names == null || value.SelectedAmmoTypeIndex >= names.Length) return false;
        var ammo = ItemClass.GetItem(names[value.SelectedAmmoTypeIndex], false);
        return ammo != null && !ammo.IsEmpty() && (npc.inventory.GetItemCount(ammo) > 0 ||
            npc.bag != null && npc.bag.GetItemCount(ammo) > 0);
    }
}
