using System;

// Pure before/after plan. Callers retain their existing native-owner/source receipt authority.
// This is never invoked by stacking or sorting separate containers.
internal sealed class RebirthWaterRefillPlan
{
    internal enum Grade { Murky,Boiled,Best }
    internal ItemStack After;
    internal double AddedMl;
    internal static bool WaterName(string name)=>name=="drinkJarRiverWater"||name=="drinkJarBoiledWater"||name=="drinkJarPureMineralWater";
    internal static string Name(Grade grade)=>grade==Grade.Murky?"drinkJarRiverWater":grade==Grade.Boiled?"drinkJarBoiledWater":"drinkJarPureMineralWater";
    private static bool ProfileGrade(string profile,out Grade grade)
    {
        grade=Grade.Murky;
        switch(profile){case "unsafeWater":return true;case "cleanWater":grade=Grade.Boiled;return true;case "mineralWater":grade=Grade.Best;return true;default:return false;}
    }
    internal static bool IsFillableJar(ItemStack before)=>before!=null&&!before.IsEmpty()&&(before.itemValue?.ItemClass?.GetItemName()=="drinkJarEmpty"||IsTopUp(before));
    internal static bool IsTopUp(ItemStack before)
    {
        if(before==null||before.IsEmpty()||!WaterName(before.itemValue?.ItemClass?.GetItemName())||
            !RebirthConsumableResolver.TryResolve(before.itemValue,out var definition)||!definition.IsDrink)return false;
        return RebirthLiquidContainerService.GetRemainingMl(before.itemValue,definition)<definition.ContainerCapacityMl;
    }
    internal static bool TryCreate(ItemStack before,Grade incoming,double availableMl,out RebirthWaterRefillPlan plan)
    {
        plan=null;
        if(before==null||before.IsEmpty()||before.count<1||before.count>10||before.itemValue?.ItemClass==null||
            (int)incoming<0||(int)incoming>2||double.IsNaN(availableMl)||availableMl<=0)return false;
        var original=before.itemValue;string name=original.ItemClass.GetItemName();
        bool empty=name=="drinkJarEmpty";
        if(!empty&&!WaterName(name))return false;
        float remaining=0,capacity=0;
        Grade existing=incoming;
        if(!empty)
        {
            if(!RebirthConsumableResolver.TryResolve(original,out var definition)||!definition.IsDrink||
                !ProfileGrade(RebirthLiquidContainerService.GetLiquidProfile(original,definition),out var profile))return false;
            existing=name=="drinkJarRiverWater"?Grade.Murky:name=="drinkJarBoiledWater"?Grade.Boiled:Grade.Best;
            if(profile<existing)existing=profile; // A mislabeled dirty profile must never become clean.
            remaining=RebirthLiquidContainerService.GetRemainingMl(original,definition);capacity=definition.ContainerCapacityMl;
        }
        Grade result=remaining>0&&existing<incoming?existing:incoming;
        var target=ItemClass.GetItem(Name(result));
        if(target==null||target.IsEmpty()||!RebirthConsumableResolver.TryResolve(target,out var output)||!output.IsDrink||
            output.ContainerCapacityMl<=0||float.IsNaN(output.ContainerCapacityMl)||float.IsInfinity(output.ContainerCapacityMl))return false;
        if(empty)capacity=output.ContainerCapacityMl;
        if(capacity!=output.ContainerCapacityMl||remaining<0||remaining>=capacity||float.IsNaN(remaining)||float.IsInfinity(remaining))return false;
        double perJar=Math.Min((double)capacity-remaining,availableMl/before.count);
        float after=(float)Math.Min(capacity,(double)remaining+perJar);
        // Native liquid volume is a float. Debit its ACTUAL representable change,
        // rounding down one float step if finite supply would otherwise be exceeded.
        double debit=((double)after-remaining)*before.count;
        if(debit>availableMl)
        {
            if(after<=0)return false;
            int bits=BitConverter.ToInt32(BitConverter.GetBytes(after),0);
            after=BitConverter.ToSingle(BitConverter.GetBytes(bits-1),0);
            debit=((double)after-remaining)*before.count;
        }
        if(debit<=0||debit>availableMl)return false;
        var value=original.Clone();value.type=target.type;
        RebirthLiquidContainerService.SetRemainingMl(value,output,after);
        plan=new RebirthWaterRefillPlan{After=new ItemStack(value,before.count),AddedMl=debit};return true;
    }
}
