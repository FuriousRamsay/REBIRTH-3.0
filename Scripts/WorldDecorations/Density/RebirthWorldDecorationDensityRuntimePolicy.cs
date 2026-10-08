using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Owns the REBIRTH tree and vehicle density factors while native 3.1 continues
/// to perform all biome-decoration and road-map placement.
///
/// Definitions are registered with their unmodified XML probabilities, allowing
/// the authoritative per-world option state to be reapplied without cumulative
/// multiplication and regardless of whether XML or the save snapshot loads first.
/// </summary>
public static class RebirthWorldDecorationDensityRuntimePolicy
{
    public static readonly int[] AllowedMultipliers =
    {
        10, 20, 30, 40, 50, 60, 70, 80, 90, 100,
        125, 150, 175, 200, 225, 250, 275, 300, 325, 350,
        375, 400, 425, 450, 475, 500
    };

    private enum BiomeDecorationKind : byte
    {
        Tree,
        Vehicle
    }

    private sealed class RegisteredBiomeDecoration
    {
        public BiomeBlockDecoration Decoration;
        public BiomeDecorationKind Kind;
        public string BiomeName;
        public string BlockName;
        public float SourceProbability;
        public float RebirthBaselineFactor;
    }

    private sealed class RegisteredRoadVehicle
    {
        public PoiMapBlock Definition;
        public string RoadName;
        public string BlockName;
        public float SourceProbability;
        public float RebirthBaselineFactor;
    }

    private static readonly object Sync = new object();
    private static readonly List<RegisteredBiomeDecoration> BiomeDefinitions =
        new List<RegisteredBiomeDecoration>();
    private static readonly List<RegisteredRoadVehicle> RoadDefinitions =
        new List<RegisteredRoadVehicle>();

    private static int treePercent = 100;
    private static int vehiclePercent = 100;

#if DEBUG
    private static int definitionGeneration;
    private static int rebindCount;
    private static int unresolvedBiomeDefinitions;
    private static int unresolvedRoadDefinitions;
    private static string definitionLoadSource = "startup";
#endif

    public static int CurrentTreePercent
    {
        get { lock (Sync) return treePercent; }
    }

    public static int CurrentVehiclePercent
    {
        get { lock (Sync) return vehiclePercent; }
    }

    public static void SetMultipliers(int requestedTreePercent, int requestedVehiclePercent)
    {
        lock (Sync)
        {
            treePercent = NormalizeMultiplier(requestedTreePercent, 100);
            vehiclePercent = NormalizeMultiplier(requestedVehiclePercent, 100);
            ReapplyAllUnsafe();
#if DEBUG
            rebindCount++;
#endif
        }
    }

    public static int NormalizeMultiplier(int value, int fallback)
    {
        for (int i = 0; i < AllowedMultipliers.Length; i++)
            if (AllowedMultipliers[i] == value)
                return value;
        return fallback;
    }

    public static int MultiplierToIndex(int value)
    {
        int nearestIndex = 0;
        int nearestDistance = Math.Abs(AllowedMultipliers[0] - value);
        for (int i = 1; i < AllowedMultipliers.Length; i++)
        {
            int distance = Math.Abs(AllowedMultipliers[i] - value);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    /// <summary>
    /// Called by the WorldBiomes.readXML patch before an instantiating definition load.
    /// A non-instantiating pass has no resolved block references and must not replace
    /// the live registrations used by chunk generation.
    /// </summary>
    public static void BeginDefinitionLoad(bool instantiateReferences)
    {
        if (!instantiateReferences)
            return;

        lock (Sync)
        {
            // Release only the probability values this policy projected before a
            // new native definition generation begins. WorldBiomes may reuse the
            // same definition objects across reloads; clearing our registration
            // list without first releasing ownership can make the next pass treat
            // an already-multiplied probability as the new native baseline.
            for (int i = 0; i < BiomeDefinitions.Count; i++)
            {
                RegisteredBiomeDecoration item = BiomeDefinitions[i];
                if (item != null && item.Decoration != null)
                {
                    float expected = item.SourceProbability
                        * item.RebirthBaselineFactor
                        * ((item.Kind == BiomeDecorationKind.Tree ? treePercent : vehiclePercent) * 0.01f);
                    if (Approximately(item.Decoration.prob, expected))
                        item.Decoration.prob = item.SourceProbability;
                }
            }

            for (int i = 0; i < RoadDefinitions.Count; i++)
            {
                RegisteredRoadVehicle item = RoadDefinitions[i];
                if (item != null && item.Definition != null)
                {
                    float expected = item.SourceProbability
                        * item.RebirthBaselineFactor
                        * (vehiclePercent * 0.01f);
                    if (Approximately(item.Definition.m_Prob, expected))
                        item.Definition.m_Prob = item.SourceProbability;
                }
            }

            BiomeDefinitions.Clear();
            RoadDefinitions.Clear();
#if DEBUG
            definitionGeneration++;
            unresolvedBiomeDefinitions = 0;
            unresolvedRoadDefinitions = 0;
#endif
        }
    }

    /// <summary>
    /// Records which authoritative save/preset supplied the current values. The call
    /// remains Debug-only; SetMultipliers already performs the functional rebind.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void BeginNativeDefinitionLoad(string source)
    {
#if DEBUG
        lock (Sync)
            definitionLoadSource = string.IsNullOrEmpty(source) ? "GameStarting" : source;
#endif
    }

    /// <summary>
    /// Registers an exact 2.6 tree group or a 3.1/legacy vehicle helper before native
    /// BiomeDefinition.AddDecoBlock stores the definition.
    /// </summary>
    public static void RegisterBiomeDecoration(BiomeDefinition biome, BiomeBlockDecoration decoration)
    {
        RegisteredBiomeDecoration registration;
        if (!TryCreateBiomeRegistration(biome, decoration, out registration))
        {
#if DEBUG
            if (decoration != null && decoration.blockValues != null && decoration.blockValues.Length > 0
                && Block.BlocksLoaded && decoration.blockValues[0].Block == null)
            {
                lock (Sync)
                    unresolvedBiomeDefinitions++;
            }
#endif
            return;
        }

        lock (Sync)
        {
            for (int i = 0; i < BiomeDefinitions.Count; i++)
            {
                if (ReferenceEquals(BiomeDefinitions[i].Decoration, decoration))
                {
                    ApplyBiomeUnsafe(BiomeDefinitions[i]);
                    return;
                }
            }

            BiomeDefinitions.Add(registration);
            ApplyBiomeUnsafe(registration);
        }
    }

    /// <summary>
    /// Registers the native PoiMapBlock objects created for the three 2.6 road classes.
    /// This is the road-placement half of Vehicle Density that was missing from v24.
    /// </summary>
    public static void RegisterRoadVehicleDefinitions(
        WorldBiomes worldBiomes,
        XDocument xml,
        bool instantiateReferences)
    {
        if (!instantiateReferences || worldBiomes == null || xml == null || worldBiomes.m_PoiMap == null)
            return;

        foreach (XElement poiElement in xml.Descendants((XName)"poi"))
        {
            string roadName = poiElement.GetAttribute((XName)"name");
            float roadFactor;
            if (!TryGetRoadBaselineFactor(roadName, out roadFactor))
                continue;

            string colorText = poiElement.GetAttribute((XName)"poimapcolor");
            if (string.IsNullOrEmpty(colorText) || colorText.Length < 2)
            {
#if DEBUG
                lock (Sync)
                    unresolvedRoadDefinitions++;
#endif
                continue;
            }

            uint color;
            try
            {
                color = Convert.ToUInt32(colorText.Substring(1), 16);
            }
            catch
            {
#if DEBUG
                lock (Sync)
                    unresolvedRoadDefinitions++;
#endif
                continue;
            }

            PoiMapElement poiMapElement;
            if (!worldBiomes.m_PoiMap.TryGetValue(color, out poiMapElement)
                || poiMapElement == null
                || poiMapElement.blocksOnTop == null)
            {
#if DEBUG
                lock (Sync)
                    unresolvedRoadDefinitions++;
#endif
                continue;
            }

            for (int i = 0; i < poiMapElement.blocksOnTop.Count; i++)
            {
                PoiMapBlock roadBlock = poiMapElement.blocksOnTop[i];
                if (roadBlock == null || roadBlock.blockValue.isair || roadBlock.blockValue.Block == null)
                    continue;

                string blockName = roadBlock.blockValue.Block.blockName;
                if (!IsVehicleHelper(blockName))
                    continue;

                RegisterRoadUnsafe(new RegisteredRoadVehicle
                {
                    Definition = roadBlock,
                    RoadName = roadName,
                    BlockName = blockName,
                    SourceProbability = roadBlock.m_Prob,
                    RebirthBaselineFactor = roadFactor
                });
            }
        }
    }

    private static void RegisterRoadUnsafe(RegisteredRoadVehicle registration)
    {
        lock (Sync)
        {
            for (int i = 0; i < RoadDefinitions.Count; i++)
            {
                if (ReferenceEquals(RoadDefinitions[i].Definition, registration.Definition))
                {
                    ApplyRoadUnsafe(RoadDefinitions[i]);
                    return;
                }
            }

            RoadDefinitions.Add(registration);
            ApplyRoadUnsafe(registration);
        }
    }

    public static string BuildDiagnosticReport()
    {
#if DEBUG
        lock (Sync)
        {
            int trees = 0;
            int biomeVehicles = 0;
            for (int i = 0; i < BiomeDefinitions.Count; i++)
            {
                if (BiomeDefinitions[i].Kind == BiomeDecorationKind.Tree)
                    trees++;
                else
                    biomeVehicles++;
            }

            StringBuilder sb = new StringBuilder(2048);
            sb.Append("[RebirthWorldDensity] tree=").Append(treePercent).Append('%')
                .Append(" vehicle=").Append(vehiclePercent).Append('%')
                .Append(" definitionGeneration=").Append(definitionGeneration)
                .Append(" registeredTrees=").Append(trees)
                .Append(" registeredBiomeVehicles=").Append(biomeVehicles)
                .Append(" registeredRoadVehicles=").Append(RoadDefinitions.Count)
                .Append(" rebinds=").Append(rebindCount)
                .Append(" unresolvedBiome=").Append(unresolvedBiomeDefinitions)
                .Append(" unresolvedRoad=").Append(unresolvedRoadDefinitions)
                .Append(" definitionLoadSource=").Append(definitionLoadSource)
                .Append(" biomePlacement=BiomeDefinition.AddDecoBlock")
                .Append(" roadPlacement=WorldBiomes.PoiMapBlock")
                .Append(" nativePlacement=true")
                .Append(" forcedStaticDataReset=false")
                .Append(" affectsExistingGeneratedChunks=false");

            int shownBiomeVehicles = 0;
            for (int i = 0; i < BiomeDefinitions.Count && shownBiomeVehicles < 6; i++)
            {
                RegisteredBiomeDecoration item = BiomeDefinitions[i];
                if (item.Kind != BiomeDecorationKind.Vehicle)
                    continue;

                sb.Append("\n  biomeVehicle biome=").Append(item.BiomeName)
                    .Append(" block=").Append(item.BlockName)
                    .Append(" sourceProb=").Append(item.SourceProbability.ToString("0.########"))
                    .Append(" baseline=").Append(item.RebirthBaselineFactor.ToString("0.###"))
                    .Append(" effectiveProb=").Append(item.Decoration.prob.ToString("0.########"));
                shownBiomeVehicles++;
            }

            for (int i = 0; i < RoadDefinitions.Count; i++)
            {
                RegisteredRoadVehicle item = RoadDefinitions[i];
                sb.Append("\n  roadVehicle road=").Append(item.RoadName)
                    .Append(" block=").Append(item.BlockName)
                    .Append(" sourceProb=").Append(item.SourceProbability.ToString("0.########"))
                    .Append(" baseline=").Append(item.RebirthBaselineFactor.ToString("0.###"))
                    .Append(" effectiveProb=").Append(item.Definition.m_Prob.ToString("0.########"));
            }

            return sb.ToString();
        }
#else
        return "[RebirthWorldDensity] diagnostics require a Debug build.";
#endif
    }

    private static bool TryCreateBiomeRegistration(
        BiomeDefinition biome,
        BiomeBlockDecoration decoration,
        out RegisteredBiomeDecoration registration)
    {
        registration = null;
        if (!Block.BlocksLoaded || decoration == null || decoration.blockValues == null
            || decoration.blockValues.Length == 0)
            return false;

        string biomeName = biome != null ? biome.m_sBiomeName : string.Empty;

        for (int i = 0; i < decoration.blockValues.Length; i++)
        {
            BlockValue value = decoration.blockValues[i];
            if (value.isair || value.Block == null)
                continue;

            string blockName = value.Block.blockName;
            if (string.IsNullOrEmpty(blockName))
                continue;

            if (IsVehicleHelper(blockName))
            {
                registration = new RegisteredBiomeDecoration
                {
                    Decoration = decoration,
                    Kind = BiomeDecorationKind.Vehicle,
                    BiomeName = biomeName,
                    BlockName = blockName,
                    SourceProbability = decoration.prob,
                    RebirthBaselineFactor = string.Equals(
                        biomeName,
                        "wasteland",
                        StringComparison.OrdinalIgnoreCase) ? 3f : 1f
                };
                return true;
            }

            float treeBaseline;
            if (!TryGetTreeBaselineFactor(blockName, biomeName, out treeBaseline))
                continue;

            registration = new RegisteredBiomeDecoration
            {
                Decoration = decoration,
                Kind = BiomeDecorationKind.Tree,
                BiomeName = biomeName,
                BlockName = blockName,
                SourceProbability = decoration.prob,
                RebirthBaselineFactor = treeBaseline
            };
            return true;
        }

        return false;
    }

    private static void ReapplyAllUnsafe()
    {
        for (int i = 0; i < BiomeDefinitions.Count; i++)
            ApplyBiomeUnsafe(BiomeDefinitions[i]);

        for (int i = 0; i < RoadDefinitions.Count; i++)
            ApplyRoadUnsafe(RoadDefinitions[i]);
    }

    private static void ApplyBiomeUnsafe(RegisteredBiomeDecoration registration)
    {
        float optionFactor = registration.Kind == BiomeDecorationKind.Tree
            ? treePercent * 0.01f
            : vehiclePercent * 0.01f;

        registration.Decoration.prob = registration.SourceProbability
            * registration.RebirthBaselineFactor
            * optionFactor;
    }

    private static void ApplyRoadUnsafe(RegisteredRoadVehicle registration)
    {
        registration.Definition.m_Prob = registration.SourceProbability
            * registration.RebirthBaselineFactor
            * (vehiclePercent * 0.01f);
    }

    private static bool Approximately(float left, float right)
    {
        float scale = Math.Max(1f, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= 0.000001f * scale;
    }

    private static bool IsVehicleHelper(string blockName)
    {
        return string.Equals(blockName, "carsRandomHelper", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "carsRandomHelperBiome", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRoadBaselineFactor(string roadName, out float factor)
    {
        if (string.Equals(roadName, "City Asphalt", StringComparison.OrdinalIgnoreCase))
        {
            factor = 2.67f;
            return true;
        }

        if (string.Equals(roadName, "Country Road Asphalt", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1.335f;
            return true;
        }

        if (string.Equals(roadName, "Road Gravel", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1f;
            return true;
        }

        factor = 0f;
        return false;
    }

    private static bool TryGetTreeBaselineFactor(
        string blockName,
        string biomeName,
        out float factor)
    {
        if (IsStandardForestTree(blockName))
        {
            factor = 0.8f;
            return true;
        }

        if (IsWinterTree(blockName))
        {
            factor = 1f;
            return true;
        }

        if (IsDeadOrBurntTree(blockName))
        {
            factor = 1.67f;
            if (string.Equals(biomeName, "burnt_forest", StringComparison.OrdinalIgnoreCase))
                factor *= 2f;
            else if (string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase))
                factor /= 250f;
            return true;
        }

        factor = 0f;
        return false;
    }

    private static bool IsStandardForestTree(string name)
    {
        switch (name)
        {
            case "treeMountainPine12m":
            case "treeMountainPine19m":
            case "treeMountainPineDry21m":
            case "treeMountainPine27m":
            case "treeMountainPine31m":
            case "treeMountainPine41m":
            case "treeMountainPine48m":
            case "treeOakLrg01":
            case "treeOakMed01":
            case "treeOakMed02":
            case "treeFirLrg01":
                return true;
            default:
                return false;
        }
    }

    private static bool IsWinterTree(string name)
    {
        switch (name)
        {
            case "treeWinterEverGreen":
            case "treeWinterPine13m":
            case "treeWinterPine19m":
            case "treeWinterPine28m":
                return true;
            default:
                return false;
        }
    }

    private static bool IsDeadOrBurntTree(string name)
    {
        switch (name)
        {
            case "treeJuniper4m":
            case "treeDeadPineLeaf":
            case "treePineBurntLrg":
            case "treePineBurntMed":
            case "treePineBurntFullMed":
            case "treeBurntMaple01":
            case "treeBurntMaple02":
            case "treeBurntMaple03":
            case "treeDeadTree01":
            case "treeDeadTree02":
                return true;
            default:
                return false;
        }
    }
}
