using System.Globalization;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

#nullable disable

/// <summary>
/// The 2.6 Berserker rage buffs, rebuilt for 3.0. 2.6 had 85 buffs in buffs.xml (15 ranks of each rage, cooldown
/// and knockdown). 3.0 has one buff per rage: the rank values live in the tables below and are written to CVars when
/// the rage starts, and the buff's stat effects read those CVars. Durations that depend on the rank are passed when
/// the buff is added. The knockdown, the cooldown choice and the Blood Moon start/stop are C# (RebirthBerserkerRage).
/// Buffs are still used as the container because they give the HUD icon and countdown, end on death, replicate to
/// other players and run the native sound, particle and camera-shake effects.
/// Not ported: TriggerRageAuras (class auras are gone in 3.0), the trainer NPC spawn (NPC work is on hold) and
/// Rampage (a separate 2.6 system).
/// </summary>
public static class RebirthRageBuffs
{
    public const int MaxTier = 15;
    public const string Defensive = "rebirthRageDefensive", Offensive = "rebirthRageOffensive", Blood = "rebirthRageBloodMoon",
        Other = "rebirthRageShared", Death = "rebirthRageDeath", DeathProtection = "rebirthRageDeathProtection",
        DefensiveCooldown = "rebirthRageDefensiveCooldown", OffensiveCooldown = "rebirthRageOffensiveCooldown",
        BloodCooldown = "rebirthRageBloodMoonCooldown", OtherCooldown = "rebirthRageSharedCooldown", DeathCooldown = "rebirthRageDeathCooldown",
        Knockdown = "rebirthRageKnockdown", Prefab = "rebirthRagePrefab", Ready = "rebirthRageReady";

    public static readonly string[] Rages = { Defensive, Offensive, Blood, Other, Death };
    public static readonly string[] Cooldowns = { DefensiveCooldown, OffensiveCooldown, BloodCooldown, OtherCooldown, DeathCooldown };

    // CVars the stat effects read. Each rage has its own set so two rages (Blood Moon plus Defensive) can run together.
    public const string DefPrefix = "$rbRageDef", OffPrefix = "$rbRageOff", BloodPrefix = "$rbRageBlood";
    public const string Jump = "Jump", Apm = "Apm", Speed = "Speed", Carry = "Carry", Ailment = "Ailment", Shock = "Shock", Rank = "Rank";

    private const string ModName = "zzz_REBIRTH__3_0";
    private const string Swords = "CrossedSwords_FR";
    private const string Melee = "<requirement name=\"HoldingItemHasTags\" tags=\"melee\"/>";
    private const string NoMotor = "<requirement name=\"!HoldingItemHasTags\" tags=\"motorTool\"/>";
    private const string FireBuffs = "buffBurningFlamingArrow,buffBurningMolotov,buffBurningEnvironmentHack,buffBurningEnvironment,buffBurningElement,buffIsOnFire";
    private const string ShockBuffs = "buffShocked";
    private const string AilmentBuffs = "buffFatiguedTrigger,buffArmSprainedCHTrigger,buffLegSprainedCHTrigger";
    private const string RadiationBuffs = "buffRadiationCatch,buffRadiation03";

    // ---------------------------------------------------------------- 2.6 rank tables (rank 1-15)

    public struct Stats { public float Jump, Apm, Speed, Carry, Ailment, Shock; }

    public static int Clamp(int tier) => tier < 1 ? 1 : tier > MaxTier ? MaxTier : tier;

    private static float Carry5(int t) => t * 5 > 50 ? 50 : t * 5;
    private static float Ailment25(int t) => t <= 1 ? 0f : t >= 10 ? 2.5f : .25f * t;

    /// <summary>2.6 FuriousRamsayRageBuffTierN.</summary>
    public static Stats DefensiveStats(int t) { t = Clamp(t); return new Stats { Jump = .05f * t, Apm = .45f + .05f * t, Speed = .02f * t, Carry = Carry5(t), Ailment = Ailment25(t) }; }
    /// <summary>2.6 FuriousRamsayOffensiveRageBuffTierN.</summary>
    public static Stats OffensiveStats(int t) { t = Clamp(t); return new Stats { Jump = .05f * t, Apm = .175f + .025f * t, Speed = .02f * t, Carry = Carry5(t), Ailment = Ailment25(t), Shock = t == 1 ? 2f : 5f }; }
    /// <summary>2.6 FuriousRamsayBloodMoonRageBuffTierN (rank 7 carry was 40 in 2.6 and is kept).</summary>
    public static Stats BloodStats(int t) { t = Clamp(t); return new Stats { Jump = .025f * t, Apm = .175f + .025f * t, Speed = .02f * t, Carry = t == 7 ? 40 : Carry5(t), Ailment = 5f, Shock = 5f }; }

    public static float OffensiveSeconds(int t) => 20 + 10 * Clamp(t);          // 30 s at rank 1 to 170 s at rank 15
    public static float DefensiveCooldownSeconds(int t) => 185 - 5 * Clamp(t);  // 180 s at rank 1 to 110 s at rank 15
    public const float DefensiveSeconds = 60f, OtherSeconds = 60f, DeathSeconds = 10f;
    public const float OffensiveCooldownSeconds = 120f, BloodCooldownSeconds = 30f, OtherCooldownSeconds = 160f, DeathCooldownSeconds = 3600f;
    public const float KnockdownRange = 2f, KnockdownDamagePerRank = 5f, KnockdownSeconds = 3.5f, KnockdownForce = 1000f;

    /// <summary>Writes a rage's rank values to the CVars its stat effects read. Call before adding the buff.</summary>
    public static void WriteStats(EntityAlive e, string prefix, int rank, Stats s)
    {
        if (e?.Buffs == null) return;
        e.Buffs.SetCustomVar(prefix + Rank, Clamp(rank));
        e.Buffs.SetCustomVar(prefix + Jump, s.Jump);
        e.Buffs.SetCustomVar(prefix + Apm, s.Apm);
        e.Buffs.SetCustomVar(prefix + Speed, s.Speed);
        e.Buffs.SetCustomVar(prefix + Carry, s.Carry);
        e.Buffs.SetCustomVar(prefix + Ailment, s.Ailment);
        e.Buffs.SetCustomVar(prefix + Shock, s.Shock);
    }

    // ---------------------------------------------------------------- registration

    [HarmonyPatch(typeof(BuffsFromXml), "clearBuffValueLinks")]
    internal static class RegisterPatch
    {
        // clearBuffValueLinks is the last step of BuffsFromXml.CreateBuffs, after every buffs.xml entry is parsed.
        private static void Prefix() { Register(); }
    }

    public static int Registered { get; private set; }

    public static void Register()
    {
        int count = 0;
        try
        {
            void Add(string xml) { BuffsFromXml.ParseBuff(XElement.Parse(xml)); count++; }
            Add(RageXml(Defensive, "FuriousRamsayRageBuffTierName", "rebirthRageDefensiveDesc", "FuriousRamsayRageBuffTierTooltip", "ui_game_symbol_FR_Rage_icon",
                "defensive", true, F(DefensiveSeconds), LeftCvar(Defensive), CvarStats(DefPrefix)));
            Add(RageXml(Offensive, "FuriousRamsayOffensiveRageBuffTierName", "rebirthRageOffensiveDesc", "FuriousRamsayOffensiveRageBuffTierTooltip", "ui_game_symbol_FR_OffensiveRage_icon",
                "offensive", false, "0", OffLeft, Elemental("@" + OffPrefix + Shock, "electrical,heat") + CvarStats(OffPrefix)));
            Add(RageXml(Blood, "FuriousRamsayBloodMoonRageBuffName", "rebirthRageBloodMoonDesc", "FuriousRamsayBloodMoonRageBuffTierTooltip", "ui_game_symbol_BloodMoonFR_Rage_icon",
                "blood", false, "0", null, Elemental("5", "electrical") + CvarStats(BloodPrefix)));
            Add(RageXml(Other, "FuriousRamsayRageBuffOtherName", "FuriousRamsayRageBuffOtherDesc", "FuriousRamsayRageBuffOtherTooltip", "ui_game_symbol_FR_Rage_icon",
                "other", true, F(OtherSeconds), LeftCvar(Other), Elemental("5", "electrical") + FixedStats(DefensiveStats(5), 1.25f)));
            Add(RageXml(Death, "FuriousRamsayDeathRageBuffName", "FuriousRamsayDeathRageBuffDesc", "FuriousRamsayDeathRageBuffTooltip", "ui_game_symbol_DeathFR_Rage_icon",
                "death", true, F(DeathSeconds), LeftCvar(Death), FixedStats(DefensiveStats(10), 2.5f)));
            Add(DeathProtectionXml());
            Add(CooldownXml(DefensiveCooldown, "FuriousRamsayRageBuffCooldownName", "rebirthRageCooldownDesc", "ui_game_symbol_FR_Rage_Cooldown_icon", "0", DefCooldownLeft, true));
            Add(CooldownXml(OffensiveCooldown, "FuriousRamsayOffensiveRageBuffCooldownName", "FuriousRamsayOffensiveRageBuffCooldownDesc", "ui_game_symbol_FR_Rage_Cooldown_icon", F(OffensiveCooldownSeconds), LeftCvar(OffensiveCooldown), true));
            Add(CooldownXml(BloodCooldown, "FuriousRamsayBloodMoonRageBuffCooldownName", "FuriousRamsayBloodMoonRageBuffCooldownDesc", "ui_game_symbol_FR_Rage_Cooldown_icon", F(BloodCooldownSeconds), LeftCvar(BloodCooldown), false));
            Add(CooldownXml(OtherCooldown, "FuriousRamsayRageBuffCooldownName", "rebirthRageSharedCooldownDesc", "ui_game_symbol_FR_Rage_Cooldown_icon", F(OtherCooldownSeconds), LeftCvar(OtherCooldown), false));
            Add(CooldownXml(DeathCooldown, "FuriousRamsayDeathRageBuffCooldownName", "FuriousRamsayDeathRageBuffCooldownDesc", "ui_game_symbol_FR_DeathRage_Cooldown_icon", F(DeathCooldownSeconds), LeftCvar(DeathCooldown), true));
            // Applied by C# to each enemy the knockdown catches; the damage is dealt by C#, the buff only ragdolls (so every client sees it).
            Add("<buff name=\"" + Knockdown + "\" hidden=\"true\" remove_on_death=\"true\"><damage_type value=\"knockdown\"/><stack_type value=\"ignore\"/><duration value=\"3\"/><effect_group>"
                + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"Ragdoll\" duration=\"" + F(KnockdownSeconds) + "\" force=\"" + F(KnockdownForce) + "\"/></effect_group></buff>");
            // Crossed swords shown for 5 s when a rage starts (2.6 FuriousRamsayRemoveRagePrefab).
            Add("<buff name=\"" + Prefab + "\" hidden=\"true\" remove_on_death=\"true\"><stack_type value=\"replace\"/><duration value=\"5\"/><effect_group>"
                + "<requirement name=\"!HoldingItemHasTags\" tags=\"ranged\"/>"
                + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"AttachPrefabToEntity\" prefab=\"#@modfolder(" + ModName + "):Resources/FR_Particles.unity3d?" + Swords + "\" parent_transform=\"Hips\" local_offset=\"0,0,0\" local_rotation=\"0,90,0\"/>"
                + "</effect_group><effect_group>"
                + "<triggered_effect trigger=\"onSelfBuffFinish\" action=\"RemovePrefabFromEntity\" prefab=\"" + Swords + "\"/>"
                + "<triggered_effect trigger=\"onSelfBuffRemove\" action=\"RemovePrefabFromEntity\" prefab=\"" + Swords + "\"/></effect_group></buff>");
            // 2.6 buffRageReady: a cue when a rage cooldown ends.
            Add("<buff name=\"" + Ready + "\" hidden=\"true\"><stack_type value=\"replace\"/><duration value=\"3\"/><effect_group>"
                + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"PlaySound\" sound=\"FuriousRamsayOhYeah\" play_in_head=\"true\"/>"
                + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"ModifyScreenEffect\" intensity=\"0.2\" fade=\"1\" effect_name=\"Greyscale\"/>"
                + "<triggered_effect trigger=\"onSelfBuffRemove\" action=\"ModifyScreenEffect\" intensity=\"0\" fade=\"1\" effect_name=\"Greyscale\"/>"
                + "</effect_group></buff>");
            Registered = count;
            Log.Out("[REBIRTH Rage] registered " + count + " rage buffs (one per rage, values from code)");
        }
        catch (System.Exception e)
        {
            Log.Error("[REBIRTH Rage] buff registration failed after " + count + " buffs: " + e);
        }
    }

    // Rank-dependent lengths (Offensive Rage 30-170 s, Defensive cooldown 110-180 s). The buff has no fixed length; the owner's
    // tick ends it from its own elapsed time (saved with the buff) and writes the time left here for the HUD countdown.
    public const string OffLeft = LeftPrefix + "Offensive", DefCooldownLeft = LeftPrefix + "DefensiveCooldown", OffSeconds = "$rbRageOffSeconds", DefCooldownSeconds = "$rbRageDefCooldownSeconds";

    // Every timed rage buff shows the time left from one of these CVars, written by the owner's tick from the buff's own elapsed time.
    public const string LeftPrefix = "$rbRageLeft";
    public static string LeftCvar(string buff) => buff == Offensive ? OffLeft : buff == DefensiveCooldown ? DefCooldownLeft : LeftPrefix + buff.Substring("rebirthRage".Length);

    private static string Display(string cvar) => cvar == null ? "" : "<display_value value=\"" + cvar + "\"/><display_value_format value=\"time\"/>";

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A stat effect while holding a melee weapon or bare hands (2.6 rule), never a motor tool.</summary>
    private static string PE(string name, string op, string value, string tags = null, bool noMotor = true)
    {
        var b = new StringBuilder("<passive_effect name=\"").Append(name).Append("\" operation=\"").Append(op).Append("\" value=\"").Append(value).Append('"');
        if (tags != null) b.Append(" tags=\"").Append(tags).Append('"');
        b.Append('>').Append(Melee);
        if (noMotor) b.Append(NoMotor);
        return b.Append("</passive_effect>").ToString();
    }

    private static string CvarStats(string prefix)
        => PE("JumpStrength", "perc_add", "@" + prefix + Jump) + PE("StaminaChangeOT", "perc_add", "5") + PE("AttacksPerMinute", "perc_add", "@" + prefix + Apm)
         + PE("RunSpeed", "perc_add", "@" + prefix + Speed) + PE("WalkSpeed", "perc_add", "@" + prefix + Speed)
         + PE("CarryCapacity", "base_add", "@" + prefix + Carry, null, false) + PE("BuffResistance", "base_add", "@" + prefix + Ailment, AilmentBuffs);

    private static string FixedStats(Stats s, float ailment)
        => PE("JumpStrength", "perc_add", F(s.Jump)) + PE("StaminaChangeOT", "perc_add", "5") + PE("AttacksPerMinute", "perc_add", F(s.Apm))
         + PE("RunSpeed", "perc_add", F(s.Speed)) + PE("WalkSpeed", "perc_add", F(s.Speed))
         + PE("CarryCapacity", "base_add", F(s.Carry), null, false) + PE("BuffResistance", "base_add", F(ailment), AilmentBuffs);

    private static string Elemental(string shock, string electrical)
        => PE("BuffResistance", "base_add", "5", FireBuffs) + PE("BuffResistance", "base_add", shock, ShockBuffs)
         + PE("ElementalDamageResist", "base_add", "200", electrical);

    /// <summary>A rage: start sound, camera shake and the C# start/finish hooks, plus its stat effects.
    /// 2.6 also drained food and water while enraged; 3.0 metabolism ignores those effects, so C# charges Energy instead.</summary>
    private static string RageXml(string name, string nameKey, string descKey, string tooltipKey, string icon, string kind, bool shake, string duration, string display, string stats)
        => "<buff name=\"" + name + "\" name_key=\"" + nameKey + "\" description_key=\"" + descKey + "\" tooltip_key=\"" + tooltipKey + "\" icon=\"" + icon
         + "\" hidden=\"false\" remove_on_death=\"true\"><stack_type value=\"ignore\"/><duration value=\"" + duration + "\"/>" + Display(display) + "<effect_group>"
         + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"PlaySound\" sound=\"FuriousRamsayRage\"/>"
         + (shake ? "<triggered_effect trigger=\"onSelfBuffStart\" target=\"self\" action=\"ShakeCamera\" shake_speed=\"3\" shake_amplitude=\"10\" shake_time=\"1\"/>" : "")
         + "<triggered_effect trigger=\"onSelfBuffStart\" action=\"RebirthRageEvent, RebirthUtils\" kind=\"" + kind + "\" event=\"start\"/>"
         + "<triggered_effect trigger=\"onSelfBuffFinish\" action=\"RebirthRageEvent, RebirthUtils\" kind=\"" + kind + "\" event=\"finish\"/>"
         + "<triggered_effect trigger=\"onSelfBuffRemove\" action=\"RebirthRageEvent, RebirthUtils\" kind=\"" + kind + "\" event=\"remove\"/>"
         + stats + "</effect_group></buff>";

    private static string CooldownXml(string name, string nameKey, string descKey, string icon, string duration, string display, bool ready)
        => "<buff name=\"" + name + "\" name_key=\"" + nameKey + "\" description_key=\"" + descKey + "\" icon=\"" + icon
         + "\" hidden=\"false\" remove_on_death=\"false\"><stack_type value=\"ignore\"/><duration value=\"" + duration + "\"/>" + Display(display) + "<effect_group>"
         + (ready ? "<triggered_effect trigger=\"onSelfBuffFinish\" action=\"AddBuff\" buff=\"" + Ready + "\"/><triggered_effect trigger=\"onSelfBuffRemove\" action=\"AddBuff\" buff=\"" + Ready + "\"/>" : "")
         + "</effect_group></buff>";

    private static string DeathProtectionXml()
        => "<buff name=\"" + DeathProtection + "\" hidden=\"true\" remove_on_death=\"true\"><stack_type value=\"ignore\"/><duration value=\"" + F(DeathSeconds) + "\"/><effect_group>"
         + "<passive_effect name=\"PhysicalDamageResist\" operation=\"base_set\" value=\"200\"/>"
         + "<passive_effect name=\"ElementalDamageResist\" operation=\"base_add\" value=\"200\" tags=\"cold,heat,electrical,radiation\"/>"
         + "<passive_effect name=\"BuffResistance\" operation=\"base_add\" value=\"5\" tags=\"" + FireBuffs + "\"/>"
         + "<passive_effect name=\"BuffResistance\" operation=\"base_add\" value=\"5\" tags=\"" + RadiationBuffs + "\"/>"
         + "</effect_group></buff>";
}

/// <summary>Start/finish hook of every rage buff; the rules are in RebirthBerserkerRage.OnRageEvent.</summary>
public class MinEventActionRebirthRageEvent : MinEventActionBase
{
    private string kind = string.Empty, evt = string.Empty;

    public override void Execute(MinEventParams _params) => RebirthBerserkerRage.OnRageEvent(_params?.Self, kind, evt);

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute)) return true;
        switch (_attribute.Name.LocalName)
        {
            case "kind": kind = _attribute.Value; return true;
            case "event": evt = _attribute.Value; return true;
        }
        return false;
    }
}
