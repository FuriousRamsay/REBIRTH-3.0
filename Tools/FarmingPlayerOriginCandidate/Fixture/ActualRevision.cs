public partial class TileEntityPlantGrowingRebirth { private static readonly object PlantIncarnationSync=new object();private static ulong s_nextPlantIncarnation=1UL;
public void BeginNewPlantIncarnation()
    {
        PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
        PlantIncarnation = AllocatePlantIncarnation();
        StateRevision = 1UL;
    }
public void EnsureAuthoritativeIncarnation()
    {
        if (PlantIncarnation == 0UL)
            PlantIncarnation = AllocatePlantIncarnation();
        ObservePlantIncarnation(PlantIncarnation);
        if (StateRevision == 0UL)
            StateRevision = 1UL;
    }
public void AdvanceStateRevision()
    { Advanced++;OnAdvance?.Invoke(); 
        EnsureAuthoritativeIncarnation();
        if (StateRevision == ulong.MaxValue)
        {
            // Preserve monotonic ordering by moving to a new incarnation rather than wrapping.
            var retainedOrigin = PlantOrigin;
            BeginNewPlantIncarnation();
            PlantOrigin = retainedOrigin; // same plant; revision rollover only
            return;
        }
        StateRevision++;
    }
private static ulong AllocatePlantIncarnation()
    {
        lock (PlantIncarnationSync)
        {
            ulong value = s_nextPlantIncarnation;
            if (value == 0UL || value == ulong.MaxValue)
                value = 1UL;
            s_nextPlantIncarnation = value + 1UL;
            return value;
        }
    }
private static void ObservePlantIncarnation(ulong incarnation)
    {
        if (incarnation == 0UL)
            return;
        lock (PlantIncarnationSync)
        {
            if (incarnation >= s_nextPlantIncarnation)
                s_nextPlantIncarnation = incarnation == ulong.MaxValue ? 1UL : incarnation + 1UL;
        }
    }}