using System.Threading;

#nullable disable

/// <summary>
/// Retired. The owner-approved B07 job maintains water in barrels and dew collectors (see RebirthP8NpcWaterRefillWork);
/// crops replant automatically on harvest, so there is no watering-crop task and no planting task. The former direct
/// plant/water mutation code was removed. The class and its adapter id stay registered (and GetReport stays callable)
/// so existing registration and console wiring keep compiling; every entry point fails closed and nothing is mutated.
/// </summary>
public sealed class RebirthNpcAdvancedFarmingPlantingWateringAdapter : IRebirthNpcFarmingWorkAdapter
{
    private static long rejected;

    public string AdapterId { get { return "rebirth.farming.advanced-plant-water"; } }
    public int Priority { get { return 600; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context) { return false; }

    public RebirthNpcWorkMutationResult BeginFarming(RebirthNpcConcreteWorkContext context)
    {
        Interlocked.Increment(ref rejected);
        return Failed("Planting and per-crop watering are retired; crops replant on harvest and water is refilled by the provider job.");
    }

    public RebirthNpcWorkMutationResult TickFarming(RebirthNpcConcreteWorkContext context)
    {
        Interlocked.Increment(ref rejected);
        return Failed("Planting and per-crop watering are retired.");
    }

    public void CancelFarming(RebirthNpcConcreteWorkContext context, string reason) { }

    public static string GetReport()
    {
        return "[REBIRTH NPC Advanced Farming Plant/Water] retired rejected=" + Interlocked.Read(ref rejected);
    }

    private static RebirthNpcWorkMutationResult Failed(string detail)
    {
        return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed,
            FailureCategory = RebirthNpcWorkFailureCategory.InvalidTarget, Detail = detail };
    }
}
