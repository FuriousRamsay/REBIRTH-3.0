using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthSkillSourceMap
{
    public string SkillId = string.Empty;
    public string[] ItemNames = new string[0];
    public string[] ItemTokens = new string[0];
    public string[] TagTokens = new string[0];
    public string[] ToolTokens = new string[0];
    public string[] BlockTokens = new string[0];
}

public sealed class RebirthRecipeKnowledgeRule
{
    public string RecipeName = string.Empty;
    public string KnowledgeId = string.Empty;
    public string LegacyKnowledgeId = string.Empty;
    public string Category = string.Empty;
    public string LiteratureItemId = string.Empty;
}

public sealed class RebirthLiteratureDefinition
{
    public string ItemId = string.Empty;
    public string Kind = string.Empty;
    public string SkillId = string.Empty;
    public float Amount;
    public string KnowledgeId = string.Empty;
    public string MarkerId = string.Empty;
    public float StudySeconds;
}



public sealed class RebirthConstitutionExposureDefinition
{
    public string ItemId=string.Empty;
    public string FamilyId=string.Empty;
    public string Tier=string.Empty;
    public string EffectBuff=string.Empty;
    public float RawAward;
}

public sealed class RebirthAudiobookDefinition
{
    public string ItemId=string.Empty;
    public string SourceLiteratureId=string.Empty;
    public string SkillId=string.Empty;
    public string Subtype=string.Empty;
    public string IconKey=string.Empty;
    public float AudioSeconds;
    public string ImplementationState=string.Empty;
}

/// <summary>Chunk 08 live authoring that is intentionally outside immutable-origin semantic hashing.</summary>
public static class RebirthProgressionRuntimeConfig
{
    private static readonly Dictionary<string, RebirthRecipeKnowledgeRule> RecipeRules =
        new Dictionary<string, RebirthRecipeKnowledgeRule>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthLiteratureDefinition> LiteratureByItem =
        new Dictionary<string, RebirthLiteratureDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthAudiobookDefinition> Audiobooks =
        new Dictionary<string, RebirthAudiobookDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<RebirthSkillSourceMap> CombatMaps = new List<RebirthSkillSourceMap>();
    private static readonly List<RebirthSkillSourceMap> HarvestMaps = new List<RebirthSkillSourceMap>();
    private static readonly Dictionary<string,RebirthConstitutionExposureDefinition> ConstitutionExposures = new Dictionary<string,RebirthConstitutionExposureDefinition>(StringComparer.OrdinalIgnoreCase);
    private static string[] CookingRecipeTokens = new string[] { "food", "drink" };
    public static string[] ChemistryRecipeTokens = new string[] { "acid", "oil", "gascan", "gunpowder", "chemical", "antibiotic", "vitamin", "steroid", "drug" };
    public static string[] MetalworkingRecipeTokens = new string[] { "forgediron", "forgedsteel", "steelshape", "ironshape", "metalpipe", "mechanicalparts", "spring", "rebirthrivetfastenerset", "rebirthsheetmetalbrackets" };
    public static string[] GunsmithingRecipeTokens = new string[] { "gunpart", "gunparts", "firearmpart", "barrel", "receiver", "rebirthfirearmcleaningkit", "ammospecialty", "modgun" };
    public static string[] TailoringRecipeTokens = new string[] { "armorprimitive", "armorrogue", "armorassassin", "armorathletic", "armorbiker", "armorcommando", "armorenforcer", "armorfarmer", "armorlumberjack", "armorminer", "armornerd", "armornomad", "armorpreacher", "armorraider", "armorranger", "armorscavenger" };
    public static string[] ConstructionRecipeTokens = new string[] { "rebirthstructuralbracketset", "cntwoodburningstove" };
    public static string[] ElectricalRecipeTokens = new string[] { "rebirthelectricalfuse", "rebirthelectricalterminalblock", "rebirthelectricalwiringharness", "switch", "tripwirepost", "electrictimerrelay", "electricwirerelay", "electricfencepost", "generatorbank", "batterybank", "speaker", "spotlightplayer" };
    public static string[] ChemistryAreaTokens = new string[] { "chemistry" };
    public static string[] MetalworkingAreaTokens = new string[] { "forge" };

    // Legacy non-weapon combat awards remain available for complex systems.
    public static float CombatPer100Damage = 0.28f;
    public static float CombatMin = 0.04f;
    public static float CombatMax = 0.35f;
    public static float ShotgunWindowSeconds = 0.20f;
    public static float ShotgunWindowMax = 0.18f;

    // Weapon-family learn-by-doing is normalized to the live weapon's sustained DPS.
    // At full theoretical output, 0.005 progress/second plus the mastery slowdown is roughly
    // nine hours of continuous effective combat from skill 0 to 100 before trait/teaching modifiers.
    public static float CombatProgressPerSustainedSecond = 0.005f;
    public static float CombatMaxEquivalentSecondsPerHit = 6.0f;
    public static float CombatSkillMultiplier0To24 = 1.00f;
    public static float CombatSkillMultiplier25To49 = 0.80f;
    public static float CombatSkillMultiplier50To74 = 0.60f;
    public static float CombatSkillMultiplier75To89 = 0.45f;
    public static float CombatSkillMultiplier90To100 = 0.30f;
    public static float WeaponDpsProfileRefreshSeconds = 0.25f;
    public static float WeaponDpsSustainWindowSeconds = 60.0f;
    public static float WeaponDpsRecentHeldSeconds = 15.0f;
    public static float MechanicsInstall = 0.35f;
    public static float MechanicsReplace = 0.30f;
    public static float MechanicsHotwire = 0.60f;
    public static float MechanicsRepairBase = 0.15f;
    public static float MechanicsRepairPer100Health = 0.30f;
    public static float CookingPerOutput = 0.12f;
    public static float CookingMax = 0.50f;
    public static float MedicineMeaningfulTreatment = 0.30f;
    public static float MaintenanceRepair = 0.30f;
    public static float GunsmithingRepair = 0.35f;
    public static float ChemistryPerOutput = 0.14f;
    public static float ChemistryMax = 0.55f;
    public static float MetalworkingPerOutput = 0.14f;
    public static float MetalworkingMax = 0.55f;
    public static float GunsmithingCraftPerOutput = 0.18f;
    public static float GunsmithingCraftMax = 0.60f;
    public static float TailoringPerOutput = 0.14f;
    public static float TailoringMax = 0.55f;
    public static float TailoringRepair = 0.35f;
    public static float ConstructionCraftPerOutput = 0.12f;
    public static float ConstructionCraftMax = 0.50f;
    public static float ElectricalCraftPerOutput = 0.14f;
    public static float ElectricalCraftMax = 0.55f;
    public static float MaintenanceCraftPerOutput = 0.12f;
    public static float MaintenanceCraftMax = 0.50f;
    public static float MechanicsCraftPerOutput = 0.14f;
    public static float MechanicsCraftMax = 0.55f;
    public static float FarmingCraftPerOutput = 0.10f;
    public static float FarmingCraftMax = 0.45f;
    public static float ExplosivesCraftPerOutput = 0.14f;
    public static float ExplosivesCraftMax = 0.55f;
    public static float TurretsCraftPerOutput = 0.14f;
    public static float TurretsCraftMax = 0.55f;
    public static float MedicineCraftPerOutput = 0.12f;
    public static float MedicineCraftMax = 0.50f;
    public static float ConstructionRepairBaseAward = 0.10f;
    public static float ConstructionRepairMaxAward = 0.45f;
    public static float ConstructionUpgradeAward = 0.50f;
    public static float ElectricalServiceAward = 0.45f;
    public static float ElectricalConditionDecayPerPoweredMinute = 0.18f;
    public static float ElectricalMaxPowerSavings = 0.12f;
    public static float ElectricalNeglectPowerPenalty = 0.12f;
    // Chunk G workmanship curves. These are implementation tuning, not design-locked Background values.
    public static float ConstructionDurabilityNegative = -0.15f;
    public static float ConstructionDurabilityPositive = 0.25f;
    public static float TechnicalTrapDurabilityNegative = -0.15f;
    public static float TechnicalTrapDurabilityPositive = 0.25f;
    public static float SkillProgressToAttribute = 0.20f;
    public static float BelowPotentialFullUntil = 0.70f;
    public static float AtPotentialMultiplier = 0.10f;
    public static float AbovePotentialMultiplier = 0.02f;
    public static float ConstitutionHealingConversion = 0.20f;
    public static float ConstitutionDirectWindowActiveSeconds = 600f;
    public static float ConstitutionDirectWindowRawCap = 0.10f;
    public static float ConstitutionExposureCooldownActiveSeconds = 900f;
    // Pass 5A: Attributes are secondary capability, never a replacement for Practical Skill.
    // 0..100 Attribute around a 50 neutral center contributes at most +/-10 Skill-equivalent points.
    public static float AttributeOutcomeCenter = 50f;
    public static float AttributeOutcomeRange = 50f;
    public static float AttributeOutcomeMaxSkillEquivalent = 10f;
    // Chunk 5 / Skill Wave A. Signed skill values use separate negative and positive endpoint tuning.
    public static float WaveASampleSeconds = 1.0f;
    public static float WaveAPassiveSyncSeconds = 0.5f;
    public static float WaveAMaxSampleDistance = 12.0f;
    public static float LockpickNegativeTime = 0.50f;
    public static float LockpickPositiveTime = -0.40f;
    public static float LockpickNegativeBreak = 0.40f;
    public static float LockpickPositiveBreak = -0.60f;
    // Phase 10: purchases train Bartering and sales train Trading. The retained native passive
    // contribution is still split/capped so old saves and the combined commerce bonus stay within
    // the former +16% envelope; this cap no longer means Bartering is untrainable.
    public static float BarterNegative = -0.12f;
    public static float BarterPositive = 0.16f;
    public static float BarterLegacyPositiveCap = 0.04f;
    public static float TradingPositive = 0.12f;
    public static float TradingRewardOptionThreshold = 50f;
    public static float BarterBaseAward = 0.10f;
    public static float BarterValueAwardCap = 0.20f;
    public static float BarterValueScale = 5000f;
    public static float BarterGlobalSeconds = 2f;
    public static float BarterRepeatSeconds = 30f;
    public static float BarterLoopSeconds = 300f;
    public static float TradingVarietyWindowSeconds = 120f;
    public static float TradingMinimumValue = 50f;
    public static float TradingMinimumUnitValue = 5f;
    public static float TradingValueSpoofMultiplier = 20f;
    public static float AthleticsNegativeJump = -0.10f;
    public static float AthleticsPositiveJump = 0.15f;
    public static float AthleticsNegativeStamina = 0.15f;
    public static float AthleticsPositiveStamina = -0.20f;
    public static float AthleticsNegativeFall = 0.10f;
    public static float AthleticsPositiveFall = -0.20f;
    public static float AthleticsDistance = 25f;
    public static float AthleticsAward = 0.08f;
    public static float StealthNegativeNoise = 0.20f;
    public static float StealthPositiveNoise = -0.25f;
    public static float StealthNegativeLight = 0.10f;
    public static float StealthPositiveLight = -0.15f;
    public static float ArmorNegativeBurden = -0.25f;
    public static float ArmorPositiveRecovery = 0.50f;
    public static float ArmorDistance = 30f;
    public static float ArmorAward = 0.06f;

    // Chunk 6 / Resource + Field Skills. Harvest modifiers are zero-neutral signed curves.
    // Tracking retains a low-skill animal baseline but never includes zombie tags in this wave.
    public static float ResourceFieldSampleSeconds = 1.0f;
    public static float ResourceFieldPassiveSyncSeconds = 0.5f;
    public static float ResourceFieldMaxSampleDistance = 12.0f;
    public static float ResourceHarvestNegative = -0.20f;
    public static float ResourceHarvestPositive = 0.30f;
    public static float FarmingHarvestNegative = -0.15f;
    public static float FarmingHarvestPositive = 0.25f;
    public static float AnimalHarvestNegative = -0.20f;
    public static float AnimalHarvestPositive = 0.35f;
    public static float TrackingMinDistance = 40f;
    public static float TrackingNeutralDistance = 75f;
    public static float TrackingMaxDistance = 150f;
    public static float TrackingNegativeAcquireSeconds = 5f;
    public static float TrackingNeutralAcquireSeconds = 3f;
    public static float TrackingPositiveAcquireSeconds = 1f;
    public static float TrackingAcquireAward = 0.20f;
    public static float TrackingFollowDistance = 25f;
    public static float TrackingFollowAward = 0.05f;
    public static float TrackingRepeatSeconds = 60f;
    public static float TrackingHabitatDistanceMultiplier = 1.20f;
    public static float TrackingHabitatAcquireMultiplier = 0.80f;
    public static string[] AnimalProcessingToolTokens = new string[] { "knife", "machete", "axe" };

    // Chunk 8 / Service + Crafting Skills. These alter existing native queue/service values only.
    // Craft failure, persistent workmanship and item destruction are intentionally not implemented in this chunk.
    public static float ServiceCraftTimeNegative = 0.20f;
    public static float ServiceCraftTimePositive = -0.20f;
    public static float ServiceRepairTimeNegative = 0.25f;
    public static float ServiceRepairTimePositive = -0.25f;
    public static float ServiceRepairAmountNegative = -0.20f;
    public static float ServiceRepairAmountPositive = 0.25f;
    public static float MechanicsRepairNegative = -0.20f;
    public static float MechanicsRepairPositive = 0.30f;
    public static float MedicineReserveNegative = -0.15f;
    public static float MedicineReservePositive = 0.20f;
    public static float MedicineFractureHealingNegative = -0.15f;
    public static float MedicineFractureHealingPositive = 0.20f;
    public static float MedicineInfectionAccelerationPositive = 0.20f;

    // Chunk 7 / Weapon-family Skills. These are handling curves only: no direct damage, magazine,
    // projectile-count, random-jam or generic firearm RPM scaling is authored here.
    public static float WeaponFamilyPassiveSyncSeconds = 0.25f;
    public static float MeleeStaminaNegative = 0.15f;
    public static float MeleeStaminaPositive = -0.20f;
    public static float MeleeSpeedNegative = -0.10f;
    public static float MeleeSpeedPositive = 0.10f;
    public static float RangedReloadNegative = -0.15f;
    public static float RangedReloadPositive = 0.20f;
    public static float RangedHandlingNegative = -0.20f;
    public static float RangedHandlingPositive = 0.40f;
    public static float RangedSpreadNegative = 0.15f;
    public static float RangedSpreadPositive = -0.15f;
    public static float RangedRecoilNegative = 0.15f;
    public static float RangedRecoilPositive = -0.15f;

    // Chunk 10 / Complex Skill systems. Only source-audited low-risk adapters are enabled here.
    // Construction Work Actions, Electrical workmanship and Chemistry payload metadata remain explicit runtime gates.
    public static float ComplexPassiveSyncSeconds = 0.50f;
    public static float ExplosivesDamageNegative = -0.20f;
    public static float ExplosivesDamagePositive = 0.25f;
    public static float ExplosivesBlockAward = 0.12f;
    public static float TurretDamageNegative = -0.20f;
    public static float TurretDamagePositive = 0.25f;
    public static float DroneDamageNegative = -0.20f;
    public static float DroneDamagePositive = 0.25f;
    public static float DroneStunCycleNegative = 0.20f;
    public static float DroneStunCyclePositive = -0.20f;
    public static float DroneShockAward = 0.20f;
    public static float DroneShockRepeatSeconds = 5.0f;
    public static float DroneStockRecoveryAward = 0.20f;
    public static float DroneStockRecoveryMinDistance = 25f;
    public static float DroneStockRecoveryRepeatSeconds = 300f;

    public static int CombatMapCount { get { return CombatMaps.Count; } }
    public static int HarvestMapCount { get { return HarvestMaps.Count; } }
    public static int RecipeRuleCount { get { return RecipeRules.Count; } }
    public static int LiteratureCount { get { return LiteratureByItem.Count; } }
    public static int AudiobookCount { get { return Audiobooks.Count; } }

    public static string Load()
    {
        lock (CombatClassCache) CombatClassCache.Clear();
        CombatMaps.Clear(); HarvestMaps.Clear(); RecipeRules.Clear(); LiteratureByItem.Clear(); Audiobooks.Clear(); ConstitutionExposures.Clear();
        string root = RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
        LoadAttributeTraining(Path.Combine(root, "attribute_training.xml"));
        LoadSkillSources(Path.Combine(root, "skill_sources.xml"));
        LoadRecipeKnowledge(Path.Combine(root, "recipe_knowledge.xml"));
        LoadLiterature(Path.Combine(root, "literature.xml"));
        LoadAudiobooks(Path.Combine(root, "audiobooks.xml"));
        string capability = RebirthCapabilityRegistry.Load(root, GetRecipeRulesSnapshot());
        string craftingPolicy = RebirthCraftingProgressionRegistry.Load(root);
        return "skillMaps=" + (CombatMaps.Count + HarvestMaps.Count) + " recipeKnowledge=" + RecipeRules.Count + " literature=" + LiteratureByItem.Count + " audiobooks=" + Audiobooks.Count + " constitutionExposures=" + ConstitutionExposures.Count + " " + capability + " " + craftingPolicy;
    }

    public static bool TryGetConstitutionExposure(string itemId,out RebirthConstitutionExposureDefinition definition)
    { return ConstitutionExposures.TryGetValue(itemId??string.Empty,out definition) && definition!=null; }

    private static void LoadAttributeTraining(string path)
    {
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        if(root==null||root.Name.LocalName!="survivor_attribute_training")throw new InvalidDataException("attribute_training.xml root invalid");
        XElement bridge=root.Element("skill_bridge"); XElement direct=root.Element("direct_constitution");
        if(bridge==null||direct==null)throw new InvalidDataException("attribute_training.xml required sections missing");
        SkillProgressToAttribute=RF(bridge,"raw_conversion",0f,1f);
        BelowPotentialFullUntil=RF(bridge,"potential_full_until_ratio",0f,1f);
        AtPotentialMultiplier=RF(bridge,"at_potential_multiplier",0f,1f);
        AbovePotentialMultiplier=RF(bridge,"above_potential_multiplier",0f,1f);
        ConstitutionHealingConversion=RF(direct,"healing_conversion",0f,1f);
        ConstitutionDirectWindowActiveSeconds=RF(direct,"rolling_active_seconds",1f,86400f);
        ConstitutionDirectWindowRawCap=RF(direct,"rolling_cap",0f,1f);
        ConstitutionExposureCooldownActiveSeconds=RF(direct,"exposure_cooldown_active_seconds",0f,86400f);
        XElement tiers=direct.Element("exposure_tiers"); if(tiers==null)throw new InvalidDataException("attribute_training.xml exposure_tiers missing");
        Dictionary<string,float> awards=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase)
        {
            {"mild",RF(tiers,"mild",0f,1f)},{"standard",RF(tiers,"standard",0f,1f)},
            {"strong",RF(tiers,"strong",0f,1f)},{"extreme",RF(tiers,"extreme",0f,1f)}
        };
        XElement exposures=direct.Element("exposures"); if(exposures==null)return;
        foreach(XElement e in exposures.Elements("item"))
        {
            string id=RA(e,"id"),family=RA(e,"family"),tier=RA(e,"tier"),buff=RA(e,"effect_buff");
            if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(family)||string.IsNullOrEmpty(buff))throw new InvalidDataException("attribute_training.xml exposure row is incomplete");
            if(ConstitutionExposures.ContainsKey(id))throw new InvalidDataException("Duplicate Constitution exposure item '"+id+"'");
            float award;if(!awards.TryGetValue(tier,out award))throw new InvalidDataException("Unknown Constitution exposure tier '"+tier+"'");
            ConstitutionExposures.Add(id,new RebirthConstitutionExposureDefinition{ItemId=id,FamilyId=family,Tier=tier,EffectBuff=buff,RawAward=award});
        }
    }

    private static string RA(XElement e,string name)
    { XAttribute a=e.Attribute(name); return a!=null?a.Value.Trim():string.Empty; }
    private static float RF(XElement e,string name,float min,float max)
    {
        float v; string raw=RA(e,name);
        if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max)
            throw new InvalidDataException("Invalid attribute_training value "+name+"='"+raw+"'");
        return v;
    }

    public static RebirthRecipeKnowledgeRule[] GetRecipeRulesSnapshot()
    {
        List<RebirthRecipeKnowledgeRule> result = new List<RebirthRecipeKnowledgeRule>(RecipeRules.Values);
        result.Sort(delegate(RebirthRecipeKnowledgeRule a, RebirthRecipeKnowledgeRule b) { return string.Compare(a.RecipeName,b.RecipeName,StringComparison.OrdinalIgnoreCase); });
        return result.ToArray();
    }

    public static bool TryGetRecipeRule(string recipeName, out RebirthRecipeKnowledgeRule rule)
    { return RecipeRules.TryGetValue(recipeName ?? string.Empty, out rule); }

    public static bool TryGetAudiobook(string itemId,out RebirthAudiobookDefinition definition)
    {
        definition=null;
        return !string.IsNullOrEmpty(itemId)&&Audiobooks.TryGetValue(itemId,out definition)&&definition!=null;
    }

    public static RebirthAudiobookDefinition[] GetAudiobookSnapshot()
    {
        RebirthAudiobookDefinition[] values=new RebirthAudiobookDefinition[Audiobooks.Count];
        Audiobooks.Values.CopyTo(values,0);
        Array.Sort(values,(a,b)=>string.Compare(a!=null?a.ItemId:string.Empty,b!=null?b.ItemId:string.Empty,StringComparison.OrdinalIgnoreCase));
        return values;
    }

    public static bool TryGetLiterature(string itemId, out RebirthLiteratureDefinition definition)
    { return LiteratureByItem.TryGetValue(itemId ?? string.Empty, out definition); }

    public static RebirthLiteratureDefinition[] GetLiteratureSnapshot()
    {
        List<RebirthLiteratureDefinition> result = new List<RebirthLiteratureDefinition>(LiteratureByItem.Values);
        result.Sort(delegate(RebirthLiteratureDefinition a, RebirthLiteratureDefinition b) { return string.Compare(a.ItemId,b.ItemId,StringComparison.OrdinalIgnoreCase); });
        return result.ToArray();
    }

    public static bool IsCookingRecipe(string recipeName)
    { return MatchesAny(recipeName ?? string.Empty, CookingRecipeTokens); }

    // The result depends only on the item class (its name and tags) and the loaded CombatMaps, so it is cached per item type.
    // It used to rebuild the tag string and scan every map on each 0.25 s weapon refresh (profiling: ~40 KB/s of garbage).
    private static readonly Dictionary<int, string> CombatClassCache = new Dictionary<int, string>();

    public static string ClassifyCombat(ItemValue item)
    {
        if (item == null || item.ItemClass == null) return string.Empty;
        int type = item.type;
        string cached;
        lock (CombatClassCache)
        {
            if (CombatClassCache.TryGetValue(type, out cached)) return cached;
        }
        string result = ClassifyCombatUncached(item);
        lock (CombatClassCache) CombatClassCache[type] = result;
        return result;
    }

    private static string ClassifyCombatUncached(ItemValue item)
    {
        string name = item.ItemClass.GetItemName() ?? string.Empty;
        string tags = item.ItemClass.ItemTags.ToString() ?? string.Empty;
        // Exact item-name authoring wins before fuzzy/token fallback. This prevents overlapping
        // families such as pistol/revolver/heavy-handgun or assault/tactical/long-range rifles
        // from being collapsed by a broad substring.
        for (int i=0;i<CombatMaps.Count;i++)
            if (MatchesExact(name, CombatMaps[i].ItemNames)) return CombatMaps[i].SkillId;
        for (int i=0;i<CombatMaps.Count;i++)
            if (MatchesAny(name, CombatMaps[i].ItemTokens) || MatchesAny(tags, CombatMaps[i].TagTokens))
                return CombatMaps[i].SkillId;
        return string.Empty;
    }

    public static string ClassifyCombatItemName(string itemName)
    {
        string name=itemName??string.Empty;
        for(int i=0;i<CombatMaps.Count;i++) if(MatchesExact(name,CombatMaps[i].ItemNames)) return CombatMaps[i].SkillId;
        for(int i=0;i<CombatMaps.Count;i++) if(MatchesAny(name,CombatMaps[i].ItemTokens)) return CombatMaps[i].SkillId;
        return string.Empty;
    }

    public static bool IsFirearmSkill(string skillId)
    {
        string s=(skillId??string.Empty).ToLowerInvariant();
        return s=="skill.pistols"||s=="skill.revolvers"||s=="skill.heavy_handguns"||s=="skill.shotguns"||s=="skill.assault_rifles"||s=="skill.tactical_rifles"||s=="skill.long_range_rifles";
    }

    public static string ClassifyHarvest(ItemValue held, string blockName)
    {
        if (held == null || held.ItemClass == null) return string.Empty;
        string toolName = held.ItemClass.GetItemName() ?? string.Empty;
        string tags = held.ItemClass.ItemTags.ToString() ?? string.Empty;
        for (int i=0;i<HarvestMaps.Count;i++)
        {
            RebirthSkillSourceMap map=HarvestMaps[i];
            bool tool = MatchesAny(toolName,map.ToolTokens) || MatchesAny(tags,map.ToolTokens);
            bool block = map.BlockTokens.Length==1 && map.BlockTokens[0]=="*" || MatchesAny(blockName??string.Empty,map.BlockTokens);
            if(tool && block) return map.SkillId;
        }
        return string.Empty;
    }

    public static string ClassifyHarvestTool(ItemValue held)
    {
        if (held == null || held.ItemClass == null) return string.Empty;
        string toolName = held.ItemClass.GetItemName() ?? string.Empty;
        string tags = held.ItemClass.ItemTags.ToString() ?? string.Empty;
        for (int i=0;i<HarvestMaps.Count;i++)
        {
            RebirthSkillSourceMap map=HarvestMaps[i];
            if(MatchesAny(toolName,map.ToolTokens) || MatchesAny(tags,map.ToolTokens)) return map.SkillId;
        }
        return string.Empty;
    }

    public static bool IsAnimalProcessingTool(ItemValue held)
    {
        if(held==null || held.ItemClass==null)return false;
        string name=held.ItemClass.GetItemName()??string.Empty;
        string tags=held.ItemClass.ItemTags.ToString()??string.Empty;
        return MatchesAny(name,AnimalProcessingToolTokens)||MatchesAny(tags,AnimalProcessingToolTokens);
    }

    private static void LoadSkillSources(string path)
    {
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        if(root==null || (string)root.Attribute("schema_version")!="1") throw new InvalidDataException("skill_sources.xml schema_version must be 1");
        XElement combat=root.Element("combat"), harvest=root.Element("harvest");
        if(combat!=null) foreach(XElement e in combat.Elements("map")) CombatMaps.Add(ParseMap(e));
        if(harvest!=null) foreach(XElement e in harvest.Elements("map")) HarvestMaps.Add(ParseMap(e));
        XElement cooking=root.Element("cooking");
        if(cooking!=null) CookingRecipeTokens=Tokens((string)cooking.Attribute("recipe_tokens"));
        XElement awards=root.Element("awards");
        if(awards!=null)
        {
            CombatPer100Damage=F(awards,"combat_per_100_damage",CombatPer100Damage); CombatMin=F(awards,"combat_min",CombatMin); CombatMax=F(awards,"combat_max",CombatMax);
            ShotgunWindowSeconds=F(awards,"shotgun_window_seconds",ShotgunWindowSeconds); ShotgunWindowMax=F(awards,"shotgun_window_max",ShotgunWindowMax);
            MechanicsInstall=F(awards,"mechanics_install",MechanicsInstall); MechanicsReplace=F(awards,"mechanics_replace",MechanicsReplace); MechanicsHotwire=F(awards,"mechanics_hotwire",MechanicsHotwire);
            MechanicsRepairBase=F(awards,"mechanics_repair_base",MechanicsRepairBase); MechanicsRepairPer100Health=F(awards,"mechanics_repair_per_100_health",MechanicsRepairPer100Health);
            CookingPerOutput=F(awards,"cooking_per_output",CookingPerOutput); CookingMax=F(awards,"cooking_max",CookingMax);
            MedicineMeaningfulTreatment=F(awards,"medicine_meaningful_treatment",MedicineMeaningfulTreatment); MaintenanceRepair=F(awards,"maintenance_repair",MaintenanceRepair); GunsmithingRepair=F(awards,"gunsmithing_repair",GunsmithingRepair); ChemistryPerOutput=F(awards,"chemistry_per_output",ChemistryPerOutput); ChemistryMax=F(awards,"chemistry_max",ChemistryMax); MetalworkingPerOutput=F(awards,"metalworking_per_output",MetalworkingPerOutput); MetalworkingMax=F(awards,"metalworking_max",MetalworkingMax); GunsmithingCraftPerOutput=F(awards,"gunsmithing_craft_per_output",GunsmithingCraftPerOutput); GunsmithingCraftMax=F(awards,"gunsmithing_craft_max",GunsmithingCraftMax); TailoringPerOutput=F(awards,"tailoring_per_output",TailoringPerOutput); TailoringMax=F(awards,"tailoring_max",TailoringMax); TailoringRepair=F(awards,"tailoring_repair",TailoringRepair); ConstructionCraftPerOutput=F(awards,"construction_craft_per_output",ConstructionCraftPerOutput); ConstructionCraftMax=F(awards,"construction_craft_max",ConstructionCraftMax); ElectricalCraftPerOutput=F(awards,"electrical_craft_per_output",ElectricalCraftPerOutput); ElectricalCraftMax=F(awards,"electrical_craft_max",ElectricalCraftMax); MaintenanceCraftPerOutput=F(awards,"maintenance_craft_per_output",MaintenanceCraftPerOutput); MaintenanceCraftMax=F(awards,"maintenance_craft_max",MaintenanceCraftMax); MechanicsCraftPerOutput=F(awards,"mechanics_craft_per_output",MechanicsCraftPerOutput); MechanicsCraftMax=F(awards,"mechanics_craft_max",MechanicsCraftMax); FarmingCraftPerOutput=F(awards,"farming_craft_per_output",FarmingCraftPerOutput); FarmingCraftMax=F(awards,"farming_craft_max",FarmingCraftMax); ExplosivesCraftPerOutput=F(awards,"explosives_craft_per_output",ExplosivesCraftPerOutput); ExplosivesCraftMax=F(awards,"explosives_craft_max",ExplosivesCraftMax); TurretsCraftPerOutput=F(awards,"turrets_craft_per_output",TurretsCraftPerOutput); TurretsCraftMax=F(awards,"turrets_craft_max",TurretsCraftMax); MedicineCraftPerOutput=F(awards,"medicine_craft_per_output",MedicineCraftPerOutput); MedicineCraftMax=F(awards,"medicine_craft_max",MedicineCraftMax); ConstructionRepairBaseAward=F(awards,"construction_repair_base_award",ConstructionRepairBaseAward); ConstructionRepairMaxAward=F(awards,"construction_repair_max_award",ConstructionRepairMaxAward); ConstructionUpgradeAward=F(awards,"construction_upgrade_award",ConstructionUpgradeAward); ElectricalServiceAward=F(awards,"electrical_service_award",ElectricalServiceAward); ElectricalConditionDecayPerPoweredMinute=F(awards,"electrical_condition_decay_per_powered_minute",ElectricalConditionDecayPerPoweredMinute); ElectricalMaxPowerSavings=F(awards,"electrical_max_power_savings",ElectricalMaxPowerSavings); ElectricalNeglectPowerPenalty=F(awards,"electrical_neglect_power_penalty",ElectricalNeglectPowerPenalty);
        }
        XElement combatTraining=root.Element("combat_training");
        if(combatTraining!=null)
        {
            CombatProgressPerSustainedSecond=F(combatTraining,"progress_per_sustained_second",CombatProgressPerSustainedSecond);
            CombatMaxEquivalentSecondsPerHit=F(combatTraining,"max_equivalent_seconds_per_hit",CombatMaxEquivalentSecondsPerHit);
            CombatSkillMultiplier0To24=F(combatTraining,"skill_multiplier_0_24",CombatSkillMultiplier0To24);
            CombatSkillMultiplier25To49=F(combatTraining,"skill_multiplier_25_49",CombatSkillMultiplier25To49);
            CombatSkillMultiplier50To74=F(combatTraining,"skill_multiplier_50_74",CombatSkillMultiplier50To74);
            CombatSkillMultiplier75To89=F(combatTraining,"skill_multiplier_75_89",CombatSkillMultiplier75To89);
            CombatSkillMultiplier90To100=F(combatTraining,"skill_multiplier_90_100",CombatSkillMultiplier90To100);
            WeaponDpsProfileRefreshSeconds=F(combatTraining,"profile_refresh_seconds",WeaponDpsProfileRefreshSeconds);
            WeaponDpsSustainWindowSeconds=F(combatTraining,"sustain_window_seconds",WeaponDpsSustainWindowSeconds);
            WeaponDpsRecentHeldSeconds=F(combatTraining,"recent_held_seconds",WeaponDpsRecentHeldSeconds);
        }
        XElement workmanship=root.Element("workmanship");
        if(workmanship!=null)
        {
            ConstructionDurabilityNegative=F(workmanship,"construction_durability_negative",ConstructionDurabilityNegative);
            ConstructionDurabilityPositive=F(workmanship,"construction_durability_positive",ConstructionDurabilityPositive);
            TechnicalTrapDurabilityNegative=F(workmanship,"technical_trap_durability_negative",TechnicalTrapDurabilityNegative);
            TechnicalTrapDurabilityPositive=F(workmanship,"technical_trap_durability_positive",TechnicalTrapDurabilityPositive);
        }
        XElement waveA=root.Element("wave_a");
        if(waveA!=null)
        {
            WaveASampleSeconds=F(waveA,"sample_seconds",WaveASampleSeconds); WaveAPassiveSyncSeconds=F(waveA,"passive_sync_seconds",WaveAPassiveSyncSeconds); WaveAMaxSampleDistance=F(waveA,"max_sample_distance",WaveAMaxSampleDistance);
            LockpickNegativeTime=F(waveA,"lockpick_negative_time",LockpickNegativeTime); LockpickPositiveTime=F(waveA,"lockpick_positive_time",LockpickPositiveTime); LockpickNegativeBreak=F(waveA,"lockpick_negative_break",LockpickNegativeBreak); LockpickPositiveBreak=F(waveA,"lockpick_positive_break",LockpickPositiveBreak);
            BarterNegative=F(waveA,"barter_negative",BarterNegative); BarterPositive=F(waveA,"barter_positive",BarterPositive); BarterLegacyPositiveCap=F(waveA,"barter_legacy_positive_cap",BarterLegacyPositiveCap); TradingPositive=F(waveA,"trading_positive",TradingPositive); TradingRewardOptionThreshold=F(waveA,"trading_reward_option_threshold",TradingRewardOptionThreshold); BarterBaseAward=F(waveA,"barter_base_award",BarterBaseAward); BarterValueAwardCap=F(waveA,"barter_value_award_cap",BarterValueAwardCap); BarterValueScale=F(waveA,"barter_value_scale",BarterValueScale); BarterGlobalSeconds=F(waveA,"barter_global_seconds",BarterGlobalSeconds); BarterRepeatSeconds=F(waveA,"barter_repeat_seconds",BarterRepeatSeconds); BarterLoopSeconds=F(waveA,"barter_loop_seconds",BarterLoopSeconds); TradingVarietyWindowSeconds=F(waveA,"trading_variety_window_seconds",TradingVarietyWindowSeconds); TradingMinimumValue=F(waveA,"trading_minimum_value",TradingMinimumValue); TradingMinimumUnitValue=F(waveA,"trading_minimum_unit_value",TradingMinimumUnitValue); TradingValueSpoofMultiplier=F(waveA,"trading_value_spoof_multiplier",TradingValueSpoofMultiplier);
            AthleticsNegativeJump=F(waveA,"athletics_negative_jump",AthleticsNegativeJump); AthleticsPositiveJump=F(waveA,"athletics_positive_jump",AthleticsPositiveJump); AthleticsNegativeStamina=F(waveA,"athletics_negative_stamina",AthleticsNegativeStamina); AthleticsPositiveStamina=F(waveA,"athletics_positive_stamina",AthleticsPositiveStamina); AthleticsNegativeFall=F(waveA,"athletics_negative_fall",AthleticsNegativeFall); AthleticsPositiveFall=F(waveA,"athletics_positive_fall",AthleticsPositiveFall); AthleticsDistance=F(waveA,"athletics_distance",AthleticsDistance); AthleticsAward=F(waveA,"athletics_award",AthleticsAward);
            StealthNegativeNoise=F(waveA,"stealth_negative_noise",StealthNegativeNoise); StealthPositiveNoise=F(waveA,"stealth_positive_noise",StealthPositiveNoise); StealthNegativeLight=F(waveA,"stealth_negative_light",StealthNegativeLight); StealthPositiveLight=F(waveA,"stealth_positive_light",StealthPositiveLight);
            ArmorNegativeBurden=F(waveA,"armor_negative_burden",ArmorNegativeBurden); ArmorPositiveRecovery=F(waveA,"armor_positive_recovery",ArmorPositiveRecovery); ArmorDistance=F(waveA,"armor_distance",ArmorDistance); ArmorAward=F(waveA,"armor_award",ArmorAward);
        }
        XElement resourceField=root.Element("resource_field");
        if(resourceField!=null)
        {
            ResourceFieldSampleSeconds=F(resourceField,"sample_seconds",ResourceFieldSampleSeconds); ResourceFieldPassiveSyncSeconds=F(resourceField,"passive_sync_seconds",ResourceFieldPassiveSyncSeconds); ResourceFieldMaxSampleDistance=F(resourceField,"max_sample_distance",ResourceFieldMaxSampleDistance);
            ResourceHarvestNegative=F(resourceField,"resource_harvest_negative",ResourceHarvestNegative); ResourceHarvestPositive=F(resourceField,"resource_harvest_positive",ResourceHarvestPositive);
            FarmingHarvestNegative=F(resourceField,"farming_harvest_negative",FarmingHarvestNegative); FarmingHarvestPositive=F(resourceField,"farming_harvest_positive",FarmingHarvestPositive);
            AnimalHarvestNegative=F(resourceField,"animal_harvest_negative",AnimalHarvestNegative); AnimalHarvestPositive=F(resourceField,"animal_harvest_positive",AnimalHarvestPositive);
            TrackingMinDistance=F(resourceField,"tracking_min_distance",TrackingMinDistance); TrackingNeutralDistance=F(resourceField,"tracking_neutral_distance",TrackingNeutralDistance); TrackingMaxDistance=F(resourceField,"tracking_max_distance",TrackingMaxDistance);
            TrackingNegativeAcquireSeconds=F(resourceField,"tracking_negative_acquire_seconds",TrackingNegativeAcquireSeconds); TrackingNeutralAcquireSeconds=F(resourceField,"tracking_neutral_acquire_seconds",TrackingNeutralAcquireSeconds); TrackingPositiveAcquireSeconds=F(resourceField,"tracking_positive_acquire_seconds",TrackingPositiveAcquireSeconds);
            TrackingAcquireAward=F(resourceField,"tracking_acquire_award",TrackingAcquireAward); TrackingFollowDistance=F(resourceField,"tracking_follow_distance",TrackingFollowDistance); TrackingFollowAward=F(resourceField,"tracking_follow_award",TrackingFollowAward); TrackingRepeatSeconds=F(resourceField,"tracking_repeat_seconds",TrackingRepeatSeconds);
            TrackingHabitatDistanceMultiplier=F(resourceField,"tracking_habitat_distance_multiplier",TrackingHabitatDistanceMultiplier); TrackingHabitatAcquireMultiplier=F(resourceField,"tracking_habitat_acquire_multiplier",TrackingHabitatAcquireMultiplier);
            string butcher=(string)resourceField.Attribute("animal_processing_tool_tokens"); if(!string.IsNullOrWhiteSpace(butcher)) AnimalProcessingToolTokens=Tokens(butcher);
        }
        XElement weaponFamily=root.Element("weapon_family");
        if(weaponFamily!=null)
        {
            WeaponFamilyPassiveSyncSeconds=F(weaponFamily,"passive_sync_seconds",WeaponFamilyPassiveSyncSeconds);
            MeleeStaminaNegative=F(weaponFamily,"melee_stamina_negative",MeleeStaminaNegative); MeleeStaminaPositive=F(weaponFamily,"melee_stamina_positive",MeleeStaminaPositive);
            MeleeSpeedNegative=F(weaponFamily,"melee_speed_negative",MeleeSpeedNegative); MeleeSpeedPositive=F(weaponFamily,"melee_speed_positive",MeleeSpeedPositive);
            RangedReloadNegative=F(weaponFamily,"ranged_reload_negative",RangedReloadNegative); RangedReloadPositive=F(weaponFamily,"ranged_reload_positive",RangedReloadPositive);
            RangedHandlingNegative=F(weaponFamily,"ranged_handling_negative",RangedHandlingNegative); RangedHandlingPositive=F(weaponFamily,"ranged_handling_positive",RangedHandlingPositive);
            RangedSpreadNegative=F(weaponFamily,"ranged_spread_negative",RangedSpreadNegative); RangedSpreadPositive=F(weaponFamily,"ranged_spread_positive",RangedSpreadPositive);
            RangedRecoilNegative=F(weaponFamily,"ranged_recoil_negative",RangedRecoilNegative); RangedRecoilPositive=F(weaponFamily,"ranged_recoil_positive",RangedRecoilPositive);
        }
        XElement serviceCrafting=root.Element("service_crafting");
        if(serviceCrafting!=null)
        {
            ServiceCraftTimeNegative=F(serviceCrafting,"craft_time_negative",ServiceCraftTimeNegative); ServiceCraftTimePositive=F(serviceCrafting,"craft_time_positive",ServiceCraftTimePositive);
            ServiceRepairTimeNegative=F(serviceCrafting,"repair_time_negative",ServiceRepairTimeNegative); ServiceRepairTimePositive=F(serviceCrafting,"repair_time_positive",ServiceRepairTimePositive);
            ServiceRepairAmountNegative=F(serviceCrafting,"repair_amount_negative",ServiceRepairAmountNegative); ServiceRepairAmountPositive=F(serviceCrafting,"repair_amount_positive",ServiceRepairAmountPositive);
            MechanicsRepairNegative=F(serviceCrafting,"mechanics_repair_negative",MechanicsRepairNegative); MechanicsRepairPositive=F(serviceCrafting,"mechanics_repair_positive",MechanicsRepairPositive);
            MedicineReserveNegative=F(serviceCrafting,"medicine_reserve_negative",MedicineReserveNegative); MedicineReservePositive=F(serviceCrafting,"medicine_reserve_positive",MedicineReservePositive);
            MedicineFractureHealingNegative=F(serviceCrafting,"medicine_fracture_healing_negative",MedicineFractureHealingNegative); MedicineFractureHealingPositive=F(serviceCrafting,"medicine_fracture_healing_positive",MedicineFractureHealingPositive);
            MedicineInfectionAccelerationPositive=F(serviceCrafting,"medicine_infection_acceleration_positive",MedicineInfectionAccelerationPositive);
            string v=(string)serviceCrafting.Attribute("chemistry_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) ChemistryRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("metalworking_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) MetalworkingRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("gunsmithing_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) GunsmithingRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("tailoring_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) TailoringRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("construction_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) ConstructionRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("electrical_recipe_tokens"); if(!string.IsNullOrWhiteSpace(v)) ElectricalRecipeTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("chemistry_area_tokens"); if(!string.IsNullOrWhiteSpace(v)) ChemistryAreaTokens=Tokens(v);
            v=(string)serviceCrafting.Attribute("metalworking_area_tokens"); if(!string.IsNullOrWhiteSpace(v)) MetalworkingAreaTokens=Tokens(v);
        }
        XElement complexSystems=root.Element("complex_systems");
        if(complexSystems!=null)
        {
            ComplexPassiveSyncSeconds=F(complexSystems,"passive_sync_seconds",ComplexPassiveSyncSeconds);
            ExplosivesDamageNegative=F(complexSystems,"explosives_damage_negative",ExplosivesDamageNegative); ExplosivesDamagePositive=F(complexSystems,"explosives_damage_positive",ExplosivesDamagePositive); ExplosivesBlockAward=F(complexSystems,"explosives_block_award",ExplosivesBlockAward);
            TurretDamageNegative=F(complexSystems,"turret_damage_negative",TurretDamageNegative); TurretDamagePositive=F(complexSystems,"turret_damage_positive",TurretDamagePositive);
            DroneDamageNegative=F(complexSystems,"drone_damage_negative",DroneDamageNegative); DroneDamagePositive=F(complexSystems,"drone_damage_positive",DroneDamagePositive);
            DroneStunCycleNegative=F(complexSystems,"drone_stun_cycle_negative",DroneStunCycleNegative); DroneStunCyclePositive=F(complexSystems,"drone_stun_cycle_positive",DroneStunCyclePositive);
            DroneShockAward=F(complexSystems,"drone_shock_award",DroneShockAward); DroneShockRepeatSeconds=F(complexSystems,"drone_shock_repeat_seconds",DroneShockRepeatSeconds);
            DroneStockRecoveryAward=F(complexSystems,"drone_stock_recovery_award",DroneStockRecoveryAward); DroneStockRecoveryMinDistance=F(complexSystems,"drone_stock_recovery_min_distance",DroneStockRecoveryMinDistance); DroneStockRecoveryRepeatSeconds=F(complexSystems,"drone_stock_recovery_repeat_seconds",DroneStockRecoveryRepeatSeconds);
        }
        XElement attributes=root.Element("attributes");
        if(attributes!=null)
        {
            SkillProgressToAttribute=F(attributes,"skill_progress_to_attribute",SkillProgressToAttribute); BelowPotentialFullUntil=F(attributes,"below_potential_full_until",BelowPotentialFullUntil);
            AtPotentialMultiplier=F(attributes,"at_potential_multiplier",AtPotentialMultiplier); AbovePotentialMultiplier=F(attributes,"above_potential_multiplier",AbovePotentialMultiplier);
            AttributeOutcomeCenter=F(attributes,"outcome_center",AttributeOutcomeCenter); AttributeOutcomeRange=F(attributes,"outcome_range",AttributeOutcomeRange); AttributeOutcomeMaxSkillEquivalent=F(attributes,"outcome_max_skill_equivalent",AttributeOutcomeMaxSkillEquivalent);
        }
        ValidateMaps();
    }

    private static void LoadRecipeKnowledge(string path)
    {
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        if(root==null || (string)root.Attribute("schema_version")!="2") throw new InvalidDataException("recipe_knowledge.xml schema_version must be 2");
        foreach(XElement e in root.Elements("recipe"))
        {
            RebirthRecipeKnowledgeRule r=new RebirthRecipeKnowledgeRule
            {
                RecipeName=((string)e.Attribute("name")??string.Empty).Trim(),
                KnowledgeId=((string)e.Attribute("knowledge")??string.Empty).Trim(),
                LegacyKnowledgeId=((string)e.Attribute("legacy_knowledge")??string.Empty).Trim(),
                Category=((string)e.Attribute("category")??string.Empty).Trim(),
                LiteratureItemId=((string)e.Attribute("literature_item")??string.Empty).Trim()
            };
            if(r.RecipeName.Length==0 || r.KnowledgeId.Length==0) throw new InvalidDataException("recipe knowledge rule requires name and knowledge");
            RebirthKnowledgeDefinition knowledgeDef; if(!RebirthSurvivorDefinitionRegistry.TryGetKnowledge(r.KnowledgeId,out knowledgeDef)) throw new InvalidDataException("recipe knowledge references unknown Knowledge ID: "+r.KnowledgeId);
            if(r.LegacyKnowledgeId.Length>0 && !RebirthSurvivorDefinitionRegistry.TryGetKnowledge(r.LegacyKnowledgeId,out knowledgeDef)) throw new InvalidDataException("recipe legacy compatibility references unknown Knowledge ID: "+r.LegacyKnowledgeId);
            if(RecipeRules.ContainsKey(r.RecipeName)) throw new InvalidDataException("duplicate recipe knowledge rule: "+r.RecipeName);
            RecipeRules.Add(r.RecipeName,r);
        }
    }

    private static void LoadAudiobooks(string path)
    {
        Audiobooks.Clear();
        if(!File.Exists(path))return;
        XDocument doc=XDocument.Load(path);
        XElement root=doc.Root;
        if(root==null||root.Name!="rebirth_audiobooks")throw new InvalidDataException("invalid audiobook root");
        XElement entries=root.Element("entries");
        if(entries==null)return;
        foreach(XElement e in entries.Elements("audiobook"))
        {
            RebirthAudiobookDefinition d=new RebirthAudiobookDefinition
            {
                ItemId=A(e,"item_id"),
                SourceLiteratureId=A(e,"source_literature_id"),
                SkillId=A(e,"skill"),
                Subtype=A(e,"subtype"),
                IconKey=A(e,"icon_key"),
                AudioSeconds=F(e,"audio_seconds",0f),
                ImplementationState=A(e,"implementation_state")
            };
            if(string.IsNullOrEmpty(d.ItemId)||string.IsNullOrEmpty(d.SourceLiteratureId)||string.IsNullOrEmpty(d.IconKey))
                throw new InvalidDataException("audiobook entry requires item_id, source_literature_id and icon_key");
            if(d.AudioSeconds<5f||d.AudioSeconds>600f)throw new InvalidDataException("audiobook audio_seconds out of range: "+d.ItemId);
            RebirthLiteratureDefinition source;
            if(!LiteratureByItem.TryGetValue(d.SourceLiteratureId,out source)||source==null)
                throw new InvalidDataException("audiobook source literature is missing: "+d.ItemId);
            if(string.Equals(source.Kind,"theory",StringComparison.OrdinalIgnoreCase))
            {
                if(string.IsNullOrEmpty(d.SkillId)||!string.Equals(source.SkillId,d.SkillId,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("theory audiobook must match its source Skill: "+d.ItemId);
            }
            else if(string.Equals(source.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            {
                if(!string.IsNullOrEmpty(d.SkillId))
                    throw new InvalidDataException("discovery audiobook must not author a Skill id: "+d.ItemId);
            }
            else throw new InvalidDataException("unsupported audiobook source kind: "+d.ItemId);
            if(Audiobooks.ContainsKey(d.ItemId))throw new InvalidDataException("duplicate audiobook item_id: "+d.ItemId);
            Audiobooks[d.ItemId]=d;
        }
    }

    private static void LoadLiterature(string path)
    {
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        if(root==null || (string)root.Attribute("schema_version")!="1") throw new InvalidDataException("literature.xml schema_version must be 1");
        foreach(XElement e in root.Elements("item"))
        {
            RebirthLiteratureDefinition d=new RebirthLiteratureDefinition
            {
                ItemId=((string)e.Attribute("id")??string.Empty).Trim(),
                Kind=((string)e.Attribute("kind")??string.Empty).Trim().ToLowerInvariant(),
                SkillId=((string)e.Attribute("skill")??string.Empty).Trim(),
                KnowledgeId=((string)e.Attribute("knowledge")??string.Empty).Trim(),
                MarkerId=((string)e.Attribute("marker")??string.Empty).Trim(),
                Amount=F(e,"amount",0f),
                StudySeconds=F(e,"study_seconds",0f)
            };
            if(d.ItemId.Length==0 || (d.Kind!="theory" && d.Kind!="discovery")) throw new InvalidDataException("literature item requires id and kind=theory|discovery");
            if(d.StudySeconds<5f || d.StudySeconds>600f) throw new InvalidDataException("literature study_seconds must be between 5 and 600: "+d.ItemId);
            if(LiteratureByItem.ContainsKey(d.ItemId)) throw new InvalidDataException("duplicate literature item: "+d.ItemId);
            if(d.Kind=="theory")
            {
                RebirthSkillDefinition skill; if(d.SkillId.Length==0 || !RebirthSurvivorDefinitionRegistry.TryGetSkill(d.SkillId,out skill)) throw new InvalidDataException("literature theory item references unknown Skill: "+d.ItemId+" -> "+d.SkillId);
                if(d.Amount<=0f || d.MarkerId.Length==0) throw new InvalidDataException("literature theory item requires positive amount and marker: "+d.ItemId);
            }
            else
            {
                RebirthKnowledgeDefinition knowledge; if(d.KnowledgeId.Length==0 || !RebirthSurvivorDefinitionRegistry.TryGetKnowledge(d.KnowledgeId,out knowledge)) throw new InvalidDataException("literature discovery item references unknown Knowledge: "+d.ItemId+" -> "+d.KnowledgeId);
            }
            LiteratureByItem.Add(d.ItemId,d);
        }
        foreach(RebirthRecipeKnowledgeRule rule in RecipeRules.Values)
            if(rule.LiteratureItemId.Length>0 && !LiteratureByItem.ContainsKey(rule.LiteratureItemId)) throw new InvalidDataException("recipe knowledge literature_item is not authored in literature.xml: "+rule.RecipeName+" -> "+rule.LiteratureItemId);
    }

    private static RebirthSkillSourceMap ParseMap(XElement e)
    {
        return new RebirthSkillSourceMap { SkillId=((string)e.Attribute("skill")??string.Empty).Trim(), ItemNames=ExactTokens((string)e.Attribute("item_names")), ItemTokens=Tokens((string)e.Attribute("item_tokens")), TagTokens=Tokens((string)e.Attribute("tag_tokens")), ToolTokens=Tokens((string)e.Attribute("tool_tokens")), BlockTokens=Tokens((string)e.Attribute("block_tokens")) };
    }
    private static void ValidateMaps()
    {
        foreach(RebirthSkillSourceMap m in CombatMaps) { RebirthSkillDefinition skillDef; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(m.SkillId,out skillDef)) throw new InvalidDataException("combat map references unknown Skill ID: "+m.SkillId); }
        foreach(RebirthSkillSourceMap m in HarvestMaps) { RebirthSkillDefinition skillDef; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(m.SkillId,out skillDef)) throw new InvalidDataException("harvest map references unknown Skill ID: "+m.SkillId); }
    }
    private static string[] ExactTokens(string raw) { if(string.IsNullOrWhiteSpace(raw))return new string[0]; string[] a=raw.Split(','); List<string> r=new List<string>(); for(int i=0;i<a.Length;i++){string v=(a[i]??string.Empty).Trim();if(v.Length>0)r.Add(v);}return r.ToArray(); }
    private static bool MatchesExact(string value,string[] tokens) { if(string.IsNullOrEmpty(value)||tokens==null)return false; for(int i=0;i<tokens.Length;i++) if(string.Equals(value,tokens[i],StringComparison.OrdinalIgnoreCase))return true; return false; }
    private static string[] Tokens(string raw) { if(string.IsNullOrWhiteSpace(raw))return new string[0]; string[] a=raw.Split(','); List<string> r=new List<string>(); for(int i=0;i<a.Length;i++){string v=(a[i]??string.Empty).Trim().ToLowerInvariant();if(v.Length>0)r.Add(v);}return r.ToArray(); }
    private static bool MatchesAny(string value,string[] tokens) { if(string.IsNullOrEmpty(value)||tokens==null)return false; string x=value.ToLowerInvariant(); for(int i=0;i<tokens.Length;i++) if(tokens[i]!="*" && x.IndexOf(tokens[i],StringComparison.Ordinal)>=0)return true; return false; }
    private static string A(XElement e,string name)
    {
        if(e==null)return string.Empty;
        XAttribute a=e.Attribute(name);
        return a!=null?(a.Value??string.Empty).Trim():string.Empty;
    }

    private static float F(XElement e,string name,float fallback) { XAttribute a=e.Attribute(name); float v; return a!=null && float.TryParse(a.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:fallback; }
}
