using System;
class Program
{
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static ItemStack Stack(int count, ushort seed, int type = 1) => new ItemStack { count = count, itemValue = new ItemValue { type = type, Seed = seed } };
    static void Main()
    {
        Check(RebirthConsumptionSourceIdentity.Matches(Stack(1, 42), 1, 42), "exact single item accepted");
        Check(RebirthConsumptionSourceIdentity.Matches(Stack(8, 42), 1, 42), "exact stacked source accepted");
        Check(!RebirthConsumptionSourceIdentity.Matches(Stack(8, 41), 1, 42), "same type different stacked seed refused");
        Check(!RebirthConsumptionSourceIdentity.Matches(Stack(1, 41), 1, 42), "different single seed refused");
        Check(RebirthConsumptionSourceIdentity.Matches(Stack(2, 0), 1, 0), "zero seed exact match accepted");
        Check(!RebirthConsumptionSourceIdentity.Matches(Stack(2, 42), 1, 0), "zero seed is not a wildcard");
        Check(!RebirthConsumptionSourceIdentity.Matches(Stack(2, 42, 2), 1, 42), "different item refused");
        Check(!RebirthConsumptionSourceIdentity.Matches(Stack(0, 42), 1, 42), "empty source refused");
        Check(!RebirthConsumptionSourceIdentity.Matches(null, 1, 42), "missing source refused");
        Console.WriteLine("SIMULATION ONLY: production identity predicate with native item doubles; no network, debit, effects, or gameplay validation.");
    }
}
public sealed class ItemValue { public int type; public ushort Seed; }
public sealed class ItemStack { public int count; public ItemValue itemValue; public bool IsEmpty() => count <= 0 || itemValue == null; }
