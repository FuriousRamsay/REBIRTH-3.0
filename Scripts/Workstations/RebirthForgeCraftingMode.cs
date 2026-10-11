using HarmonyLib;

// Use the native no-smelting path for REBIRTH progression on both server and client.
public static class RebirthForgeCraftingMode
{
    public static void Apply()
    {
        XUiM_Recipes.DisableSmelter = RebirthSurvivorMode.IsEnabledForCurrentWorld()
            || SandboxOptions.SandboxOptionManager.GetBool(SandboxOptions.SandboxOptions.SmeltingType);
    }
}

[HarmonyPatch(typeof(SandboxOptions.SandboxOptionManager), "UpdateInGameValuesWithSandboxOptions")]
public static class RebirthForgeCraftingModeNativeOptionsPatch
{
    private static void Postfix() => RebirthForgeCraftingMode.Apply();
}
