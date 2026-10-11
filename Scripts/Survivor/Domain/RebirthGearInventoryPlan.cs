using System;
using System.Collections.Generic;

// Pure validation foundation; not yet connected to live gear transactions.
// ItemData is the exact serialized ItemValue, not an item name or seed alone.
public sealed class RebirthGearInventoryPlan
{
    public sealed class Stack
    {
        public string ItemData = string.Empty;
        public int Count;
    }

    public sealed class Change
    {
        public bool IsBag;
        public int Index;
        public Stack Before = new Stack();
        public Stack After = new Stack();
    }

    public enum RecoveryOrigin { Backpack, Belt, DisplacedGear }
    public sealed class RecoveryEntry
    {
        public RecoveryOrigin Origin;
        public int SourceIndex = -1;
        public Stack Item = new Stack();
    }

    public int BagSlotsBefore, BagSlotsAfter;
    public int BeltSlotsBefore, BeltSlotsAfter;
    public Stack GearBefore = new Stack(), GearAfter = new Stack();
    public readonly List<Change> Changes = new List<Change>();
    public readonly List<RecoveryEntry> Recovery = new List<RecoveryEntry>();

    public bool IsConserved()
    {
        if (!BagCapacity(BagSlotsBefore) || !BagCapacity(BagSlotsAfter)
            || !BeltCapacity(BeltSlotsBefore) || !BeltCapacity(BeltSlotsAfter)
            || Changes.Count > 189 || Recovery.Count > 189
            || GearBefore == null || GearAfter == null
            || GearBefore.Count > 1 || GearAfter.Count > 1) return false;
        var totals = new Dictionary<string, long>(StringComparer.Ordinal);
        var positions = new HashSet<int>();
        int encodedBudget = 4 * 1024 * 1024;
        if (!Add(totals, GearBefore, 1, ref encodedBudget) || !Add(totals, GearAfter, -1, ref encodedBudget)) return false;
        foreach (var change in Changes)
        {
            if (change == null || change.Index < 0) return false;
            int beforeLimit = change.IsBag ? BagSlotsBefore : BeltSlotsBefore;
            int afterLimit = change.IsBag ? BagSlotsAfter : BeltSlotsAfter;
            if (change.Index >= Math.Max(beforeLimit, afterLimit)
                || !positions.Add(change.Index + (change.IsBag ? 0 : 169))) return false;
            if (change.Before == null || change.After == null
                || (change.Index >= beforeLimit && change.Before.Count != 0)
                || (change.Index >= afterLimit && change.After.Count != 0)
                || !Add(totals, change.Before, 1, ref encodedBudget) || !Add(totals, change.After, -1, ref encodedBudget)) return false;
        }
        var recoverySources = new HashSet<int>();
        foreach (var entry in Recovery)
        {
            if (entry == null || entry.Item == null || entry.Item.Count <= 0
                || !Add(totals, entry.Item, -1, ref encodedBudget)) return false;
            if (entry.Origin == RecoveryOrigin.DisplacedGear)
            {
                if (entry.SourceIndex != -1 || !Same(entry.Item, GearBefore)
                    || !recoverySources.Add(189)) return false;
                continue;
            }
            bool isBag = entry.Origin == RecoveryOrigin.Backpack;
            if (!isBag && entry.Origin != RecoveryOrigin.Belt) return false;
            int retained = isBag ? BagSlotsAfter : BeltSlotsAfter;
            int previous = isBag ? BagSlotsBefore : BeltSlotsBefore;
            if (entry.SourceIndex < retained || entry.SourceIndex >= previous
                || !recoverySources.Add(entry.SourceIndex + (isBag ? 0 : 169))) return false;
            Change source = Changes.Find(c => c.IsBag == isBag && c.Index == entry.SourceIndex);
            if (source == null || source.Before.ItemData != entry.Item.ItemData
                || source.Before.Count < entry.Item.Count) return false;
        }
        foreach (long total in totals.Values) if (total != 0) return false;
        return true;
    }

    // Caller supplies every physical inventory slot except the native belt dummy.
    // Captured capacities describe this snapshot, not a subsequently projected gear state.
    // Missing expansion slots are empty; existing tail slots may never be omitted.
    public bool MatchesBefore(Stack[] bag, Stack[] belt, Stack gear,
        int bagCapacity, int beltCapacity)
    {
        if (!IsConserved() || bagCapacity != BagSlotsBefore || beltCapacity != BeltSlotsBefore
            || bag == null || belt == null || bag.Length != bagCapacity || belt.Length != beltCapacity
            || !Same(gear, GearBefore)) return false;
        var bagChanges = new HashSet<int>();
        var beltChanges = new HashSet<int>();
        foreach (var change in Changes)
        {
            Stack[] slots = change.IsBag ? bag : belt;
            Stack actual = change.Index < slots.Length ? slots[change.Index] : new Stack();
            if (!Same(actual, change.Before)) return false;
            (change.IsBag ? bagChanges : beltChanges).Add(change.Index);
        }
        return TailCovered(bag, BagSlotsAfter, bagChanges)
            && TailCovered(belt, BeltSlotsAfter, beltChanges);
    }

    public enum ApplicationState { Conflict, Before, Partial, After }

    // Use mixed pre/postimages ONLY with this transaction's persisted applying
    // receipt, and while inventory interactions are held. This is not proof of
    // receipt authenticity or permission to apply another transaction.
    public ApplicationState InspectApplication(Stack[] bag, Stack[] belt, bool hasApplyingReceipt)
    {
        if (!IsConserved() || bag == null || belt == null
            || bag.Length != Math.Max(BagSlotsBefore, BagSlotsAfter)
            || belt.Length != Math.Max(BeltSlotsBefore, BeltSlotsAfter)) return ApplicationState.Conflict;
        bool anyBefore = false, anyAfter = false;
        var bagChanges = new HashSet<int>();
        var beltChanges = new HashSet<int>();
        foreach (var change in Changes)
        {
            Stack actual = (change.IsBag ? bag : belt)[change.Index];
            bool before = Same(actual, change.Before), after = Same(actual, change.After);
            if (!before && !after) return ApplicationState.Conflict;
            if (before && !after) anyBefore = true;
            if (after && !before) anyAfter = true;
            (change.IsBag ? bagChanges : beltChanges).Add(change.Index);
        }
        if (!TailCovered(bag, BagSlotsAfter, bagChanges) || !TailCovered(belt, BeltSlotsAfter, beltChanges)
            || !ExpansionCovered(bag, BagSlotsBefore, bagChanges) || !ExpansionCovered(belt, BeltSlotsBefore, beltChanges)
            || (anyAfter && !hasApplyingReceipt)) return ApplicationState.Conflict;
        if (!anyAfter) return ApplicationState.Before;
        return anyBefore ? ApplicationState.Partial : ApplicationState.After;
    }

    // Receipt comparison only: all affected slots must hold their exact postimage.
    // Unchanged retained slots remain the owner's inventory, not transaction payload.
    public bool MatchesAppliedInventory(Stack[] bag, Stack[] belt)
    {
        if (InspectApplication(bag, belt, true) == ApplicationState.Conflict) return false;
        foreach (var change in Changes)
            if (!Same((change.IsBag ? bag : belt)[change.Index], change.After)) return false;
        return true;
    }
    private static bool ExpansionCovered(Stack[] slots, int original, HashSet<int> changes)
    {
        for (int i = original; i < slots.Length; i++)
            if (!changes.Contains(i) && !Same(slots[i], new Stack())) return false;
        return true;
    }

    private static bool TailCovered(Stack[] slots, int retained, HashSet<int> changes)
    {
        for (int i = retained; i < slots.Length; i++)
        {
            Stack stack = slots[i];
            if (stack == null || stack.Count < 0 || stack.Count > ushort.MaxValue || stack.ItemData == null) return false;
            if (stack.Count == 0 && stack.ItemData.Length != 0) return false;
            if (stack.Count > 0 && !changes.Contains(i)) return false;
        }
        return true;
    }

    private static bool Same(Stack left, Stack right)
    {
        return left != null && right != null && left.Count == right.Count
            && string.Equals(left.ItemData, right.ItemData, StringComparison.Ordinal);
    }

    private static bool BagCapacity(int value) { return value >= 44 && value <= 169; }
    private static bool BeltCapacity(int value) { return value >= 4 && value <= 20; }

    private static bool Add(Dictionary<string, long> totals, Stack stack, int sign, ref int encodedBudget)
    {
        if (stack == null || stack.Count < 0 || stack.Count > ushort.MaxValue || stack.ItemData == null) return false;
        if (stack.Count == 0) return stack.ItemData.Length == 0;
        if (stack.ItemData.Length == 0 || stack.ItemData.Length > 262144
            || stack.ItemData.Length > encodedBudget) return false;
        encodedBudget -= stack.ItemData.Length;
        // Require canonical encoding so equivalent bytes cannot use different ledger keys.
        try
        {
            byte[] bytes = Convert.FromBase64String(stack.ItemData);
            if (bytes.Length == 0 || Convert.ToBase64String(bytes) != stack.ItemData) return false;
        }
        catch (FormatException) { return false; }
        long previous;
        totals.TryGetValue(stack.ItemData, out previous);
        totals[stack.ItemData] = previous + (long)sign * stack.Count;
        return true;
    }
}
