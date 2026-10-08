using System;
using System.IO;

// Detached input for the forthcoming gear transaction planner. No inventory writes.
public sealed class RebirthGearInventorySnapshot
{
    public readonly RebirthGearInventoryPlan.Stack[] Bag;
    public readonly RebirthGearInventoryPlan.Stack[] Belt;
    public readonly int OwnedBeltSlots;

    private RebirthGearInventorySnapshot(RebirthGearInventoryPlan.Stack[] bag,
        RebirthGearInventoryPlan.Stack[] belt, int ownedBeltSlots)
    {
        Bag = bag; Belt = belt; OwnedBeltSlots = ownedBeltSlots;
    }

    // Detached reconstruction only. This factory grants no authority or writes.
    public static bool TryCaptureEncoded(RebirthGearInventoryPlan.Stack[] bag,
        RebirthGearInventoryPlan.Stack[] belt,int owned,out RebirthGearInventorySnapshot snapshot)
    {
        snapshot=null;
        if(!RebirthGearEncodedSnapshot.TryCopy(bag,belt,owned,out var b,out var t))return false;
        snapshot=new RebirthGearInventorySnapshot(b,t,owned);return true;
    }
    public bool IsUsableSource(bool isBag, int index)
    {
        return index >= 0 && index < (isBag ? Bag.Length : OwnedBeltSlots);
    }

    // Arrays must come from the authenticated owner's latest native player data
    // for a remote player. Never fall back to the remote EntityPlayer inventory.
    // Keep locked/retired belt contents for recovery, including the final real ItemGrid slot.
    public static bool TryCapture(ItemStack[] bag, ItemStack[] nativeBelt,
        int ownedBeltSlots, out RebirthGearInventorySnapshot snapshot)
    {
        snapshot = null;
        if (bag == null || bag.Length < 52 || bag.Length > 169
            || nativeBelt == null || nativeBelt.Length < 4 || nativeBelt.Length > 20
            || ownedBeltSlots < 4 || ownedBeltSlots > 18
            || ownedBeltSlots > nativeBelt.Length) return false;
        var bagCopy = new RebirthGearInventoryPlan.Stack[bag.Length];
        var beltCopy = new RebirthGearInventoryPlan.Stack[nativeBelt.Length];
        int budget = 4 * 1024 * 1024;
        for (int i = 0; i < bagCopy.Length; i++)
            if (!TryEncode(bag[i], ref budget, out bagCopy[i])) return false;
        for (int i = 0; i < beltCopy.Length; i++)
            if (!TryEncode(nativeBelt[i], ref budget, out beltCopy[i])) return false;
        snapshot = new RebirthGearInventorySnapshot(bagCopy, beltCopy, ownedBeltSlots);
        return true;
    }

    private static bool TryEncode(ItemStack stack, ref int budget,
        out RebirthGearInventoryPlan.Stack encoded)
    {
        encoded = null;
        // Null cells and native empty cells carry no inventory item. A positive
        // count with a missing/invalid value must not silently disappear.
        if (stack == null || stack.count == 0)
        {
            encoded = new RebirthGearInventoryPlan.Stack();
            return true;
        }
        if (stack.count < 0 || stack.itemValue == null || stack.itemValue.IsEmpty()
            || stack.itemValue.ItemClass == null) return false;
        try
        {
            using (var stream = new MemoryStream())
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);
                ItemValue.Write(stack.itemValue, writer);
                writer.Flush();
                if (stream.Length == 0 || stream.Length > 196608) return false;
                string data = Convert.ToBase64String(stream.ToArray());
                if (data.Length > budget) return false;
                budget -= data.Length;
                encoded = new RebirthGearInventoryPlan.Stack { ItemData = data, Count = stack.count };
                return true;
            }
        }
        catch (Exception) { return false; }
    }
}
