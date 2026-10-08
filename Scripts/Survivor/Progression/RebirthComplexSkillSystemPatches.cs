using HarmonyLib;

#nullable disable

[HarmonyPatch(typeof(Explosion), nameof(Explosion.AttackBlocks))]
internal static class RebirthSurvivorExplosiveBlockSkillPatch
{
    private static void Postfix(Explosion __instance, int _entityThatCausedExplosion, ItemValue _itemValueExplosionSource)
    {
        RebirthComplexSkillSystemService.OnExplosionBlocksCompleted(__instance, _entityThatCausedExplosion, _itemValueExplosionSource);
    }
}

[HarmonyPatch(typeof(DroneWeapons.StunBeamWeapon), nameof(DroneWeapons.StunBeamWeapon.Fire))]
internal static class RebirthSurvivorDroneStunSkillPatch
{
    private static void Postfix(DroneWeapons.StunBeamWeapon __instance, EntityAlive _target)
    {
        RebirthComplexSkillSystemService.OnDroneShockCompleted(__instance, _target);
    }
}
