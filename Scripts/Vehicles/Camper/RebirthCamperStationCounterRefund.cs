using System;

// Detached arithmetic only. The original cancellation checkpoint owns commit;
// parsed refund records alone cannot apply this image to a live station.
internal static class RebirthCamperStationCounterRefund
{
    internal static bool TryBuild(RebirthCamperStationState station,RebirthStationCancellationRefund refund,out ItemStack[] after)
    {
        after=null;
        if(station?.Input==null||refund==null||station.MaterialNames==null||station.InputSlotCount<0||
            station.Input.Length!=station.InputSlotCount+station.MaterialNames.Length)return false;
        var result=new ItemStack[station.Input.Length];
        for(int i=0;i<result.Length;i++)
        {
            if(station.Input[i]?.itemValue==null||station.Input[i].count<0)return false;
            result[i]=station.Input[i].Clone();
        }
        if(refund.IsMaterialReturn)
            foreach(var units in refund.CopyCounterRefunds())
            {
                if(units?.itemValue==null||units.itemValue.IsEmpty()||units.count<1)return false;
                int selected=-1;
                for(int i=station.InputSlotCount;i<result.Length;i++)
                    if(result[i].itemValue.type==units.itemValue.type)
                    {
                        if(selected>=0||result[i].itemValue.ItemClass?.GetItemName()!="unit_"+station.MaterialNames[i-station.InputSlotCount])return false;
                        var exact=result[i].Clone();exact.count=units.count;
                        if(!RebirthStationGridIngredients.IsSameStackSnapshot(exact,units))return false;
                        selected=i;
                    }
                if(selected<0||(long)result[selected].count+units.count>int.MaxValue)return false;
                result[selected].count+=units.count;
            }
        after=result;return true;
    }
}
