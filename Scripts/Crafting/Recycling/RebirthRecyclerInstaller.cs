using HarmonyLib;

internal static class RebirthRecyclerInstaller
{
    internal static void Install()
    {
        RebirthRecyclerRuntime.Install();
        RebirthRecipeOutputs.Install();
        RebirthRecipeOutputIntegration.Install();
        var harmony = new Harmony("rebirth.recycler.ui");
        foreach (var type in new[] { typeof(RebirthRecyclerShiftAdmission), typeof(RebirthRecyclerToolPresentation),
            typeof(RebirthRecyclerContextActions), typeof(RebirthRecyclerPreferredRouting) })
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, type);
    }
}
