using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

public enum RebirthArmorBonusRarity { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }

public sealed class RebirthArmorBonusDefinition
{
    public string Id, FamilyId;
    public EquipmentSlots Slot;
    public PassiveEffects Effect;
    public PassiveEffects[] AlsoEffects;
    public RebirthArmorBonusRarity Rarity;
    public float Min, Max;
    public bool HasTags;
    public FastTags<TagGroup.Global> Tags;
    public HashSet<string> Skills, Items;
    public bool AddsToBase, Flat, Negative, ShowAsSeconds, ScaleWithWeaponSkill, HasContext, HasAltContext;
    public FastTags<TagGroup.Global> Context, AltContext;
    public float AltValue;
    public string Special;
    /// <summary>What the bonus applies to, from its skills, context, items or tags (null when it applies to everything).
    /// A bonus whose tokens include another bonus's tokens is a bigger version of it (armor bonus focus subjects).</summary>
    public string[] TokenNames;
    /// <summary>Kept so items that already have it keep it, but never rolled again (owner 2026-11-08).</summary>
    public bool Retired;

    // One rule for play and for tests: does this bonus apply to that weapon?
    public bool AppliesToWeapon(ItemValue weapon)
    {
        var item = weapon != null && !weapon.IsEmpty() ? weapon.ItemClass : null;
        if (Items != null && (item == null || !Items.Contains(item.GetItemName()))) return false;
        if (HasTags && (item == null || !item.ItemTags.Test_AnySet(Tags))) return false;
        if (Skills != null && (item == null || !Skills.Contains(RebirthArmorBonusWeaponSkills.Of(weapon)))) return false;
        return true;
    }
}

public struct RebirthArmorRolledBonus
{
    public RebirthArmorBonusDefinition Definition;
    public float Value;
}

/// <summary>Caches the REBIRTH skill of each weapon type.</summary>
public static class RebirthArmorBonusWeaponSkills
{
    private static readonly Dictionary<int, string> Cache = new Dictionary<int, string>();
    // Half the bonus at skill 0, all of it at skill 100.
    public static float Scale(EntityAlive entity, ItemValue weapon)
    {
        float skill;
        var player = entity as EntityPlayer;
        if (player == null || !RebirthSkillOutcomeValueService.TryGetEffective(player, Of(weapon), out skill)) return 0.5f;
        return 0.5f + 0.5f * Math.Max(0f, Math.Min(1f, skill / 100f));
    }

    public static string Of(ItemValue weapon)
    {
        if (weapon == null || weapon.IsEmpty()) return string.Empty;
        string skill;
        lock (Cache)
        {
            if (!Cache.TryGetValue(weapon.type, out skill))
            {
                skill = RebirthProgressionRuntimeConfig.ClassifyCombat(weapon) ?? string.Empty;
                Cache[weapon.type] = skill;
            }
        }
        return skill;
    }
}

/// <summary>Bonus definitions from Config/_ArmorBonuses/bonuses.xml. A family belongs to one slot.</summary>
public static class RebirthArmorBonusCatalog
{
    private static readonly Dictionary<string, RebirthArmorBonusDefinition> ById = new Dictionary<string, RebirthArmorBonusDefinition>(StringComparer.Ordinal);
    private static readonly List<RebirthArmorBonusDefinition> All = new List<RebirthArmorBonusDefinition>();
    private static readonly Dictionary<int, int[]> LevelWeights = new Dictionary<int, int[]>();
    private static int[] qualityStep = new int[4];
    private static bool[] relevantEffects = new bool[0];
    private static bool loaded;

    public static IReadOnlyList<RebirthArmorBonusDefinition> Definitions { get { EnsureLoaded(); return All; } }

    public static bool TryGet(string id, out RebirthArmorBonusDefinition definition)
    {
        EnsureLoaded();
        return ById.TryGetValue(id, out definition);
    }

    public static bool IsRelevantEffect(PassiveEffects effect)
    {
        EnsureLoaded();
        int index = (int)effect;
        return index >= 0 && index < relevantEffects.Length && relevantEffects[index];
    }

    /// <summary>Weights for a kit level at an item quality. Quality closes the gap between rarities.</summary>
    public static int[] GetRarityWeights(int level, int quality)
    {
        EnsureLoaded();
        int[] baseWeights;
        if (!LevelWeights.TryGetValue(level, out baseWeights)) baseWeights = new[] { 100, 0, 0, 0 };
        int steps = Math.Max(0, Math.Min(5, quality - 1));
        return new[]
        {
            baseWeights[0],
            baseWeights[1] + qualityStep[1] * steps,
            baseWeights[2] + qualityStep[2] * steps,
            baseWeights[3] + qualityStep[3] * steps
        };
    }

    private static string[] Tokens(string list)
    {
        if (string.IsNullOrEmpty(list)) return null;
        var parts = list.Split(new[] { (char)44 }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        return parts.Length == 0 ? null : parts;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        string path = Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot() ?? string.Empty, "..", "_ArmorBonuses", "bonuses.xml");
        try
        {
            var root = XDocument.Load(path).Root;
            var weights = root.Element("rarity_weights");
            foreach (var level in weights.Elements("level"))
                LevelWeights[(int)level.Attribute("id")] = new[] { (int)level.Attribute("common"), (int)level.Attribute("uncommon"), (int)level.Attribute("rare"), (int)level.Attribute("legendary") };
            var step = weights.Element("quality_step");
            qualityStep = new[] { 0, (int)step.Attribute("uncommon"), (int)step.Attribute("rare"), (int)step.Attribute("legendary") };

            int maxEffect = 0;
            foreach (var effectValue in Enum.GetValues(typeof(PassiveEffects))) maxEffect = Math.Max(maxEffect, Convert.ToInt32(effectValue));
            relevantEffects = new bool[maxEffect + 1];
            foreach (var family in root.Elements("family"))
            {
                string familyId = (string)family.Attribute("id");
                var slot = (EquipmentSlots)Enum.Parse(typeof(EquipmentSlots), (string)family.Attribute("slot"));
                var effect = (PassiveEffects)Enum.Parse(typeof(PassiveEffects), (string)family.Attribute("effect"));
                if (effect != PassiveEffects.None) relevantEffects[(int)effect] = true;
                var alsoEffects = new List<PassiveEffects>();
                foreach (string extra in ((string)family.Attribute("also_effects") ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parsed = (PassiveEffects)Enum.Parse(typeof(PassiveEffects), extra);
                    alsoEffects.Add(parsed);
                    relevantEffects[(int)parsed] = true;
                }
                foreach (var bonus in family.Elements("bonus"))
                {
                    string tags = (string)bonus.Attribute("tags") ?? (string)family.Attribute("tags");
                    string skills = (string)bonus.Attribute("skills"), items = (string)bonus.Attribute("items");
                    string context = (string)bonus.Attribute("context") ?? (string)family.Attribute("context"), altContext = (string)bonus.Attribute("alt_context");
                    var definition = new RebirthArmorBonusDefinition
                    {
                        Id = (string)bonus.Attribute("id"),
                        FamilyId = (string)family.Attribute("roll_group") ?? familyId,
                        Slot = slot,
                        Effect = effect,
                        AlsoEffects = alsoEffects.ToArray(),
                        Rarity = (RebirthArmorBonusRarity)Enum.Parse(typeof(RebirthArmorBonusRarity), (string)bonus.Attribute("rarity")),
                        Min = float.Parse((string)bonus.Attribute("min"), CultureInfo.InvariantCulture),
                        Max = float.Parse((string)bonus.Attribute("max"), CultureInfo.InvariantCulture),
                        HasTags = !string.IsNullOrEmpty(tags),
                        Tags = string.IsNullOrEmpty(tags) ? default(FastTags<TagGroup.Global>) : FastTags<TagGroup.Global>.Parse(tags),
                        Skills = string.IsNullOrEmpty(skills) ? null : new HashSet<string>(skills.Split(','), StringComparer.OrdinalIgnoreCase),
                        Items = string.IsNullOrEmpty(items) ? null : new HashSet<string>(items.Split(','), StringComparer.Ordinal),
                        AddsToBase = (string)family.Attribute("op") == "base",
                        Flat = (string)family.Attribute("flat") == "true",
                        Negative = (string)family.Attribute("negative") == "true",
                        ShowAsSeconds = (string)family.Attribute("display") == "seconds",
                        ScaleWithWeaponSkill = (string)family.Attribute("scale_with_weapon_skill") == "true",
                        HasContext = !string.IsNullOrEmpty(context),
                        Context = string.IsNullOrEmpty(context) ? default(FastTags<TagGroup.Global>) : FastTags<TagGroup.Global>.Parse(context),
                        HasAltContext = !string.IsNullOrEmpty(altContext),
                        AltContext = string.IsNullOrEmpty(altContext) ? default(FastTags<TagGroup.Global>) : FastTags<TagGroup.Global>.Parse(altContext),
                        AltValue = bonus.Attribute("alt_value") != null ? float.Parse((string)bonus.Attribute("alt_value"), CultureInfo.InvariantCulture) : 0f,
                        Special = (string)family.Attribute("special"),
                        TokenNames = Tokens(skills ?? (string)bonus.Attribute("context") ?? (string)family.Attribute("context") ?? items ?? tags),
                        Retired = (string)bonus.Attribute("retired") == "true" || (string)family.Attribute("retired") == "true"
                    };
                    ById.Add(definition.Id, definition);
                    All.Add(definition);
                }
            }
            Log.Out("[REBIRTH ArmorBonuses] Loaded " + All.Count + " bonuses from " + path);
        }
        catch (Exception e)
        {
            Log.Error("[REBIRTH ArmorBonuses] Could not load " + path + ": " + e.Message);
        }
    }
}

/// <summary>Bonuses live in one string on the item, so native saves and network packets carry them.</summary>
public static class RebirthArmorBonusStorage
{
    public const string MetadataKey = "rbArmorBonus";
    private static readonly RebirthArmorRolledBonus[] None = new RebirthArmorRolledBonus[0];
    private static readonly Dictionary<string, RebirthArmorRolledBonus[]> Parsed = new Dictionary<string, RebirthArmorRolledBonus[]>(StringComparer.Ordinal);

    public static RebirthArmorRolledBonus[] Read(ItemValue item)
    {
        string raw;
        if (item == null || item.Metadata == null || !item.TryGetMetadata(MetadataKey, out raw) || string.IsNullOrEmpty(raw)) return None;
        RebirthArmorRolledBonus[] result;
        lock (Parsed)
        {
            if (Parsed.TryGetValue(raw, out result)) return result;
            var list = new List<RebirthArmorRolledBonus>();
            foreach (string part in raw.Split(';'))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                RebirthArmorBonusDefinition definition;
                float value;
                // A bonus removed from the XML is ignored, never an error for an old save.
                if (RebirthArmorBonusCatalog.TryGet(part.Substring(0, colon), out definition) &&
                    float.TryParse(part.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    list.Add(new RebirthArmorRolledBonus { Definition = definition, Value = value });
            }
            result = list.ToArray();
            Parsed[raw] = result;
        }
        return result;
    }

    public static void Write(ItemValue item, IEnumerable<RebirthArmorRolledBonus> bonuses)
    {
        var text = new StringBuilder();
        foreach (var bonus in bonuses)
        {
            if (text.Length > 0) text.Append(';');
            text.Append(bonus.Definition.Id).Append(':').Append(bonus.Value.ToString("0.####", CultureInfo.InvariantCulture));
        }
        item.SetMetadata(MetadataKey, text.ToString());
    }

    public static void Clear(ItemValue item)
    {
        if (item?.Metadata != null) item.Metadata.Remove(MetadataKey);
    }
}

/// <summary>Rarity first, then family, then bonus, with the 2.6 family precedence rules.</summary>
public static class RebirthArmorBonusRoller
{
    /// <summary>Alchemy and Regal: 75% end at 3 bonuses, 25% at 4.</summary>
    public static int RollTargetCount(Random random)
    {
        return random.NextDouble() < 0.25 ? 4 : 3;
    }

    public static List<RebirthArmorRolledBonus> Roll(EquipmentSlots slot, IEnumerable<RebirthArmorRolledBonus> existing, int targetCount, int level, int quality, Random random, IEnumerable<string> excludedIds = null)
        => Roll(slot, existing, targetCount, level, quality, random, excludedIds, null);

    /// <summary>
    /// Focused roll (spec section 5): the focus only narrows the candidates; the draw itself is unchanged.
    /// A chosen (focused) bonus only has to differ from the bonuses already rolled; it may share their family
    /// (owner 2026-11-08: Construction XP and Batons XP are different bonuses). Random rows keep the 2.6 family rule.
    /// </summary>
    public static List<RebirthArmorRolledBonus> Roll(EquipmentSlots slot, IEnumerable<RebirthArmorRolledBonus> existing, int targetCount, int level, int quality, Random random, RebirthArmorBonusFocus focus, IEnumerable<string> excludedIds = null)
        => focus.Depth == RebirthArmorBonusFocusDepth.Any
            ? Roll(slot, existing, targetCount, level, quality, random, excludedIds, null)
            : Roll(slot, existing, targetCount, level, quality, random, excludedIds, RebirthArmorBonusTree.Pool(slot, focus), true);

    private static List<RebirthArmorRolledBonus> Roll(EquipmentSlots slot, IEnumerable<RebirthArmorRolledBonus> existing, int targetCount, int level, int quality, Random random, IEnumerable<string> excludedIds, HashSet<string> allowed, bool sameBonusOnly = false)
    {
        var excluded = excludedIds != null ? new HashSet<string>(excludedIds, StringComparer.Ordinal) : null;
        var result = new List<RebirthArmorRolledBonus>(existing);
        var weights = RebirthArmorBonusCatalog.GetRarityWeights(level, quality);
        // The 2.6 draw: a family with even odds, then a rarity that family has, weighted by kit
        // level and item quality, then a bonus of that rarity. A family appears once per item.
        while (result.Count < targetCount)
        {
            var families = RebirthArmorBonusCatalog.Definitions
                .Where(d => d.Slot == slot && !d.Retired && (allowed == null || allowed.Contains(d.Id)) && (excluded == null || !excluded.Contains(d.Id))
                    && (sameBonusOnly
                        ? !result.Any(r => RebirthArmorBonusTree.ExactKey(r.Definition.Id) == RebirthArmorBonusTree.ExactKey(d.Id))
                        : !result.Any(r => r.Definition.FamilyId == d.FamilyId)))
                .GroupBy(d => d.FamilyId).ToList();
            if (families.Count == 0) break;
            var family = families[random.Next(families.Count)].ToList();
            var rarity = RollRarity(family, weights, random);
            var options = family.Where(d => d.Rarity == rarity).ToList();
            var chosen = options[random.Next(options.Count)];
            float value = chosen.Min + (float)random.NextDouble() * (chosen.Max - chosen.Min);
            result.Add(new RebirthArmorRolledBonus { Definition = chosen, Value = value });
        }
        return result;
    }

    /// <summary>Retuning "Reroll": the same exact bonus, its rarity rolled again among the rarities that bonus has
    /// (kit level and quality weights), then a new value in that row's range.</summary>
    public static RebirthArmorRolledBonus RerollSame(RebirthArmorRolledBonus bonus, int level, int quality, Random random)
    {
        if (bonus.Definition == null || !RebirthArmorBonusTree.TryGetExact(RebirthArmorBonusTree.ExactKey(bonus.Definition.Id), out var exact) || exact.Rows.Count == 0)
            return bonus;
        var rarity = RollRarity(exact.Rows, RebirthArmorBonusCatalog.GetRarityWeights(level, quality), random);
        var options = exact.Rows.Where(d => d.Rarity == rarity).ToList();
        var chosen = options[random.Next(options.Count)];
        return new RebirthArmorRolledBonus { Definition = chosen, Value = chosen.Min + (float)random.NextDouble() * (chosen.Max - chosen.Min) };
    }

    private static RebirthArmorBonusRarity RollRarity(List<RebirthArmorBonusDefinition> pool, int[] weights, Random random)
    {
        int total = 0;
        for (int i = 0; i < weights.Length; i++)
            if (pool.Any(d => (int)d.Rarity == i)) total += weights[i];
        // Only rarities the pool can actually supply take part.
        if (total <= 0) return pool[random.Next(pool.Count)].Rarity;
        int roll = random.Next(total);
        for (int i = 0; i < weights.Length; i++)
        {
            if (!pool.Any(d => (int)d.Rarity == i)) continue;
            if (roll < weights[i]) return (RebirthArmorBonusRarity)i;
            roll -= weights[i];
        }
        return pool[0].Rarity;
    }
}

public static class RebirthArmorBonusText
{
    private static readonly string[] Colors = { "c8c8c8", "88aaff", "ffaa55", "cc88ff" }; // 2.6 rarity text colours

    private static string Amount(RebirthArmorRolledBonus bonus)
    {
        float value = System.Math.Abs(bonus.Value);
        // Every bonus shows the single value rolled when it was made (owner 2026-11-08: no weapon-skill range).
        if (bonus.Definition.ShowAsSeconds) return (1f / value).ToString("0", CultureInfo.InvariantCulture);
        return bonus.Definition.Flat ? value.ToString("0", CultureInfo.InvariantCulture) : (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The bonus text with its value. Some texts are written "{0}% more ..." while percentages already carry
    /// their "%", so a "%" right after {0} is dropped (owner 2026-11-08: "2.6%% more axes skill experience").</summary>
    private static string Format(RebirthArmorRolledBonus bonus, string amount)
    {
        string format = Localization.Get("aeBonusFmt_" + bonus.Definition.Id);
        if (amount.EndsWith("%", StringComparison.Ordinal)) format = format.Replace("{0}%", "{0}");
        return format.Replace("{0}", amount);
    }

    public static string Describe(RebirthArmorRolledBonus bonus)
    {
        string format = Localization.Get("aeBonusFmt_" + bonus.Definition.Id);
        string amount = Amount(bonus);
        return "[" + Colors[(int)bonus.Definition.Rarity] + "]" + Format(bonus, amount) + "[-]";
    }

    public static string Plain(RebirthArmorRolledBonus bonus)
    {
        string amount = Amount(bonus);
        return Format(bonus, amount);
    }

    public static string DescribeArmor(ItemValue item) => string.Join("\n", RebirthArmorBonusStorage.Read(item).Select(Describe));

    /// <summary>Armor bonuses, then the weapon bonus lines (2.6 put those last).</summary>
    public static string DescribeAll(ItemValue item)
    {
        string armor = DescribeArmor(item), weapon = RebirthWeaponBonuses.Describe(item);
        return armor.Length == 0 ? weapon : weapon.Length == 0 ? armor : armor + "\n" + weapon;
    }
}

/// <summary>Adds rolled bonuses to the native stat calculation for worn armor.</summary>
[HarmonyPatch(typeof(ItemValue), nameof(ItemValue.ModifyValue))]
internal static class RebirthArmorBonusEffectPatch
{
    private static void Postfix(ItemValue __instance, EntityAlive _entity, ItemValue _originalItemValue, PassiveEffects _passiveEffect, ref float _originalValue, ref float _perc_value, FastTags<TagGroup.Global> _tags)
    {
        // Almost every item leaves here: this runs for each item on each stat check.
        if (__instance.Metadata == null || _entity == null) return;
        if (!RebirthArmorBonusCatalog.IsRelevantEffect(_passiveEffect)) return;
        var bonuses = RebirthArmorBonusStorage.Read(__instance);
        if (bonuses.Length == 0) return;
        // Harvest and some other checks do not name the weapon, so use what is in hand.
        var weapon = _originalItemValue != null && !_originalItemValue.IsEmpty() ? _originalItemValue : _entity.inventory?.holdingItemItemValue;
        for (int i = 0; i < bonuses.Length; i++)
        {
            var definition = bonuses[i].Definition;
            if ((definition.Effect != _passiveEffect && Array.IndexOf(definition.AlsoEffects, _passiveEffect) < 0) || !definition.AppliesToWeapon(weapon)) continue;
            float value = bonuses[i].Value;
            var state = _tags | _entity.CurrentMovementTag | _entity.CurrentStanceTag;
            if (definition.HasContext && !state.Test_AnySet(definition.Context))
            {
                if (!definition.HasAltContext || !state.Test_AnySet(definition.AltContext)) continue;
                value = definition.AltValue;
            }
            if (definition.Special == "low_light" && _entity.GetCVar("_lightlevel") > RebirthArmorBonusSpecials.LowLightLevel) continue;
            // No weapon-skill scaling: the rolled value applies as it is, as in 2.6 (owner 2026-11-08).
            if (definition.AddsToBase) _originalValue += value;
            else _perc_value += value;
        }
    }
}

/// <summary>Lists an item's bonuses under its description.</summary>
[HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValueInternal))]
internal static class RebirthArmorBonusTooltipPatch
{
    private static void Postfix(XUiC_ItemInfoWindow __instance, ref string value, string bindingName)
    {
        if (bindingName != "itemdescription" || __instance?.itemStack == null || __instance.itemStack.IsEmpty()) return;
        // Armor bonuses above the description; the weapon bonus and its chance below it, as in 2.6.
        string armor = RebirthArmorBonusText.DescribeArmor(__instance.itemStack.itemValue);
        string weapon = RebirthWeaponBonuses.Describe(__instance.itemStack.itemValue);
        if (armor.Length > 0) value = armor + "\n\n" + value;
        if (weapon.Length > 0) value = value + "\n\n" + weapon;
    }
}

[Preserve]
public sealed class RebirthArmorBonusModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("rebirth.armorbonuses");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorBonusEffectPatch));
        // Bonus lines now live on the 2.6 star tab (RebirthArmorBonusInfoTab).
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorBonusSleeperPatch));
    }
}

/// <summary>Phase 1 test command until the workstation and kits exist.</summary>
[Preserve]
public sealed class ConsoleCmdRebirthArmorBonus : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rbarmorbonus" };
    public override string getDescription() => "Test armor bonuses on your equipped armor.";
    public override string getHelp() => "rbarmorbonus roll [level 1-3] [slot=Hands]  rolls and replaces bonuses\nrbarmorbonus show [slot=Hands]\nrbarmorbonus clear [slot=Hands]\nrbarmorbonus sim [level 1-3] [quality 1-6]  rarity percentages from 10000 rolls\nrbarmorbonus test  measures every bonus through the game's own stat calculation\nrbarmorbonus trace  toggles logging of your real hits";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        string action = args.Count > 0 ? args[0].ToLowerInvariant() : "show";
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (action == "sim") { Simulate(args, console); return; }
        if (player == null) { console.Output("No local player."); return; }
        if (action == "test") { RebirthArmorBonusSelfTest.Run(player, console); return; }
        if (action == "trace") { RebirthArmorBonusHitTrace.Enabled = !RebirthArmorBonusHitTrace.Enabled; console.Output("Hit trace " + (RebirthArmorBonusHitTrace.Enabled ? "on" : "off")); return; }

        var slot = EquipmentSlots.Hands;
        string slotArg = args.Count > 1 ? args[args.Count - 1] : null;
        EquipmentSlots parsed;
        if (slotArg != null && Enum.TryParse(slotArg, true, out parsed) && !int.TryParse(slotArg, out _)) slot = parsed;

        var item = player.equipment.GetSlotItem((int)slot);
        if (item == null || item.IsEmpty()) { console.Output("Nothing equipped in " + slot + "."); return; }

        if (action == "roll")
        {
            int level = 3;
            if (args.Count > 1) int.TryParse(args[1], out level);
            var random = new Random();
            var rolled = RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], RebirthArmorBonusRoller.RollTargetCount(random), level, item.Quality, random);
            var copy = item.Clone();
            RebirthArmorBonusStorage.Write(copy, rolled);
            player.equipment.SetSlotItem((int)slot, copy);
            item = copy;
        }
        else if (action == "clear")
        {
            var copy = item.Clone();
            RebirthArmorBonusStorage.Clear(copy);
            player.equipment.SetSlotItem((int)slot, copy);
            console.Output("Cleared " + slot + ".");
            return;
        }

        var bonuses = RebirthArmorBonusStorage.Read(item);
        console.Output(slot + " " + item.ItemClass.GetItemName() + " Q" + item.Quality + ": " + bonuses.Length + " bonuses");
        foreach (var bonus in bonuses)
            console.Output("  " + bonus.Definition.Rarity + " " + bonus.Definition.Id + " " + bonus.Value.ToString("P1", CultureInfo.InvariantCulture));
    }

    private static void Simulate(List<string> args, SdtdConsole console)
    {
        int level = 3, quality = 1;
        if (args.Count > 1) int.TryParse(args[1], out level);
        if (args.Count > 2) int.TryParse(args[2], out quality);
        var random = new Random();
        var counts = new int[4];
        int total = 0;
        for (int i = 0; i < 10000; i++)
            foreach (var bonus in RebirthArmorBonusRoller.Roll(EquipmentSlots.Hands, new RebirthArmorRolledBonus[0], 1, level, quality, random))
            { counts[(int)bonus.Definition.Rarity]++; total++; }
        console.Output("Level " + level + " Q" + quality + ": " + string.Join("  ", Enumerable.Range(0, 4).Select(i => (RebirthArmorBonusRarity)i + " " + (counts[i] * 100f / Math.Max(1, total)).ToString("0.0", CultureInfo.InvariantCulture) + "%")));
    }
}
