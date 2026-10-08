using HarmonyLib;

#nullable disable

namespace Rebirth.WorldDecorations.Density
{
    /// <summary>
    /// Registers native biome decoration definitions and applies the exact REBIRTH
    /// 2.6 tree/vehicle probability calibration without replacing native placement.
    /// </summary>
    [HarmonyPatch(typeof(BiomeDefinition), nameof(BiomeDefinition.AddDecoBlock))]
    public class AddDecoBlockPatch
    {
        public static void Prefix(BiomeDefinition __instance, BiomeBlockDecoration _deco)
        {
            RebirthWorldDecorationDensityRuntimePolicy.RegisterBiomeDecoration(__instance, _deco);
        }
    }
}
