using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>2.6: event bosses and their support zombies cannot be dismembered.</summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.GetDismemberChance))]
internal static class RebirthBossDismemberImmunityPatch
{
    private static void Postfix(EntityAlive __instance, ref float __result)
    {
        if (__result <= 0f || __instance.Buffs == null) return;
        if (__instance.Buffs.GetCustomVar("$varFuriousRamsayBoss") == 1f ||
            __instance.Buffs.GetCustomVar("$varFuriousRamsaySupportMinion") == 1f)
            __result = 0f;
    }
}

[Preserve]
public sealed class RebirthBossDismemberImmunityModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.bossevents.dismember"), typeof(RebirthBossDismemberImmunityPatch));
    }
}
