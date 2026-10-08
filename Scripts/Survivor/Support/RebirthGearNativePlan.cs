using System.Collections.Generic;

// Decode the complete plan before calling any native setter. This preparation does
// not make subsequent native writes atomic and must be followed by preimage checks.
public sealed class RebirthGearNativePlan
{
    public sealed class Change
    {
        public bool IsBag;
        public int Index;
        public ItemStack Before, After;
    }
    public ItemStack GearBefore, GearAfter;
    public readonly List<Change> Changes = new List<Change>();
    public readonly List<ItemStack> Recovery = new List<ItemStack>();

    public static bool TryDecode(RebirthGearInventoryPlan plan, out RebirthGearNativePlan decoded)
    {
        decoded = null;
        if (plan == null || !plan.IsConserved()) return false;
        var candidate = new RebirthGearNativePlan();
        if (!Stack(plan.GearBefore, out candidate.GearBefore) || !Stack(plan.GearAfter, out candidate.GearAfter)) return false;
        foreach (var entry in plan.Changes)
        {
            var change = new Change { IsBag = entry.IsBag, Index = entry.Index };
            if (!Stack(entry.Before, out change.Before) || !Stack(entry.After, out change.After)) return false;
            candidate.Changes.Add(change);
        }
        foreach (var entry in plan.Recovery)
        {
            ItemStack item;
            if (!Stack(entry.Item, out item)) return false;
            candidate.Recovery.Add(item);
        }
        decoded = candidate;
        return true;
    }

    private static bool Stack(RebirthGearInventoryPlan.Stack source, out ItemStack item)
    {
        item = null;
        if (source.Count == 0) { item = ItemStack.Empty.Clone(); return true; }
        ItemValue value;
        if (!RebirthGearItemCodec.TryDecode(source.ItemData, out value)) return false;
        item = new ItemStack(value, source.Count);
        return true;
    }
}
