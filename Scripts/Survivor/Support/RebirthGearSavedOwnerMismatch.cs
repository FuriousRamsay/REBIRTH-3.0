using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// Diagnostic comparison of already-detached encoded snapshots. Never rereads,
// encodes native items, repairs a snapshot, or grants custody authority.
internal static class RebirthGearSavedOwnerMismatch
{
    internal static bool Fail(RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,string savedOwnerKey,string predicate,string section,int actual=-1,int expected=-1)
    {
        try{RebirthGearTrace.Witness(offer,stage.ToString(),predicate,section,actual,expected,savedOwnerKey:savedOwnerKey);}catch{}
        return false;
    }
    internal static void Inventory(RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,string savedOwnerKey,RebirthGearInventoryPlan plan,
        RebirthGearInventoryPlan.Stack[] bag,RebirthGearInventoryPlan.Stack[] belt)
    {
        if(!RebirthGearTrace.Enabled)return;
        try
        {
            bool before=stage==RebirthGearOwnerReceipt.Rejected;
            if(!plan.IsConserved()){Fail(offer,stage,savedOwnerKey,"plan.IsConserved","plan");return;}
            int expectedBag=before?plan.BagSlotsBefore:Math.Max(plan.BagSlotsBefore,plan.BagSlotsAfter);
            int expectedBelt=before?plan.BeltSlotsBefore:Math.Max(plan.BeltSlotsBefore,plan.BeltSlotsAfter);
            if(bag==null||bag.Length!=expectedBag){Fail(offer,stage,savedOwnerKey,"inventory.shape","bag",bag?.Length??-1,expectedBag);return;}
            if(belt==null||belt.Length!=expectedBelt){Fail(offer,stage,savedOwnerKey,"inventory.shape","belt",belt?.Length??-1,expectedBelt);return;}
            var bagChanges=new HashSet<int>();var beltChanges=new HashSet<int>();
            foreach(var change in plan.Changes)
            {
                var slots=change.IsBag?bag:belt;var actual=change.Index<slots.Length?slots[change.Index]:new RebirthGearInventoryPlan.Stack();
                var expected=before?change.Before:change.After;
                bool matches=Same(actual,expected);
                if(!before)matches=Same(actual,change.Before)||Same(actual,change.After);
                if(!matches){Slot(offer,stage,savedOwnerKey,"inventory.change."+(before?"before":"before-or-after.expectedHash-is-after"),change.IsBag?"bag":"belt",change.Index,actual,expected);return;}
                (change.IsBag?bagChanges:beltChanges).Add(change.Index);
            }
            if(Tail(offer,stage,savedOwnerKey,bag,plan.BagSlotsAfter,bagChanges,"bag")||Tail(offer,stage,savedOwnerKey,belt,plan.BeltSlotsAfter,beltChanges,"belt"))return;
            if(!before&&(Expansion(offer,stage,savedOwnerKey,bag,plan.BagSlotsBefore,bagChanges,"bag")||Expansion(offer,stage,savedOwnerKey,belt,plan.BeltSlotsBefore,beltChanges,"belt")))return;
            if(stage==RebirthGearOwnerReceipt.Applied)foreach(var change in plan.Changes)
            {
                var actual=(change.IsBag?bag:belt)[change.Index];
                if(!Same(actual,change.After)){Slot(offer,stage,savedOwnerKey,"inventory.change.after",change.IsBag?"bag":"belt",change.Index,actual,change.After);return;}
            }
            Fail(offer,stage,savedOwnerKey,"inventory.comparison-unclassified","inventory");
        }
        catch{Fail(offer,stage,savedOwnerKey,"diagnostic-comparison-unavailable","inventory");}
    }
    private static bool Tail(RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,string savedOwnerKey,RebirthGearInventoryPlan.Stack[] slots,int retained,HashSet<int> changes,string section)
    {
        for(int i=retained;i<slots.Length;i++)
        {
            var slot=slots[i];
            if(slot==null||slot.Count<0||slot.Count>ushort.MaxValue||slot.ItemData==null||(slot.Count==0&&slot.ItemData.Length!=0)||(slot.Count>0&&!changes.Contains(i)))
            {Slot(offer,stage,savedOwnerKey,"inventory.TailCovered",section,i,slot,new RebirthGearInventoryPlan.Stack());return true;}
        }
        return false;
    }
    private static bool Expansion(RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,string savedOwnerKey,RebirthGearInventoryPlan.Stack[] slots,int original,HashSet<int> changes,string section)
    {
        for(int i=original;i<slots.Length;i++)if(!changes.Contains(i)&&!Same(slots[i],new RebirthGearInventoryPlan.Stack()))
        {Slot(offer,stage,savedOwnerKey,"inventory.ExpansionCovered",section,i,slots[i],new RebirthGearInventoryPlan.Stack());return true;}
        return false;
    }
    private static bool Same(RebirthGearInventoryPlan.Stack a,RebirthGearInventoryPlan.Stack b)
        =>a!=null&&b!=null&&a.Count==b.Count&&string.Equals(a.ItemData,b.ItemData,StringComparison.Ordinal);
    private static string Hash(RebirthGearInventoryPlan.Stack value)
    {
        if(value==null)return null;
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+value.ItemData))).Replace("-","").ToLowerInvariant();
    }
    private static void Slot(RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,string savedOwnerKey,string predicate,string section,int index,RebirthGearInventoryPlan.Stack actual,RebirthGearInventoryPlan.Stack expected)
        =>RebirthGearTrace.Witness(offer,stage.ToString(),predicate,section,index:index,actualHash:Hash(actual),expectedHash:Hash(expected),savedOwnerKey:savedOwnerKey);
}

