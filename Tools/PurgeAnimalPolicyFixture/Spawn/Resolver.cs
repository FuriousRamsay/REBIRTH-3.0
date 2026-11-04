using System;
static class Resolver {
    public static void Postfix(string _sEntityGroupName, ref int lastClassId, GameRandom random, ref int __result)
    {
        RebirthSpawnContext active = RebirthSpawnCompositionRuntimeIntegration.Current;
        if (active == null || active.DeferCompositionUntilPosition ||
            !RebirthSpawnCompositionService.IsRestrictedNoSpecialSurface(active.Surface))
            return;

        EntityClass selectedClass = EntityClass.GetEntityClass(__result);
        bool animalSleeper=active.Surface==RebirthSpawnSurface.Sleeper&&IsNonZombieAnimal(selectedClass);
        if (!animalSleeper&&!RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(selectedClass))
            return;

        GameRandom authoritativeRandom = random;
        if (authoritativeRandom == null && GameManager.Instance != null && GameManager.Instance.World != null)
            authoritativeRandom = GameManager.Instance.World.GetGameRandom();

        RebirthSpawnTrace replacementTrace;
        if (authoritativeRandom != null)
        {
            RebirthSpawnContext replacementContext = new RebirthSpawnContext
            {
                Surface = active.Surface,
                ProgressionMode = active.ProgressionMode,
                GameStage = active.GameStage,
                Biome = active.Biome,
                RequestedGroup = string.Empty,
                PrefabName = active.PrefabName,
                HistoryKey = active.HistoryKey
            };

            if (RebirthSpawnCompositionService.TrySelect(
                    replacementContext,
                    delegate { return (double)authoritativeRandom.RandomFloat; },
                    out replacementTrace) &&
                replacementTrace != null &&
                (!animalSleeper||!IsNonZombieAnimal(EntityClass.GetEntityClass(replacementTrace.EntityClassId)))&&
                !RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(replacementTrace.EntityClassId))
            {
                if(animalSleeper)Log.Out("[RebirthSpawn] sleeper animal replaced group="+_sEntityGroupName+" original="+selectedClass.entityClassName+" replacement="+replacementTrace.EntityName+" prefab="+active.PrefabName);
                __result = replacementTrace.EntityClassId;
                lastClassId = replacementTrace.EntityClassId;
                return;
            }
        }

        // Absolute fallback: never return a screamer, crawler or demolition zombie
        // into Sleeper/Wandering/Event surfaces even when a vanilla-preserved group
        // contains only a forbidden entity or the managed replacement pool is unavailable.
        int fallbackId = EntityClass.FromString("zombieArlene");
        EntityClass fallbackClass = EntityClass.GetEntityClass(fallbackId);
        if (fallbackClass != null &&
            !RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(fallbackClass))
        {
            __result = fallbackId;
            lastClassId = fallbackId;
        }
    }
    private static readonly FastTags<TagGroup.Global> ZombieTag=FastTags<TagGroup.Global>.Parse("zombie");
    internal static bool IsNonZombieAnimal(EntityClass definition)
        =>definition!=null&&definition.bIsAnimalEntity&&!definition.Tags.Test_AnySet(ZombieTag);
}
