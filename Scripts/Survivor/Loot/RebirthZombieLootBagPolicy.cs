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
    // 2.6 Moonlock (RebirthVariables.hiveLootbagDrop + RebirthUtilities.LootDropChance(biome)).
    private const float HordeNightBase = 0.10f;
    private static readonly string[] Biomes = { "pine_forest", "desert", "snow", "wasteland", "burnt_forest" };

    /// <summary>pine forest 10%, desert 16.61%, snow 25.10%, wasteland 36%, burnt forest 50%; other biomes count as pine forest.</summary>
    internal static float HordeNightChance(EntityAlive entity)
    {
        string biome = entity?.biomeStandingOn?.m_sBiomeName;
        if (string.IsNullOrEmpty(biome) && entity?.world?.ChunkCache?.ChunkProvider?.GetBiomeProvider() is IBiomeProvider provider)
            biome = provider.GetBiomeAt((int)entity.position.x, (int)entity.position.z)?.m_sBiomeName;
        return HordeNightChance(biome);
    }

    internal static float HordeNightChance(string biomeName)
    {
        int id = System.Array.FindIndex(Biomes, name => string.Equals(name, biomeName, System.StringComparison.OrdinalIgnoreCase));
        if (id < 0) id = 0;
        double curve = 0.4 * (System.Math.Exp(0.25 * id) - 1.0) / (System.Math.Exp(0.25 * 4.0) - 1.0);
        return HordeNightBase + (float)System.Math.Round(curve, 4);
    }

    [System.ThreadStatic] internal static bool Denied;

    private static void Prefix(EntityAlive __instance)
    {
        Denied = false;
        if (!(__instance is EntityEnemy enemy) || enemy.world == null || enemy.world.IsRemote()) return;
        if (enemy.Buffs != null && enemy.Buffs.GetCustomVar("$varFuriousRamsayBoss") == 1f) { enemy.lootDropProb = 1f; return; }
        if (enemy.lootDropProb >= 1f) return;
        if (enemy.world.aiDirector?.BloodMoonComponent?.BloodMoonActive == true)
        {
            // 2.6 Moonlock horde night: the bag chance is 10% plus a biome amount, replacing the zombie's own chance
            // and the killer's loot perks. Rolled here; a won roll is kept certain through the native killer-effect step.
            if (enemy.rand.RandomFloat < HordeNightChance(enemy)) { enemy.lootDropProb = 1f; return; }
            enemy.lootDropProb = 0f;
            Denied = true;
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
