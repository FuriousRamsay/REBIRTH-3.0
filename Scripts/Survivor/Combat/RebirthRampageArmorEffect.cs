using System;

// The 2.6 armor tiers extended the interval between three head
// dismemberments and the resulting Rampage duration. Keep the active effect in
// the native buff system; equipment owns the two tier CVars through XML.
internal static class RebirthRampageArmorEffect
{
    private const string ChainBuff = "FuriousRamsayDismemberedHeadCounter";
    private const string CooldownBuff = "FuriousRamsayDismemberedHeadCooldown";
    private const string RampageBuff = "FuriousRamsayRampage";
    private const string ChainCount = "rbRampageHeadCount";

    internal static void Observe(EntityAlive target, DamageResponse response, DamageSource source, float actualHealthLoss)
    {
        var world = target?.world;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || source == null ||
            !ReferenceEquals(world, GameManager.Instance?.World)) return;

        var actor = world.GetEntity(source.getEntityId()) as EntityAlive;
        if (actor == null || actor.IsDead() || ReferenceEquals(actor, target) ||
            !ReferenceEquals(actor.world, world) || !ReferenceEquals(world.GetEntity(actor.entityId), actor) ||
            !(actor is EntityPlayer) && !(actor is EntityRebirthNPC)) return;

        var buffs = actor.Buffs;
        if (buffs == null) return;

        // The original Rampage buff extends by five seconds on each kill,
        // capped at 55–110 seconds according to the worn tier.
        if (actualHealthLoss > 0f && target.IsDead() && buffs.HasBuff(RampageBuff))
        {
            var active = buffs.GetBuff(RampageBuff);
            if (active != null)
            {
                float durationBonus = TierBonus(buffs.GetCustomVar("$RampageIncreaseDuration"));
                uint cap = (uint)Math.Ceiling(55f * (1f + durationBonus) * 20f);
                active.DurationInTicks = Math.Min(cap, active.DurationInTicks + 100u);
            }
        }

        // The 2.6 damage hook used item and target tags. It sent both melee
        // and ranged zombie/bandit head dismemberments into this same chain,
        // despite the legacy buff's "Ranged" name. Preserve that behavior.
        string weaponTags = source.AttackingItem?.ItemClass?.ItemTags.ToString() ?? string.Empty;
        string targetTags = target.EntityTags.ToString();
        bool eligibleWeapon = weaponTags.IndexOf("ranged", StringComparison.OrdinalIgnoreCase) >= 0 ||
            weaponTags.IndexOf("melee", StringComparison.OrdinalIgnoreCase) >= 0;
        bool eligibleTarget = targetTags.IndexOf("zombie", StringComparison.OrdinalIgnoreCase) >= 0 ||
            targetTags.IndexOf("bandit", StringComparison.OrdinalIgnoreCase) >= 0;
        if (actualHealthLoss <= 0f || !eligibleTarget || !eligibleWeapon || !response.Dismember ||
            response.HitBodyPart != EnumBodyPartHit.Head || buffs.HasBuff(CooldownBuff)) return;

        float chanceTier = TierBonus(buffs.GetCustomVar("$RampageIncreaseChance"));
        if (chanceTier <= 0f) return;

        // Despite its legacy name, "Chance" shortened the time needed for
        // three eligible decapitations: Tier 1 grants 12 seconds, Tier 5 20.
        bool chainActive = buffs.HasBuff(ChainBuff);
        int count = chainActive
            ? Math.Max(0, Math.Min(2, (int)buffs.GetCustomVar(ChainCount))) + 1
            : 1;
        if (count >= 3)
        {
            buffs.RemoveBuff(ChainBuff);
            buffs.SetCustomVar(ChainCount, 0f, true);
            buffs.AddBuff(CooldownBuff, -1, false, false, 300f);
            float durationBonus = TierBonus(buffs.GetCustomVar("$RampageIncreaseDuration"));
            buffs.AddBuff(RampageBuff, -1, false, false, 30f * (1f + durationBonus));
            return;
        }

        buffs.SetCustomVar(ChainCount, count, true);
        // 2.6 used stack_type=ignore on the counter: later dismemberments
        // count toward the first hit's deadline, rather than refreshing it.
        if (!chainActive) buffs.AddBuff(ChainBuff, -1, false, false, 10f + 10f * chanceTier);
    }

    private static float TierBonus(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        return Math.Max(0f, Math.Min(1f, (float)Math.Round(value * 5f) / 5f));
    }
}
