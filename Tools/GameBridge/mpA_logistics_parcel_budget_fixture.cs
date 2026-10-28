// Actual production pure encoded-buffer admission. Does not simulate the
// native save lock, pool capacities, proof observer, or game object RSS.
public static class MpALogisticsParcelBudgetFixture
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Main()
    {
        const long mib=1024*1024;
        Check(RebirthLogisticsParcelBudget.CanReserve(48*mib,9,72*mib,192*mib),"all nine captures reserved with bounded existing native queue");
        Check(!RebirthLogisticsParcelBudget.CanReserve(48*mib+1,9,72*mib,192*mib),"one byte beyond shared pending allowance refused before capture");
        Check(!RebirthLogisticsParcelBudget.CanReserve(0,10,72*mib,192*mib),"unbounded nearby chunk count refused");
        Check(!RebirthLogisticsParcelBudget.CanReserve(long.MaxValue,1,0,0),"overflow queue input cannot wrap admission");
        Check(!RebirthLogisticsParcelBudget.CanReserve(0,1,72*mib+1,192*mib),"cached proof bound");
        Check(!RebirthLogisticsParcelBudget.CanReserve(0,1,72*mib,192*mib+1),"detached before/after compression workspace bound");
        Check(RebirthLogisticsParcelBudget.CanReserveActual(88*mib,1,32*1024*1024,72*mib,192*mib),"one rich native capture fits exact remaining queue");
        Check(!RebirthLogisticsParcelBudget.CanReserveActual(88*mib+1,1,32*1024*1024,72*mib,192*mib),"rich prospective byte beyond native allowance refused");
        Check(RebirthLogisticsParcelBudget.CanReserveActual(120*mib,0,32*1024*1024,72*mib,192*mib),"post-capture actual queue check adds no imaginary captures");
        Check(!RebirthLogisticsParcelBudget.CanReserveActual(120*mib+1,0,32*1024*1024,72*mib,192*mib),"actual excessive native capacity refused before execute");
        Check(!RebirthLogisticsParcelBudget.CanReserveActual(0,1,int.MaxValue,0,0),"unbounded native capture allowance refused");
        Console.WriteLine("PASS actual encoded parcel budget predicate; native allocation/lock NOT VALIDATED");
    }
}
