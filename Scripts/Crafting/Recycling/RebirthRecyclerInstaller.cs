using HarmonyLib;

internal static class RebirthRecyclerInstaller
{
    internal static void Install()
    {
        RebirthRecyclerRuntime.Install();
        RebirthRecipeOutputs.Install();
        RebirthRecipeOutputIntegration.Install();
        RebirthCookingOutputDelivery.Install();

        var harmony = new Harmony("rebirth.recycler.ui");
        RebirthRecyclerPlacedSettlement.InstallAuthorityController(harmony);
        foreach (var type in new[] { typeof(RebirthRecyclerShiftAdmission), typeof(RebirthRecyclerToolPresentation),
            typeof(RebirthRecyclerContextActions), typeof(RebirthRecyclerPreferredRouting) })
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, type);
    }
}
