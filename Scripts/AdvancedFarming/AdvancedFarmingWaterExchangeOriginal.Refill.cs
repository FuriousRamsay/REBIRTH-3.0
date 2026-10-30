using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class AdvancedFarmingWaterExchangeOriginal
{
    private static bool TryRefill(ItemValue original,int count,out RebirthWaterRefillPlan plan)
    {
        plan=null;
        return RebirthWaterRefillPlan.IsFillableJar(new ItemStack(original,count))&&
            RebirthWaterRefillPlan.TryCreate(new ItemStack(original,count),RebirthWaterRefillPlan.Grade.Murky,double.PositiveInfinity,out plan);
    }
    private static bool ValidRemainder(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0&&value<500;
    private static double Volume(int units,double remainder)=>units*500d+remainder;
    private static bool NewConversionAllowed(Record request)
    {
        if(request.Version==2)return true;
        var original=Item(request.Before);
        if(!RebirthWaterRefillPlan.WaterName(original.ItemClass.GetItemName()))return true;
        // Do not create a new whole-jar deposit from a partial/unknown jar. Already
        // prepared legacy receipts still decode/settle under their original v1 contract.
        return RebirthConsumableResolver.TryResolve(original,out var d)&&d.IsDrink&&d.ContainerCapacityMl==500f&&
            RebirthLiquidContainerService.GetRemainingMl(original,d)==500f;
    }
    private static bool LegacyCapacityAllows(TileEntityWaterTankRebirth tank,int after,int maximum)
        =>ValidRemainder(tank.WaterRemainderMl)&&after>=0&&after<=maximum&&Volume(after,tank.WaterRemainderMl)<=maximum*500d;
    private static bool TryRefillAfter(Record request,TileEntityWaterTankRebirth tank,out int after,out double residual)
    {
        after=0;residual=0;
        if(!ValidRemainder(tank.WaterRemainderMl)||!TryRefill(Item(request.Before),request.Count,out var plan))return false;
        double total=Volume(tank.waterCount,tank.WaterRemainderMl),next=total-plan.AddedMl;
        if(tank.waterCount<0||next<0||double.IsNaN(next)||double.IsInfinity(next))return false;
        double whole=Math.Floor(next/500d);if(whole>int.MaxValue)return false;
        after=(int)whole;residual=next-whole*500d;return ValidRemainder(residual);
    }
    private static bool RefillFrameValid(Record r,ItemValue original)
    {
        if(r.Version!=2||!TryRefill(original,r.Count,out var plan)||!ValidRemainder(r.RemainderBefore)||!ValidRemainder(r.RemainderAfter))return false;
        if(r.Source==Guid.Empty)return r.RemainderBefore==0&&r.RemainderAfter==0;
        return r.Revision>0&&r.MaxWater>0&&r.MaxWater<=1000000&&r.WaterBefore>=0&&r.WaterAfter>=0&&
            r.WaterBefore<=r.MaxWater&&r.WaterAfter<=r.MaxWater&&
            Volume(r.WaterAfter,r.RemainderAfter)==Volume(r.WaterBefore,r.RemainderBefore)-plan.AddedMl;
    }
    private static void StampRemainder(Record r,TileEntityWaterTankRebirth tank)
    {
        if(r.Version==2)tank.WaterRemainderMl=r.RemainderAfter;
        tank.WaterExchangeBeforeRemainderMl=r.Version==2?r.RemainderBefore:tank.WaterRemainderMl;
        tank.WaterExchangeAfterRemainderMl=r.Version==2?r.RemainderAfter:tank.WaterRemainderMl;
    }
    internal static bool CanUseNaturalRefill(EntityPlayerLocal player)
        =>player!=null&&!RebirthHydrationOwnerHold.IsHeld(player)&&!ForeignBusy(player)&&!Owners.Values.Any(Current)&&
            (player.DragAndDropItem==null||player.DragAndDropItem.IsEmpty());
}
