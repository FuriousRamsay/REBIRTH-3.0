using System;

#nullable disable

public enum RebirthPlayerProgressionMode
{
    BaseGame = 0,
    Rebirth = 1
}

public enum RebirthTraitPolarity
{
    Positive = 0,
    Negative = 1,
    Mixed = 2
}

public enum RebirthDefinitionAvailability
{
    Universal = 0,
    Restricted = 1,
    Deferred = 2
}

public static class RebirthSurvivorIds
{
    public const string BackgroundCleanSlate = "background.clean_slate";

    public const string DietUnrestricted = "diet.unrestricted";
    public const string DietVegetarian = "diet.vegetarian";
    public const string DietVegan = "diet.vegan";
    public const string DietCarnivore = "diet.carnivore";

    public const string TraitSmoker = "trait.smoker";
    public const string TraitHeavyDrinker = "trait.heavy_drinker";
    public const string TraitCaffeineDependent = "trait.caffeine_dependent";
    public const string TraitTeetotaler = "trait.teetotaler";
    public const string TraitMeatCentricPalate = "trait.meat_centric_palate";
    public const string TraitRefinedPalate = "trait.refined_palate";
    public const string TraitFoodIndifferent = "trait.food_indifferent";

    public const string SkillSpears = "skill.spears";
    public const string SkillClubs = "skill.clubs";
    public const string SkillSwords = "skill.swords";
    public const string SkillAxes = "skill.axes";
    public const string SkillBatons = "skill.batons";
    public const string SkillHammers = "skill.hammers";
    public const string SkillKnives = "skill.knives";
    public const string SkillScythes = "skill.scythes";
    public const string SkillKnuckles = "skill.knuckles";
    public const string SkillUnarmed = "skill.unarmed";
    public const string SkillArchery = "skill.archery";
    public const string SkillPistols = "skill.pistols";
    public const string SkillRevolvers = "skill.revolvers";
    public const string SkillHeavyHandguns = "skill.heavy_handguns";
    public const string SkillShotguns = "skill.shotguns";
    public const string SkillAssaultRifles = "skill.assault_rifles";
    public const string SkillTacticalRifles = "skill.tactical_rifles";
    public const string SkillLongRangeRifles = "skill.long_range_rifles";
    public const string SkillExplosives = "skill.explosives";
    public const string SkillDeployableTurrets = "skill.deployable_turrets";
    public const string SkillDroneOperations = "skill.drone_operations";
    public const string SkillMining = "skill.mining";
    public const string SkillLogging = "skill.logging";
    public const string SkillSalvage = "skill.salvage";
    public const string SkillFarming = "skill.farming";
    public const string SkillAnimalProcessing = "skill.animal_processing";
    public const string SkillTracking = "skill.tracking";
    public const string SkillMechanics = "skill.mechanics";
    public const string SkillMedicine = "skill.medicine";
    public const string SkillCooking = "skill.cooking";
    public const string SkillDrinkPreparation = "skill.drink_preparation";
    public const string SkillMaintenance = "skill.maintenance";
    public const string SkillConstruction = "skill.construction";
    public const string SkillElectrical = "skill.electrical";
    public const string SkillMetalworking = "skill.metalworking";
    public const string SkillGunsmithing = "skill.gunsmithing";
    public const string SkillChemistry = "skill.chemistry";
    public const string SkillLockpicking = "skill.lockpicking";
    public const string SkillStealth = "skill.stealth";
    public const string SkillAthletics = "skill.athletics";
    public const string SkillArmorProficiency = "skill.armor_proficiency";
    public const string SkillBartering = "skill.bartering";
    public const string SkillTrading = "skill.trading";
    public const string SkillTeaching = "skill.teaching";

    // Advanced Disciplines foundation. Animal Handling is current as of Chunk B; Black Magic/Rage
    // Black Magic is active as of Chunk F; Rage remains a stable planned Skill ID until its owning chunk.
    public const string SkillAnimalHandling = "skill.animal_handling";
    public const string SkillBlackMagic = "skill.black_magic";
    public const string SkillRage = "skill.rage";
    public const string KnowledgeRageBasic = "knowledge.rage.basic";
    public const string KnowledgeRageControlled = "knowledge.rage.controlled";
    public const string KnowledgeRageOffensive = "knowledge.rage.offensive";
    public const string KnowledgeRageBlood = "knowledge.rage.blood";
    public const string KnowledgeInfectedAnimalBehavior = "knowledge.animal_handling.infected_behavior";
    public const string KnowledgePantherHandling = "knowledge.animal_handling.panther_handling";
    public const string KnowledgeBlackMagicPantherBinding = "knowledge.black_magic.panther_binding";

    public const string DisciplineBeastmaster = "discipline.beastmaster";
    public const string DisciplineWitchDoctor = "discipline.witch_doctor";
    public const string DisciplineBerserker = "discipline.berserker";

    public const string KnowledgeBeastmasterPredatorHandling = "knowledge.beastmaster.predator_handling";
    public const string KnowledgeBeastmasterPackBonding = "knowledge.beastmaster.pack_bonding";
    public const string KnowledgeBeastmasterApexPredatorHandling = "knowledge.beastmaster.apex_predator_handling";
    public const string KnowledgeBlackMagicMindControlI = "knowledge.black_magic.mind_control_i";
    public const string KnowledgeBlackMagicMindControlII = "knowledge.black_magic.mind_control_ii";
    public const string KnowledgeBlackMagicMindControlIII = "knowledge.black_magic.mind_control_iii";
    public const string KnowledgeBlackMagicUndeadConditioning = "knowledge.black_magic.undead_conditioning";
    public const string KnowledgeBlackMagicBindingRitual = "knowledge.black_magic.binding_ritual";
    public const string KnowledgeBlackMagicFeralBinding = "knowledge.black_magic.feral_binding";
    public const string KnowledgeBlackMagicRadiatedBinding = "knowledge.black_magic.radiated_binding";
    public const string KnowledgeBlackMagicChargedBinding = "knowledge.black_magic.charged_binding";
    public const string KnowledgeBlackMagicInfernalBinding = "knowledge.black_magic.infernal_binding";
    public const string KnowledgeBlackMagicLesserSummoning = "knowledge.black_magic.lesser_summoning";
    public const string KnowledgeBlackMagicGreaterSummoning = "knowledge.black_magic.greater_summoning";
    public const string KnowledgeBlackMagicArmyOfTheDead = "knowledge.black_magic.army_of_the_dead";
    public const string AccomplishmentDogAdvancedTraining = "accomplishment.dog.advanced_training";
    public const string TrialBeastmasterInitiation = "trial.beastmaster.initiation";
    public const string TrialWitchDoctorInitiation = "trial.witch_doctor.initiation";
    public const string TrialBerserkerInitiation = "trial.berserker.initiation";
}

public static class RebirthSurvivorMode
{
    private static int configuredMode = (int)RebirthPlayerProgressionMode.Rebirth;

    public static RebirthPlayerProgressionMode ConfiguredMode
    {
        get { return (RebirthPlayerProgressionMode)configuredMode; }
    }

    public static bool IsEnabledForCurrentWorld()
    {
        return ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
    }

    public static bool IsBaseGameForCurrentWorld()
    {
        return ConfiguredMode == RebirthPlayerProgressionMode.BaseGame;
    }

    public static bool IsEnabled(RebirthSandboxState state)
    {
        return state != null && state.PlayerProgression == RebirthPlayerProgressionMode.Rebirth;
    }

    public static void ApplyFromSandbox(RebirthPlayerProgressionMode mode)
    {
        if (mode != RebirthPlayerProgressionMode.BaseGame && mode != RebirthPlayerProgressionMode.Rebirth)
            mode = RebirthPlayerProgressionMode.BaseGame;
        configuredMode = (int)mode;
    }
}
