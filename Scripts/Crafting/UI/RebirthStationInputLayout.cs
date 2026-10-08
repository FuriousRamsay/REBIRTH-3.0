using System;
using System.Collections.Generic;

// Detached layout migration. Native/UI/smelting/destruction adapters must agree on offset9.
public static class RebirthStationInputLayout
{
    public const int PhysicalSlots=9;
    public const int LegacyPhysicalSlots=3;
    public static bool TryExpand(IList<ItemStack> original,int materialCount,out ItemStack[] expanded)
    {
        expanded=null;
        if(original==null||materialCount<0||materialCount>byte.MaxValue-PhysicalSlots||
            original.Count!=LegacyPhysicalSlots+materialCount&&original.Count!=PhysicalSlots+materialCount)return false;
        int physical=original.Count-materialCount;
        var result=ItemStack.CreateArray(PhysicalSlots+materialCount);
        for(int i=0;i<original.Count;i++)
        {
            var stack=original[i];
            if(stack==null||stack.itemValue==null||stack.count<0)return false;
            int destination=i<physical?i:PhysicalSlots+i-physical;
            result[destination]=stack.Clone();
        }
        expanded=result;return true;
    }
    public static bool TryPhysicalGrid(IList<ItemStack> expanded,int materialCount,out ItemStack[] grid)
        =>TryReadPhysical(expanded,PhysicalSlots,materialCount,out grid);
    public static bool TryReadPhysical(IList<ItemStack> original,int physicalSlots,int materialCount,out ItemStack[] grid)
    {
        grid=null;
        if(original==null||physicalSlots!=LegacyPhysicalSlots&&physicalSlots!=PhysicalSlots||
            materialCount<0||materialCount>byte.MaxValue-physicalSlots||original.Count!=physicalSlots+materialCount)return false;
        foreach(var stack in original)
            if(stack==null||stack.itemValue==null||stack.count<0)return false;
        var result=new ItemStack[physicalSlots];
        for(int i=0;i<physicalSlots;i++)result[i]=original[i].Clone();
        grid=result;return true;
    }
}