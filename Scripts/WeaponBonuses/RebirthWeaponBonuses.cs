using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 Moonlock weapon bonuses. The bonus is stored on the weapon, and needs no class, perk or mod.
/// Weapons roll one from loot and quest rewards; boss crates always give one.
/// </summary>
public static class RebirthWeaponBonuses
{
    public sealed class Bonus
    {
        public string Key, TargetBuff, PlayerBuff;
        public float ChancePerLevel;
        public bool PlayerBuffHasLevels = true;
    }

    public static readonly Bonus[] All =
    {
        new Bonus { Key = "ArrowRain", TargetBuff = "FuriousRamsayArrowRainParticle", ChancePerLevel = 3f },
        new Bonus { Key = "RangedStun", TargetBuff = "FuriousRamsayRangedStun", ChancePerLevel = 2f },
        new Bonus { Key = "RangedBleed", TargetBuff = "FuriousRamsayRangedBleedDamage", ChancePerLevel = 3f },
        new Bonus { Key = "RangedDoubleDamage", PlayerBuff = "FuriousRamsayRangedDoubleDamage", PlayerBuffHasLevels = false, ChancePerLevel = 3f },
        new Bonus { Key = "RangedShock", TargetBuff = "FuriousRamsayAddShockBonusDamageAoE", ChancePerLevel = 3f },
        new Bonus { Key = "RangedExplosion", PlayerBuff = "FuriousRamsayAddBonusMadmanExplosionDamage", ChancePerLevel = 3f },
        new Bonus { Key = "RangedDismember", PlayerBuff = "FuriousRamsayDismember", ChancePerLevel = 2f },
        new Bonus { Key = "RangedSlashes", TargetBuff = "FuriousRamsayKnifeSlashesParticle", ChancePerLevel = 3f },
    };

    internal const string KeyMetadata = "rebirthWeaponBonus", LevelMetadata = "rebirthWeaponBonusLevel";
    // Provisional: 2.6 had no container loot route, only offers and quests.
    internal const float LootChance = 0.10f;
    internal const float QuestChance = 0.25f;
    internal static bool ForceTrigger;
    internal static int Triggers;

    private static readonly System.Random random = new System.Random();
    private static readonly Dictionary<int, float> nextMultiHit = new Dictionary<int, float>();

    public static Bonus Find(string key)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].Key == key) return All[i];
        return null;
    }

    public static bool TryRead(ItemValue item, out Bonus bonus, out int level)
    {
        bonus = null; level = 0;
        if (item == null || item.Metadata == null) return false;
        string key;
        if (!item.TryGetMetadata(KeyMetadata, out key) || !item.TryGetMetadata(LevelMetadata, out level)) return false;
        bonus = Find(key);
        return bonus != null && level > 0;
    }

    public static void Write(ItemValue item, Bonus bonus, int level)
    {
        item.SetMetadata(KeyMetadata, bonus.Key);
        item.SetMetadata(LevelMetadata, Mathf.Clamp(level, 1, MaxLevel));
    }

    internal const int MaxLevel = 10;

    public static void Clear(ItemValue item)
    {
        if (item?.Metadata == null) return;
        item.Metadata.Remove(KeyMetadata);
        item.Metadata.Remove(LevelMetadata);
    }

    public static bool CanHold(ItemValue item)
    {
        var cls = item?.ItemClass;
        if (cls == null || !item.HasQuality) return false;
        if (cls.HasAnyTags(MotorTool) || cls.HasAnyTags(Ammo) || cls.HasAnyTags(RepairTool)) return false;
        return cls.HasAnyTags(Ranged) || (cls.HasAnyTags(Melee) && !cls.HasAnyTags(Tool)) || cls.HasAnyTags(Axe);
    }

    private static readonly FastTags<TagGroup.Global> MotorTool = FastTags<TagGroup.Global>.Parse("motorTool"), Ammo = FastTags<TagGroup.Global>.Parse("ammo"),
        RepairTool = FastTags<TagGroup.Global>.Parse("repairTool"), Ranged = FastTags<TagGroup.Global>.Parse("ranged"), Melee = FastTags<TagGroup.Global>.Parse("melee"),
        Tool = FastTags<TagGroup.Global>.Parse("tool"), Axe = FastTags<TagGroup.Global>.Parse("axe"), Ally = FastTags<TagGroup.Global>.Parse("ally,trader,vehicle,drone");

    private static bool TryRoll(ItemStack stack, int level, float chance, out ItemStack result)
    {
        result = null;
        if (stack == null || stack.IsEmpty() || !CanHold(stack.itemValue)) return false;
        Bonus held; int heldLevel;
        if (TryRead(stack.itemValue, out held, out heldLevel) || random.NextDouble() >= chance) return false;
        result = stack.Clone();
        Write(result.itemValue, All[random.Next(All.Length)], level);
        return true;
    }

    /// <summary>Boss crates name their tier in the loot list and always give an bonus at that level.</summary>
    private static int BossTier(string lootList)
    {
        const string prefix = "rebirthBossRewardT";
        int tier;
        return lootList != null && lootList.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(lootList.Substring(prefix.Length), out tier) ? tier : 0;
    }

    private static int BiomeTier(World world, Vector3i position)
    {
        switch ((world.GetBiome(position.x, position.z)?.m_sBiomeName ?? string.Empty).ToLowerInvariant())
        {
            case "desert": return 2;
            case "snow": return 3;
            case "wasteland": return 4;
            case "burnt_forest": return 5;
            default: return 1;
        }
    }

    internal static void ApplyToLoot(ItemStack[] slots, string lootList, World world, Vector3i position)
    {
        if (slots == null || world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        int boss = BossTier(lootList), tier = BiomeTier(world, position);
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack rolled;
            if (TryRoll(slots[i], boss > 0 ? boss : random.Next(1, tier + 1), boss > 0 ? 1f : LootChance, out rolled)) slots[i] = rolled;
        }
    }

    internal static void ApplyToQuestReward(RewardLootItem reward)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || reward?.OwnerQuest?.QuestClass == null) return;
        int tier = reward.OwnerQuest.QuestClass.DifficultyTier;
        ItemStack rolled;
        if (tier > 1 && TryRoll(reward.Item, tier - 1, QuestChance, out rolled)) reward.Item.itemValue = rolled.itemValue;
    }

    /// <summary>2.6 job rewards: armor can carry bonuses, more often and more of them on harder jobs.</summary>
    internal static void ApplyArmorToQuestReward(RewardLootItem reward)
    {
        var item = reward?.Item?.itemValue;
        EquipmentSlots slot;
        if (item == null || reward.OwnerQuest?.QuestClass == null || !RebirthArmorBonusKits.TryGetSlot(item, out slot) || RebirthArmorBonusStorage.Read(item).Length > 0) return;
        int tier = reward.OwnerQuest.QuestClass.DifficultyTier;
        float chance = tier == 1 ? 50f : tier == 2 ? 62f : tier == 3 ? 75f : tier == 4 ? 87f : 100f;
        if (random.NextDouble() * 100.0 > chance) return;
        int count = tier <= 2 ? 1 : tier == 3 ? 2 : tier == 4 ? (random.NextDouble() < 0.5 ? 2 : 3) : (random.NextDouble() < 0.85 ? 3 : 4);
        int level = tier <= 2 ? 1 : tier <= 4 ? 2 : 3;
        var rolled = RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], count, level, Math.Max(1, (int)item.Quality), random);
        if (rolled.Count > 0) RebirthArmorBonusStorage.Write(item, rolled);
    }

    /// <summary>The 2.6 chance, in percent. Ranged ignores level there, and melee uses attack speed.</summary>
    private static readonly FastTags<TagGroup.Global>
        Throwable = FastTags<TagGroup.Global>.Parse("throwable");

    /// <summary>2.6 RebirthUtilities.isMeleeWeapon: the weapon's own "melee" tag.</summary>
    public static bool IsMelee(ItemValue item) => item?.ItemClass != null && item.ItemClass.HasAnyTags(Melee);

    /// <summary>
    /// 2.6 SetBonusChance / GetBonusChance (all eight bonuses use overrideChance):
    /// melee = level x chancePerLevel x 2, x 3/4 on a primary attack, x 180 / attacks per minute;
    /// ranged = 10 x chancePerLevel x 60 / rounds per minute (a weapon whose base magazine holds one round counts as 60),
    /// so a ranged chance does not grow with the level. Capped at 20%. Rampage (x3) and the forced test mode are
    /// applied by the hit, after the cap, as in 2.6. A null holder gives the item-only chance 2.6 printed in the description.
    /// </summary>
    public static float Chance(ItemValue item, Bonus bonus, int level, EntityAlive holder, bool primary)
    {
        float chance;
        if (IsMelee(item))
        {
            var tags = holder == null ? default(FastTags<TagGroup.Global>) : primary ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag;
            float attacksPerMinute = EffectManager.GetValue(PassiveEffects.AttacksPerMinute, item, 60f, holder, tags: tags);
            chance = level * bonus.ChancePerLevel * 2f;
            if (primary) chance *= 0.75f;
            chance = chance * 180f / Mathf.Max(1f, attacksPerMinute);
        }
        else
        {
            var ranged = item.ItemClass.Actions[0] as ItemActionRanged;
            if (ranged == null) return 0f;
            float roundsPerMinute = EffectManager.GetValue(PassiveEffects.RoundsPerMinute, item, 60f / Mathf.Max(0.01f, ranged.Delay), holder);
            var baseItem = ItemClass.GetItem(item.ItemClass.GetItemName(), false);
            int magazine = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, baseItem, ranged.BulletsPerMagazine, holder);
            if (magazine == 1) roundsPerMinute = 60f;
            chance = 10f * bonus.ChancePerLevel * 60f / Mathf.Max(1f, roundsPerMinute);
        }
        return Mathf.Min(chance, 20f);
    }

    /// <summary>Per-player test switch: every hit with a bonus weapon triggers (the 2.6 test mode).</summary>
    public const string ForceCVar = "$rbWeaponBonusForce";
    public static bool Forced(EntityAlive attacker) => ForceTrigger || attacker != null && attacker.Buffs != null && attacker.Buffs.GetCustomVar(ForceCVar) > 0f;

    /// <summary>Test helper: applies the bonus's buffs to this target, exactly as a trigger does.</summary>
    internal static string Fire(Bonus bonus, int level, EntityPlayer attacker, EntityAlive target)
    {
        string result = string.Empty;
        if (bonus.PlayerBuff != null) { string name = bonus.PlayerBuffHasLevels ? bonus.PlayerBuff + level : bonus.PlayerBuff; result += name + " on player: " + attacker.Buffs.AddBuff(name) + " "; }
        if (bonus.TargetBuff != null) result += bonus.TargetBuff + level + " on target: " + target.Buffs.AddBuff(bonus.TargetBuff + level, attacker.entityId);
        return result;
    }

    internal static void OnHit(EntityAlive target, DamageSource source, int damage)
    {
        if (damage <= 0 || source == null || target.world == null || target.world.IsRemote() || target.IsDead() || source.damageSource != EnumDamageSource.External) return;
        if (target is EntityPlayer || target.HasAnyTags(Ally)) return;
        // 2.6: any attacker holding the weapon (players and followers), never from a vehicle.
        var attacker = target.world.GetEntity(source.getEntityId()) as EntityAlive;
        ItemValue item = source.AttackingItem;
        Bonus bonus; int level;
        if (attacker == null || ReferenceEquals(attacker, target) || attacker.AttachedToEntity != null || !TryRead(item, out bonus, out level)) return;
        // 2.6: melee weapons or ranged-tagged weapons, never motor tools, never a gun loaded with mind control ammo.
        bool melee = IsMelee(item);
        if (!melee && !item.ItemClass.HasAnyTags(Ranged) || item.ItemClass.HasAnyTags(MotorTool)) return;
        if (!melee && item.ItemClass.Actions[0] is ItemActionAttack gun && gun.MagazineItemNames != null && item.SelectedAmmoTypeIndex >= 0
            && item.SelectedAmmoTypeIndex < gun.MagazineItemNames.Length
            && gun.MagazineItemNames[item.SelectedAmmoTypeIndex].IndexOf("mindcontrol", StringComparison.OrdinalIgnoreCase) >= 0) return;
        int owner;
        RebirthNpcStableId stable;
        if (RebirthBlackMagicService.IsControlled(target.entityId, out owner) || RebirthBoundUndeadService.IsBoundEntity(target.entityId, out stable)) return;

        // Shotgun pellets count as one trigger, as in 2.6.
        // 2.6: a shotgun rolls once per second (pellets are one shot) and always as a ranged, non-primary roll.
        bool shotgun = item.ItemClass.HasAnyTags(Shotgun);
        if (shotgun)
        {
            float next;
            if (nextMultiHit.TryGetValue(attacker.entityId, out next) && Time.time < next) return;
            nextMultiHit[attacker.entityId] = Time.time + 1f;
        }

        // 2.6: a melee hit is primary unless the power attack is running; throwables always count as primary.
        bool primary = shotgun ? false : !melee || item.ItemClass.HasAnyTags(Throwable) || IsPrimary(attacker);
        float chance = Chance(item, bonus, level, attacker, primary);
        bool forced = Forced(attacker);
        if (forced) chance = 100f;
        if (attacker.Buffs.HasBuff("FuriousRamsayRampage")) chance *= 3f;
        if (random.Next(0, 100) >= chance) return;
        Triggers++;
        if (forced) Log.Out("[REBIRTH WeaponBonus] " + bonus.Key + " level " + level + " fired on " + target.EntityClass?.entityClassName);

        if (bonus.PlayerBuff != null) attacker.Buffs.AddBuff(bonus.PlayerBuffHasLevels ? bonus.PlayerBuff + level : bonus.PlayerBuff);
        if (bonus.TargetBuff != null) target.Buffs.AddBuff(bonus.TargetBuff + level, attacker.entityId);
    }

    private static readonly FastTags<TagGroup.Global> Shotgun = FastTags<TagGroup.Global>.Parse("shotgun");

    private static bool IsPrimary(EntityAlive attacker)
    {
        var actions = attacker.inventory?.holdingItemData?.actionData;
        var power = actions != null && actions.Count > 1 ? actions[1] as ItemActionDynamicMelee.ItemActionDynamicMeleeData : null;
        return power == null || !power.Attacking;
    }

    /// <summary>
    /// The 2.6 item description lines: "BONUS: name (Level n)" and "CHANCE TO TRIGGER: x.xx%". As in 2.6 the chance is the
    /// weapon's own (its mods count, the holder's perks and buffs do not), for a primary attack.
    /// </summary>
    public static string Describe(ItemValue item, EntityAlive holder = null)
    {
        Bonus bonus; int level;
        if (!TryRead(item, out bonus, out level)) return string.Empty;
        float chance = Chance(item, bonus, level, null, true);
        return Localization.Get("statBonus").ToUpper() + ": "
            + string.Format("{0} [c9c7c7]([-]{1} [e0dcab]{2}[-][c9c7c7])[-]", Localization.Get("xui" + bonus.Key), Localization.Get("ttLevelFull"), level) + "\n"
            + Localization.Get("statChanceToTrigger").ToUpper() + ": [e0dcab]" + chance.ToString("F2", CultureInfo.InvariantCulture) + "[-]%";
    }

    /// <summary>The bonus key used as the item type icon (ui_game_symbol_&lt;key&gt;), as 2.6 did in item info, trader and reward entries.</summary>
    internal static bool TryIcon(ItemStack stack, out string key)
    {
        key = null;
        Bonus bonus; int level;
        if (stack == null || stack.IsEmpty() || !TryRead(stack.itemValue, out bonus, out level)) return false;
        key = bonus.Key;
        return true;
    }
}

/// <summary>Bonus effects spare undead that a player controls, as 2.6 spared mind-controlled ones.</summary>
[Preserve]
public class RebirthWeaponBonusTargetAllowed : TargetedCompareRequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params) || target == null) return false;
        int owner; RebirthNpcStableId stable;
        bool spared = RebirthBlackMagicService.IsControlled(target.entityId, out owner) || RebirthBoundUndeadService.IsBoundEntity(target.entityId, out stable);
        return invert ? spared : !spared;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.DamageEntity), new[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
internal static class RebirthWeaponBonusHitPatch
{
    private static void Postfix(EntityAlive __instance, DamageSource _damageSource, int __result)
    {
        RebirthWeaponBonuses.OnHit(__instance, _damageSource, __result);
    }
}

/// <summary>While a Dismember bonus runs, the holder's hits dismember at the 2.6 rates. Bosses stay immune.</summary>
[HarmonyPatch(typeof(EntityAlive), "GetDismemberChance")]
internal static class RebirthWeaponBonusDismemberPatch
{
    private static readonly FastTags<TagGroup.Global> Boss = FastTags<TagGroup.Global>.Parse("boss"), Feral = FastTags<TagGroup.Global>.Parse("feral"), Radiated = FastTags<TagGroup.Global>.Parse("radiated");

    private static void Postfix(EntityAlive __instance, ref float __result, ref DamageResponse _dmResponse)
    {
        if (_dmResponse.Source == null || __instance.world == null) return;
        var attacker = __instance.world.GetEntity(_dmResponse.Source.getEntityId()) as EntityPlayer;
        if (attacker == null || __instance.HasAnyTags(Boss)) return;
        for (int i = 1; i < 11; i++)
        {
            if (!attacker.Buffs.HasBuff("FuriousRamsayDismember" + i)) continue;
            float chance = __instance.HasAnyTags(Radiated) ? i * 1.5f / 100f : __instance.HasAnyTags(Feral) ? (10f + i * 2f) / 100f : (20f + i * 2.5f) / 100f;
            __result = Mathf.Max(__result, chance);
            return;
        }
    }
}

[HarmonyPatch(typeof(LootManager), nameof(LootManager.LootContainerOpened))]
internal static class RebirthWeaponBonusContainerPatch
{
    private static void Prefix(TEFeatureStorage _tileEntity, out bool __state)
    {
        __state = _tileEntity != null && _tileEntity.ItemGrid.Touched;
    }

    private static void Postfix(TEFeatureStorage _tileEntity, bool __state)
    {
        if (__state || _tileEntity == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        RebirthWeaponBonuses.ApplyToLoot(_tileEntity.ItemGrid.items, _tileEntity.lootListName, GameManager.Instance?.World, _tileEntity.ToWorldPos());
    }
}

[HarmonyPatch(typeof(LootManager), nameof(LootManager.LootBagOpened))]
internal static class RebirthWeaponBonusBagPatch
{
    private static void Prefix(Bag _bag, out bool __state)
    {
        __state = _bag != null && _bag.Touched;
    }

    private static void Postfix(Bag _bag, Entity _bagOwner, bool __state)
    {
        if (__state || _bag == null || _bagOwner == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        RebirthWeaponBonuses.ApplyToLoot(_bag.ItemGrid.items, _bagOwner.GetLootList(), GameManager.Instance?.World, _bagOwner.GetBlockPosition());
    }
}

[HarmonyPatch(typeof(RewardLootItem), nameof(RewardLootItem.SetupReward))]
internal static class RebirthWeaponBonusQuestPatch
{
    private static void Postfix(RewardLootItem __instance)
    {
        RebirthWeaponBonuses.ApplyToQuestReward(__instance);
        RebirthWeaponBonuses.ApplyArmorToQuestReward(__instance);
    }
}

/// <summary>Shows the bonus's symbol on the item slot, as 2.6 did.</summary>
// 2.6: a weapon with a bonus shows the bonus symbol as its item type icon in the item info panel, trader stock
// entries and quest reward entries (ui_game_symbol_{itemtypeicon}).
internal static class RebirthWeaponBonusTypeIcon
{
    internal static void Apply(ItemStack stack, string bindingName, ref string value, ref bool result)
    {
        if (bindingName != "itemtypeicon" && bindingName != "hasitemtypeicon" && bindingName != "itemtypeicontint") return;
        string key;
        if (!RebirthWeaponBonuses.TryIcon(stack, out key)) return;
        // The symbol keeps its own colours (the native tint marks boosted or combined items).
        value = bindingName == "itemtypeicon" ? key : bindingName == "hasitemtypeicon" ? "true" : "255,255,255,255";
        result = true;
    }

    /// <summary>The 3.0 toolbelt keeps its type, lock and favourite icons top right, so its bonus symbol has its own top-left sprite, as in 2.6.</summary>
    internal static void ApplyOwnSprite(ItemStack stack, string bindingName, ref string value, ref bool result)
    {
        if (bindingName != "bonustypeicon" && bindingName != "hasbonustypeicon") return;
        string key;
        bool has = RebirthWeaponBonuses.TryIcon(stack, out key);
        value = bindingName == "bonustypeicon" ? (has ? "ui_game_symbol_" + key : string.Empty) : has ? "true" : "false";
        result = true;
    }
}

[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValueInternal))]
internal static class RebirthWeaponBonusInfoIconPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance, ref string value, string bindingName, ref bool __result)
        => RebirthWeaponBonusTypeIcon.Apply(__instance.itemStack, bindingName, ref value, ref __result);
}

[HarmonyPatch(typeof(XUiC_TraderItemEntry), nameof(XUiC_TraderItemEntry.GetBindingValueInternal))]
internal static class RebirthWeaponBonusTraderIconPatch
{
    private static void Postfix(XUiC_TraderItemEntry __instance, ref string value, string bindingName, ref bool __result)
        => RebirthWeaponBonusTypeIcon.Apply(__instance.item, bindingName, ref value, ref __result);
}

[HarmonyPatch(typeof(XUiC_QuestTurnInEntry), nameof(XUiC_QuestTurnInEntry.GetBindingValueInternal))]
internal static class RebirthWeaponBonusRewardIconPatch
{
    private static void Postfix(XUiC_QuestTurnInEntry __instance, ref string value, string bindingName, ref bool __result)
        => RebirthWeaponBonusTypeIcon.Apply(__instance.reward != null ? __instance.item : null, bindingName, ref value, ref __result);
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.GetBindingValueInternal))]
internal static class RebirthWeaponBonusSlotIconPatch
{
    // Every slot (backpack, toolbelt, containers, stations, vehicles, NPC and camper storage) shows the bonus as its item type icon.
    private static void Postfix(XUiC_ItemStack __instance, ref string _value, string _bindingName, ref bool __result)
    {
        if (__instance is XUiC_RebirthToolbeltItemSlot) RebirthWeaponBonusTypeIcon.ApplyOwnSprite(__instance.itemStack, _bindingName, ref _value, ref __result);
        else RebirthWeaponBonusTypeIcon.Apply(__instance.itemStack, _bindingName, ref _value, ref __result);
    }
}

// The bonus symbol is never tinted. Every window (slots, item info, trader, quest rewards, combine grid) gets the
// item type icon colour from this one helper, both its typed bindings and the text bindings, so a weapon with a
// bonus gets plain white here; the native colours (boosted stats, combined, recipe unlock) are for type icons only.
[HarmonyPatch(typeof(XUiBindingHelper), nameof(XUiBindingHelper.GetItemTypeIconTintColor))]
internal static class RebirthWeaponBonusNoTintPatch
{
    private static void Postfix(ItemStack itemStack, ref Color32 __result)
    {
        if (RebirthWeaponBonuses.TryIcon(itemStack, out _)) __result = new Color32(255, 255, 255, 255);
    }
}

// Equipment-style slots (XUiC_ItemStackSlot) use typed bindings for the icon itself, not GetBindingValueInternal.
[HarmonyPatch(typeof(XUiC_ItemStackSlot), nameof(XUiC_ItemStackSlot.itemTypeIcon))]
internal static class RebirthWeaponBonusStackSlotIconPatch
{
    private static void Postfix(ItemStack _stack, ref string __result)
    {
        if (RebirthWeaponBonuses.TryIcon(_stack, out string key)) __result = key;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStackSlot), nameof(XUiC_ItemStackSlot.hasItemTypeIcon))]
internal static class RebirthWeaponBonusStackSlotHasIconPatch
{
    private static void Postfix(ItemStack _stack, ref bool __result)
    {
        if (RebirthWeaponBonuses.TryIcon(_stack, out _)) __result = true;
    }
}

[Preserve]
public sealed class RebirthWeaponBonusModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("rebirth.weaponbonuses");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusHitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusDismemberPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusContainerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusBagPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusQuestPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusSlotIconPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusInfoIconPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusTraderIconPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusRewardIconPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusNoTintPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusStackSlotIconPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthWeaponBonusStackSlotHasIconPatch));
    }
}

/// <summary>
/// 2.6: a weapon with a bonus shows the bonus symbol on its icon wherever it is selected. Each selected-item panel has a
/// "&lt;icon id&gt;Bonus" sprite; this puts it on the top-left corner of that panel's icon, scaled to the icon.
/// </summary>
public static class RebirthWeaponBonusSelectedIcon
{
    public static void Show(XUiController owner, string iconId, ItemValue item)
    {
        var icon = owner?.GetChildById(iconId)?.ViewComponent as XUiV_Sprite;
        var badge = owner?.GetChildById(iconId + "Bonus")?.ViewComponent as XUiV_Sprite;
        if (badge == null) return;
        string key;
        bool show = icon != null && icon.IsVisible && item != null && !item.IsEmpty() && RebirthWeaponBonuses.TryIcon(new ItemStack(item, 1), out key);
        if (show)
        {
            RebirthWeaponBonuses.TryIcon(new ItemStack(item, 1), out key);
            int size = Mathf.Clamp(Mathf.RoundToInt(icon.Size.x * 0.3f), 20, 32);
            badge.SpriteName = "ui_game_symbol_" + key;
            badge.Size = new Vector2i(size, size);
            badge.Position = new Vector2i(icon.Position.x + 2, icon.Position.y - 2);
            badge.TryUpdatePosition();
        }
        if (badge.IsVisible != show) badge.IsVisible = show;
        badge.isDirty = true;
    }
}
