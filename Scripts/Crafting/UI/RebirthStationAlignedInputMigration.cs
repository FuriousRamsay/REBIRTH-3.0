using System;
using System.Collections.Generic;

// Detached aligned migration; no native tile mutation or layout authority.
internal static class RebirthStationAlignedInputMigration
{
    internal static bool TryExpand(IList<ItemStack> input,IList<ItemStack> lastInput,IList<float> timers,
        IList<string> savedMaterials,IList<string> currentMaterials,
        out ItemStack[] expandedInput,out ItemStack[] expandedLastInput,out float[] expandedTimers)
    {
        expandedInput=null;expandedLastInput=null;expandedTimers=null;
        if(input==null||lastInput==null||timers==null||savedMaterials==null||currentMaterials==null||
            savedMaterials.Count!=currentMaterials.Count||savedMaterials.Count>byte.MaxValue-RebirthStationInputLayout.PhysicalSlots)return false;
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<savedMaterials.Count;i++)
            if(string.IsNullOrWhiteSpace(savedMaterials[i])||savedMaterials[i]!=currentMaterials[i]||!names.Add(savedMaterials[i]))return false;
        int physical=input.Count-savedMaterials.Count;
        if((physical!=RebirthStationInputLayout.LegacyPhysicalSlots&&physical!=RebirthStationInputLayout.PhysicalSlots)||
            lastInput.Count!=physical||timers.Count!=physical)return false;
        for(int i=0;i<physical;i++)
            if(lastInput[i]==null||lastInput[i].itemValue==null||lastInput[i].count<0||
                float.IsNaN(timers[i])||float.IsInfinity(timers[i]))return false;
        if(!RebirthStationInputLayout.TryExpand(input,savedMaterials.Count,out var migrated))return false;
        var previous=ItemStack.CreateArray(RebirthStationInputLayout.PhysicalSlots);
        var remaining=new float[RebirthStationInputLayout.PhysicalSlots];
        for(int i=0;i<physical;i++){previous[i]=lastInput[i].Clone();remaining[i]=timers[i];}
        expandedInput=migrated;expandedLastInput=previous;expandedTimers=remaining;return true;
    }
}