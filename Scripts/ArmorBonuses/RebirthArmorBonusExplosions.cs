using System;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Explosive damage does not pass through a stat the armor can change, so it is scaled where the
/// blast hits. The server does this, and only for damage inside a blast the player caused.
/// </summary>
internal static class RebirthArmorBonusExplosions
{
    [ThreadStatic] private static ItemValue currentItem;
    [ThreadStatic] private static int currentCause;
    [ThreadStatic] private static int depth;

    internal static float BonusFor(EntityPlayer player, ItemValue source)
    {
        if (player?.equipment == null || source == null || source.IsEmpty()) return 0f;
        float total = 0f;
        for (int slot = 0; slot <= (int)EquipmentSlots.Feet; slot++)
        {
            var bonuses = RebirthArmorBonusStorage.Read(player.equipment.GetSlotItem(slot));
            for (int i = 0; i < bonuses.Length; i++)
                if (bonuses[i].Definition.Special == "explosion" && bonuses[i].Definition.AppliesToWeapon(source))
                    total += bonuses[i].Value;
        }
        return total;
    }

    internal static void Enter(int _entityThatCausedExplosion, ItemValue _itemValueExplosionSource)
    {
        if (depth++ == 0) { currentCause = _entityThatCausedExplosion; currentItem = _itemValueExplosionSource; }
    }

    internal static Exception Exit(Exception __exception)
    {
        if (--depth <= 0) { depth = 0; currentItem = null; currentCause = 0; }
        return __exception;
    }

    internal static void ScaleHit(EntityAlive __instance, ref int _strength)
    {
        if (depth <= 0 || currentItem == null || _strength <= 0 || __instance.world == null || __instance.world.IsRemote()) return;
        var player = __instance.world.GetEntity(currentCause) as EntityPlayer;
        if (player == null) return;
        float bonus = BonusFor(player, currentItem);
        if (bonus > 0f) _strength = (int)Math.Round(_strength * (1f + bonus));
    }
}

[Preserve]
public sealed class RebirthArmorBonusExplosionModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("rebirth.armorbonuses.explosions");
        var attack = AccessTools.Method(typeof(Explosion), nameof(Explosion.AttackEntites), new[] { typeof(int), typeof(ItemValue), typeof(EnumDamageTypes) });
        var damage = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.DamageEntity), new[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) });
        if (attack == null || damage == null) throw new MissingMethodException("Native explosion damage targets changed.");
        harmony.Patch(attack,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(RebirthArmorBonusExplosions), nameof(RebirthArmorBonusExplosions.Enter))),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(RebirthArmorBonusExplosions), nameof(RebirthArmorBonusExplosions.Exit))));
        harmony.Patch(damage, prefix: new HarmonyMethod(AccessTools.Method(typeof(RebirthArmorBonusExplosions), nameof(RebirthArmorBonusExplosions.ScaleHit))));
    }
}
