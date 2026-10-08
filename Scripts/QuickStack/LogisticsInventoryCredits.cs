using System.Collections.Generic;
using UnityEngine;

#nullable disable

// Credit-only withdrawals send their added items instead of
// replacing a whole backpack that may have changed while the response was in flight.
internal static class LogisticsInventoryCredits
{
    internal static bool UsesReceipt(QuickStackRadialAction action)
    {
        return action == QuickStackRadialAction.CompanionInventoryPull ||
            action == QuickStackRadialAction.PullVehicleCredits || action == QuickStackRadialAction.PullDroneCredits;
    }

    internal static QuickStackRadialAction RequestAction(QuickStackRadialAction action)
    {
        if (action == QuickStackRadialAction.PullVehicle) return QuickStackRadialAction.PullVehicleCredits;
        if (action == QuickStackRadialAction.PullDrone) return QuickStackRadialAction.PullDroneCredits;
        return action;
    }

    internal static QuickStackRadialAction ExecutionAction(QuickStackRadialAction action)
    {
        if (action == QuickStackRadialAction.PullVehicleCredits) return QuickStackRadialAction.PullVehicle;
        if (action == QuickStackRadialAction.PullDroneCredits) return QuickStackRadialAction.PullDrone;
        return action;
    }

    internal static ItemStack[] Capture(IList<ItemStack> before, IList<ItemStack> after)
    {
        var remaining = new List<ItemStack>();
        for (int i = 0; after != null && i < after.Count; i++)
        {
            ItemStack stack = after[i];
            if (stack != null && !stack.IsEmpty() && stack.count > 0) remaining.Add(stack.Clone());
        }
        for (int i = 0; before != null && i < before.Count; i++)
        {
            ItemStack previous = before[i];
            if (previous == null || previous.IsEmpty() || previous.count <= 0) continue;
            int accounted = previous.count;
            for (int j = 0; j < remaining.Count && accounted > 0; j++)
            {
                ItemStack candidate = remaining[j];
                if (candidate.count <= 0 || !previous.itemValue.Equals(candidate.itemValue)) continue;
                int count = System.Math.Min(accounted, candidate.count);
                candidate.count -= count;
                accounted -= count;
            }
        }
        remaining.RemoveAll(stack => stack.count <= 0);
        return remaining.ToArray();
    }

    internal static void Apply(ItemStack[] credits)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player?.bag == null || credits == null) return;
        foreach (ItemStack credit in credits)
        {
            if (credit == null || credit.IsEmpty() || credit.count <= 0) continue;
            ItemStack remaining = credit.Clone();
            LogisticsTransferService.MoveIntoBag(player, remaining);
            if (remaining.count > 0)
                GameManager.Instance.ItemDropServer(remaining, player.position, Vector3.zero,
                    player.entityId, 120f, false);
        }
    }
}
