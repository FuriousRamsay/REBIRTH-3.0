using System.Xml.Linq;
using HarmonyLib;

#nullable disable

namespace Rebirth.WorldDecorations.Density
{
    /// <summary>
    /// Restores the second half of the 2.6 Vehicle Density option: vehicle blocks
    /// placed from the POI-map road definitions rather than biome decorations.
    /// </summary>
    [HarmonyPatch(typeof(WorldBiomes), nameof(WorldBiomes.readXML))]
    public class WorldBiomesReadXmlDensityPatch
    {
        public static void Prefix(bool _instantiateReferences)
        {
            RebirthWorldDecorationDensityRuntimePolicy.BeginDefinitionLoad(_instantiateReferences);
        }

        public static void Postfix(
            WorldBiomes __instance,
            XDocument _xml,
            bool _instantiateReferences)
        {
            RebirthWorldDecorationDensityRuntimePolicy.RegisterRoadVehicleDefinitions(
                __instance,
                _xml,
                _instantiateReferences);
        }
    }
}
