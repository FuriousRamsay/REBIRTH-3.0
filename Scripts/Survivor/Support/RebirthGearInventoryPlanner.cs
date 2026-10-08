using System;
using Stack = RebirthGearInventoryPlan.Stack;

// Computes custody only. Caller must authorize gear/requirements and persist the
// complete transaction before any owner inventory application or recovery spawn.
public static class RebirthGearInventoryPlanner
{
    // sourceIndex == -1 means unequip. Other negative indexes are invalid.
    public static bool TryPlan(RebirthGearInventorySnapshot snapshot, Stack equipped,
        bool sourceIsBag, int sourceIndex, int targetBagSlots, int targetBeltSlots,
        out RebirthGearInventoryPlan plan)
    {
        plan = null;
        if (snapshot == null || equipped == null || sourceIndex < -1
            || equipped.Count < 0 || equipped.Count > 1
            || targetBagSlots < 52 || targetBagSlots > 169
            || targetBeltSlots < 4 || targetBeltSlots > 18) return false;
        if (sourceIndex >= 0 && !snapshot.IsUsableSource(sourceIsBag, sourceIndex)) return false;
        if (sourceIndex == -1 && equipped.Count != 1) return false;
        var next = new RebirthGearInventoryPlan {
            BagSlotsBefore = snapshot.Bag.Length, BagSlotsAfter = targetBagSlots,
            BeltSlotsBefore = snapshot.Belt.Length, BeltSlotsAfter = targetBeltSlots,
            GearBefore = Copy(equipped)
        };
        Stack[] bag = CopySlots(snapshot.Bag, Math.Max(snapshot.Bag.Length, targetBagSlots));
        Stack[] belt = CopySlots(snapshot.Belt, Math.Max(snapshot.Belt.Length, targetBeltSlots));
        if (sourceIndex >= 0)
        {
            Stack[] source = sourceIsBag ? bag : belt;
            if (source[sourceIndex].Count <= 0) return false;
            next.GearAfter = new Stack { ItemData = source[sourceIndex].ItemData, Count = 1 };
            if (--source[sourceIndex].Count == 0) source[sourceIndex] = new Stack();
        }
        ReleaseTail(bag, targetBagSlots, true, next);
        ReleaseTail(belt, targetBeltSlots, false, next);
        if (equipped.Count == 1)
        {
            int destination = -1;
            for (int i = 0; i < targetBagSlots; i++)
                if (bag[i].Count == 0) { destination = i; break; }
            if (destination >= 0) bag[destination] = Copy(equipped);
            else next.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry {
                Origin = RebirthGearInventoryPlan.RecoveryOrigin.DisplacedGear, Item = Copy(equipped)
            });
        }
        RecordChanges(snapshot.Bag, bag, true, next);
        RecordChanges(snapshot.Belt, belt, false, next);
        if (!next.MatchesBefore(snapshot.Bag, snapshot.Belt, equipped,
            snapshot.Bag.Length, snapshot.Belt.Length)) return false;
        plan = next;
        return true;
    }

    private static Stack Copy(Stack stack)
    {
        return new Stack { ItemData = stack.ItemData, Count = stack.Count };
    }

    private static Stack[] CopySlots(Stack[] source, int length)
    {
        var copy = new Stack[length];
        for (int i = 0; i < length; i++) copy[i] = i < source.Length ? Copy(source[i]) : new Stack();
        return copy;
    }

    private static void ReleaseTail(Stack[] slots, int retained, bool isBag, RebirthGearInventoryPlan plan)
    {
        for (int i = retained; i < slots.Length; i++)
        {
            if (slots[i].Count > 0) plan.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry {
                Origin = isBag ? RebirthGearInventoryPlan.RecoveryOrigin.Backpack : RebirthGearInventoryPlan.RecoveryOrigin.Belt,
                SourceIndex = i, Item = Copy(slots[i])
            });
            slots[i] = new Stack();
        }
    }

    private static void RecordChanges(Stack[] before, Stack[] after, bool isBag, RebirthGearInventoryPlan plan)
    {
        for (int i = 0; i < after.Length; i++)
        {
            Stack prior = i < before.Length ? before[i] : new Stack();
            if (prior.Count == after[i].Count && string.Equals(prior.ItemData, after[i].ItemData, StringComparison.Ordinal)) continue;
            plan.Changes.Add(new RebirthGearInventoryPlan.Change {
                IsBag = isBag, Index = i, Before = Copy(prior), After = Copy(after[i])
            });
        }
    }
}
