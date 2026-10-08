using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public sealed class RebirthDogBreedDefinition
{
    public string BreedId { get; }
    public string DisplayName { get; }
    public string LocalizationKey { get; }
    public string EntityClassName { get; }
    public string SpawnDeployId { get; }
    public string PickedUpDeployId { get { return SpawnDeployId + "_PickedUp"; } }
    public string IconName { get; }
    public string MeshUri { get; }

    public RebirthDogBreedDefinition(
        string breedId,
        string displayName,
        string localizationKey,
        string entityClassName,
        string spawnDeployId,
        string iconName,
        string meshUri)
    {
        BreedId = Require(breedId, nameof(breedId));
        DisplayName = Require(displayName, nameof(displayName));
        LocalizationKey = Require(localizationKey, nameof(localizationKey));
        EntityClassName = Require(entityClassName, nameof(entityClassName));
        SpawnDeployId = Require(spawnDeployId, nameof(spawnDeployId));
        IconName = Require(iconName, nameof(iconName));
        MeshUri = Require(meshUri, nameof(meshUri));
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Dog breed definition field is required.", name);
        return value.Trim();
    }
}

/// <summary>
/// Canonical REBIRTH dog-companion breed authoring registry.
/// Breed definitions remain presentation/identity-only; shared gameplay behavior
/// lives in the modern dog runtime/lifecycle/inventory services so it is not
/// duplicated across eight breed classes.
/// </summary>
public static class RebirthDogDefinitions
{
    public const string ProfileId = "companion.dog";
    public const string CommonEntityClassName = "FuriousRamsayNPCDog";
    public const string RuntimeClassName = "EntityRebirthDogCompanion";
    public const string RuntimeClassAssemblyName = "EntityRebirthDogCompanion, RebirthUtils";
    public const string PhysicsBodyName = "FuriousRamsayAnimalDog";
    public const int ExpectedBreedCount = 8;

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthDogBreedDefinition> ByBreedId =
        new Dictionary<string, RebirthDogBreedDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthDogBreedDefinition> ByEntityClass =
        new Dictionary<string, RebirthDogBreedDefinition>(StringComparer.OrdinalIgnoreCase);
    private static bool initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (initialized) return;

            Register(new RebirthDogBreedDefinition(
                "german_shepherd", "German Shepherd", "xuiRebirthDogBreedGermanShepherd",
                "FuriousRamsayNPCShepherd001_FR", "FuriousRamsaySpawnCubeShepherdDog001_FR",
                "Shepherd001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Shepherd001_FR"));
            Register(new RebirthDogBreedDefinition(
                "pitbull", "Pitbull", "xuiRebirthDogBreedPitbull",
                "FuriousRamsayNPCPitbull001_FR", "FuriousRamsaySpawnCubePitbullDog001_FR",
                "Pitbull001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Pitbull001_FR"));
            Register(new RebirthDogBreedDefinition(
                "labrador", "Labrador", "xuiRebirthDogBreedLabrador",
                "FuriousRamsayNPCLabrador001_FR", "FuriousRamsaySpawnCubeLabradorDog001_FR",
                "Labrador001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Labrador001_FR"));
            Register(new RebirthDogBreedDefinition(
                "husky", "Husky", "xuiRebirthDogBreedHusky",
                "FuriousRamsayNPCHusky001_FR", "FuriousRamsaySpawnCubeHuskyDog001_FR",
                "Husky001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Husky001_FR"));
            Register(new RebirthDogBreedDefinition(
                "golden_retriever", "Golden Retriever", "xuiRebirthDogBreedGoldenRetriever",
                "FuriousRamsayNPCGoldenRetriever001_FR", "FuriousRamsaySpawnCubeGoldenRetrieverDog001_FR",
                "GoldenRetriever001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?GoldenRetriever001_FR"));
            Register(new RebirthDogBreedDefinition(
                "doberman", "Doberman", "xuiRebirthDogBreedDoberman",
                "FuriousRamsayNPCDoberman001_FR", "FuriousRamsaySpawnCubeDobermanDog001_FR",
                "Doberman001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Doberman001_FR"));
            Register(new RebirthDogBreedDefinition(
                "dalmatian", "Dalmatian", "xuiRebirthDogBreedDalmatian",
                "FuriousRamsayNPCDalmatian001_FR", "FuriousRamsaySpawnCubeDalmatianDog001_FR",
                "Dalmatian001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?Dalmatian001_FR"));
            Register(new RebirthDogBreedDefinition(
                "bull_terrier", "Bull Terrier", "xuiRebirthDogBreedBullTerrier",
                "FuriousRamsayNPCBullTerrier001_FR", "FuriousRamsaySpawnCubeBullTerrierDog001_FR",
                "BullTerrier001_FR_icon", "#@modfolder:Resources/FR_Animals.unity3d?BullTerrier001_FR"));

            if (ByBreedId.Count != ExpectedBreedCount || ByEntityClass.Count != ExpectedBreedCount)
                throw new InvalidOperationException(
                    "REBIRTH dog breed registry must contain exactly " + ExpectedBreedCount + " unique breeds.");

            initialized = true;
        }
    }

    public static RebirthDogBreedDefinition[] GetSnapshot()
    {
        EnsureInitialized();
        lock (Sync)
        {
            RebirthDogBreedDefinition[] result = new RebirthDogBreedDefinition[ByBreedId.Count];
            ByBreedId.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) =>
                string.Compare(left.BreedId, right.BreedId, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }

    public static bool TryGetByBreedId(string breedId, out RebirthDogBreedDefinition definition)
    {
        EnsureInitialized();
        lock (Sync)
            return ByBreedId.TryGetValue(breedId ?? string.Empty, out definition);
    }

    public static bool TryGetByEntityClass(string entityClassName, out RebirthDogBreedDefinition definition)
    {
        EnsureInitialized();
        lock (Sync)
            return ByEntityClass.TryGetValue(entityClassName ?? string.Empty, out definition);
    }

    private static void Register(RebirthDogBreedDefinition definition)
    {
        if (ByBreedId.ContainsKey(definition.BreedId))
            throw new InvalidOperationException("Duplicate REBIRTH dog BreedId: " + definition.BreedId);
        if (ByEntityClass.ContainsKey(definition.EntityClassName))
            throw new InvalidOperationException("Duplicate REBIRTH dog entity class: " + definition.EntityClassName);
        ByBreedId.Add(definition.BreedId, definition);
        ByEntityClass.Add(definition.EntityClassName, definition);
    }
}

public sealed class RebirthDogAuthoringValidationResult
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public int ExpectedBreedCount { get; internal set; }
    public int ResolvedBreedCount { get; internal set; }
    public int ErrorCount => errors.Count;
    public int WarningCount => warnings.Count;
    public bool Succeeded => errors.Count == 0 && ResolvedBreedCount == ExpectedBreedCount;

    internal void Error(string message) { errors.Add(message); }
    internal void Warning(string message) { warnings.Add(message); }

    public string ToReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH Dog] authoring validation result=")
            .Append(Succeeded ? "PASS" : "FAIL")
            .Append(" breeds=").Append(ResolvedBreedCount).Append('/').Append(ExpectedBreedCount)
            .Append(" errors=").Append(ErrorCount)
            .Append(" warnings=").Append(WarningCount);
        for (int i = 0; i < errors.Count; i++)
            builder.AppendLine().Append("  ERROR: ").Append(errors[i]);
        for (int i = 0; i < warnings.Count; i++)
            builder.AppendLine().Append("  WARN: ").Append(warnings[i]);
        return builder.ToString();
    }
}

/// <summary>
/// Startup authoring validator for the common dog and all eight breeds. Runtime
/// lifecycle/behavior/inventory parity is validated separately by the dog
/// completeness audit and runtime test matrix.
/// </summary>
public static class RebirthDogAuthoringValidator
{
    public static RebirthDogAuthoringValidationResult ValidateLoadedEntityClasses()
    {
        RebirthDogDefinitions.EnsureInitialized();
        RebirthNpcProductionProfileCatalogue.EnsureInitialized();

        RebirthDogAuthoringValidationResult result = new RebirthDogAuthoringValidationResult
        {
            ExpectedBreedCount = RebirthDogDefinitions.ExpectedBreedCount
        };

        if (EntityClass.list == null)
        {
            result.Error("EntityClass.list is unavailable.");
            return result;
        }

        ValidateProductionProfile(result);
        ValidateCommonClass(result);

        RebirthDogBreedDefinition[] breeds = RebirthDogDefinitions.GetSnapshot();
        for (int i = 0; i < breeds.Length; i++)
            ValidateBreed(breeds[i], result);

        return result;
    }

    private static void ValidateProductionProfile(RebirthDogAuthoringValidationResult result)
    {
        RebirthNpcProductionProfile profile;
        if (!RebirthNpcProductionProfileCatalogue.TryGet(RebirthDogDefinitions.ProfileId, out profile) || profile == null)
        {
            result.Error("Production profile '" + RebirthDogDefinitions.ProfileId + "' is missing.");
            return;
        }

        if (profile.Category != RebirthNpcCategory.DogCompanion)
            result.Error("companion.dog production profile category is not DogCompanion.");
        if (profile.ModelPipeline != RebirthNpcModelPipeline.Dog)
            result.Error("companion.dog production profile model pipeline is not Dog.");
        if (!string.Equals(profile.EntityClassKey, RebirthDogDefinitions.RuntimeClassName, StringComparison.Ordinal))
            result.Error("companion.dog EntityClassKey='" + profile.EntityClassKey +
                "' but canonical runtime class is '" + RebirthDogDefinitions.RuntimeClassName + "'.");
    }

    private static void ValidateCommonClass(RebirthDogAuthoringValidationResult result)
    {
        EntityClass definition;
        if (!TryGetEntityClass(RebirthDogDefinitions.CommonEntityClassName, out definition))
        {
            result.Error("Common dog entity class '" + RebirthDogDefinitions.CommonEntityClassName + "' is missing.");
            return;
        }

        ValidateClassBinding(RebirthDogDefinitions.CommonEntityClassName, definition, result);
        string profile = Get(definition, "RebirthProfile");
        if (!string.Equals(profile, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase))
            result.Error("Common dog class RebirthProfile='" + profile + "' instead of '" + RebirthDogDefinitions.ProfileId + "'.");
        if (!string.Equals(Get(definition, "PhysicsBody"), RebirthDogDefinitions.PhysicsBodyName, StringComparison.Ordinal))
            result.Error("Common dog class does not use PhysicsBody='" + RebirthDogDefinitions.PhysicsBodyName + "'.");
        if (!string.Equals(Get(definition, "Faction"), "whiteriver", StringComparison.OrdinalIgnoreCase))
            result.Error("Common dog class must author friendly whiteriver faction.");
        if (!string.Equals(Get(definition, "IsEnemyEntity"), "false", StringComparison.OrdinalIgnoreCase))
            result.Error("Common dog class must explicitly set IsEnemyEntity=false.");
    }

    private static void ValidateBreed(RebirthDogBreedDefinition breed, RebirthDogAuthoringValidationResult result)
    {
        EntityClass definition;
        if (!TryGetEntityClass(breed.EntityClassName, out definition))
        {
            result.Error("Missing dog breed entity class '" + breed.EntityClassName + "' (BreedId=" + breed.BreedId + ").");
            return;
        }

        ValidateClassBinding(breed.EntityClassName, definition, result);

        string breedId = Get(definition, "RebirthDogBreedId");
        string loc = Get(definition, "RebirthDogBreedLocalizationKey");
        string icon = Get(definition, "RebirthDogIcon");
        string mesh = Get(definition, "Mesh");
        string profile = Get(definition, "RebirthProfile");

        if (!string.Equals(breedId, breed.BreedId, StringComparison.OrdinalIgnoreCase))
            result.Error(breed.EntityClassName + " BreedId='" + breedId + "' expected '" + breed.BreedId + "'.");
        if (!string.Equals(loc, breed.LocalizationKey, StringComparison.Ordinal))
            result.Error(breed.EntityClassName + " localization key='" + loc + "' expected '" + breed.LocalizationKey + "'.");
        if (!string.Equals(icon, breed.IconName, StringComparison.Ordinal))
            result.Error(breed.EntityClassName + " icon='" + icon + "' expected '" + breed.IconName + "'.");
        // XmlPatcher expands the shorthand @modfolder: token to
        // @modfolder(<loaded mod name>): before the effective entityclasses XML is
        // parsed. Compare the canonicalized URI rather than treating that native
        // expansion as an authoring mismatch.
        string normalizedMesh = NormalizeModFolderUri(mesh);
        string normalizedExpectedMesh = NormalizeModFolderUri(breed.MeshUri);
        if (!string.Equals(normalizedMesh, normalizedExpectedMesh, StringComparison.Ordinal))
            result.Error(breed.EntityClassName + " mesh URI='" + mesh + "' normalized='" + normalizedMesh +
                "' expected='" + normalizedExpectedMesh + "'.");
        if (!string.Equals(profile, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase))
            result.Error(breed.EntityClassName + " does not resolve RebirthProfile='companion.dog'.");

        result.ResolvedBreedCount++;
    }

    private static void ValidateClassBinding(
        string displayName,
        EntityClass definition,
        RebirthDogAuthoringValidationResult result)
    {
        string configured = Get(definition, "Class");
        if (!string.Equals(configured, RebirthDogDefinitions.RuntimeClassAssemblyName, StringComparison.Ordinal))
            result.Error(displayName + " Class='" + configured + "' expected '" +
                RebirthDogDefinitions.RuntimeClassAssemblyName + "'.");
        if (definition.classname != typeof(EntityRebirthDogCompanion))
            result.Error(displayName + " CLR class did not resolve to EntityRebirthDogCompanion.");
    }

    private static bool TryGetEntityClass(string entityClassName, out EntityClass definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(entityClassName) || EntityClass.list == null) return false;
        return EntityClass.list.TryGetValue(EntityClass.FromString(entityClassName), out definition) && definition != null;
    }

    public static string NormalizeModFolderUri(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        const string marker = "@modfolder(";
        int start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return value;

        int close = value.IndexOf("):", start + marker.Length, StringComparison.Ordinal);
        if (close < 0) return value;

        return value.Substring(0, start) + "@modfolder:" + value.Substring(close + 2);
    }

    private static string Get(EntityClass definition, string key)
    {
        if (definition == null || definition.Properties == null || definition.Properties.Values == null)
            return string.Empty;
        string value;
        return definition.Properties.Values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }


}
