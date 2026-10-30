using System;
using System.Globalization;

internal static partial class RebirthCamperSink
{
#if REBIRTH_DEBUG
    internal static bool TryObserveExisting(EntityVehicle vehicle,out double ml,out long revision)
    {
        ml=0;revision=0;
        if(!Current(vehicle)||!tanks.TryGetValue(vehicle,out var tank))return false;
        ml=tank.Ml;revision=tank.Revision;return true;
    }
#endif
    internal static bool TryCurrentConversion(ItemStack before,out ItemStack after,out double delta,out bool refill)
    {
        after=null;delta=0;refill=false;
        if(RebirthWaterRefillPlan.IsFillableJar(before))
        {
            if(!RebirthWaterRefillPlan.TryCreate(before,RebirthWaterRefillPlan.Grade.Murky,double.PositiveInfinity,out var plan))return false;
            after=plan.After;delta=-plan.AddedMl;refill=true;return Math.Abs(delta)<=CapacityMl;
        }
        if(!TryConversion(before,out after,out int whole))return false;
        delta=whole;return true;
    }
    private static bool ReadReservoirMl(string version,string text,out double ml)
    {
        ml=0;
        if(version=="1")
        {if(!int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out int whole))return false;ml=whole;}
        else if(version=="2")
        {if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out ml)||ml.ToString("R",CultureInfo.InvariantCulture)!=text)return false;}
        else return false;
        return !double.IsNaN(ml)&&!double.IsInfinity(ml)&&ml>=0&&ml<=CapacityMl;
    }
}
