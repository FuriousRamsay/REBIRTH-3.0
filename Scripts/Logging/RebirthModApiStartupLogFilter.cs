using System;
using HarmonyLib;

internal static class RebirthModApiStartupLogFilter
{
    private static readonly Harmony Harmony = new Harmony("rebirth.dedicated.modapi-log");
    private static readonly System.Reflection.MethodInfo LogInfo = ((Action<string>)Log.Out).Method;
    private static string discovery;
    private static string completion;
    private static bool installed;

    internal static void Install(Mod mod)
    {
        if (!GameManager.IsDedicatedServer || installed) return;

        try
        {
            string assembly = ReflectionHelpers.GetAssemblyNameWithLocation(typeof(RebirthModApiStartupLogFilter).Assembly, null);
            discovery = $"[MODS]     Found ModAPI in assembly {assembly}, creating instance";
            completion = $"[MODS]     Initialized code in mod '{mod.Name}' in assembly {assembly}";
            ModEvents.GameAwake.RegisterHandler(Remove);
            Harmony.Patch(LogInfo, prefix: new HarmonyMethod(((Func<string, bool>)KeepMessage).Method));
            installed = true;
        }
        catch (Exception error)
        {
            Log.Warning("[REBIRTH] Could not filter repeated ModAPI startup messages: " + error.Message);
        }
    }

    private static bool KeepMessage(string __0) => __0 != discovery && __0 != completion;

    private static void Remove(ref ModEvents.SGameAwakeData _)
    {
        if (!installed) return;
        Harmony.Unpatch(LogInfo, HarmonyPatchType.Prefix, Harmony.Id);
        installed = false;
    }
}
