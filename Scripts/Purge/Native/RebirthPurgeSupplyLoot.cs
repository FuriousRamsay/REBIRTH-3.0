using System;
using System.Collections.Generic;

internal static class RebirthPurgeSupplyLoot
{
    private static readonly string[] Order={"pine_forest","desert","snow","wasteland","burnt_forest"};
    // 2.6 chose the first unfinished objective, not the crate/player's biome.
    // Independent objectives retain that reward rule without restricting entry.
    internal static string Select(Func<string,bool?> complete)
    {
        foreach(var biome in Order)if(complete(biome)==false)return biome;
        return "burnt_forest";
    }
    internal static bool BeforeLootList(EntityAlive __instance,ref string __result)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||
           !(__instance is EntitySupplyCrate)||__instance.world==null||__instance.world.IsRemote())return true;
        var progress=RebirthPurgeObjectiveProgress.Instance;
        var published=progress.Published;
        string biome=published==null?"pine_forest":Select(name=> {
            RebirthPurgeObjectiveProgress.BiomeProgress value;
            if(!published.TryGetValue(name,out value)||value.Eligible==0)return (bool?)null;
            return value.RemainingFor(progress.TargetPercentage)==0;
        });
        __result="airDropPurge_"+biome;return false;
    }
}
