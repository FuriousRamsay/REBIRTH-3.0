using System.Linq;
internal static class RebirthTheorySoloAvailability
{
    internal static bool HasOriginalWork(RebirthTheorySoloState solo,string subject,string creation)
    {
        return solo!=null&&solo.CreationId==creation&&RebirthTheorySoloRegistry.TryGet(subject,out _)&&
            (solo.Session?.Subject==subject||solo.CancelPendingSubject==subject&&!string.IsNullOrEmpty(solo.CancelPendingId)||
             solo.Evidence.Any(e=>e.Subject==subject)||solo.LastSettlement.ContainsKey(subject));
    }
}