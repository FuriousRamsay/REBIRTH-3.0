using HarmonyLib;

#nullable disable

public static class RebirthPitchBlackPatchInstaller
{
    private const string HarmonyId = "rebirth.pitchblack.3.1";
    private static readonly HarmonyLib.Harmony HarmonyInstance = new HarmonyLib.Harmony(HarmonyId);
    private static bool installed;

    public static string Install()
    {
        if (GameManager.IsDedicatedServer)
            return "[PitchBlack] dedicated server: rendering patch not installed.";
        if (installed)
            return "[PitchBlack] patch already installed.";

        HarmonyInstance.CreateClassProcessor(typeof(RebirthPitchBlackEnvironmentPatch)).Patch();
        installed = true;
        return "[PitchBlack] installed client-side nighttime environment postfix.";
    }

    internal static void Apply(World ___world, EntityPlayerLocal ___localPlayer)
    {
        RebirthPitchBlackPolicy.Apply(___world, ___localPlayer);
    }
}

[HarmonyPatch(typeof(WorldEnvironment), nameof(WorldEnvironment.SpectrumsFrameUpdate))]
internal static class RebirthPitchBlackEnvironmentPatch
{
    private static void Prefix()
    {
        RebirthPitchBlackPolicy.BeforeNativeEnvironmentUpdate();
    }

    private static void Postfix(World ___world, EntityPlayerLocal ___localPlayer)
    {
        RebirthPitchBlackPatchInstaller.Apply(___world, ___localPlayer);
    }
}
