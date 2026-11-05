using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// REBIRTH 2.6 loot bag rule. Ordinary zombies only roll a bag on horde night or as part of an
/// outbreak. Entities set to always drop keep that, and bosses always drop.
/// </summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.dropItemOnDeath))]
internal static class RebirthZombieLootBagPolicy
{
    // 2.6 default: 3 x customHordeNightLootDropMultiplier 70%, and a quarter of that for reducedHNloot.
    private const float HordeNightMultiplier = 2.1f;
    private const float ReducedHordeNightScale = 0.25f;
    private static readonly FastTags<TagGroup.Global> ReducedHordeNightLoot = FastTags<TagGroup.Global>.Parse("reducedHNloot");

    [System.ThreadStatic] internal static bool Denied;

    private static void Prefix(EntityAlive __instance)
    {
        Denied = false;
        if (!(__instance is EntityEnemy enemy) || enemy.world == null || enemy.world.IsRemote()) return;
        if (enemy.Buffs != null && enemy.Buffs.GetCustomVar("$varFuriousRamsayBoss") == 1f) { enemy.lootDropProb = 1f; return; }
        if (enemy.lootDropProb >= 1f) return;
        if (enemy.world.aiDirector?.BloodMoonComponent?.BloodMoonActive == true)
        {
            enemy.lootDropProb *= enemy.HasAnyTags(ReducedHordeNightLoot) ? HordeNightMultiplier * ReducedHordeNightScale : HordeNightMultiplier;
            return;
        }
        // 2.6 rule: outside horde night, only outbreak zombies keep their bag chance. The flag survives saves.
        if (enemy.Buffs != null && enemy.Buffs.GetCustomVar(RebirthWanderingHerds.HerdVar) == 1f) return;
        // Native recomputes the chance from the killer's effects after this, so block the bag itself too.
        enemy.lootDropProb = 0f;
        Denied = true;
    }

    private static void Finalizer() { Denied = false; }
}

// Declared on Entity, not EntityAlive.
[HarmonyPatch(typeof(Entity), nameof(Entity.DropBagServer))]
internal static class RebirthZombieLootBagDeniedDrop
{
    private static bool Prefix() => !RebirthZombieLootBagPolicy.Denied;
}

[Preserve]
public sealed class RebirthZombieLootBagPolicyModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.zombie-loot-bags");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthZombieLootBagPolicy));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthZombieLootBagDeniedDrop));
    }
}
